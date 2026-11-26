package app.xingshu.installer;

import org.junit.Test;
import static org.junit.Assert.*;

public final class ThemePaletteTest {
    @Test public void resolvesAllSixDesktopPresets() {
        assertEquals("星海蓝", ThemePalette.byId("azure").nameZh);
        assertEquals("星樱粉", ThemePalette.byId("rose").nameZh);
        assertEquals("星辉金", ThemePalette.byId("amber").nameZh);
        assertEquals("晴空青", ThemePalette.byId("mint").nameZh);
        assertEquals("星夜紫", ThemePalette.byId("violet").nameZh);
        ThemePalette obs = ThemePalette.byId("obsidian");
        assertEquals("玄素黑", obs.nameZh);
        assertTrue(obs.isDark);
        assertFalse(ThemePalette.byId("azure").isDark);
        assertEquals("azure", ThemePalette.byId("unknown").id);
    }

    @Test public void generatesCustomLightAndDarkPalettesAndFallsBackSafely() {
        ThemePalette light = ThemePalette.resolve("custom", "#E11D48", false);
        assertEquals("custom", light.id);
        assertFalse(light.isDark);
        assertNotEquals(light.canvas, light.ink);
        assertNotEquals(light.primary, light.surface);

        ThemePalette dark = ThemePalette.resolve("custom", "#10B981", true);
        assertEquals("custom", dark.id);
        assertTrue(dark.isDark);
        assertEquals(0xFFF1F5F9, dark.ink);

        assertEquals(ThemePalette.DEFAULT_CUSTOM_HEX, ThemePalette.normalizeHex("not-a-color"));
        assertEquals("#1370F3", ThemePalette.formatHex(ThemePalette.parseHexColor("bad")));
    }

    @Test public void roundTripsRgbHsvConversion() {
        int azure = 0xFF1370F3;
        float[] hsv = ThemePalette.rgbToHsv(azure);
        int rebuilt = ThemePalette.hsvToRgb(hsv[0], hsv[1], hsv[2]);
        int dr = Math.abs(((azure >> 16) & 0xFF) - ((rebuilt >> 16) & 0xFF));
        int dg = Math.abs(((azure >> 8) & 0xFF) - ((rebuilt >> 8) & 0xFF));
        int db = Math.abs((azure & 0xFF) - (rebuilt & 0xFF));
        assertTrue(dr <= 2 && dg <= 2 && db <= 2);
    }
}
