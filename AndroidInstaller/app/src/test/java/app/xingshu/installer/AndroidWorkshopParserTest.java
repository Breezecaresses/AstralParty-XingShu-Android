package app.xingshu.installer;

import org.junit.Test;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import static org.junit.Assert.*;

public final class AndroidWorkshopParserTest {
    @Test public void selectsPeCandidateAndExtractsPeVersionFromDetail() throws Exception {
        String listJson = "{"
            + "\"items\":["
            + "  {\"id\":\"xingshu.official.fast\",\"platforms\":[\"STEAM_CN\"],\"installKind\":\"launcher_module\",\"platformLabels\":[\"PC 端\"]},"
            + "  {\"id\":\"xingshu.community.nunnan-cards\",\"platforms\":[\"STEAM_CN\",\"ANDROID_CN\"],\"installKind\":\"bundle_mod\",\"platformLabels\":[\"PC 端\",\"手机端 (PE)\"]}"
            + "]}";

        List<String> ids = AndroidWorkshopClient.extractAndroidCandidateIds(listJson);
        assertEquals(1, ids.size());
        assertEquals("xingshu.community.nunnan-cards", ids.get(0));

        String detailJson = "{\"item\":{"
            + "\"id\":\"xingshu.community.nunnan-cards\","
            + "\"slug\":\"nunnan-cards\","
            + "\"name\":\"NunNan 系列卡牌替换汉化 Mod v1.3\","
            + "\"author\":\"抚夏不是拂夏\","
            + "\"category\":\"card\","
            + "\"categoryLabel\":\"卡牌替换\","
            + "\"description\":\"v1.3 全端整合版\","
            + "\"targetInfo\":\"PC / PE\","
            + "\"downloads\":9,"
            + "\"versions\":["
            + "  {\"id\":\"v1.3.0-pc\",\"version\":\"1.3.0\",\"platforms\":[\"STEAM_CN\"],\"platformLabel\":\"PC 端\",\"installKind\":\"bundle_mod\",\"sizeBytes\":12205692,\"sha256\":\"b23d0db74c79a385ecda0d3902f405ae1cf33820fb05c820340e43cfe80de524\",\"downloadUrl\":\"/api/workshop/mods/xingshu.community.nunnan-cards/versions/v1.3.0-pc/download\"},"
            + "  {\"id\":\"v1.3.0-pe\",\"version\":\"1.3.0\",\"title\":\"手机端 / PE 版\",\"fileName\":\"PE手机端(星枢).zip\",\"platforms\":[\"ANDROID_CN\"],\"platformLabel\":\"手机端 (PE)\",\"installKind\":\"external_archive\",\"sizeBytes\":11222485,\"sha256\":\"bc7dd5bd9040dbd4d5f64ce2d139cc7128d32858b818e4d3685b78e59684d9ef\",\"downloadUrl\":\"/api/workshop/mods/xingshu.community.nunnan-cards/versions/v1.3.0-pe/download\",\"downloads\":3}"
            + "]}}";

        AndroidWorkshopClient.WorkshopMod mod = AndroidWorkshopClient.parseAndroidVersionFromDetail(detailJson);
        assertNotNull(mod);
        assertEquals("v1.3.0-pe", mod.versionId);
        assertEquals("1.3.0", mod.version);
        assertEquals(11222485L, mod.sizeBytes);
        assertEquals("bc7dd5bd9040dbd4d5f64ce2d139cc7128d32858b818e4d3685b78e59684d9ef", mod.sha256);
        assertEquals(
            "https://astralparty-xingshu.lanxichaxu.cn/api/workshop/mods/xingshu.community.nunnan-cards/versions/v1.3.0-pe/download",
            mod.downloadUrl);
    }

    @Test public void rejectsUntrustedWorkshopHostsAndInvalidUrls() {
        assertNull(AndroidWorkshopClient.resolveOfficialWorkshopUrl("https://evil.example.com/mod.zip"));
        assertNull(AndroidWorkshopClient.resolveOfficialWorkshopUrl("http://astralparty-xingshu.lanxichaxu.cn/api/workshop/mods/x/download"));
        assertNotNull(AndroidWorkshopClient.resolveOfficialWorkshopUrl("/api/workshop/mods/demo/download"));
    }

    @Test public void parsesSingleVersionXspkgModulesFastAndDefersMultiVersionPacks() throws Exception {
        String sha = "32b40b282dbfabfa390ce98b0d259fadeb458be3597633223415c73f5c9257cf";
        String listJson = "{"
            + "\"items\":["
            + "  {\"id\":\"xingshu.launcher.hand-monitor\",\"slug\":\"hand-monitor\",\"name\":\"手牌监视\","
            + "   \"author\":\"星枢官方\",\"category\":\"optimization\",\"categoryLabel\":\"优化 Mod\","
            + "   \"description\":\"局内手牌与队友手牌实时透视（支持 PC / 安卓端）\",\"version\":\"1.0.0\","
            + "   \"versionCount\":1,\"installKind\":\"xspkg\",\"platforms\":[\"STEAM_CN\",\"ANDROID_CN\"],"
            + "   \"platformLabels\":[\"PC · 手机端\"],\"sizeBytes\":384,\"sha256\":\"" + sha + "\","
            + "   \"downloadUrl\":\"/api/workshop/mods/xingshu.launcher.hand-monitor/download\",\"downloads\":6},"
            + "  {\"id\":\"xingshu.community.nunnan-cards\",\"versionCount\":2,\"installKind\":\"bundle_mod\","
            + "   \"platforms\":[\"STEAM_CN\",\"ANDROID_CN\"],\"platformLabels\":[\"PC 端\",\"手机端 (PE)\"]},"
            + "  {\"id\":\"xingshu.pc.only\",\"versionCount\":1,\"installKind\":\"bundle_mod\","
            + "   \"platforms\":[\"STEAM_CN\"],\"platformLabels\":[\"PC 端\"]}"
            + "]}";

        List<String> multiIds = new ArrayList<>();
        List<AndroidWorkshopClient.WorkshopMod> fast = AndroidWorkshopClient.parseAndroidModsFromListFast(listJson, multiIds);

        assertEquals(1, fast.size());
        AndroidWorkshopClient.WorkshopMod mod = fast.get(0);
        assertEquals("xingshu.launcher.hand-monitor", mod.id);
        assertEquals("xspkg", mod.installKind);
        assertTrue(mod.isXspkgModule());
        assertEquals("PC · 手机端", mod.platformLabel);
        assertEquals(384L, mod.sizeBytes);
        assertEquals(
            "https://astralparty-xingshu.lanxichaxu.cn/api/workshop/mods/xingshu.launcher.hand-monitor/download",
            mod.downloadUrl);
        assertEquals(Collections.singletonList("xingshu.community.nunnan-cards"), multiIds);
    }

    @Test public void parsesAllLiveModsIncludingPcBrowseAndSortsCommunityFirst() throws Exception {
        String shaOfficial = "32b40b282dbfabfa390ce98b0d259fadeb458be3597633223415c73f5c9257cf";
        String shaPe = "bc7dd5bd9040dbd4d5f64ce2d139cc7128d32858b818e4d3685b78e59684d9ef";
        String listJson = "{"
            + "\"items\":["
            + "  {\"id\":\"xingshu.launcher.hand-monitor\",\"slug\":\"hand-monitor\",\"name\":\"手牌监视\","
            + "   \"author\":\"星枢官方\",\"category\":\"optimization\",\"categoryLabel\":\"优化 Mod\","
            + "   \"description\":\"官方模块\",\"version\":\"1.0.0\",\"versionCount\":1,\"installKind\":\"xspkg\","
            + "   \"platforms\":[\"STEAM_CN\",\"ANDROID_CN\"],\"platformLabels\":[\"PC · 手机端\"],"
            + "   \"sizeBytes\":730,\"sha256\":\"" + shaOfficial + "\","
            + "   \"downloadUrl\":\"/api/workshop/mods/xingshu.launcher.hand-monitor/download\",\"downloads\":50},"
            + "  {\"id\":\"xingshu.pc.voice\",\"slug\":\"pc-voice\",\"name\":\"PC 语音包\","
            + "   \"author\":\"社区作者\",\"category\":\"voice\",\"categoryLabel\":\"语音·音效\","
            + "   \"description\":\"仅 PC 端语音包\",\"version\":\"1.0.0\",\"versionCount\":1,\"installKind\":\"bundle_mod\","
            + "   \"platforms\":[\"STEAM_CN\"],\"platformLabels\":[\"PC 端\"],"
            + "   \"sizeBytes\":4096,\"sha256\":\"" + shaOfficial + "\","
            + "   \"downloadUrl\":\"/api/workshop/mods/xingshu.pc.voice/download\",\"downloads\":25},"
            + "  {\"id\":\"xingshu.pe.skin\",\"slug\":\"pe-skin\",\"name\":\"PE 卡面贴图\","
            + "   \"author\":\"社区画师\",\"category\":\"card\",\"categoryLabel\":\"卡牌替换\","
            + "   \"description\":\"手机直装贴图包\",\"version\":\"1.1.0\",\"versionCount\":1,\"installKind\":\"modimg\","
            + "   \"platforms\":[\"ANDROID_CN\"],\"platformLabels\":[\"手机端 (PE)\"],"
            + "   \"sizeBytes\":2048,\"sha256\":\"" + shaPe + "\","
            + "   \"downloadUrl\":\"/api/workshop/mods/xingshu.pe.skin/download\",\"downloads\":12},"
            + "  {\"id\":\"xingshu.community.nunnan-cards\",\"slug\":\"nunnan-cards\",\"name\":\"NunNan\","
            + "   \"versionCount\":2,\"installKind\":\"bundle_mod\",\"platforms\":[\"STEAM_CN\",\"ANDROID_CN\"],"
            + "   \"platformLabels\":[\"PC 端\",\"手机端 (PE)\"]}"
            + "]}";

        List<String> multiIds = new ArrayList<>();
        List<AndroidWorkshopClient.WorkshopMod> parsed = AndroidWorkshopClient.parseAllLiveModsFromList(listJson, multiIds);
        assertEquals(Collections.singletonList("xingshu.community.nunnan-cards"), multiIds);
        assertEquals(3, parsed.size());

        List<AndroidWorkshopClient.WorkshopMod> sorted = AndroidWorkshopClient.sortLiveWorkshopMods(parsed);
        assertEquals("xingshu.pe.skin", sorted.get(0).id);
        assertTrue(sorted.get(0).androidInstallable);

        assertEquals("xingshu.pc.voice", sorted.get(1).id);
        assertFalse(sorted.get(1).androidInstallable);
        assertEquals("仅 PC 端", sorted.get(1).platformLabel);
        assertEquals(
            "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/extensions/mods/pc-voice",
            sorted.get(1).webDetailUrl());

        assertEquals("xingshu.launcher.hand-monitor", sorted.get(2).id);
        assertTrue(sorted.get(2).androidInstallable);
        assertTrue(sorted.get(2).isXspkgModule());
    }
}
