package app.xingshu.installer;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Map;
import org.junit.Test;

public class OfficialModulesCleanupTest {
    private static void writeModule(File dir, String id) throws Exception {
        if (!dir.isDirectory()) {
            assertTrue(dir.mkdirs());
        }
        File target = new File(dir, "xingshu.launcher." + id + ".module");
        try (FileOutputStream out = new FileOutputStream(target)) {
            out.write(("xingshu-module-v1:" + id + "\n").getBytes(StandardCharsets.UTF_8));
        }
    }

    @Test
    public void wipeAllAndPurgeLegacyDeleteRetiredModulesFromBothDirs() throws Exception {
        File root = Files.createTempDirectory("xingshu-mod-cleanup-").toFile();
        File plugins = new File(root, "xingshu/plugins");
        File disabled = new File(root, ".xingshu-mods/disabled-modules");

        writeModule(plugins, "hand-monitor");
        writeModule(plugins, "monster-targeting");
        writeModule(plugins, "quick-thanks");
        writeModule(disabled, "monster-cooldown");
        writeModule(disabled, "custom-lobby-bg");

        // Enabling merged module should delete its retired aliases immediately.
        InstallerEngine.applyOfficialModulesToDirs(plugins, disabled, "monster-action", true, false);
        assertTrue(new File(plugins, "xingshu.launcher.monster-action.module").isFile());
        assertFalse(new File(plugins, "xingshu.launcher.monster-targeting.module").exists());
        assertFalse(new File(disabled, "xingshu.launcher.monster-cooldown.module").exists());

        // "legacy" should remove remaining retired modules without touching canonical ones.
        String legacyResp = InstallerEngine.applyOfficialModulesToDirs(plugins, disabled, "legacy", false, true);
        Map<String, Object> legacyParsed = MiniJson.parseObject(legacyResp);
        assertTrue(Boolean.TRUE.equals(legacyParsed.get("ok")));
        assertFalse(new File(plugins, "xingshu.launcher.quick-thanks.module").exists());
        assertFalse(new File(disabled, "xingshu.launcher.custom-lobby-bg.module").exists());
        assertTrue(new File(plugins, "xingshu.launcher.hand-monitor.module").isFile());

        // "all" should wipe every canonical and retired module descriptor cleanly.
        writeModule(disabled, "free-camera");
        String wipeResp = InstallerEngine.applyOfficialModulesToDirs(plugins, disabled, "all", false, true);
        Map<String, Object> wipeParsed = MiniJson.parseObject(wipeResp);
        assertTrue(Boolean.TRUE.equals(wipeParsed.get("ok")));
        assertFalse(new File(plugins, "xingshu.launcher.hand-monitor.module").exists());
        assertFalse(new File(plugins, "xingshu.launcher.monster-action.module").exists());
        assertFalse(new File(disabled, "xingshu.launcher.free-camera.module").exists());
    }
}
