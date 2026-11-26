package app.xingshu.installer;

import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import javax.net.ssl.HttpsURLConnection;

/**
 * Lightweight client for XingShu Platform Account (/api/platform/*) authentication
 * and Workshop sticker reactions (/api/platform/reactions/set).
 */
final class PlatformAccountClient {
    static final String ORIGIN = "https://" + PayloadDownloadManager.OFFICIAL_HOST;
    private static final int MAX_JSON_BYTES = 1024 * 1024;

    static final class PlatformUser {
        final String id;
        final long uid;
        final String username;
        final String displayName;
        final String role;
        final String avatarColor;

        PlatformUser(String id, long uid, String username, String displayName, String role, String avatarColor) {
            this.id = id == null ? "" : id;
            this.uid = uid;
            this.username = username == null ? "" : username;
            this.displayName = displayName == null ? "" : displayName;
            this.role = role == null ? "" : role;
            this.avatarColor = avatarColor == null ? "" : avatarColor;
        }
    }

    static final class ReactionEntry {
        final String emoteId;
        final int count;

        ReactionEntry(String emoteId, int count) {
            this.emoteId = emoteId == null ? "" : emoteId;
            this.count = Math.max(0, count);
        }
    }

    static final class SessionResult {
        final String token;
        final PlatformUser user;
        final Map<String, String> reactions;

        SessionResult(String token, PlatformUser user, Map<String, String> reactions) {
            this.token = token == null ? "" : token;
            this.user = user;
            this.reactions = reactions == null ? Collections.emptyMap() : reactions;
        }
    }

    static final class ReactionUpdateResult {
        final String modId;
        final String viewerEmoteId;
        final List<ReactionEntry> reactions;
        final int reactionTotal;
        final Map<String, String> userReactions;

        ReactionUpdateResult(
                String modId,
                String viewerEmoteId,
                List<ReactionEntry> reactions,
                int reactionTotal,
                Map<String, String> userReactions) {
            this.modId = modId == null ? "" : modId;
            this.viewerEmoteId = viewerEmoteId == null ? "" : viewerEmoteId;
            this.reactions = reactions == null ? Collections.emptyList() : reactions;
            this.reactionTotal = Math.max(0, reactionTotal);
            this.userReactions = userReactions == null ? Collections.emptyMap() : userReactions;
        }
    }

    static final class UnauthorizedException extends IOException {
        UnauthorizedException(String message) {
            super(message);
        }
    }

    private PlatformAccountClient() { }

    static SessionResult login(String username, String password, String deviceLabel) throws Exception {
        JSONObject body = new JSONObject();
        body.put("username", username == null ? "" : username.trim());
        body.put("password", password == null ? "" : password);
        body.put("clientType", "android");
        body.put("deviceLabel", deviceLabel == null ? "Android" : deviceLabel);
        Map<String, Object> resp = requestJson("POST", "/api/platform/login", null, body.toString());
        String token = MiniJson.stringField(resp, "token");
        PlatformUser user = parseUser(MiniJson.objectField(resp, "user"));
        if (token.isEmpty() || user == null) {
            throw new IOException("登录响应缺少有效账号凭据");
        }
        return new SessionResult(token, user, parseUserReactions(MiniJson.objectField(resp, "reactions")));
    }

    static SessionResult register(String username, String password, String displayName, String deviceLabel) throws Exception {
        JSONObject body = new JSONObject();
        body.put("username", username == null ? "" : username.trim());
        body.put("password", password == null ? "" : password);
        body.put("displayName", displayName == null ? "" : displayName.trim());
        body.put("clientType", "android");
        body.put("deviceLabel", deviceLabel == null ? "Android" : deviceLabel);
        Map<String, Object> resp = requestJson("POST", "/api/platform/register", null, body.toString());
        String token = MiniJson.stringField(resp, "token");
        PlatformUser user = parseUser(MiniJson.objectField(resp, "user"));
        if (token.isEmpty() || user == null) {
            throw new IOException("注册响应缺少有效账号凭据");
        }
        return new SessionResult(token, user, parseUserReactions(MiniJson.objectField(resp, "reactions")));
    }

    static SessionResult fetchMe(String token) throws Exception {
        Map<String, Object> resp = requestJson("GET", "/api/platform/me", token, null);
        PlatformUser user = parseUser(MiniJson.objectField(resp, "user"));
        if (user == null) {
            throw new UnauthorizedException("星枢平台会话已过期");
        }
        return new SessionResult(token, user, parseUserReactions(MiniJson.objectField(resp, "reactions")));
    }

    static void logout(String token) {
        if (token == null || token.trim().isEmpty()) return;
        try {
            requestJson("POST", "/api/platform/logout", token, "{}");
        } catch (Exception ignored) { }
    }

    static ReactionUpdateResult setModReaction(String token, String modId, String emoteId, String action) throws Exception {
        JSONObject body = new JSONObject();
        body.put("modId", modId == null ? "" : modId);
        body.put("emoteId", emoteId == null ? "" : emoteId);
        body.put("action", action == null ? "toggle" : action);
        Map<String, Object> resp = requestJson("POST", "/api/platform/reactions/set", token, body.toString());
        String respModId = MiniJson.stringField(resp, "modId");
        String viewerEmoteId = MiniJson.stringField(resp, "viewerEmoteId");
        List<ReactionEntry> reactions = parseReactionList(resp.get("reactions"));
        int total = (int) MiniJson.longField(resp, "reactionTotal", 0L);
        Map<String, String> userReactions = parseUserReactions(MiniJson.objectField(resp, "userReactions"));
        return new ReactionUpdateResult(respModId, viewerEmoteId, reactions, total, userReactions);
    }

    @SuppressWarnings("unchecked")
    static List<ReactionEntry> parseReactionList(Object raw) {
        if (!(raw instanceof List<?>)) return Collections.emptyList();
        List<ReactionEntry> out = new ArrayList<>();
        for (Object item : (List<?>) raw) {
            if (!(item instanceof Map<?, ?>)) continue;
            Map<String, Object> map = (Map<String, Object>) item;
            String emoteId = MiniJson.stringField(map, "emoteId").trim();
            int count = (int) MiniJson.longField(map, "count", 0L);
            if (!emoteId.isEmpty() && count > 0) {
                out.add(new ReactionEntry(emoteId, count));
            }
        }
        return Collections.unmodifiableList(out);
    }

    private static PlatformUser parseUser(Map<String, Object> map) {
        if (map == null) return null;
        String id = MiniJson.stringField(map, "id");
        if (id.isEmpty()) return null;
        return new PlatformUser(
                id,
                MiniJson.longField(map, "uid", 0L),
                MiniJson.stringField(map, "username"),
                MiniJson.stringField(map, "displayName"),
                MiniJson.stringField(map, "role"),
                MiniJson.stringField(map, "avatarColor"));
    }

    private static Map<String, String> parseUserReactions(Map<String, Object> map) {
        if (map == null || map.isEmpty()) return Collections.emptyMap();
        Map<String, String> out = new LinkedHashMap<>();
        for (Map.Entry<String, Object> e : map.entrySet()) {
            String k = e.getKey() == null ? "" : e.getKey().trim();
            String v = e.getValue() == null ? "" : String.valueOf(e.getValue()).trim();
            if (!k.isEmpty() && !v.isEmpty()) {
                out.put(k, v);
            }
        }
        return out;
    }

    private static Map<String, Object> requestJson(String method, String path, String token, String jsonBody) throws Exception {
        URL url = PayloadDownloadManager.validateOfficialUrl(ORIGIN + path);
        HttpURLConnection rawConn = (HttpURLConnection) url.openConnection();
        if (!(rawConn instanceof HttpsURLConnection)) {
            throw new IOException("仅允许 HTTPS 官方接口连接");
        }
        HttpsURLConnection conn = (HttpsURLConnection) rawConn;
        conn.setInstanceFollowRedirects(false);
        conn.setConnectTimeout(10000);
        conn.setReadTimeout(15000);
        conn.setRequestMethod(method);
        conn.setRequestProperty("Accept", "application/json");
        conn.setRequestProperty("User-Agent", "XingShu-Android/" + BuildConfig.VERSION_NAME);
        if (token != null && !token.trim().isEmpty()) {
            conn.setRequestProperty("Authorization", "Bearer " + token.trim());
        }
        if (jsonBody != null) {
            byte[] payload = jsonBody.getBytes(StandardCharsets.UTF_8);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            conn.setFixedLengthStreamingMode(payload.length);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(payload);
                os.flush();
            }
        }
        try {
            int code = conn.getResponseCode();
            InputStream stream = code >= 200 && code < 300 ? conn.getInputStream() : conn.getErrorStream();
            String respText = "";
            if (stream != null) {
                try (InputStream in = stream) {
                    ByteArrayOutputStream out = new ByteArrayOutputStream();
                    byte[] buf = new byte[8192];
                    int total = 0;
                    int n;
                    while ((n = in.read(buf)) != -1) {
                        total += n;
                        if (total > MAX_JSON_BYTES) throw new IOException("响应数据超出大小限制");
                        out.write(buf, 0, n);
                    }
                    respText = new String(out.toByteArray(), StandardCharsets.UTF_8);
                }
            }
            Map<String, Object> parsed = respText.trim().startsWith("{")
                    ? MiniJson.parseObject(respText)
                    : Collections.emptyMap();
            if (code == 401) {
                String msg = MiniJson.stringField(parsed, "error");
                throw new UnauthorizedException(msg.isEmpty() ? "星枢平台账号未登录或会话已失效" : msg);
            }
            if (code < 200 || code >= 300) {
                String msg = MiniJson.stringField(parsed, "error");
                throw new IOException(msg.isEmpty() ? ("服务器返回错误 HTTP " + code) : msg);
            }
            return parsed;
        } finally {
            conn.disconnect();
        }
    }
}
