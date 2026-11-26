using System;
using UnityEngine;
using XingShuRuntime.Utilities;

namespace UnityEngine
{
    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
}

namespace XingShuTests
{
    internal static class HintLayoutAndHitboxTests
    {
        private static void AssertTrue(bool cond, string message)
        {
            if (!cond)
            {
                throw new Exception("Assertion failed: " + message);
            }
        }

        private static void AssertNear(float actual, float expected, float eps, string label)
        {
            if (Math.Abs(actual - expected) > eps)
            {
                throw new Exception(string.Format("Assertion failed [{0}]: expected {1}, got {2}", label, expected, actual));
            }
        }

        public static int Main()
        {
            TestFiniteClamp();
            TestClampedPlacementAndRoundTrip();
            TestDiscardCardHitboxContainment();
            Console.WriteLine("HintLayoutAndHitboxTests: ALL PASSED");
            return 0;
        }

        private static void TestFiniteClamp()
        {
            AssertNear(FloatingHintLayoutMath.FiniteClamp(float.NaN, 0f, 1f, 0.42f), 0.42f, 1e-5f, "NaN fallback");
            AssertNear(FloatingHintLayoutMath.FiniteClamp(float.PositiveInfinity, 0.6f, 1.8f, 1.0f), 1.0f, 1e-5f, "Inf fallback");
            AssertNear(FloatingHintLayoutMath.FiniteClamp(-0.2f, 0f, 1f, 0.5f), 0f, 1e-5f, "Min clamp");
            AssertNear(FloatingHintLayoutMath.FiniteClamp(2.5f, 0.6f, 1.8f, 1.0f), 1.8f, 1e-5f, "Max clamp");
            AssertNear(FloatingHintLayoutMath.FiniteClamp(1.25f, 0.6f, 1.8f, 1.0f), 1.25f, 1e-5f, "In-range");
        }

        private static void TestClampedPlacementAndRoundTrip()
        {
            const float gw = 1920f;
            const float gh = 1080f;
            const float bw = 320f;
            const float bh = 110f;
            const float margin = 12f;

            float[] scales = { 0.4f, 0.6f, 1.0f, 1.45f, 1.8f, 2.5f };
            float[] normsX = { 0.0f, 0.03f, 0.5f, 0.97f, 0.98f, 1.0f };
            float[] normsY = { 0.0f, 0.16f, 0.56f, 1.0f };

            foreach (float scaleRaw in scales)
            {
                float safeScale = FloatingHintLayoutMath.FiniteClamp(scaleRaw, FloatingHintLayoutMath.HintMinScale, FloatingHintLayoutMath.HintMaxScale, 1f);
                foreach (float nx in normsX)
                {
                    foreach (float ny in normsY)
                    {
                        float x, y;
                        FloatingHintLayoutMath.ComputeClampedXY(gw, gh, bw, bh, scaleRaw, nx, ny, margin, out x, out y);
                        AssertTrue(x >= margin - 1e-3f, "X within left margin");
                        AssertTrue(y >= margin - 1e-3f, "Y within top margin");
                        AssertTrue(x + bw * safeScale <= gw - margin + 1e-3f, "X+scaledW within right margin");
                        AssertTrue(y + bh * safeScale <= gh - margin + 1e-3f, "Y+scaledH within bottom margin");

                        float rtX, rtY;
                        FloatingHintLayoutMath.ComputeNormalizedFromXY(gw, gh, bw, bh, scaleRaw, x, y, margin, out rtX, out rtY);
                        AssertNear(rtX, nx, 1e-3f, "RoundTrip X");
                        AssertNear(rtY, ny, 1e-3f, "RoundTrip Y");
                    }
                }
            }

            // Extreme out-of-bounds drag clamping
            float cx, cy;
            FloatingHintLayoutMath.ClampXYToBounds(gw, gh, bw, bh, 1.5f, -500f, 5000f, margin, out cx, out cy);
            AssertNear(cx, margin, 1e-3f, "Clamp negative X");
            AssertNear(cy, gh - bh * 1.5f - margin, 1e-3f, "Clamp overflow Y");

            // Narrow viewport should collapse safely to margin without NaN
            FloatingHintLayoutMath.ComputeClampedXY(200f, 100f, bw, bh, 1.8f, 0.9f, 0.9f, margin, out cx, out cy);
            AssertNear(cx, margin, 1e-3f, "Small viewport X");
            AssertNear(cy, margin, 1e-3f, "Small viewport Y");
        }

        private static void TestDiscardCardHitboxContainment()
        {
            // Native LoseCard_Button_Large dimensions (340x510)
            Rect cardRect = FloatingHintLayoutMath.ComputeCardHitRect(340f, 510f);
            AssertNear(cardRect.x, 0f, 1e-5f, "HitRect x");
            AssertNear(cardRect.y, 0f, 1e-5f, "HitRect y");
            AssertNear(cardRect.width, 340f, 1e-5f, "HitRect w");
            AssertNear(cardRect.height, 510f, 1e-5f, "HitRect h");

            Rect zeroRect = FloatingHintLayoutMath.ComputeCardHitRect(0f, -10f);
            AssertNear(zeroRect.width, 1f, 1e-5f, "Min HitRect w");
            AssertNear(zeroRect.height, 1f, 1e-5f, "Min HitRect h");

            // Click near bottom of Row 1 card stays inside Row 1 card bounds
            AssertTrue(FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 170f, 495f), "Row 1 lower area click valid");
            AssertTrue(FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 0f, 0f), "Top-left corner valid");

            // Overflowing Row 2 loader_CardFront extends above Row 2 origin (-148px into Row 1);
            // clamped local hitArea of Row 2 must reject negative local Y and points outside width/height.
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 170f, -100f), "Row 2 overflow above origin rejected");
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 170f, -1f), "Just above card rejected");
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, -5f, 250f), "Left outside rejected");
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 340f, 250f), "Right exclusive edge rejected");
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(340f, 510f, 170f, 510f), "Bottom exclusive edge rejected");
            AssertTrue(!FloatingHintLayoutMath.ContainsCardHitPoint(0f, 510f, 10f, 10f), "Zero width rejected");
        }
    }
}
