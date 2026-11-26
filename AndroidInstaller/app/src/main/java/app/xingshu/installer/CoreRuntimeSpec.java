package app.xingshu.installer;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Pattern;
import javax.net.ssl.HttpsURLConnection;

/**
 * Decoupled runtime manifest for the independent XingShu Core Framework Mod.
 * Loaded remotely from official HTTPS with strict schema/path/hash validation,
 * local disk cache fallback, and compile-time embedded baseline fallback.
 */
final class CoreRuntimeSpec {
    static final String MANIFEST_URL =
        "https://astralparty-xingshu.lanxichaxu.cn/xingshu-hub/assets/launcher-runtime/android-core-manifest.json";
    static final int MAX_MANIFEST_BYTES = 512 * 1024;
    static final long MAX_CORE_PAYLOAD_BYTES = 128L * 1024L * 1024L;
    static final long CACHE_TTL_MS = 60_000L;
    static final long OFFLINE_BACKOFF_MS = 15_000L;
    static final String CACHE_FILE_NAME = "android-core-manifest-cache.json";
    static final String CACHE_VER_FILE_NAME = "android-core-manifest-cache.ver";

    private static final String RUNTIME_PATH_PREFIX = "/xingshu-hub/assets/launcher-runtime/";
    private static final Pattern SAFE_RUNTIME_VERSION = Pattern.compile("[A-Za-z0-9._-]{1,64}");
    private static final Pattern SAFE_SHORT_VERSION = Pattern.compile("[A-Za-z0-9._-]{1,32}");
    private static final Pattern SAFE_CATALOG_REL = Pattern.compile("com\\.unity\\.addressables/[A-Za-z0-9._/-]+\\.json");
    private static final Pattern SAFE_BUNDLE_REL = Pattern.compile(
        "com\\.unity\\.addressables/AssetBundles/[0-9a-f]{32}/[0-9a-f]{32}/__data");
    private static final Pattern SHA256_LOWER = Pattern.compile("[0-9a-f]{64}");

    private static CoreRuntimeSpec cachedSpec;
    private static long cachedExpiryMs;

    final String runtimeVersion;
    final String gameVersion;
    final String resourceVersion;
    final String catalogRelativePath;
    final String catalogSha256;
    final String bundleRelativePath;
    final String patchSha256;
    final String vanillaSha256;
    final String candidateUrl;
    final String vanillaUrl;
    final long candidateSize;
    final long vanillaSize;
    final List<String> previousHashes;
    final String source;

    private CoreRuntimeSpec(
            String runtimeVersion,
            String gameVersion,
            String resourceVersion,
            String catalogRelativePath,
            String catalogSha256,
            String bundleRelativePath,
            String patchSha256,
            String vanillaSha256,
            String candidateUrl,
            String vanillaUrl,
            long candidateSize,
            long vanillaSize,
            List<String> previousHashes,
            String source) {
        this.runtimeVersion = runtimeVersion;
        this.gameVersion = gameVersion;
        this.resourceVersion = resourceVersion;
        this.catalogRelativePath = catalogRelativePath;
        this.catalogSha256 = catalogSha256;
        this.bundleRelativePath = bundleRelativePath;
        this.patchSha256 = patchSha256;
        this.vanillaSha256 = vanillaSha256;
        this.candidateUrl = candidateUrl;
        this.vanillaUrl = vanillaUrl;
        this.candidateSize = candidateSize;
        this.vanillaSize = vanillaSize;
        this.previousHashes = previousHashes;
        this.source = source;
    }

    static CoreRuntimeSpec embedded() {
        LinkedHashSet<String> prev = new LinkedHashSet<>();
        if (PayloadSpec.PREVIOUS != null) {
            for (String h : PayloadSpec.PREVIOUS) {
                if (h != null) {
                    String lower = h.trim().toLowerCase(Locale.ROOT);
                    if (SHA256_LOWER.matcher(lower).matches()) {
                        prev.add(lower);
                    }
                }
            }
        }
        return new CoreRuntimeSpec(
            PayloadSpec.VERSION,
            "3.3.0",
            "160",
            "com.unity.addressables/catalog_3.3.0.json",
            PayloadSpec.CATALOG.toLowerCase(Locale.ROOT),
            PayloadSpec.RELATIVE,
            PayloadSpec.PATCH.toLowerCase(Locale.ROOT),
            PayloadSpec.VANILLA.toLowerCase(Locale.ROOT),
            PayloadSpec.CANDIDATE_URL,
            PayloadSpec.VANILLA_URL,
            PayloadSpec.CANDIDATE_SIZE,
            PayloadSpec.VANILLA_SIZE,
            Collections.unmodifiableList(new ArrayList<>(prev)),
            "embedded"
        );
    }

    static CoreRuntimeSpec parseAndValidate(String jsonText, String source) throws IOException {
        if (jsonText == null || jsonText.trim().isEmpty()) {
            throw new IOException("核心组件清单为空");
        }
        if (jsonText.getBytes(StandardCharsets.UTF_8).length > MAX_MANIFEST_BYTES) {
            throw new IOException("核心组件清单体积超出限制");
        }
        Map<String, Object> root = MiniJson.parseObject(jsonText);
        if (MiniJson.longField(root, "formatVersion", -1L) != 1L) {
            throw new IOException("核心组件清单版本不受支持");
        }

        String runtimeVersion = MiniJson.stringField(root, "runtimeVersion").trim();
        if (!SAFE_RUNTIME_VERSION.matcher(runtimeVersion).matches()) {
            throw new IOException("核心组件版本号格式无效");
        }

        String gameVersion = MiniJson.stringField(root, "gameVersion").trim();
        if (!SAFE_SHORT_VERSION.matcher(gameVersion).matches()) {
            throw new IOException("核心组件适配游戏版本号格式无效");
        }

        String resourceVersion = MiniJson.stringField(root, "resourceVersion").trim();
        if (!resourceVersion.isEmpty() && !SAFE_SHORT_VERSION.matcher(resourceVersion).matches()) {
            throw new IOException("核心组件资源版本号格式无效");
        }

        String catalogRelativePath = MiniJson.stringField(root, "catalogRelativePath").trim();
        if (catalogRelativePath.contains("..")
                || catalogRelativePath.contains("\\")
                || catalogRelativePath.contains("//")
                || !SAFE_CATALOG_REL.matcher(catalogRelativePath).matches()) {
            throw new IOException("核心组件资源清单路径不安全");
        }

        String bundleRelativePath = MiniJson.stringField(root, "bundleRelativePath").trim();
        if (!SAFE_BUNDLE_REL.matcher(bundleRelativePath).matches()) {
            throw new IOException("核心组件目标资产路径无效");
        }

        String catalogSha256 = requireSha256(MiniJson.stringField(root, "catalogSha256"), "catalogSha256");
        String patchSha256 = requireSha256(MiniJson.stringField(root, "patchSha256"), "patchSha256");
        String vanillaSha256 = requireSha256(MiniJson.stringField(root, "vanillaSha256"), "vanillaSha256");

        String candidateUrl = validateRuntimeUrl(MiniJson.stringField(root, "candidateUrl"));
        String vanillaUrl = validateRuntimeUrl(MiniJson.stringField(root, "vanillaUrl"));

        long candidateSize = MiniJson.longField(root, "candidateSize", -1L);
        long vanillaSize = MiniJson.longField(root, "vanillaSize", -1L);
        if (candidateSize < 1L || candidateSize > MAX_CORE_PAYLOAD_BYTES
                || vanillaSize < 1L || vanillaSize > MAX_CORE_PAYLOAD_BYTES) {
            throw new IOException("核心组件文件大小超出安全范围");
        }

        List<?> rawPrev = MiniJson.listField(root, "previousHashes");
        if (rawPrev.size() > 256) {
            throw new IOException("核心组件历史兼容哈希条目过多");
        }
        LinkedHashSet<String> prev = new LinkedHashSet<>();
        for (Object item : rawPrev) {
            if (!(item instanceof String)) {
                throw new IOException("核心组件历史兼容哈希包含非字符串项");
            }
            prev.add(requireSha256((String) item, "previousHashes"));
        }

        String resolvedSource = (source == null || source.isEmpty()) ? "remote" : source;
        return new CoreRuntimeSpec(
            runtimeVersion,
            gameVersion,
            resourceVersion,
            catalogRelativePath,
            catalogSha256,
            bundleRelativePath,
            patchSha256,
            vanillaSha256,
            candidateUrl,
            vanillaUrl,
            candidateSize,
            vanillaSize,
            Collections.unmodifiableList(new ArrayList<>(prev)),
            resolvedSource
        );
    }

    private static String requireSha256(String raw, String field) throws IOException {
        String lower = raw == null ? "" : raw.trim().toLowerCase(Locale.ROOT);
        if (!SHA256_LOWER.matcher(lower).matches()) {
            throw new IOException("核心组件清单缺少有效 SHA-256（" + field + "）");
        }
        return lower;
    }

    private static String validateRuntimeUrl(String rawUrl) throws IOException {
        String trimmed = rawUrl == null ? "" : rawUrl.trim();
        URL url = PayloadDownloadManager.validateOfficialUrl(trimmed);
        if (url.getUserInfo() != null || (url.getPort() != -1 && url.getPort() != 443)
                || url.getQuery() != null || url.getRef() != null) {
            throw new IOException("核心组件下载地址包含不允许的参数或端口");
        }
        String path = url.getPath();
        if (path == null
                || !path.startsWith(RUNTIME_PATH_PREFIX)
                || path.contains("..")
                || path.contains("\\")
                || path.contains("//")) {
            throw new IOException("核心组件下载路径不在允许的运行时目录内");
        }
        return trimmed;
    }

    String toCanonicalJson() {
        Map<String, Object> root = new LinkedHashMap<>();
        root.put("formatVersion", 1);
        root.put("runtimeVersion", runtimeVersion);
        root.put("gameVersion", gameVersion);
        root.put("resourceVersion", resourceVersion);
        root.put("catalogRelativePath", catalogRelativePath);
        root.put("catalogSha256", catalogSha256);
        root.put("bundleRelativePath", bundleRelativePath);
        root.put("patchSha256", patchSha256);
        root.put("vanillaSha256", vanillaSha256);
        root.put("candidateUrl", candidateUrl);
        root.put("vanillaUrl", vanillaUrl);
        root.put("candidateSize", candidateSize);
        root.put("vanillaSize", vanillaSize);
        root.put("previousHashes", previousHashes);
        return MiniJson.stringify(root);
    }

    String catalogFileName() {
        int slash = catalogRelativePath.lastIndexOf('/');
        return slash >= 0 ? catalogRelativePath.substring(slash + 1) : catalogRelativePath;
    }

    boolean isKnownHash(String hash) {
        if (hash == null || hash.isEmpty()) return false;
        String lower = hash.trim().toLowerCase(Locale.ROOT);
        return lower.equals(vanillaSha256)
            || lower.equals(patchSha256)
            || previousHashes.contains(lower);
    }

    static void saveDiskCache(File filesDir, CoreRuntimeSpec spec, int appVersionCode) {
        if (filesDir == null || spec == null) return;
        try {
            if (!filesDir.isDirectory() && !filesDir.mkdirs()) return;
            writeUtf8Atomic(new File(filesDir, CACHE_FILE_NAME), spec.toCanonicalJson());
            writeUtf8Atomic(new File(filesDir, CACHE_VER_FILE_NAME), Integer.toString(appVersionCode));
        } catch (IOException ignored) {
        }
    }

    static CoreRuntimeSpec loadDiskCache(File filesDir, int currentAppVersionCode) {
        if (filesDir == null) return null;
        File verFile = new File(filesDir, CACHE_VER_FILE_NAME);
        File cacheFile = new File(filesDir, CACHE_FILE_NAME);
        if (!verFile.isFile() || !cacheFile.isFile() || cacheFile.length() <= 0L || cacheFile.length() > MAX_MANIFEST_BYTES) {
            return null;
        }
        try {
            String verText = readUtf8File(verFile, 64).trim();
            int savedCode = Integer.parseInt(verText);
            if (savedCode < currentAppVersionCode) {
                return null;
            }
            String json = readUtf8File(cacheFile, MAX_MANIFEST_BYTES);
            return parseAndValidate(json, "cache");
        } catch (Exception ignored) {
            cacheFile.delete();
            verFile.delete();
            return null;
        }
    }

    static synchronized CoreRuntimeSpec resolve(File filesDir, int currentAppVersionCode, boolean forceRefresh) {
        long now = System.currentTimeMillis();
        if (!forceRefresh && cachedSpec != null && now < cachedExpiryMs) {
            return cachedSpec;
        }
        try {
            String remoteJson = fetchRemoteManifest(MANIFEST_URL);
            CoreRuntimeSpec parsed = parseAndValidate(remoteJson, "remote");
            saveDiskCache(filesDir, parsed, currentAppVersionCode);
            cachedSpec = parsed;
            cachedExpiryMs = System.currentTimeMillis() + CACHE_TTL_MS;
            return parsed;
        } catch (Exception ignored) {
            CoreRuntimeSpec fallback = loadDiskCache(filesDir, currentAppVersionCode);
            if (fallback == null) {
                fallback = embedded();
            }
            cachedSpec = fallback;
            cachedExpiryMs = System.currentTimeMillis() + OFFLINE_BACKOFF_MS;
            return fallback;
        }
    }

    static synchronized void clearMemoryCacheForTest() {
        cachedSpec = null;
        cachedExpiryMs = 0L;
    }

    private static String fetchRemoteManifest(String urlStr) throws IOException {
        URL url = PayloadDownloadManager.validateOfficialUrl(urlStr);
        HttpsURLConnection conn = (HttpsURLConnection) url.openConnection();
        conn.setInstanceFollowRedirects(false);
        conn.setConnectTimeout(8000);
        conn.setReadTimeout(10000);
        conn.setRequestMethod("GET");
        conn.setRequestProperty("User-Agent", "XingShu-Android-Launcher/1");
        conn.setRequestProperty("Accept", "application/json");
        conn.setRequestProperty("Cache-Control", "no-cache");
        try {
            int code = conn.getResponseCode();
            if (code != HttpURLConnection.HTTP_OK) {
                throw new IOException("远端核心清单响应 HTTP " + code);
            }
            long cl = conn.getContentLengthLong();
            if (cl > MAX_MANIFEST_BYTES) {
                throw new IOException("远端核心清单体积超出上限");
            }
            try (InputStream in = conn.getInputStream()) {
                return readStreamUtf8(in, MAX_MANIFEST_BYTES);
            }
        } finally {
            conn.disconnect();
        }
    }

    static String readUtf8File(File file, int maxBytes) throws IOException {
        try (FileInputStream in = new FileInputStream(file)) {
            return readStreamUtf8(in, maxBytes);
        }
    }

    private static String readStreamUtf8(InputStream in, int maxBytes) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int total = 0;
        int n;
        while ((n = in.read(buf)) != -1) {
            total += n;
            if (total > maxBytes) {
                throw new IOException("核心清单数据超出大小限制");
            }
            out.write(buf, 0, n);
        }
        return new String(out.toByteArray(), StandardCharsets.UTF_8);
    }

    private static void writeUtf8Atomic(File target, String content) throws IOException {
        File parent = target.getParentFile();
        if (parent != null && !parent.isDirectory() && !parent.mkdirs()) {
            throw new IOException("无法创建缓存目录");
        }
        File tmp = new File(parent, target.getName() + "." + System.nanoTime() + ".tmp");
        try {
            try (FileOutputStream out = new FileOutputStream(tmp)) {
                out.write(content.getBytes(StandardCharsets.UTF_8));
                out.flush();
                try {
                    out.getFD().sync();
                } catch (IOException ignored) {
                }
            }
            if (target.exists() && !target.delete()) {
                throw new IOException("无法替换旧核心清单缓存");
            }
            if (!tmp.renameTo(target)) {
                throw new IOException("无法提交核心清单缓存");
            }
        } finally {
            if (tmp.exists()) {
                tmp.delete();
            }
        }
    }
}
