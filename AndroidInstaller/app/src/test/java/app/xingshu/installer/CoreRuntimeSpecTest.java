package app.xingshu.installer;

import org.junit.After;
import org.junit.Rule;
import org.junit.Test;
import org.junit.rules.TemporaryFolder;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.Map;
import static org.junit.Assert.*;

public final class CoreRuntimeSpecTest {
    private static final String HASH_A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static final String HASH_B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static final String HASH_C = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static final String HASH_PREV = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    @Rule public final TemporaryFolder tmp = new TemporaryFolder();

    @After public void resetCache() {
        CoreRuntimeSpec.clearMemoryCacheForTest();
    }

    private static Map<String, Object> validManifestMap() {
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("formatVersion", 1);
        m.put("runtimeVersion", "1.0.0-android-preview8.37");
        m.put("gameVersion", "3.4.0");
        m.put("resourceVersion", "170");
        m.put("catalogRelativePath", "com.unity.addressables/catalog_3.4.0.json");
        m.put("catalogSha256", HASH_A);
        m.put("bundleRelativePath",
            "com.unity.addressables/AssetBundles/1234567890abcdef1234567890abcdef/abcdef1234567890abcdef1234567890/__data");
        m.put("patchSha256", HASH_B);
        m.put("vanillaSha256", HASH_C);
        m.put("candidateUrl",
            "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-preview8.37/candidate.__data");
        m.put("vanillaUrl",
            "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-preview8.37/vanilla.__data");
        m.put("candidateSize", 9_123_456L);
        m.put("vanillaSize", 8_234_567L);
        m.put("previousHashes", Arrays.asList(HASH_PREV));
        return m;
    }

    private static void assertRejected(Map<String, Object> manifest, String reason) {
        String json = MiniJson.stringify(manifest);
        try {
            CoreRuntimeSpec.parseAndValidate(json, "remote");
            fail("Expected rejection for: " + reason);
        } catch (IOException | SecurityException expected) {
            assertNotNull(expected.getMessage());
        }
    }

    @Test public void parsesAndValidatesRemoteHotUpdateToNewGameAndRuntimeVersion() throws Exception {
        String raw = MiniJson.stringify(validManifestMap());
        CoreRuntimeSpec spec = CoreRuntimeSpec.parseAndValidate(raw, "remote");

        assertEquals("1.0.0-android-preview8.37", spec.runtimeVersion);
        assertEquals("3.4.0", spec.gameVersion);
        assertEquals("170", spec.resourceVersion);
        assertEquals("com.unity.addressables/catalog_3.4.0.json", spec.catalogRelativePath);
        assertEquals("catalog_3.4.0.json", spec.catalogFileName());
        assertEquals(HASH_A, spec.catalogSha256);
        assertEquals(HASH_B, spec.patchSha256);
        assertEquals(HASH_C, spec.vanillaSha256);
        assertEquals(9_123_456L, spec.candidateSize);
        assertEquals(8_234_567L, spec.vanillaSize);
        assertEquals("remote", spec.source);
        assertTrue(spec.isKnownHash(HASH_B));
        assertTrue(spec.isKnownHash(HASH_C));
        assertTrue(spec.isKnownHash(HASH_PREV));
        assertFalse(spec.isKnownHash(HASH_A));

        CoreRuntimeSpec roundTrip = CoreRuntimeSpec.parseAndValidate(spec.toCanonicalJson(), "ipc");
        assertEquals(spec.runtimeVersion, roundTrip.runtimeVersion);
        assertEquals(spec.gameVersion, roundTrip.gameVersion);
        assertEquals(spec.bundleRelativePath, roundTrip.bundleRelativePath);
        assertEquals(spec.previousHashes, roundTrip.previousHashes);

        InstallerEngine engine = new InstallerEngine();
        Map<String, Object> ipcResp = MiniJson.parseObject(engine.updateCoreSpec(spec.toCanonicalJson()));
        assertEquals(Boolean.TRUE, ipcResp.get("ok"));
        assertEquals("1.0.0-android-preview8.37", ipcResp.get("runtimeVersion"));
        assertEquals("3.4.0", ipcResp.get("gameVersion"));
    }

    @Test public void rejectsTamperedHostPathTraversalAndInvalidHashes() {
        Map<String, Object> badHost = validManifestMap();
        badHost.put("candidateUrl", "https://evil.example.com/xingshu-hub/assets/launcher-runtime/a.__data");
        assertRejected(badHost, "non-official host");

        Map<String, Object> badPrefix = validManifestMap();
        badPrefix.put("vanillaUrl", "https://astralparty-xingshu.lanxichaxu.cn/api/evil/vanilla.__data");
        assertRejected(badPrefix, "disallowed URL prefix");

        Map<String, Object> badCatalog = validManifestMap();
        badCatalog.put("catalogRelativePath", "com.unity.addressables/../evil.json");
        assertRejected(badCatalog, "catalog path traversal");

        Map<String, Object> badBundle = validManifestMap();
        badBundle.put("bundleRelativePath", "com.unity.addressables/AssetBundles/../nothex/__data");
        assertRejected(badBundle, "invalid bundle relative path");

        Map<String, Object> badSha = validManifestMap();
        badSha.put("patchSha256", HASH_B.substring(0, 63));
        assertRejected(badSha, "63-char SHA-256");

        Map<String, Object> badSize = validManifestMap();
        badSize.put("candidateSize", CoreRuntimeSpec.MAX_CORE_PAYLOAD_BYTES + 1L);
        assertRejected(badSize, "oversized candidateSize");
    }

    @Test public void diskCacheFallbackRespectsAppVersionUpgrade() throws Exception {
        File filesDir = tmp.newFolder("app-files");
        CoreRuntimeSpec futureSpec = CoreRuntimeSpec.parseAndValidate(MiniJson.stringify(validManifestMap()), "remote");

        CoreRuntimeSpec.saveDiskCache(filesDir, futureSpec, 100);

        CoreRuntimeSpec cachedSameCode = CoreRuntimeSpec.loadDiskCache(filesDir, 100);
        assertNotNull(cachedSameCode);
        assertEquals("cache", cachedSameCode.source);
        assertEquals("1.0.0-android-preview8.37", cachedSameCode.runtimeVersion);
        assertEquals("3.4.0", cachedSameCode.gameVersion);

        CoreRuntimeSpec upgradedApk = CoreRuntimeSpec.loadDiskCache(filesDir, 101);
        assertNull("Offline APK upgrade must ignore older appVersionCode disk cache", upgradedApk);

        // Corrupt cache file should be rejected and purged automatically.
        try (FileOutputStream out = new FileOutputStream(new File(filesDir, CoreRuntimeSpec.CACHE_FILE_NAME))) {
            out.write("{\"bad\":true}".getBytes(StandardCharsets.UTF_8));
        }
        assertNull(CoreRuntimeSpec.loadDiskCache(filesDir, 100));
        assertFalse(new File(filesDir, CoreRuntimeSpec.CACHE_FILE_NAME).exists());
    }
}
