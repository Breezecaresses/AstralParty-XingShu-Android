using System;
using FairyGUI;
using Tools;
using UI;
using UnityEngine;

namespace XingShuRuntime.Utilities;

/// <summary>
/// Fixes a native UI overlap bug in <see cref="LoseCardWindow"/> where overflowing children
/// inside <see cref="UICom_Card"/> (especially alt-art <c>loader_CardFront</c> with Y=[-187..837]
/// and <c>bg_txt</c> with Y=[-38..696]) extend upward into Row 1 cards inside <c>list_Card</c>
/// (which only has a 25px <c>lineGap</c>), causing lower clicks on Row 1 to hit Row 2 instead.
/// </summary>
internal sealed class LoseCardHitboxGuard : MonoBehaviour
{
    private const float IdlePollSeconds = 0.10f;
    private static LoseCardHitboxGuard _instance;

    private float _nextPollAt;

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<LoseCardHitboxGuard>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPollAt) return;
        try
        {
            LoseCardWindow win = FindActiveLoseCardWindow();
            if (win == null || !(win.contentPane is UILoseCardWindow pane) || pane.isDisposed)
            {
                _nextPollAt = Time.unscaledTime + IdlePollSeconds;
                return;
            }
            if (pane.list_Card != null && !pane.list_Card.isDisposed)
            {
                ClampCardListHitboxes(pane.list_Card);
            }
        }
        catch (Exception)
        {
            _nextPollAt = Time.unscaledTime + IdlePollSeconds;
        }
    }

    private static LoseCardWindow FindActiveLoseCardWindow()
    {
        GRoot root = GRoot.inst;
        if (root != null)
        {
            for (int i = root.numChildren - 1; i >= 0; i--)
            {
                if (root.GetChildAt(i) is LoseCardWindow win && !win.isDisposed && win.visible && win.onStage && win.contentPane != null)
                {
                    return win;
                }
            }
        }
        if (SimpleSingletonProvider<UIManager>.hasInstance)
        {
            return ShownNativeWindows.Find<LoseCardWindow>(SimpleSingletonProvider<UIManager>.inst);
        }
        return null;
    }

    internal static void ClampCardListHitboxes(GList list)
    {
        int count = list.numChildren;
        for (int i = 0; i < count; i++)
        {
            if (!(list.GetChildAt(i) is UILoseCard_Button_Large btn) || btn.isDisposed)
            {
                continue;
            }

            // Clamp the button item container strictly to its own card frame [0..width, 0..height]
            // without clipping visual overflow so alt-art frames remain visually intact.
            ApplyRectHitArea(btn.container, btn.width, btn.height);
            if (!btn.opaque) btn.opaque = true;

            if (btn.com_Card is UICom_Card card && !card.isDisposed)
            {
                ApplyRectHitArea(card.container, card.width, card.height);
                DisableOverflowTouch(card.loader_CardFront);
                DisableOverflowTouch(card.bg_txt);
                DisableOverflowTouch(card.com_CardName);
                DisableOverflowTouch(card.txt_Name);
                DisableOverflowTouch(card.txt_Content);
            }
        }
    }

    private static void ApplyRectHitArea(Container container, float width, float height)
    {
        if (container == null || container.isDisposed || width <= 0f || height <= 0f) return;
        Rect targetRect = FloatingHintLayoutMath.ComputeCardHitRect(width, height);
        if (container.hitArea is RectHitTest existing)
        {
            if (existing.rect != targetRect)
            {
                existing.rect = targetRect;
            }
        }
        else
        {
            container.hitArea = new RectHitTest { rect = targetRect };
        }
    }

    private static void DisableOverflowTouch(GObject obj)
    {
        if (obj != null && !obj.isDisposed && obj.touchable)
        {
            obj.touchable = false;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
