package app.xingshu.installer;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Rule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.RandomAccessFile;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Arrays;

public class XsvidWriterTest {
    @Rule
    public TemporaryFolder tmp = new TemporaryFolder();

    @Test
    public void writesValidLittleEndianHeaderIndexFramesAndAudio() throws Exception {
        byte[] f0 = new byte[20];
        f0[0] = (byte) 0xFF; f0[1] = (byte) 0xD8; f0[19] = (byte) 0xD9;
        byte[] f1 = new byte[36];
        f1[0] = (byte) 0xFF; f1[1] = (byte) 0xD8; f1[35] = (byte) 0xD9;
        byte[] f2 = new byte[24];
        f2[0] = (byte) 0xFF; f2[1] = (byte) 0xD8; f2[23] = (byte) 0xD9;

        File audio = tmp.newFile("sample.m4a");
        byte[] audioBytes = new byte[16];
        for (int i = 0; i < audioBytes.length; i++) audioBytes[i] = (byte) (i + 10);
        Files.write(audio.toPath(), audioBytes);

        File out = new File(tmp.getRoot(), "bg.xsvid");
        XsvidWriter.INSTANCE.writePackage(out, 960, 540, 18000, Arrays.asList(f0, f1, f2), audio);
        assertTrue(out.isFile());

        try (RandomAccessFile raf = new RandomAccessFile(out, "r")) {
            byte[] headerAndIndex = new byte[40 + 3 * 12];
            raf.readFully(headerAndIndex);
            ByteBuffer bb = ByteBuffer.wrap(headerAndIndex).order(ByteOrder.LITTLE_ENDIAN);

            byte[] magic = new byte[4];
            bb.get(magic);
            assertEquals("XSVD", new String(magic, StandardCharsets.US_ASCII));
            assertEquals(1, bb.getInt());
            assertEquals(960, bb.getInt());
            assertEquals(540, bb.getInt());
            assertEquals(18000, bb.getInt());
            assertEquals(3, bb.getInt());

            long audioOff = bb.getLong();
            int audioLen = bb.getInt();
            assertEquals(0, bb.getInt());

            long expectedF0 = 40L + 3L * 12L;
            long expectedF1 = expectedF0 + f0.length;
            long expectedF2 = expectedF1 + f1.length;
            long expectedAud = expectedF2 + f2.length;

            assertEquals(expectedF0, bb.getLong()); assertEquals(f0.length, bb.getInt());
            assertEquals(expectedF1, bb.getLong()); assertEquals(f1.length, bb.getInt());
            assertEquals(expectedF2, bb.getLong()); assertEquals(f2.length, bb.getInt());
            assertEquals(expectedAud, audioOff);
            assertEquals(audioBytes.length, audioLen);

            byte[] gotF1 = new byte[f1.length];
            raf.seek(expectedF1);
            raf.readFully(gotF1);
            assertArrayEquals(f1, gotF1);

            byte[] gotAud = new byte[audioLen];
            raf.seek(audioOff);
            raf.readFully(gotAud);
            assertArrayEquals(audioBytes, gotAud);
        }
    }

    @Test
    public void rawStreamWriterMatchesInMemoryWriterByteForByte() throws Exception {
        byte[] f0 = new byte[22];
        f0[0] = (byte) 0xFF; f0[1] = (byte) 0xD8; f0[21] = (byte) 0xD9;
        byte[] f1 = new byte[48];
        f1[0] = (byte) 0xFF; f1[1] = (byte) 0xD8; f1[47] = (byte) 0xD9;
        byte[] f2 = new byte[30];
        f2[0] = (byte) 0xFF; f2[1] = (byte) 0xD8; f2[29] = (byte) 0xD9;

        File audio = tmp.newFile("stream_audio.m4a");
        byte[] audioBytes = new byte[64];
        for (int i = 0; i < audioBytes.length; i++) audioBytes[i] = (byte) (i * 3 + 1);
        Files.write(audio.toPath(), audioBytes);

        File rawFrames = tmp.newFile("frames.bin");
        ByteArrayOutputStream rawBaos = new ByteArrayOutputStream();
        rawBaos.write(f0);
        rawBaos.write(f1);
        rawBaos.write(f2);
        Files.write(rawFrames.toPath(), rawBaos.toByteArray());

        File memOut = new File(tmp.getRoot(), "mem.xsvid");
        File streamOut = new File(tmp.getRoot(), "stream.xsvid");
        XsvidWriter.INSTANCE.writePackage(memOut, 854, 480, 15000, Arrays.asList(f0, f1, f2), audio);
        XsvidWriter.INSTANCE.writePackageFromRawStream(
            streamOut,
            854,
            480,
            15000,
            rawFrames,
            new int[] { f0.length, f1.length, f2.length },
            audio
        );

        assertArrayEquals(Files.readAllBytes(memOut.toPath()), Files.readAllBytes(streamOut.toPath()));
    }

    @Test
    public void adaptiveProfileCoversFullFiveMinutesWithinRuntimeFrameCap() {
        double[] durations = new double[] { 15.0, 45.0, 90.0, 180.0, 300.0 };
        for (double sec : durations) {
            XsvidWriter.TranscodeProfile hw = XsvidWriter.INSTANCE.selectAdaptiveProfile(sec, false);
            assertTrue(hw.getMaxFrames() <= 3600);
            assertTrue(sec * hw.getTargetFps() <= 3550.0);

            XsvidWriter.TranscodeProfile fb = XsvidWriter.INSTANCE.selectAdaptiveProfile(sec, true);
            assertTrue(fb.getMaxFrames() <= 3600);
            assertTrue(sec * fb.getTargetFps() <= 3550.0);
        }

        XsvidWriter.TranscodeProfile shortHw = XsvidWriter.INSTANCE.selectAdaptiveProfile(30.0, false);
        assertEquals(960, shortHw.getMaxWidth());
        assertEquals(18.0, shortHw.getTargetFps(), 0.01);

        XsvidWriter.TranscodeProfile fiveMinHw = XsvidWriter.INSTANCE.selectAdaptiveProfile(300.0, false);
        assertEquals(720, fiveMinHw.getMaxWidth());
        assertTrue(fiveMinHw.getTargetFps() <= 11.7);
    }

    @Test
    public void computesCalibratedFpsMilliFromActualDuration() {
        assertEquals(12000, XsvidWriter.INSTANCE.computeCalibratedFpsMilli(240, 20_000_000L, 18000));
        assertEquals(15000, XsvidWriter.INSTANCE.computeCalibratedFpsMilli(900, 60_000_000L, 18000));
        assertEquals(18000, XsvidWriter.INSTANCE.computeCalibratedFpsMilli(0, 20_000_000L, 18000));
        assertEquals(18000, XsvidWriter.INSTANCE.computeCalibratedFpsMilli(10, 50_000L, 18000));
    }
}
