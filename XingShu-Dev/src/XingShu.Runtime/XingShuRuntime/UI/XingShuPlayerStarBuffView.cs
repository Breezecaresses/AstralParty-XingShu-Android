using System;
using System.Collections.Generic;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Player Star Buff HUD (星级Buff, Tab 17).
    /// </summary>
    internal sealed class XingShuPlayerStarBuffView
    {
        private const int StarBuffRowsPerPage = 8;

        private readonly GameObject _root;
        private Toggle _starBuffEnabledToggle;
        private Toggle _starBuffLockToggle;
        private Text _starBuffSummaryText;
        private Button[] _starBuffAnchorButtons;
        private Text[] _starBuffAnchorBtnTexts;
        private Toggle _starBuffCatMoveToggle;
        private Toggle _starBuffCatHeroToggle;
        private Toggle _starBuffCatCombatToggle;
        private Toggle _starBuffCatDebuffToggle;
        private Toggle _starBuffCatEventToggle;
        private Toggle _starBuffCatOtherToggle;
        private Slider _starBuffScaleSlider;
        private Slider _starBuffOffXSlider;
        private Slider _starBuffOffYSlider;
        private Slider _starBuffMaxSlider;
        private Text _starBuffScaleText;
        private Text _starBuffOffXText;
        private Text _starBuffOffYText;
        private Text _starBuffMaxText;
        private InputField _starBuffSearchInput;
        private Button[] _starBuffFilterButtons;
        private Text[] _starBuffFilterBtnTexts;
        private Text _starBuffPageText;
        private readonly List<Toggle> _starBuffRowToggles = new List<Toggle>(8);
        private readonly List<Text> _starBuffRowLabels = new List<Text>(8);
        private readonly List<PlayerStarBuffHud.BuffCatalogEntry> _starBuffVisibleEntries = new List<PlayerStarBuffHud.BuffCatalogEntry>();
        private int _starBuffFilterTab;
        private string _starBuffSearchQuery = "";
        private int _starBuffPage;
        private int _lastStarBuffRevision = -1;
        private int _lastRenderedAnchorMode = -1;
        private bool _lastRenderedGlassActive;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuPlayerStarBuffView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_PlayerStarBuff", parent.gameObject);
            BuildUI();
            UpdateSync(forceCatalog: true);
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "PlayerStarBuffScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "PlayerStarBuffHudCard", 784f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "角色Buff显示（左侧头像⭐区域）", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(450f, 26f));

            _starBuffSummaryText = XingShuUIFactory.CreateText(card, "Description",
                "在左侧角色星级左右展示疾行/角色专属/战斗增益等 Buff；支持分类与单项精确开关",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(_starBuffSummaryText.rectTransform, 38f, 20f, 40f);

            _starBuffEnabledToggle = XingShuUIFactory.CreateToggle(card, "EnableStarBuffHud", "显示Buff", PlayerStarBuffHud.IsEnabled,
                value => { if (!_suppressCallbacks) PlayerStarBuffHud.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_starBuffEnabledToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(160f, 34f));

            _starBuffLockToggle = XingShuUIFactory.CreateToggle(card, "StarBuffLockToggle", "锁定位置", PlayerStarBuffHud.Locked,
                value => { if (!_suppressCallbacks) PlayerStarBuffHud.SetLocked(value); });
            XingShuViewCardHelper.PlaceTopLeft(_starBuffLockToggle.GetComponent<RectTransform>(), new Vector2(20f, -64f), new Vector2(130f, 30f));

            Button allOnBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffAllOnBtn", "全选Buff",
                () => { PlayerStarBuffHud.SetAllCategories(true); UpdateSync(forceCatalog: true); }, 92f, 30f, 12);
            XingShuViewCardHelper.PlaceTopLeft(allOnBtn.GetComponent<RectTransform>(), new Vector2(160f, -64f), new Vector2(92f, 30f));

            Button allOffBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffAllOffBtn", "全不选",
                () => { PlayerStarBuffHud.SetAllCategories(false); UpdateSync(forceCatalog: true); }, 82f, 30f, 12);
            XingShuViewCardHelper.PlaceTopLeft(allOffBtn.GetComponent<RectTransform>(), new Vector2(260f, -64f), new Vector2(82f, 30f));

            Button resetBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffResetBtn", "恢复默认",
                () => { PlayerStarBuffHud.ResetLayout(); UpdateSync(forceCatalog: true); }, 96f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetBtn.GetComponent<RectTransform>(), new Vector2(-20f, -64f), new Vector2(96f, 30f));

            Text anchorLabel = XingShuUIFactory.CreateText(card, "StarBuffAnchorLabel", "显示方位：", 12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceTopLeft(anchorLabel.rectTransform, new Vector2(20f, -102f), new Vector2(76f, 28f));

            string[] anchorNames = { "星级右侧", "星级左侧", "星级下方" };
            _starBuffAnchorButtons = new Button[anchorNames.Length];
            _starBuffAnchorBtnTexts = new Text[anchorNames.Length];
            for (int i = 0; i < anchorNames.Length; i++)
            {
                int capturedMode = i;
                Button btn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffAnchor_" + i, anchorNames[i],
                    () => { PlayerStarBuffHud.SetAnchorMode(capturedMode); UpdateSync(forceCatalog: false); }, 96f, 28f, 12);
                XingShuViewCardHelper.PlaceTopLeft(btn.GetComponent<RectTransform>(), new Vector2(96f + i * 106f, -102f), new Vector2(96f, 28f));
                _starBuffAnchorButtons[i] = btn;
                _starBuffAnchorBtnTexts[i] = btn.GetComponentInChildren<Text>();
            }

            _starBuffCatMoveToggle = CreateStarBuffCategoryToggle(card, "CatMove", "疾行/移速", 20f, -138f, StarBuffCategory.Move);
            _starBuffCatHeroToggle = CreateStarBuffCategoryToggle(card, "CatHero", "角色专属", 190f, -138f, StarBuffCategory.Hero);
            _starBuffCatCombatToggle = CreateStarBuffCategoryToggle(card, "CatCombat", "攻防战斗", 360f, -138f, StarBuffCategory.Combat);
            _starBuffCatDebuffToggle = CreateStarBuffCategoryToggle(card, "CatDebuff", "减益/毒咒", 20f, -172f, StarBuffCategory.Debuff);
            _starBuffCatEventToggle = CreateStarBuffCategoryToggle(card, "CatEvent", "事件/回复", 190f, -172f, StarBuffCategory.Event);
            _starBuffCatOtherToggle = CreateStarBuffCategoryToggle(card, "CatOther", "其他Buff", 360f, -172f, StarBuffCategory.Other);

            XingShuViewCardHelper.AddPercentSliderRow(card, "StarBuffScale", "Buff图标大小（50%–180%）", 210f,
                XingShuConfigKeys.PlayerStarBuffsScaleMin, XingShuConfigKeys.PlayerStarBuffsScaleMax,
                PlayerStarBuffHud.Scale,
                v => { PlayerStarBuffHud.SetScale(Mathf.Round(v * 20f) / 20f); UpdateSync(false); },
                () => _suppressCallbacks,
                out _starBuffScaleSlider, out _starBuffScaleText);

            XingShuViewCardHelper.AddSliderRow(card, "StarBuffOffX", "水平偏移 X（-220–260 px）", 264f,
                XingShuConfigKeys.PlayerStarBuffsOffsetXMin, XingShuConfigKeys.PlayerStarBuffsOffsetXMax,
                PlayerStarBuffHud.OffsetX, $"{Mathf.RoundToInt(PlayerStarBuffHud.OffsetX)} px",
                v => { PlayerStarBuffHud.SetOffset(Mathf.Round(v), PlayerStarBuffHud.OffsetY); UpdateSync(false); },
                () => _suppressCallbacks,
                out _starBuffOffXSlider, out _starBuffOffXText);

            XingShuViewCardHelper.AddSliderRow(card, "StarBuffOffY", "垂直偏移 Y（-160–160 px）", 318f,
                XingShuConfigKeys.PlayerStarBuffsOffsetYMin, XingShuConfigKeys.PlayerStarBuffsOffsetYMax,
                PlayerStarBuffHud.OffsetY, $"{Mathf.RoundToInt(PlayerStarBuffHud.OffsetY)} px",
                v => { PlayerStarBuffHud.SetOffset(PlayerStarBuffHud.OffsetX, Mathf.Round(v)); UpdateSync(false); },
                () => _suppressCallbacks,
                out _starBuffOffYSlider, out _starBuffOffYText);

            XingShuViewCardHelper.AddSliderRow(card, "StarBuffMaxIcons", "单玩家最多显示 Buff 数（3–16）", 372f,
                XingShuConfigKeys.PlayerStarBuffsMaxIconsMin, XingShuConfigKeys.PlayerStarBuffsMaxIconsMax,
                PlayerStarBuffHud.MaxIcons, $"{PlayerStarBuffHud.MaxIcons} 个",
                v => { PlayerStarBuffHud.SetMaxIcons(Mathf.RoundToInt(v)); UpdateSync(false); },
                () => _suppressCallbacks,
                out _starBuffMaxSlider, out _starBuffMaxText);

            Text listHeader = XingShuUIFactory.CreateText(card, "StarBuffListHeader", "可选 Buff 精确开关（点勾选框即可单独开启/关闭任意 Buff）", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(listHeader.rectTransform, new Vector2(20f, -430f), new Vector2(480f, 22f));

            Button refreshCatalogBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffRefreshCatalog", "刷新Buff",
                () => RefreshStarBuffCatalogRows(resetPage: false), 84f, 28f, 12);
            XingShuViewCardHelper.PlaceTopRight(refreshCatalogBtn.GetComponent<RectTransform>(), new Vector2(-20f, -427f), new Vector2(84f, 28f));

            _starBuffSearchInput = XingShuUIFactory.CreateInputField(
                card, "StarBuffSearchInput", "搜索 Buff 名称或 ID（如 疾行 / 狂暴 / 治愈）", "",
                "PlayerStarBuffSearch",
                q =>
                {
                    if (_suppressCallbacks) return;
                    _starBuffSearchQuery = q ?? "";
                    _starBuffPage = 0;
                    RefreshStarBuffCatalogRows(resetPage: true);
                },
                null, 12);
            XingShuViewCardHelper.PlaceStretchTop(_starBuffSearchInput.GetComponent<RectTransform>(), 458f, 30f, 40f);

            string[] filterLabels = { "全部", "局内", "疾行", "专属", "战斗", "减益", "事件/他" };
            _starBuffFilterButtons = new Button[filterLabels.Length];
            _starBuffFilterBtnTexts = new Text[filterLabels.Length];
            for (int i = 0; i < filterLabels.Length; i++)
            {
                int capturedTab = i;
                float bw = i == filterLabels.Length - 1 ? 74f : 60f;
                float bx = 20f + i * 64f;
                Button fBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffFilter_" + i, filterLabels[i],
                    () =>
                    {
                        _starBuffFilterTab = capturedTab;
                        _starBuffPage = 0;
                        RefreshStarBuffCatalogRows(resetPage: true);
                    }, bw, 26f, 11);
                XingShuViewCardHelper.PlaceTopLeft(fBtn.GetComponent<RectTransform>(), new Vector2(bx, -494f), new Vector2(bw, 26f));
                _starBuffFilterButtons[i] = fBtn;
                _starBuffFilterBtnTexts[i] = fBtn.GetComponentInChildren<Text>();
            }

            _starBuffRowToggles.Clear();
            _starBuffRowLabels.Clear();
            for (int r = 0; r < StarBuffRowsPerPage; r++)
            {
                int rowIdx = r;
                float rowY = -(526f + r * 28f);
                Toggle rowToggle = XingShuUIFactory.CreateToggle(card, "StarBuffRow_" + r, "", true,
                    val =>
                    {
                        if (_suppressCallbacks) return;
                        int entryIndex = _starBuffPage * StarBuffRowsPerPage + rowIdx;
                        if (entryIndex >= 0 && entryIndex < _starBuffVisibleEntries.Count)
                        {
                            var entry = _starBuffVisibleEntries[entryIndex];
                            PlayerStarBuffHud.SetBuffOverride(entry.BuffId, entry.Category, val);
                            UpdateSync(forceCatalog: true);
                        }
                    });
                var rRt = rowToggle.GetComponent<RectTransform>();
                rRt.anchorMin = new Vector2(0f, 1f);
                rRt.anchorMax = new Vector2(1f, 1f);
                rRt.pivot = new Vector2(0.5f, 1f);
                rRt.anchoredPosition = new Vector2(0f, rowY);
                rRt.sizeDelta = new Vector2(-40f, 26f);
                Text rowLbl = rowToggle.GetComponentInChildren<Text>();
                if (rowLbl != null) rowLbl.fontSize = 12;
                _starBuffRowToggles.Add(rowToggle);
                _starBuffRowLabels.Add(rowLbl);
            }

            Button prevPageBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffPrevPage", "上一页",
                () =>
                {
                    if (_starBuffPage > 0)
                    {
                        _starBuffPage--;
                        RenderStarBuffPage();
                    }
                }, 86f, 26f, 12);
            XingShuViewCardHelper.PlaceTopLeft(prevPageBtn.GetComponent<RectTransform>(), new Vector2(20f, -750f), new Vector2(86f, 26f));

            _starBuffPageText = XingShuUIFactory.CreateText(card, "StarBuffPageText", "第 1 / 1 页", 12, XingShuUITheme.SecondaryText, TextAnchor.MiddleCenter);
            _starBuffPageText.rectTransform.anchorMin = _starBuffPageText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _starBuffPageText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _starBuffPageText.rectTransform.anchoredPosition = new Vector2(0f, -750f);
            _starBuffPageText.rectTransform.sizeDelta = new Vector2(220f, 26f);

            Button nextPageBtn = XingShuViewCardHelper.CreateActionButton(card, "StarBuffNextPage", "下一页",
                () =>
                {
                    int totalPages = Math.Max(1, (_starBuffVisibleEntries.Count + StarBuffRowsPerPage - 1) / StarBuffRowsPerPage);
                    if (_starBuffPage + 1 < totalPages)
                    {
                        _starBuffPage++;
                        RenderStarBuffPage();
                    }
                }, 86f, 26f, 12);
            XingShuViewCardHelper.PlaceTopRight(nextPageBtn.GetComponent<RectTransform>(), new Vector2(-20f, -750f), new Vector2(86f, 26f));

            RefreshStarBuffCatalogRows(resetPage: true);
        }

        private Toggle CreateStarBuffCategoryToggle(GameObject card, string id, string label, float x, float y, StarBuffCategory category)
        {
            Toggle toggle = XingShuUIFactory.CreateToggle(card, "StarBuff_" + id, label, PlayerStarBuffHud.GetCategoryEnabled(category),
                val =>
                {
                    if (_suppressCallbacks) return;
                    PlayerStarBuffHud.SetCategoryEnabled(category, val);
                    UpdateSync(forceCatalog: true);
                });
            XingShuViewCardHelper.PlaceTopLeft(toggle.GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(160f, 30f));
            return toggle;
        }

        private void RefreshStarBuffCatalogRows(bool resetPage)
        {
            _starBuffVisibleEntries.Clear();
            var list = PlayerStarBuffHud.GetCatalogEntries(_starBuffSearchQuery, _starBuffFilterTab);
            for (int i = 0; i < list.Count; i++)
            {
                _starBuffVisibleEntries.Add(list[i]);
            }
            int totalPages = Math.Max(1, (_starBuffVisibleEntries.Count + StarBuffRowsPerPage - 1) / StarBuffRowsPerPage);
            if (resetPage || _starBuffPage >= totalPages)
            {
                _starBuffPage = resetPage ? 0 : Math.Max(0, totalPages - 1);
            }
            if (_starBuffFilterButtons != null)
            {
                for (int i = 0; i < _starBuffFilterButtons.Length; i++)
                {
                    if (_starBuffFilterButtons[i] != null)
                    {
                        XingShuGlassSkin.ApplySelectableButton(_starBuffFilterButtons[i], i == _starBuffFilterTab);
                    }
                }
            }
            if (!XingShuGlassSkin.IsActive && _starBuffFilterBtnTexts != null)
            {
                for (int i = 0; i < _starBuffFilterBtnTexts.Length; i++)
                {
                    if (_starBuffFilterBtnTexts[i] != null)
                    {
                        _starBuffFilterBtnTexts[i].color = i == _starBuffFilterTab ? XingShuUITheme.LimeAccent : XingShuUITheme.PrimaryText;
                    }
                }
            }
            RenderStarBuffPage();
        }

        private void RenderStarBuffPage()
        {
            int total = _starBuffVisibleEntries.Count;
            int totalPages = Math.Max(1, (total + StarBuffRowsPerPage - 1) / StarBuffRowsPerPage);
            if (_starBuffPage >= totalPages) _starBuffPage = totalPages - 1;
            if (_starBuffPage < 0) _starBuffPage = 0;

            bool prevSuppress = _suppressCallbacks;
            _suppressCallbacks = true;
            try
            {
                int start = _starBuffPage * StarBuffRowsPerPage;
                for (int r = 0; r < _starBuffRowToggles.Count; r++)
                {
                    Toggle tg = _starBuffRowToggles[r];
                    if (tg == null) continue;
                    int idx = start + r;
                    if (idx < total)
                    {
                        var entry = _starBuffVisibleEntries[idx];
                        tg.gameObject.SetActive(true);
                        tg.isOn = entry.Enabled;
                        if (r < _starBuffRowLabels.Count && _starBuffRowLabels[r] != null)
                        {
                            string prefix = entry.IsActiveInMatch ? "[局内] " : "";
                            string catName = PlayerStarBuffPolicy.GetCategoryLabel(entry.Category);
                            _starBuffRowLabels[r].text = $"{prefix}{entry.Name}  ({catName} · #{entry.BuffId})";
                        }
                    }
                    else
                    {
                        tg.gameObject.SetActive(false);
                    }
                }
            }
            finally
            {
                _suppressCallbacks = prevSuppress;
            }

            if (_starBuffPageText != null)
            {
                _starBuffPageText.text = $"第 {_starBuffPage + 1} / {totalPages} 页（共 {total} 项）";
            }
        }

        internal void UpdateSync(bool forceCatalog = false)
        {
            if (_starBuffEnabledToggle == null) return;
            float scale = PlayerStarBuffHud.Scale;
            float offX = PlayerStarBuffHud.OffsetX;
            float offY = PlayerStarBuffHud.OffsetY;
            int maxIcons = PlayerStarBuffHud.MaxIcons;
            int anchorMode = PlayerStarBuffHud.AnchorMode;
            bool locked = PlayerStarBuffHud.Locked;

            _suppressCallbacks = true;
            _starBuffEnabledToggle.isOn = PlayerStarBuffHud.IsEnabled;
            if (_starBuffLockToggle != null) _starBuffLockToggle.isOn = locked;
            if (_starBuffCatMoveToggle != null) _starBuffCatMoveToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Move);
            if (_starBuffCatHeroToggle != null) _starBuffCatHeroToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Hero);
            if (_starBuffCatCombatToggle != null) _starBuffCatCombatToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Combat);
            if (_starBuffCatDebuffToggle != null) _starBuffCatDebuffToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Debuff);
            if (_starBuffCatEventToggle != null) _starBuffCatEventToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Event);
            if (_starBuffCatOtherToggle != null) _starBuffCatOtherToggle.isOn = PlayerStarBuffHud.GetCategoryEnabled(StarBuffCategory.Other);
            if (_starBuffScaleSlider != null && Mathf.Abs(_starBuffScaleSlider.value - scale) > 0.005f) _starBuffScaleSlider.value = scale;
            if (_starBuffOffXSlider != null && Mathf.Abs(_starBuffOffXSlider.value - offX) > 0.5f) _starBuffOffXSlider.value = offX;
            if (_starBuffOffYSlider != null && Mathf.Abs(_starBuffOffYSlider.value - offY) > 0.5f) _starBuffOffYSlider.value = offY;
            if (_starBuffMaxSlider != null && Mathf.RoundToInt(_starBuffMaxSlider.value) != maxIcons) _starBuffMaxSlider.value = maxIcons;
            _suppressCallbacks = false;

            if (_starBuffScaleText != null) _starBuffScaleText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_starBuffOffXText != null) _starBuffOffXText.text = $"{Mathf.RoundToInt(offX)} px";
            if (_starBuffOffYText != null) _starBuffOffYText.text = $"{Mathf.RoundToInt(offY)} px";
            if (_starBuffMaxText != null) _starBuffMaxText.text = $"{maxIcons} 个";
            bool glassActive = XingShuGlassSkin.IsActive;
            if (forceCatalog || anchorMode != _lastRenderedAnchorMode || glassActive != _lastRenderedGlassActive)
            {
                _lastRenderedAnchorMode = anchorMode;
                _lastRenderedGlassActive = glassActive;
                if (_starBuffAnchorButtons != null)
                {
                    for (int i = 0; i < _starBuffAnchorButtons.Length; i++)
                    {
                        if (_starBuffAnchorButtons[i] != null)
                        {
                            XingShuGlassSkin.ApplySelectableButton(_starBuffAnchorButtons[i], i == anchorMode);
                        }
                    }
                }
                if (!glassActive && _starBuffAnchorBtnTexts != null)
                {
                    for (int i = 0; i < _starBuffAnchorBtnTexts.Length; i++)
                    {
                        if (_starBuffAnchorBtnTexts[i] != null)
                        {
                            _starBuffAnchorBtnTexts[i].color = i == anchorMode ? XingShuUITheme.LimeAccent : XingShuUITheme.PrimaryText;
                        }
                    }
                }
            }
            if (_starBuffSummaryText != null)
            {
                _starBuffSummaryText.text = $"已开启 {PlayerStarBuffHud.EnabledCategoryCount}/6 类 Buff · 单项自定义 {PlayerStarBuffHud.OverrideCount} 项 · 可拖动/调偏移";
            }
            if (forceCatalog || _lastStarBuffRevision != PlayerStarBuffHud.Revision)
            {
                _lastStarBuffRevision = PlayerStarBuffHud.Revision;
                RefreshStarBuffCatalogRows(resetPage: false);
            }
        }
    }
}
