using System;
using UnityEngine;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Pure math helpers for normalized floating hint placement and discard card hitbox bounds.
    /// Kept decoupled from FairyGUI types so conformance/unit tests can run headlessly.
    /// </summary>
    internal static class FloatingHintLayoutMath
    {
    internal const float HintMinScale = 0.60f;
    internal const float HintMaxScale = 1.80f;
    internal const float DefaultScreenMargin = 12f;

    internal static float FiniteClamp(float value, float min, float max, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    internal static void ComputeClampedXY(
        float containerWidth,
        float containerHeight,
        float baseWidth,
        float baseHeight,
        float scale,
        float normX,
        float normY,
        float margin,
        out float outX,
        out float outY)
    {
        float safeScale = FiniteClamp(scale, HintMinScale, HintMaxScale, 1.0f);
        float nx = FiniteClamp(normX, 0f, 1f, 0.5f);
        float ny = FiniteClamp(normY, 0f, 1f, 0.5f);
        float m = Math.Max(0f, margin);
        float scaledW = Math.Max(1f, baseWidth) * safeScale;
        float scaledH = Math.Max(1f, baseHeight) * safeScale;
        float minX = m;
        float minY = m;
        float maxX = Math.Max(minX, containerWidth - scaledW - m);
        float maxY = Math.Max(minY, containerHeight - scaledH - m);
        outX = minX + (maxX - minX) * nx;
        outY = minY + (maxY - minY) * ny;
    }

    internal static void ComputeNormalizedFromXY(
        float containerWidth,
        float containerHeight,
        float baseWidth,
        float baseHeight,
        float scale,
        float posX,
        float posY,
        float margin,
        out float normX,
        out float normY)
    {
        float safeScale = FiniteClamp(scale, HintMinScale, HintMaxScale, 1.0f);
        float m = Math.Max(0f, margin);
        float scaledW = Math.Max(1f, baseWidth) * safeScale;
        float scaledH = Math.Max(1f, baseHeight) * safeScale;
        float minX = m;
        float minY = m;
        float spanX = Math.Max(0f, containerWidth - scaledW - 2f * m);
        float spanY = Math.Max(0f, containerHeight - scaledH - 2f * m);
        normX = spanX > 0.5f ? FiniteClamp((posX - minX) / spanX, 0f, 1f, 0.5f) : 0f;
        normY = spanY > 0.5f ? FiniteClamp((posY - minY) / spanY, 0f, 1f, 0.5f) : 0f;
    }

    internal static void ClampXYToBounds(
        float containerWidth,
        float containerHeight,
        float baseWidth,
        float baseHeight,
        float scale,
        float x,
        float y,
        float margin,
        out float clampedX,
        out float clampedY)
    {
        float safeScale = FiniteClamp(scale, HintMinScale, HintMaxScale, 1.0f);
        float m = Math.Max(0f, margin);
        float scaledW = Math.Max(1f, baseWidth) * safeScale;
        float scaledH = Math.Max(1f, baseHeight) * safeScale;
        float maxX = Math.Max(m, containerWidth - scaledW - m);
        float maxY = Math.Max(m, containerHeight - scaledH - m);
        clampedX = FiniteClamp(x, m, maxX, m);
        clampedY = FiniteClamp(y, m, maxY, m);
    }

    internal static Rect ComputeCardHitRect(float width, float height)
    {
        return new Rect(0f, 0f, Math.Max(1f, width), Math.Max(1f, height));
    }

        internal static bool ContainsCardHitPoint(float rectWidth, float rectHeight, float localX, float localY)
        {
            if (rectWidth <= 0f || rectHeight <= 0f) return false;
            return localX >= 0f && localX < rectWidth && localY >= 0f && localY < rectHeight;
        }
    }
}
