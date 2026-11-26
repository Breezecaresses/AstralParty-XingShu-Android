package app.xingshu.installer

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

enum class ThemeBrightnessMode(val prefValue: String, val labelZh: String) {
    SYSTEM("system", "跟随系统"),
    LIGHT("light", "浅色模式"),
    DARK("dark", "深色模式"),
    ;

    companion object {
        fun fromPref(value: String?): ThemeBrightnessMode =
            entries.firstOrNull { it.prefValue.equals(value, ignoreCase = true) } ?: SYSTEM
    }
}

private val XingShuTypography = Typography(
    headlineSmall = TextStyle(fontSize = 22.sp, fontWeight = FontWeight.Bold, lineHeight = 28.sp),
    titleLarge = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.Bold, lineHeight = 24.sp),
    titleMedium = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold, lineHeight = 21.sp),
    bodyMedium = TextStyle(fontSize = 13.5.sp, fontWeight = FontWeight.Normal, lineHeight = 19.sp),
    bodySmall = TextStyle(fontSize = 12.sp, fontWeight = FontWeight.Normal, lineHeight = 17.sp),
    labelMedium = TextStyle(fontSize = 11.5.sp, fontWeight = FontWeight.SemiBold, lineHeight = 16.sp),
)

private val XingShuShapes = Shapes(
    extraSmall = RoundedCornerShape(8.dp),
    small = RoundedCornerShape(12.dp),
    medium = RoundedCornerShape(18.dp),
    large = RoundedCornerShape(24.dp),
    extraLarge = RoundedCornerShape(28.dp),
)

fun buildXingShuColorScheme(
    themeId: String,
    customHex: String,
    isDark: Boolean,
): ColorScheme {
    val preset = ThemePalette.byId(themeId)
    val seedHex = if (themeId == "custom") {
        ThemePalette.normalizeHex(customHex)
    } else if (themeId == "obsidian") {
        "#3B82F6"
    } else {
        ThemePalette.formatHex(preset.primary)
    }
    val effective = ThemePalette.fromCustomHex(seedHex, isDark)
    val primary = Color(effective.primary)
    val onPrimary = if (primary.luminance() > 0.55f) Color(0xFF0B1220) else Color.White

    return if (isDark) {
        darkColorScheme(
            primary = primary,
            onPrimary = onPrimary,
            primaryContainer = Color(effective.accentSoft),
            onPrimaryContainer = Color(effective.accentInk),
            secondary = Color(effective.accentInk),
            onSecondary = Color(0xFF0E1726),
            secondaryContainer = Color(effective.surfaceSubtle),
            onSecondaryContainer = Color(effective.ink),
            tertiary = Color(0xFF5EEAD4),
            background = Color(effective.canvas),
            onBackground = Color(effective.ink),
            surface = Color(effective.surface),
            onSurface = Color(effective.ink),
            surfaceVariant = Color(effective.surfaceSubtle),
            onSurfaceVariant = Color(effective.inkMuted),
            surfaceContainerLowest = Color(effective.canvas),
            surfaceContainerLow = Color(effective.surfaceLog),
            surfaceContainer = Color(effective.surface),
            surfaceContainerHigh = Color(effective.surfaceSubtle),
            surfaceContainerHighest = Color(effective.accentSoft),
            outline = Color(effective.border),
            outlineVariant = Color(effective.border).copy(alpha = 0.55f),
            error = Color(0xFFF87171),
            onError = Color(0xFF3F0D0D),
            errorContainer = Color(0xFF5C1919),
            onErrorContainer = Color(0xFFFEE2E2),
        )
    } else {
        lightColorScheme(
            primary = primary,
            onPrimary = onPrimary,
            primaryContainer = Color(effective.accentSoft),
            onPrimaryContainer = Color(effective.accentInk),
            secondary = Color(effective.accentInk),
            onSecondary = Color.White,
            secondaryContainer = Color(effective.surfaceSubtle),
            onSecondaryContainer = Color(effective.ink),
            tertiary = Color(0xFF0F766E),
            background = Color(effective.canvas),
            onBackground = Color(effective.ink),
            surface = Color(effective.surface),
            onSurface = Color(effective.ink),
            surfaceVariant = Color(effective.surfaceSubtle),
            onSurfaceVariant = Color(effective.inkMuted),
            surfaceContainerLowest = Color.White,
            surfaceContainerLow = Color(effective.surfaceLog),
            surfaceContainer = Color(effective.surface),
            surfaceContainerHigh = Color(effective.surfaceSubtle),
            surfaceContainerHighest = Color(effective.accentSoft),
            outline = Color(effective.border),
            outlineVariant = Color(effective.border).copy(alpha = 0.60f),
            error = Color(0xFFDC2626),
            onError = Color.White,
            errorContainer = Color(0xFFFEE2E2),
            onErrorContainer = Color(0xFF7F1D1D),
        )
    }
}

@Composable
fun XingShuTheme(
    themeId: String,
    customHex: String,
    brightnessMode: ThemeBrightnessMode,
    content: @Composable () -> Unit,
) {
    val systemDark = isSystemInDarkTheme()
    val effectiveDark = when (brightnessMode) {
        ThemeBrightnessMode.SYSTEM -> systemDark || themeId == "obsidian"
        ThemeBrightnessMode.LIGHT -> false
        ThemeBrightnessMode.DARK -> true
    }
    val scheme = buildXingShuColorScheme(themeId, customHex, effectiveDark)
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as? Activity)?.window
            if (window != null) {
                val controller = WindowCompat.getInsetsController(window, view)
                controller.isAppearanceLightStatusBars = !effectiveDark
                controller.isAppearanceLightNavigationBars = !effectiveDark
            }
        }
    }
    MaterialTheme(
        colorScheme = scheme,
        typography = XingShuTypography,
        shapes = XingShuShapes,
        content = content,
    )
}
