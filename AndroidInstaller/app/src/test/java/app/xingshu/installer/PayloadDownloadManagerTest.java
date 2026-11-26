package app.xingshu.installer;

import org.junit.Rule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.List;
import static org.junit.Assert.*;

public final class PayloadDownloadManagerTest {
    @Rule public final TemporaryFolder tmp = new TemporaryFolder();

    @Test public void enforcesOfficialHttpsHostAndAtomicSha256Cache() throws Exception {
        try {
            PayloadDownloadManager.validateOfficialUrl("http://astralparty-xingshu.lanxichaxu.cn/a");
            fail("Expected HTTP rejection");
        } catch (SecurityException expected) {
            assertTrue(expected.getMessage().contains("非官方 HTTPS"));
        }
        try {
            PayloadDownloadManager.validateOfficialUrl("https://evil.example.com/a");
            fail("Expected foreign host rejection");
        } catch (SecurityException expected) {
            assertTrue(expected.getMessage().contains("非官方 HTTPS"));
        }

        byte[] bytes = "xingshu-android-payload-sample".getBytes(StandardCharsets.UTF_8);
        String sha = PayloadDownloadManager.hex(MessageDigest.getInstance("SHA-256").digest(bytes));
        File cacheDir = tmp.newFolder("payload-cache");
        File target = PayloadDownloadManager.cacheFileForHash(cacheDir, sha);

        List<Long> seen = new ArrayList<>();
        File out = PayloadDownloadManager.downloadStreamToCache(
            new ByteArrayInputStream(bytes),
            cacheDir,
            target,
            sha,
            bytes.length,
            "测试包",
            (rx, tot, spd, st) -> seen.add(rx));

        assertEquals(target, out);
        assertEquals(bytes.length, out.length());
        assertEquals(Long.valueOf(bytes.length), seen.get(seen.size() - 1));
        assertTrue(PayloadDownloadManager.hasCachedFile(cacheDir, sha, bytes.length));
        assertNotNull(PayloadDownloadManager.getCachedIfValid(cacheDir, sha, bytes.length));
        assertEquals(bytes.length, PayloadDownloadManager.getCacheTotalBytes(cacheDir));
        assertEquals(bytes.length, PayloadDownloadManager.clearCache(cacheDir));
        assertFalse(PayloadDownloadManager.hasCachedFile(cacheDir, sha, bytes.length));
    }

    @Test public void rejectsCorruptedOrTruncatedDownloadsWithoutLeavingCache() throws Exception {
        byte[] bytes = "valid-bytes".getBytes(StandardCharsets.UTF_8);
        String sha = PayloadDownloadManager.hex(MessageDigest.getInstance("SHA-256").digest(bytes));
        File cacheDir = tmp.newFolder("bad-cache");
        File target = PayloadDownloadManager.cacheFileForHash(cacheDir, sha);

        try {
            PayloadDownloadManager.downloadStreamToCache(
                new ByteArrayInputStream("tamperbytes".getBytes(StandardCharsets.UTF_8)),
                cacheDir,
                target,
                sha,
                bytes.length,
                "损坏包",
                null);
            fail("Expected SHA-256 failure");
        } catch (SecurityException expected) {
            assertTrue(expected.getMessage().contains("SHA-256"));
        }
        assertFalse(target.exists());

        try {
            PayloadDownloadManager.downloadStreamToCache(
                new ByteArrayInputStream("short".getBytes(StandardCharsets.UTF_8)),
                cacheDir,
                target,
                sha,
                bytes.length,
                "截断包",
                null);
            fail("Expected truncation IOException");
        } catch (IOException expected) {
            assertTrue(expected.getMessage().contains("完整"));
        }
        assertFalse(target.exists());
    }

    @Test public void splitsParallelRangesCleanly() {
        long[][] ranges = PayloadDownloadManager.computeRanges(10_000_003L, 4);
        assertEquals(4, ranges.length);
        assertEquals(0L, ranges[0][0]);
        long covered = 0L;
        for (int i = 0; i < ranges.length; i++) {
            long start = ranges[i][0];
            long end = ranges[i][1];
            assertTrue(end >= start);
            if (i > 0) {
                assertEquals(ranges[i - 1][1] + 1L, start);
            }
            covered += (end - start + 1L);
        }
        assertEquals(10_000_003L, covered);
        assertEquals(10_000_002L, ranges[3][1]);
    }
}
