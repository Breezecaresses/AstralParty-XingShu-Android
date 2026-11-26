package app.xingshu.installer;

import java.util.Locale;

/**
 * Pure-JVM theme palette matching Desktop XingShu Hub's 6 presets (星海蓝 / 星樱粉 /
 * 星辉金 / 晴空青 / 星夜紫 / 玄素黑) plus custom hex + light/dark generation.
 */
final class ThemePalette {
    static final String DEFAULT_THEME_ID = "azure";
    static final String DEFAULT_CUSTOM_HEX = "#1370F3";

    final String id;
    final String nameZh;
    final boolean isDark;
    final int headerStart;
    final int headerEnd;
    final int canvas;
    final int surface;
    final int surfaceSubtle;
    final int surfaceLog;
    final int border;
    final int ink;
    final int inkMuted;
    final int inkSubtle;
    final int primary;
    final int accentSoft;
    final int accentBorder;
    final int accentInk;
    final int okSoft;
    final int okBorder;
    final int okInk;
    final int navBg;
    final int navBorder;

    private ThemePalette(
            String id,
            String nameZh,
            boolean isDark,
            int headerStart,
            int headerEnd,
            int canvas,
            int surface,
            int surfaceSubtle,
            int surfaceLog,
            int border,
            int ink,
            int inkMuted,
            int inkSubtle,
            int primary,
            int accentSoft,
            int accentBorder,
            int accentInk,
            int okSoft,
            int okBorder,
            int okInk,
            int navBg,
            int navBorder) {
        this.id = id;
        this.nameZh = nameZh;
        this.isDark = isDark;
        this.headerStart = headerStart;
        this.headerEnd = headerEnd;
        this.canvas = canvas;
        this.surface = surface;
        this.surfaceSubtle = surfaceSubtle;
        this.surfaceLog = surfaceLog;
        this.border = border;
        this.ink = ink;
        this.inkMuted = inkMuted;
        this.inkSubtle = inkSubtle;
        this.primary = primary;
        this.accentSoft = accentSoft;
        this.accentBorder = accentBorder;
        this.accentInk = accentInk;
        this.okSoft = okSoft;
        this.okBorder = okBorder;
        this.okInk = okInk;
        this.navBg = navBg;
        this.navBorder = navBorder;
    }

    static final ThemePalette[] PRESETS = {
        new ThemePalette(
            "azure", "星海蓝", false,
            0xFF1269E6, 0xFF2B83F6,
            0xFFF3F7FE, 0xFFFFFFFF, 0xFFF0F6FF, 0xFFF8FAFC, 0xFFD9E6FA,
            0xFF0F172A, 0xFF475569, 0xFF64748B,
            0xFF1370F3, 0xFFEBF3FF, 0xFFC5DCFD, 0xFF1D67DF,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, 0xFFD9E6FA),
        new ThemePalette(
            "rose", "星樱粉", false,
            0xFFC7366B, 0xFFEC6398,
            0xFFFFF5F8, 0xFFFFFFFF, 0xFFFFF0F5, 0xFFFFF8FA, 0xFFFAD8E4,
            0xFF1F1220, 0xFF634857, 0xFF836576,
            0xFFD9467A, 0xFFFFEEF4, 0xFFF7BED3, 0xFFC03165,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, 0xFFFAD8E4),
        new ThemePalette(
            "amber", "星辉金", false,
            0xFFB45A0A, 0xFFE68D28,
            0xFFFFF8EE, 0xFFFFFFFF, 0xFFFFF3E0, 0xFFFFFAF2, 0xFFF6DEC0,
            0xFF23180B, 0xFF6A553E, 0xFF88725A,
            0xFFC96A12, 0xFFFFF4E6, 0xFFF4D0A4, 0xFFB0580B,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, 0xFFF6DEC0),
        new ThemePalette(
            "mint", "晴空青", false,
            0xFF0B7868, 0xFF22A894,
            0xFFF1FCF9, 0xFFFFFFFF, 0xFFE8FAF5, 0xFFF6FDFB, 0xFFC7EDE4,
            0xFF0E201D, 0xFF42625D, 0xFF62817B,
            0xFF0F8C7A, 0xFFE7F9F5, 0xFFB0E5DA, 0xFF0B7566,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, 0xFFC7EDE4),
        new ThemePalette(
            "violet", "星夜紫", false,
            0xFF4E3DC4, 0xFF7A6AE8,
            0xFFF5F4FF, 0xFFFFFFFF, 0xFFEFEDFF, 0xFFF8F7FF, 0xFFD9D5FB,
            0xFF16132E, 0xFF4E4970, 0xFF6C6791,
            0xFF5C4AD8, 0xFFEEECFF, 0xFFC8C1FA, 0xFF4C3BC4,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, 0xFFD9D5FB),
        new ThemePalette(
            "obsidian", "玄素黑", true,
            0xFF1E293B, 0xFF334155,
            0xFF0F172A, 0xFF172233, 0xFF1E2B3E, 0xFF131C2B, 0xFF2D3B4E,
            0xFFF1F5F9, 0xFFCBD5E1, 0xFF94A3B8,
            0xFF4B6382, 0xFF233247, 0xFF3B4F6B, 0xFF93C5FD,
            0xFF0F2E22, 0xFF1E5740, 0xFF6EE7B7,
            0xFF131D2D, 0xFF2A394D),
    };

    static ThemePalette byId(String id) {
        if (id != null) {
            for (ThemePalette p : PRESETS) {
                if (p.id.equalsIgnoreCase(id.trim())) return p;
            }
        }
        return PRESETS[0];
    }

    static ThemePalette resolve(String themeId, String customHex, boolean customDark) {
        if ("custom".equalsIgnoreCase(themeId == null ? "" : themeId.trim())) {
            return fromCustomHex(customHex, customDark);
        }
        return byId(themeId);
    }

    static String normalizeHex(String rawHex) {
        if (rawHex == null) return DEFAULT_CUSTOM_HEX;
        String clean = rawHex.trim();
        if (clean.startsWith("#")) clean = clean.substring(1);
        if (!clean.matches("[0-9a-fA-F]{6}")) return DEFAULT_CUSTOM_HEX;
        return "#" + clean.toUpperCase(Locale.ROOT);
    }

    static int parseHexColor(String rawHex) {
        String norm = normalizeHex(rawHex);
        return 0xFF000000 | Integer.parseInt(norm.substring(1), 16);
    }

    static String formatHex(int color) {
        return String.format(Locale.ROOT, "#%06X", color & 0xFFFFFF);
    }

    static float[] rgbToHsv(int color) {
        int r = (color >> 16) & 0xFF;
        int g = (color >> 8) & 0xFF;
        int b = color & 0xFF;
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;
        float max = Math.max(rf, Math.max(gf, bf));
        float min = Math.min(rf, Math.min(gf, bf));
        float d = max - min;
        float h = 0f;
        if (d > 1e-6f) {
            if (max == rf) h = 60f * (((gf - bf) / d) % 6f);
            else if (max == gf) h = 60f * (((bf - rf) / d) + 2f);
            else h = 60f * (((rf - gf) / d) + 4f);
            if (h < 0f) h += 360f;
        }
        float s = max <= 1e-6f ? 0f : d / max;
        return new float[] { h, s, max };
    }

    static int hsvToRgb(float h, float s, float v) {
        float hh = ((h % 360f) + 360f) % 360f;
        float ss = Math.max(0f, Math.min(1f, s));
        float vv = Math.max(0f, Math.min(1f, v));
        float c = vv * ss;
        float x = c * (1f - Math.abs((hh / 60f) % 2f - 1f));
        float m = vv - c;
        float r1, g1, b1;
        if (hh < 60f) { r1 = c; g1 = x; b1 = 0f; }
        else if (hh < 120f) { r1 = x; g1 = c; b1 = 0f; }
        else if (hh < 180f) { r1 = 0f; g1 = c; b1 = x; }
        else if (hh < 240f) { r1 = 0f; g1 = x; b1 = c; }
        else if (hh < 300f) { r1 = x; g1 = 0f; b1 = c; }
        else { r1 = c; g1 = 0f; b1 = x; }
        int r = Math.min(255, Math.max(0, Math.round((r1 + m) * 255f)));
        int g = Math.min(255, Math.max(0, Math.round((g1 + m) * 255f)));
        int b = Math.min(255, Math.max(0, Math.round((b1 + m) * 255f)));
        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    private static int mix(int a, int b, float ratioB) {
        float rb = Math.max(0f, Math.min(1f, ratioB));
        float ra = 1f - rb;
        int r = Math.round(((a >> 16) & 0xFF) * ra + ((b >> 16) & 0xFF) * rb);
        int g = Math.round(((a >> 8) & 0xFF) * ra + ((b >> 8) & 0xFF) * rb);
        int bl = Math.round((a & 0xFF) * ra + (b & 0xFF) * rb);
        return 0xFF000000 | (r << 16) | (g << 8) | bl;
    }

    static ThemePalette fromCustomHex(String hex, boolean darkMode) {
        int base = parseHexColor(hex);
        float[] hsv = rgbToHsv(base);
        float hue = hsv[0];
        float sat = Math.max(0.28f, hsv[1]);
        int headerStart = hsvToRgb(hue, Math.min(1f, sat * 1.05f), darkMode ? 0.52f : 0.84f);
        int headerEnd = hsvToRgb((hue + 6f) % 360f, Math.max(0.24f, sat * 0.90f), darkMode ? 0.68f : 0.95f);

        if (darkMode) {
            int primary = hsvToRgb(hue, Math.min(0.88f, sat), 0.84f);
            int canvas = mix(0xFF0D1420, primary, 0.08f);
            int surface = mix(0xFF151F2E, primary, 0.10f);
            int surfaceSubtle = mix(0xFF1C293C, primary, 0.14f);
            int surfaceLog = mix(0xFF111A27, primary, 0.08f);
            int border = mix(0xFF2B3A4F, primary, 0.24f);
            int accentSoft = mix(0xFF1A283B, primary, 0.28f);
            int accentBorder = mix(0xFF2F435E, primary, 0.45f);
            int accentInk = mix(0xFFFFFFFF, primary, 0.58f);
            return new ThemePalette(
                "custom", "自定义深色", true,
                headerStart, headerEnd,
                canvas, surface, surfaceSubtle, surfaceLog, border,
                0xFFF1F5F9, 0xFFCBD5E1, 0xFF94A3B8,
                primary, accentSoft, accentBorder, accentInk,
                0xFF0F2E22, 0xFF1E5740, 0xFF6EE7B7,
                surfaceLog, border);
        }

        int primary = hsvToRgb(hue, Math.min(0.95f, sat), Math.max(0.55f, Math.min(0.88f, hsv[2])));
        int canvas = mix(0xFFFFFFFF, primary, 0.055f);
        int surface = 0xFFFFFFFF;
        int surfaceSubtle = mix(0xFFFFFFFF, primary, 0.08f);
        int surfaceLog = mix(0xFFFFFFFF, primary, 0.03f);
        int border = mix(0xFFFFFFFF, primary, 0.22f);
        int accentSoft = mix(0xFFFFFFFF, primary, 0.10f);
        int accentBorder = mix(0xFFFFFFFF, primary, 0.30f);
        int accentInk = mix(primary, 0xFF000000, 0.14f);
        return new ThemePalette(
            "custom", "自定义配色", false,
            headerStart, headerEnd,
            canvas, surface, surfaceSubtle, surfaceLog, border,
            0xFF0F172A, 0xFF475569, 0xFF64748B,
            primary, accentSoft, accentBorder, accentInk,
            0xFFECFDF5, 0xFFA7F3D0, 0xFF15803D,
            0xFFFFFFFF, border);
    }
}
