package app.xingshu.installer;

import org.junit.Rule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class CustomizeEngineTest {
    @Rule
    public TemporaryFolder tmp = new TemporaryFolder();

    @Test
    public void lobbyBgImportSaveAndDeleteWorkAtomically() throws Exception {
        File filesRoot = tmp.newFolder("files");
        InstallerEngine engine = new InstallerEngine();

        String importResp = engine.importCustomizeFileForRoot(
                filesRoot,
                "lobbybg",
                "video_「新宿决战」雨爱雄_0.mp4",
                new ByteArrayInputStream("FAKE_MP4_DATA".getBytes(StandardCharsets.UTF_8)));
        Map<String, Object> importMap = MiniJson.parseObject(importResp);
        assertEquals(Boolean.TRUE, importMap.get("ok"));
        assertEquals("video", MiniJson.stringField(importMap, "mediaKind"));
        assertTrue(new File(filesRoot, "lobbybg/video_「新宿决战」雨爱雄_0.mp4").isFile());

        String savePayload = "{\"enabled\":true,\"selectedFile\":\"video_「新宿决战」雨爱雄_0.mp4\",\"fillMode\":\"contain\",\"brightness\":0.85,\"speed\":1.5,\"muted\":false,\"volume\":0.6,\"muteVanillaBgm\":true,\"syncSubPanels\":true,\"randomOnLobby\":false}";
        String saveResp = engine.manageCustomizeForRoot(filesRoot, "lobbybg_save", savePayload);
        Map<String, Object> saveMap = MiniJson.parseObject(saveResp);
        assertEquals(Boolean.TRUE, saveMap.get("ok"));
        assertTrue(new File(filesRoot, "lobbybg/xingshu_lobby_bg.json").isFile());

        Map<String, Object> stateMap = MiniJson.parseObject(
                engine.manageCustomizeForRoot(filesRoot, "lobbybg_state", "{}"));
        Map<String, Object> cfg = MiniJson.objectField(stateMap, "config");
        assertEquals(Boolean.TRUE, cfg.get("enabled"));
        assertEquals("video_「新宿决战」雨爱雄_0.mp4", MiniJson.stringField(cfg, "selectedFile"));
        assertEquals("contain", MiniJson.stringField(cfg, "fillMode"));
        assertEquals(Boolean.TRUE, cfg.get("muteVanillaBgm"));
        List<?> files = MiniJson.listField(stateMap, "files");
        assertEquals(1, files.size());

        String delResp = engine.manageCustomizeForRoot(
                filesRoot,
                "lobbybg_delete",
                "{\"fileName\":\"video_「新宿决战」雨爱雄_0.mp4\"}");
        Map<String, Object> delMap = MiniJson.parseObject(delResp);
        assertEquals(Boolean.TRUE, delMap.get("ok"));
        assertFalse(new File(filesRoot, "lobbybg/video_「新宿决战」雨爱雄_0.mp4").isFile());
        Map<String, Object> delCfg = MiniJson.objectField(delMap, "config");
        assertEquals(Boolean.FALSE, delCfg.get("enabled"));
        assertEquals("", MiniJson.stringField(delCfg, "selectedFile"));
    }

    @Test
    public void customizeImportRejectsPathTraversalAndUnsupportedExt() throws Exception {
        File filesRoot = tmp.newFolder("files_sec");
        InstallerEngine engine = new InstallerEngine();

        Map<String, Object> trav = MiniJson.parseObject(engine.importCustomizeFileForRoot(
                filesRoot, "lobbybg", "../evil.png",
                new ByteArrayInputStream(new byte[] { 1, 2, 3 })));
        assertFalse(trav.containsKey("ok"));
        assertFalse(MiniJson.stringField(trav, "error").isEmpty());

        Map<String, Object> badExt = MiniJson.parseObject(engine.importCustomizeFileForRoot(
                filesRoot, "fonts", "bad.exe",
                new ByteArrayInputStream(new byte[] { 1, 2, 3 })));
        assertFalse(badExt.containsKey("ok"));
        assertFalse(MiniJson.stringField(badExt, "error").isEmpty());
    }

    @Test
    public void tuningSaveAndFontImportPersistInDebugConfig() throws Exception {
        File filesRoot = tmp.newFolder("files_tuning");
        InstallerEngine engine = new InstallerEngine();

        Map<String, Object> fontImport = MiniJson.parseObject(engine.importCustomizeFileForRoot(
                filesRoot, "fonts", "XingShuTestFont.ttf",
                new ByteArrayInputStream("TTF".getBytes(StandardCharsets.UTF_8))));
        assertEquals(Boolean.TRUE, fontImport.get("ok"));
        assertTrue(new File(filesRoot, "xingshu/fonts/XingShuTestFont.ttf").isFile());

        String saveTuning = "{\"selectedFontFamily\":\"Microsoft YaHei\",\"selectedFontFile\":\"XingShuTestFont.ttf\",\"fontCjkOnly\":true,\"fontReplaceHeadings\":false,\"homeUiDebug\":{\"hideVanillaBgAnim\":true,\"compactLobbyBadges\":true,\"showDebugOutline\":false,\"customHudOpacity\":72,\"hideLobbyAds\":true}}";
        Map<String, Object> resp = MiniJson.parseObject(
                engine.manageCustomizeForRoot(filesRoot, "tuning_save", saveTuning));
        assertEquals(Boolean.TRUE, resp.get("ok"));
        Map<String, Object> tuning = MiniJson.objectField(resp, "tuning");
        assertEquals("Microsoft YaHei", MiniJson.stringField(tuning, "selectedFontFamily"));
        assertEquals("XingShuTestFont.ttf", MiniJson.stringField(tuning, "selectedFontFile"));
        Map<String, Object> hud = MiniJson.objectField(tuning, "homeUiDebug");
        assertEquals(Boolean.TRUE, hud.get("hideVanillaBgAnim"));
        assertEquals(72L, MiniJson.longField(hud, "customHudOpacity", 0L));
        assertEquals(1, MiniJson.listField(resp, "fontFiles").size());
    }
}
