using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Contract implemented by external optimization mods or third-party XingShu plugins
    /// that mount a custom settings page into the built-in Function Center.
    /// </summary>
    public interface IXingShuExternalPage
    {
        string Id { get; }
        string Title { get; }
        string Category { get; }
        string Subtitle { get; }
        int Order { get; }
        bool IsAvailable();
        void BuildContent(GameObject parent);
        void OnSync();
    }

    internal sealed class DelegateExternalPage : IXingShuExternalPage
    {
        private readonly Func<bool> _isAvailable;
        private readonly Action<GameObject> _buildContent;
        private readonly Action _onSync;

        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string Subtitle { get; }
        public int Order { get; }

        internal DelegateExternalPage(
            string id,
            string title,
            string category,
            string subtitle,
            int order,
            Func<bool> isAvailable,
            Action<GameObject> buildContent,
            Action onSync)
        {
            Id = id ?? "";
            Title = title ?? "";
            Category = string.IsNullOrEmpty(category) ? "扩展插件" : category;
            Subtitle = subtitle ?? "";
            Order = order;
            _isAvailable = isAvailable;
            _buildContent = buildContent;
            _onSync = onSync;
        }

        public bool IsAvailable() => _isAvailable == null || _isAvailable();

        public void BuildContent(GameObject parent) => _buildContent?.Invoke(parent);

        public void OnSync() => _onSync?.Invoke();
    }

    /// <summary>
    /// Public thread-safe registry and glass UI helper surface for external optimization mods
    /// and plugins targeting the XingShu v2 Glassmorphism Function Center framework.
    /// Kept free of desktop P/Invoke so both PC and Android plugins share one API.
    /// </summary>
    public static class XingShuCenterPluginHost
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IXingShuExternalPage> Pages = new List<IXingShuExternalPage>();

        public static event Action PagesChanged;

        public static void RegisterPage(IXingShuExternalPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.Id) || string.IsNullOrEmpty(page.Title))
                return;

            lock (SyncRoot)
            {
                for (int i = Pages.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(Pages[i].Id, page.Id, StringComparison.OrdinalIgnoreCase))
                        Pages.RemoveAt(i);
                }
                Pages.Add(page);
                Pages.Sort(ComparePages);
            }
            PagesChanged?.Invoke();
        }

        public static void RegisterSimplePage(
            string id,
            string title,
            string category,
            string subtitle,
            int order,
            Action<GameObject> buildContent,
            Action onSync = null,
            Func<bool> isAvailable = null)
        {
            RegisterPage(new DelegateExternalPage(id, title, category, subtitle, order, isAvailable, buildContent, onSync));
        }

        public static bool UnregisterPage(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool removed = false;
            lock (SyncRoot)
            {
                for (int i = Pages.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(Pages[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        Pages.RemoveAt(i);
                        removed = true;
                    }
                }
            }
            if (removed) PagesChanged?.Invoke();
            return removed;
        }

        public static IXingShuExternalPage[] GetPagesSnapshot()
        {
            lock (SyncRoot)
            {
                return Pages.ToArray();
            }
        }

        private static int ComparePages(IXingShuExternalPage a, IXingShuExternalPage b)
        {
            int c = a.Order.CompareTo(b.Order);
            return c != 0 ? c : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        // ---- Reusable builder primitives for external plugin authors ----

        public static ScrollRect CreatePageScrollContainer(GameObject parent, string name, out GameObject content)
        {
            return XingShuViewCardHelper.CreatePageScroll(parent, name, out content);
        }

        public static GameObject CreateCard(GameObject scrollContent, string name, float height, bool accentHeader = false)
        {
            return XingShuViewCardHelper.CreateCardContainer(scrollContent, name, height, accentHeader);
        }

        public static Text CreateLabel(
            GameObject card,
            string name,
            string text,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            return XingShuUIFactory.CreateText(card, name, text, fontSize, color, alignment, style);
        }

        public static Toggle CreateToggle(GameObject card, string name, string label, bool initialValue, Action<bool> onValueChanged)
        {
            return XingShuUIFactory.CreateToggle(card, name, label, initialValue, onValueChanged);
        }

        public static Button CreateButton(GameObject card, string name, string label, Action onClick, float width = 148f, float height = 36f, int fontSize = 13)
        {
            return XingShuViewCardHelper.CreateActionButton(card, name, label, onClick, width, height, fontSize);
        }

        public static Slider CreateSlider(GameObject card, string name, float min, float max, float initial, Action<float> onChanged)
        {
            return XingShuUIFactory.CreateSlider(card, name, min, max, initial, onChanged);
        }
    }
}
