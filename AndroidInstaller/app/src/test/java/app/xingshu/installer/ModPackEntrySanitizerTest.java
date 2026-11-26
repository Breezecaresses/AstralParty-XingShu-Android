package app.xingshu.installer;

import org.junit.Rule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;
import static org.junit.Assert.*;

public final class ModPackEntrySanitizerTest {
    @Rule public final TemporaryFolder tmp = new TemporaryFolder();

    @Test public void normalizesPeModImgPathsAcrossWrappers() {
        assertEquals("UT_HandCard_21020.png",
            InstallerEngine.sanitizeModImgEntry("PE手机端(星枢)/modimg/UT_HandCard_21020.png"));
        assertEquals("UT_Event_30002.png",
            InstallerEngine.sanitizeModImgEntry("modimg/UT_Event_30002.png"));
        assertEquals("sub/Card.PNG",
            InstallerEngine.sanitizeModImgEntry("./wrapper/MODIMG\\sub\\Card.PNG"));
    }

    @Test public void rejectsTraversalMacOsAndNonPngEntries() {
        assertNull(InstallerEngine.sanitizeModImgEntry("../../evil.png"));
        assertNull(InstallerEngine.sanitizeModImgEntry("PE/modimg/../../evil.png"));
        assertNull(InstallerEngine.sanitizeModImgEntry("PE/modimg/../escape.png"));
        assertNull(InstallerEngine.sanitizeModImgEntry("__MACOSX/PE/modimg/._UT_Card.png"));
        assertNull(InstallerEngine.sanitizeModImgEntry("PE/modimg/._hidden.png"));
        assertNull(InstallerEngine.sanitizeModImgEntry("PE/modimg/readme.txt"));
        assertNull(InstallerEngine.sanitizeModImgEntry("PE/modimg/"));
        assertNull(InstallerEngine.sanitizeModImgEntry("C:\\PE\\modimg\\bad.png"));
    }

    @Test public void extractsUtf8AndGbkZipsSafelyIntoModImg() throws Exception {
        byte[] fakePng = new byte[] { (byte) 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        File gbkZip = tmp.newFile("pe-gbk.zip");
        try (ZipOutputStream zos = new ZipOutputStream(new FileOutputStream(gbkZip), Charset.forName("GBK"))) {
            zos.putNextEntry(new ZipEntry("PE手机端(星枢)/安装说明.txt"));
            zos.write("readme".getBytes(StandardCharsets.UTF_8));
            zos.closeEntry();

            zos.putNextEntry(new ZipEntry("PE手机端(星枢)/modimg/UT_HandCard_21020.png"));
            zos.write(fakePng);
            zos.closeEntry();

            zos.putNextEntry(new ZipEntry("PE手机端(星枢)/modimg/../../outside.png"));
            zos.write(fakePng);
            zos.closeEntry();
        }

        File targetDir = tmp.newFolder("modimg-out");
        List<String> extracted = InstallerEngine.extractModPackArchive(gbkZip, targetDir);
        assertEquals(Arrays.asList("UT_HandCard_21020.png"), extracted);
        assertTrue(new File(targetDir, "UT_HandCard_21020.png").isFile());
        assertFalse(new File(targetDir, "安装说明.txt").exists());
        assertFalse(new File(targetDir.getParentFile(), "outside.png").exists());
    }

    @Test public void installsValidXspkgModuleAtomically() throws Exception {
        byte[] moduleBytes = "xingshu-module-v1:hand-monitor\n".getBytes(StandardCharsets.UTF_8);
        String sha = sha256Hex(moduleBytes);
        String manifest = "{\"schemaVersion\":1,\"files\":[{\"source\":\"payload/feature.module\","
            + "\"target\":\"xingshu/plugins/xingshu.launcher.hand-monitor.module\","
            + "\"sha256\":\"" + sha + "\",\"size\":" + moduleBytes.length + "}]}";

        File xspkg = tmp.newFile("hand-monitor.xspkg");
        try (ZipOutputStream zos = new ZipOutputStream(new FileOutputStream(xspkg))) {
            zos.putNextEntry(new ZipEntry("manifest.json"));
            zos.write(manifest.getBytes(StandardCharsets.UTF_8));
            zos.closeEntry();

            zos.putNextEntry(new ZipEntry("payload/feature.module"));
            zos.write(moduleBytes);
            zos.closeEntry();
        }

        File filesRoot = tmp.newFolder("game-files");
        List<String> written = InstallerEngine.installXspkgArchive(xspkg, filesRoot);
        assertEquals(Arrays.asList("xingshu/plugins/xingshu.launcher.hand-monitor.module"), written);

        File outMod = new File(filesRoot, "xingshu/plugins/xingshu.launcher.hand-monitor.module");
        assertTrue(outMod.isFile());
        byte[] actual = new byte[moduleBytes.length];
        try (FileInputStream fis = new FileInputStream(outMod)) {
            assertEquals(moduleBytes.length, fis.read(actual));
        }
        assertArrayEquals(moduleBytes, actual);
    }

    @Test public void rejectsTamperedOrUnsafeXspkgArchives() throws Exception {
        assertTrue(InstallerEngine.isSafeXspkgTarget("xingshu/plugins/xingshu.launcher.hand-monitor.module"));
        assertFalse(InstallerEngine.isSafeXspkgTarget("xingshu/plugins/../evil.module"));
        assertFalse(InstallerEngine.isSafeXspkgTarget("AstralParty_Data/Managed/evil.dll"));

        byte[] moduleBytes = "xingshu-module-v1:hand-monitor\n".getBytes(StandardCharsets.UTF_8);
        String wrongSha = sha256Hex("tampered".getBytes(StandardCharsets.UTF_8));

        File badShaPkg = tmp.newFile("bad-sha.xspkg");
        try (ZipOutputStream zos = new ZipOutputStream(new FileOutputStream(badShaPkg))) {
            zos.putNextEntry(new ZipEntry("manifest.json"));
            zos.write(("{\"schemaVersion\":1,\"files\":[{\"source\":\"payload/feature.module\","
                + "\"target\":\"xingshu/plugins/xingshu.launcher.hand-monitor.module\","
                + "\"sha256\":\"" + wrongSha + "\",\"size\":" + moduleBytes.length + "}]}")
                .getBytes(StandardCharsets.UTF_8));
            zos.closeEntry();
            zos.putNextEntry(new ZipEntry("payload/feature.module"));
            zos.write(moduleBytes);
            zos.closeEntry();
        }
        File filesRoot = tmp.newFolder("game-files-bad");
        try {
            InstallerEngine.installXspkgArchive(badShaPkg, filesRoot);
            fail("Expected SHA-256 mismatch rejection");
        } catch (Exception expected) {
            assertFalse(new File(filesRoot, "xingshu/plugins/xingshu.launcher.hand-monitor.module").exists());
        }

        File unsafeTargetPkg = tmp.newFile("unsafe-target.xspkg");
        try (ZipOutputStream zos = new ZipOutputStream(new FileOutputStream(unsafeTargetPkg))) {
            zos.putNextEntry(new ZipEntry("manifest.json"));
            zos.write(("{\"schemaVersion\":1,\"files\":[{\"source\":\"payload/feature.module\","
                + "\"target\":\"../escape.module\","
                + "\"sha256\":\"" + sha256Hex(moduleBytes) + "\",\"size\":" + moduleBytes.length + "}]}")
                .getBytes(StandardCharsets.UTF_8));
            zos.closeEntry();
            zos.putNextEntry(new ZipEntry("payload/feature.module"));
            zos.write(moduleBytes);
            zos.closeEntry();
        }
        try {
            InstallerEngine.installXspkgArchive(unsafeTargetPkg, filesRoot);
            fail("Expected unsafe target path rejection");
        } catch (Exception expected) {
            assertFalse(new File(filesRoot.getParentFile(), "escape.module").exists());
        }
    }

    private static String sha256Hex(byte[] data) throws Exception {
        MessageDigest md = MessageDigest.getInstance("SHA-256");
        byte[] dig = md.digest(data);
        StringBuilder sb = new StringBuilder(dig.length * 2);
        for (byte b : dig) {
            sb.append(String.format(Locale.ROOT, "%02x", b & 0xff));
        }
        return sb.toString();
    }
}
