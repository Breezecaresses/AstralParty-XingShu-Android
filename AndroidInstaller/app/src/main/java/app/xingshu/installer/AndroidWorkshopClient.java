package app.xingshu.installer;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import javax.net.ssl.HttpsURLConnection;

/**
 * Fetches and resolves Android PE compatible mods (both .xspkg optimization modules and modimg packs)
 * from the official XingShu Workshop API.
 */
final class AndroidWorkshopClient {
    static final String ORIGIN = "https://" + PayloadDownloadManager.OFFICIAL_HOST;
    private static final int MAX_API_BYTES = 4 * 1024 * 1024;
    private static final int MAX_CANDIDATES = 48;

    static final class WorkshopMod {
        final String id;
        final String slug;
        final String name;
        final String author;
        final String category;
        final String categoryLabel;
        final String description;
        final String versionId;
        final String version;
        final String versionTitle;
        final String fileName;
        final long sizeBytes;
        final String sha256;
        final String downloadUrl;
        final String platformLabel;
        final String installKind;
        final String targetInfo;
        final String notes;
        final long downloads;
        final String previewUrl;
        final boolean androidInstallable;
        final List<PlatformAccountClient.ReactionEntry> reactions;
        final int reactionTotal;

        WorkshopMod(
                String id,
                String slug,
                String name,
                String author,
                String category,
                String categoryLabel,
                String description,
                String versionId,
                String version,
                String versionTitle,
                String fileName,
                long sizeBytes,
                String sha256,
                String downloadUrl,
                String platformLabel,
                String installKind,
                String targetInfo,
                String notes,
                long downloads) {
            this(id, slug, name, author, category, categoryLabel, description,
                    versionId, version, versionTitle, fileName, sizeBytes, sha256,
                    downloadUrl, platformLabel, installKind, targetInfo, notes, downloads, "", true,
                    Collections.emptyList(), 0);
        }

        WorkshopMod(
                String id,
                String slug,
                String name,
                String author,
                String category,
                String categoryLabel,
                String description,
                String versionId,
                String version,
                String versionTitle,
                String fileName,
                long sizeBytes,
                String sha256,
                String downloadUrl,
                String platformLabel,
                String installKind,
                String targetInfo,
                String notes,
                long downloads,
                String previewUrl) {
            this(id, slug, name, author, category, categoryLabel, description,
                    versionId, version, versionTitle, fileName, sizeBytes, sha256,
                    downloadUrl, platformLabel, installKind, targetInfo, notes, downloads, previewUrl, true,
                    Collections.emptyList(), 0);
        }

        WorkshopMod(
                String id,
                String slug,
                String name,
                String author,
                String category,
                String categoryLabel,
                String description,
                String versionId,
                String version,
                String versionTitle,
                String fileName,
                long sizeBytes,
                String sha256,
                String downloadUrl,
                String platformLabel,
                String installKind,
                String targetInfo,
                String notes,
                long downloads,
                String previewUrl,
                boolean androidInstallable) {
            this(id, slug, name, author, category, categoryLabel, description,
                    versionId, version, versionTitle, fileName, sizeBytes, sha256,
                    downloadUrl, platformLabel, installKind, targetInfo, notes, downloads, previewUrl,
                    androidInstallable, Collections.emptyList(), 0);
        }

        WorkshopMod(
                String id,
                String slug,
                String name,
                String author,
                String category,
                String categoryLabel,
                String description,
                String versionId,
                String version,
                String versionTitle,
                String fileName,
                long sizeBytes,
                String sha256,
                String downloadUrl,
                String platformLabel,
                String installKind,
                String targetInfo,
                String notes,
                long downloads,
                String previewUrl,
                boolean androidInstallable,
                List<PlatformAccountClient.ReactionEntry> reactions,
                int reactionTotal) {
            this.id = id == null ? "" : id;
            this.slug = slug == null ? "" : slug;
            this.name = name == null ? "" : name;
            this.author = author == null ? "" : author;
            this.category = category == null ? "" : category;
            this.categoryLabel = categoryLabel == null ? "" : categoryLabel;
            this.description = description == null ? "" : description;
            this.versionId = versionId == null ? "" : versionId;
            this.version = version == null ? "" : version;
            this.versionTitle = versionTitle == null ? "" : versionTitle;
            this.fileName = fileName == null ? "" : fileName;
            this.sizeBytes = sizeBytes;
            this.sha256 = sha256 == null ? "" : sha256.toLowerCase(Locale.ROOT);
            this.downloadUrl = downloadUrl == null ? "" : downloadUrl;
            this.platformLabel = platformLabel == null ? (androidInstallable ? "手机端 (PE)" : "仅 PC 端") : platformLabel;
            this.installKind = installKind == null ? "" : installKind;
            this.targetInfo = targetInfo == null ? "" : targetInfo;
            this.notes = notes == null ? "" : notes;
            this.downloads = downloads;
            this.previewUrl = previewUrl == null ? "" : previewUrl;
            this.androidInstallable = androidInstallable;
            this.reactions = reactions == null ? Collections.emptyList() : reactions;
            this.reactionTotal = Math.max(0, reactionTotal);
        }

        WorkshopMod withReactions(List<PlatformAccountClient.ReactionEntry> nextReactions, int nextTotal) {
            return new WorkshopMod(
                    id, slug, name, author, category, categoryLabel, description,
                    versionId, version, versionTitle, fileName, sizeBytes, sha256,
                    downloadUrl, platformLabel, installKind, targetInfo, notes,
                    downloads, previewUrl, androidInstallable, nextReactions, nextTotal);
        }

        boolean isXspkgModule() {
            return "xspkg".equalsIgnoreCase(installKind);
        }

        String webDetailUrl() {
            String key = slug.isEmpty() ? id : slug;
            return ORIGIN + "/xingshu-hub/extensions/mods/" + key;
        }
    }

    private AndroidWorkshopClient() { }

    static List<WorkshopMod> fetchAndroidMods() throws Exception {
        String bust = "?t=" + System.currentTimeMillis();
        String listJson = httpGetJson(ORIGIN + "/api/workshop/mods" + bust);
        List<String> multiVersionIds = new ArrayList<>();
        List<WorkshopMod> resolved = new ArrayList<>(parseAllLiveModsFromList(listJson, multiVersionIds));
        int detailLimit = Math.min(multiVersionIds.size(), MAX_CANDIDATES);
        for (int i = 0; i < detailLimit; i++) {
            String modId = multiVersionIds.get(i);
            if (!isSafeModId(modId)) continue;
            try {
                String encoded = URLEncoder.encode(modId, "UTF-8").replace("+", "%20");
                String detailJson = httpGetJson(ORIGIN + "/api/workshop/mods/" + encoded + bust);
                WorkshopMod parsed = parseAndroidVersionFromDetail(detailJson);
                if (parsed != null) {
                    resolved.add(parsed);
                }
            } catch (Exception ignored) {
                // Skip transient item error without breaking the rest of the catalog.
            }
        }
        return sortLiveWorkshopMods(resolved);
    }

    static boolean isSafeModId(String modId) {
        return modId != null && modId.matches("[A-Za-z0-9._-]{1,120}");
    }

    static List<String> extractAndroidCandidateIds(String listJson) throws IOException {
        Map<String, Object> root = MiniJson.parseObject(listJson);
        Object rawItems = root.get("items");
        if (!(rawItems instanceof List<?>)) {
            return Collections.emptyList();
        }
        List<String> ids = new ArrayList<>();
        for (Object obj : (List<?>) rawItems) {
            if (!(obj instanceof Map<?, ?>)) continue;
            @SuppressWarnings("unchecked")
            Map<String, Object> item = (Map<String, Object>) obj;
            String id = MiniJson.stringField(item, "id");
            if (!isSafeModId(id)) continue;
            if (isAndroidCandidateMap(item)) {
                ids.add(id);
            }
        }
        return ids;
    }

    /**
     * Parses single-version ANDROID_CN mods (.xspkg modules or single-package PE mods) directly
     * from /api/workshop/mods without redundant per-item detail round-trips, collecting any
     * multi-version items (like NunNan PE/PC split archives) into multiVersionIdsOut.
     */
    @SuppressWarnings("unchecked")
    static List<WorkshopMod> parseAndroidModsFromListFast(String listJson, List<String> multiVersionIdsOut) throws IOException {
        Map<String, Object> root = MiniJson.parseObject(listJson);
        Object rawItems = root.get("items");
        if (!(rawItems instanceof List<?>)) {
            return Collections.emptyList();
        }
        List<WorkshopMod> direct = new ArrayList<>();
        for (Object obj : (List<?>) rawItems) {
            if (!(obj instanceof Map<?, ?>)) continue;
            Map<String, Object> item = (Map<String, Object>) obj;
            String id = MiniJson.stringField(item, "id");
            if (!isSafeModId(id) || !isAndroidCandidateMap(item)) continue;

            long versionCount = MiniJson.longField(item, "versionCount", 1L);
            String kind = MiniJson.stringField(item, "installKind");
            String dlUrl = resolveOfficialWorkshopUrl(MiniJson.stringField(item, "downloadUrl"));
            String sha = MiniJson.stringField(item, "sha256");
            long size = MiniJson.longField(item, "sizeBytes", 0L);

            boolean fastEligible = versionCount <= 1L
                && dlUrl != null
                && PayloadDownloadManager.isValidSha256(sha)
                && size > 0L
                && ("xspkg".equalsIgnoreCase(kind) || containsString(item.get("platforms"), "ANDROID_CN"));

            if (fastEligible) {
                direct.add(buildDirectModFromItem(item, id, kind, dlUrl, sha, size, true));
            } else if (multiVersionIdsOut != null) {
                multiVersionIdsOut.add(id);
            }
        }
        return direct;
    }

    /**
     * Parses ALL live Workshop entries (both PE-installable mods and PC-only community browse mods)
     * so mobile users immediately see every newly published Workshop project.
     */
    @SuppressWarnings("unchecked")
    static List<WorkshopMod> parseAllLiveModsFromList(String listJson, List<String> multiVersionIdsOut) throws IOException {
        Map<String, Object> root = MiniJson.parseObject(listJson);
        Object rawItems = root.get("items");
        if (!(rawItems instanceof List<?>)) {
            return Collections.emptyList();
        }
        List<WorkshopMod> mods = new ArrayList<>();
        for (Object obj : (List<?>) rawItems) {
            if (!(obj instanceof Map<?, ?>)) continue;
            Map<String, Object> item = (Map<String, Object>) obj;
            String id = MiniJson.stringField(item, "id");
            if (!isSafeModId(id)) continue;

            String kind = MiniJson.stringField(item, "installKind");
            String dlUrl = resolveOfficialWorkshopUrl(MiniJson.stringField(item, "downloadUrl"));
            String sha = MiniJson.stringField(item, "sha256");
            long size = MiniJson.longField(item, "sizeBytes", 0L);

            if (isAndroidCandidateMap(item)) {
                long versionCount = MiniJson.longField(item, "versionCount", 1L);
                boolean fastEligible = versionCount <= 1L
                    && dlUrl != null
                    && PayloadDownloadManager.isValidSha256(sha)
                    && size > 0L
                    && ("xspkg".equalsIgnoreCase(kind) || containsString(item.get("platforms"), "ANDROID_CN"));
                if (fastEligible) {
                    mods.add(buildDirectModFromItem(item, id, kind, dlUrl, sha, size, true));
                } else if (multiVersionIdsOut != null) {
                    multiVersionIdsOut.add(id);
                }
            } else {
                mods.add(buildDirectModFromItem(item, id, kind, dlUrl == null ? "" : dlUrl, sha, size, false));
            }
        }
        return mods;
    }

    static List<WorkshopMod> sortLiveWorkshopMods(List<WorkshopMod> input) {
        List<WorkshopMod> indexed = new ArrayList<>(input);
        indexed.sort((a, b) -> {
            int rankA = sortBucketRank(a);
            int rankB = sortBucketRank(b);
            if (rankA != rankB) {
                return Integer.compare(rankA, rankB);
            }
            if (rankA < 2) {
                int dlCmp = Long.compare(b.downloads, a.downloads);
                if (dlCmp != 0) return dlCmp;
            }
            return a.name.compareToIgnoreCase(b.name);
        });
        return indexed;
    }

    private static int sortBucketRank(WorkshopMod mod) {
        if (mod.androidInstallable && !mod.isXspkgModule()) return 0;
        if (!mod.androidInstallable) return 1;
        return 2;
    }

    private static WorkshopMod buildDirectModFromItem(
            Map<String, Object> item,
            String id,
            String kind,
            String dlUrl,
            String sha,
            long size,
            boolean androidInstallable) {
        String label;
        if (!androidInstallable) {
            label = "仅 PC 端";
        } else if ("xspkg".equalsIgnoreCase(kind)) {
            label = "PC · 手机端";
        } else {
            label = "手机端 (PE)";
            Object rawLabels = item.get("platformLabels");
            if (rawLabels instanceof List<?> && !((List<?>) rawLabels).isEmpty()) {
                List<?> lbls = (List<?>) rawLabels;
                label = lbls.size() > 1 ? "PC · 手机端" : String.valueOf(lbls.get(0));
            }
        }
        String ver = MiniJson.stringField(item, "version").trim();
        if (ver.isEmpty()) ver = "1.0.0";
        return new WorkshopMod(
                id,
                MiniJson.stringField(item, "slug"),
                MiniJson.stringField(item, "name"),
                MiniJson.stringField(item, "author"),
                MiniJson.stringField(item, "category"),
                MiniJson.stringField(item, "categoryLabel"),
                MiniJson.stringField(item, "description"),
                "",
                ver,
                "",
                MiniJson.stringField(item, "packageFile"),
                size,
                sha,
                dlUrl,
                label,
                kind,
                MiniJson.stringField(item, "targetInfo"),
                "",
                MiniJson.longField(item, "downloads", 0L),
                extractPreviewUrl(item),
                androidInstallable,
                PlatformAccountClient.parseReactionList(item.get("reactions")),
                (int) MiniJson.longField(item, "reactionTotal", 0L));
    }

    static WorkshopMod parseAndroidVersionFromDetail(String detailJson) throws IOException {
        Map<String, Object> root = MiniJson.parseObject(detailJson);
        Map<String, Object> item = MiniJson.objectField(root, "item");
        if (item == null) {
            item = root;
        }
        String id = MiniJson.stringField(item, "id");
        if (!isSafeModId(id)) return null;

        String slug = MiniJson.stringField(item, "slug");
        String name = MiniJson.stringField(item, "name");
        String author = MiniJson.stringField(item, "author");
        String category = MiniJson.stringField(item, "category");
        String categoryLabel = MiniJson.stringField(item, "categoryLabel");
        String description = MiniJson.stringField(item, "description");
        String targetInfo = MiniJson.stringField(item, "targetInfo");
        String previewUrl = extractPreviewUrl(item);

        Map<String, Object> chosenVersion = selectBestAndroidVersion(item.get("versions"));
        if (chosenVersion != null) {
            String dlUrl = resolveOfficialWorkshopUrl(MiniJson.stringField(chosenVersion, "downloadUrl"));
            String sha = MiniJson.stringField(chosenVersion, "sha256");
            long size = MiniJson.longField(chosenVersion, "sizeBytes", 0L);
            if (dlUrl == null || !PayloadDownloadManager.isValidSha256(sha) || size <= 0L) {
                return null;
            }
            String vId = MiniJson.stringField(chosenVersion, "id");
            String ver = MiniJson.stringField(chosenVersion, "version");
            String vTitle = MiniJson.stringField(chosenVersion, "title");
            String fileName = MiniJson.stringField(chosenVersion, "fileName");
            String pLabel = MiniJson.stringField(chosenVersion, "platformLabel");
            String iKind = MiniJson.stringField(chosenVersion, "installKind");
            String notes = MiniJson.stringField(chosenVersion, "notes");
            long dlCount = MiniJson.longField(chosenVersion, "downloads",
                    MiniJson.longField(item, "downloads", 0L));
            return new WorkshopMod(
                    id, slug, name, author, category, categoryLabel, description,
                    vId, ver, vTitle, fileName, size, sha, dlUrl,
                    pLabel.isEmpty() ? "手机端 (PE)" : pLabel,
                    iKind, targetInfo, notes, dlCount, previewUrl, true,
                    PlatformAccountClient.parseReactionList(item.get("reactions")),
                    (int) MiniJson.longField(item, "reactionTotal", 0L));
        }

        if (containsString(item.get("platforms"), "ANDROID_CN")
                || "modimg".equalsIgnoreCase(MiniJson.stringField(item, "installKind"))) {
            String dlUrl = resolveOfficialWorkshopUrl(MiniJson.stringField(item, "downloadUrl"));
            String sha = MiniJson.stringField(item, "sha256");
            long size = MiniJson.longField(item, "sizeBytes", 0L);
            if (dlUrl == null || !PayloadDownloadManager.isValidSha256(sha) || size <= 0L) {
                return null;
            }
            String kind = MiniJson.stringField(item, "installKind");
            return buildDirectModFromItem(item, id, kind, dlUrl, sha, size, true);
        }
        return null;
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> selectBestAndroidVersion(Object rawVersions) {
        if (!(rawVersions instanceof List<?>)) return null;
        Map<String, Object> fallbackPe = null;
        for (Object obj : (List<?>) rawVersions) {
            if (!(obj instanceof Map<?, ?>)) continue;
            Map<String, Object> ver = (Map<String, Object>) obj;
            boolean hasAndroid = containsString(ver.get("platforms"), "ANDROID_CN");
            String installKind = MiniJson.stringField(ver, "installKind");
            String platLabel = MiniJson.stringField(ver, "platformLabel");
            String title = MiniJson.stringField(ver, "title");
            boolean labelPe = containsPeHint(platLabel) || containsPeHint(title);
            if (hasAndroid) {
                return ver;
            }
            if (fallbackPe == null && ("modimg".equalsIgnoreCase(installKind) || "xspkg".equalsIgnoreCase(installKind) || labelPe)) {
                fallbackPe = ver;
            }
        }
        return fallbackPe;
    }

    private static boolean isAndroidCandidateMap(Map<String, Object> item) {
        if (containsString(item.get("platforms"), "ANDROID_CN")) return true;
        if ("modimg".equalsIgnoreCase(MiniJson.stringField(item, "installKind"))) return true;
        Object labels = item.get("platformLabels");
        if (labels instanceof List<?>) {
            for (Object label : (List<?>) labels) {
                if (label != null && containsPeHint(String.valueOf(label))) {
                    return true;
                }
            }
        }
        return false;
    }

    private static boolean containsString(Object rawList, String expected) {
        if (!(rawList instanceof List<?>)) return false;
        for (Object item : (List<?>) rawList) {
            if (item != null && expected.equalsIgnoreCase(String.valueOf(item).trim())) {
                return true;
            }
        }
        return false;
    }

    private static boolean containsPeHint(String text) {
        if (text == null || text.isEmpty()) return false;
        String upper = text.toUpperCase(Locale.ROOT);
        return upper.contains("ANDROID") || upper.contains("PE") || text.contains("手机");
    }

    private static String extractPreviewUrl(Map<String, Object> item) {
        String raw = MiniJson.stringField(item, "previewUrl");
        if (raw.isEmpty()) raw = MiniJson.stringField(item, "featuredImageUrl");
        return resolveOfficialPreviewUrl(raw);
    }

    static String resolveOfficialPreviewUrl(String pathOrUrl) {
        if (pathOrUrl == null) return "";
        String trimmed = pathOrUrl.trim();
        if (trimmed.isEmpty()) return "";
        if (trimmed.startsWith("/api/workshop/") || trimmed.startsWith("/uploads/")) {
            return ORIGIN + trimmed;
        }
        if (trimmed.startsWith(ORIGIN + "/")) {
            return trimmed;
        }
        return "";
    }

    static String resolveOfficialWorkshopUrl(String pathOrUrl) {
        if (pathOrUrl == null) return null;
        String trimmed = pathOrUrl.trim();
        if (trimmed.startsWith("/api/workshop/mods/")) {
            return ORIGIN + trimmed;
        }
        if (trimmed.startsWith(ORIGIN + "/api/workshop/mods/")) {
            return trimmed;
        }
        return null;
    }

    private static String httpGetJson(String urlStr) throws IOException {
        URL url = PayloadDownloadManager.validateOfficialUrl(urlStr);
        HttpsURLConnection conn = (HttpsURLConnection) url.openConnection();
        conn.setInstanceFollowRedirects(false);
        conn.setUseCaches(false);
        conn.setConnectTimeout(15000);
        conn.setReadTimeout(20000);
        conn.setRequestMethod("GET");
        conn.setRequestProperty("User-Agent", "XingShu-Android-Launcher/1");
        conn.setRequestProperty("Accept", "application/json");
        conn.setRequestProperty("Cache-Control", "no-cache");
        conn.setRequestProperty("Pragma", "no-cache");
        try {
            int status = conn.getResponseCode();
            if (status != HttpURLConnection.HTTP_OK) {
                throw new IOException("星枢工坊返回 HTTP " + status);
            }
            try (InputStream in = conn.getInputStream()) {
                ByteArrayOutputStream out = new ByteArrayOutputStream();
                byte[] buf = new byte[8192];
                int total = 0;
                int n;
                while ((n = in.read(buf)) != -1) {
                    total += n;
                    if (total > MAX_API_BYTES) {
                        throw new IOException("星枢工坊响应超出大小限制");
                    }
                    out.write(buf, 0, n);
                }
                return new String(out.toByteArray(), StandardCharsets.UTF_8);
            }
        } finally {
            conn.disconnect();
        }
    }
}
