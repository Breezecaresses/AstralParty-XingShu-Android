using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Role Selection Map Name Hint (选人地图, Tab 22).
    /// </summary>
    internal sealed class XingShuSelectRoleMapView
    {
        private readonly GameObject _root;
        private Toggle _selectRoleMapToggle;
        private Toggle _selectRoleMapLockToggle;
        private Slider _selectRoleMapScaleSlider;
        private Slider _selectRoleMapXSlider;
        private Slider _selectRoleMapYSlider;
        private Text _selectRoleMapScaleText;
        private Text _selectRoleMapXText;
        private Text _selectRoleMapYText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuSelectRoleMapView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_SelectRoleMap", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "SelectRoleMapScrollView", out GameObject content);

            GameObject mapHintCard = XingShuViewCardHelper.CreateCardContainer(content, "Card_SelectRoleMapName", 282f);
            Text mhTitle = XingShuUIFactory.CreateText(mapHintCard, "Title", "选人地图提示", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(mhTitle.rectTransform, new Vector2(20f, -12f), new Vector2(360f, 26f));

            Text mhDesc = XingShuUIFactory.CreateText(mapHintCard, "Description",
                "选人界面显示地图名称、模式与 PvE 难度；未锁定时可直接拖动",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(mhDesc.rectTransform, 40f, 20f, 40f);

            _selectRoleMapToggle = XingShuUIFactory.CreateToggle(mapHintCard, "SelectRoleMapToggle", "显示地图", SelectRoleMapNameHint.IsEnabled,
                value => { if (!_suppressCallbacks) SelectRoleMapNameHint.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_selectRoleMapToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));

            _selectRoleMapLockToggle = XingShuUIFactory.CreateToggle(mapHintCard, "SelectRoleMapLockToggle", "锁定位置（禁止拖动）", SelectRoleMapNameHint.Locked,
                value => { if (!_suppressCallbacks) SelectRoleMapNameHint.SetLocked(value); });
            XingShuViewCardHelper.PlaceTopLeft(_selectRoleMapLockToggle.GetComponent<RectTransform>(), new Vector2(20f, -66f), new Vector2(240f, 32f));

            Button resetMapBtn = XingShuViewCardHelper.CreateActionButton(mapHintCard, "ResetSelectRoleMapLayout", "恢复默认",
                () => { SelectRoleMapNameHint.ResetLayout(); UpdateSync(); }, 104f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetMapBtn.GetComponent<RectTransform>(), new Vector2(-20f, -66f), new Vector2(104f, 30f));

            XingShuViewCardHelper.AddPercentSliderRow(mapHintCard, "MapScale", "显示大小（60%–180%）", 106f,
                XingShuConfigKeys.SelectRoleMapNameScaleMin, XingShuConfigKeys.SelectRoleMapNameScaleMax,
                SelectRoleMapNameHint.Scale,
                v => { SelectRoleMapNameHint.SetScale(Mathf.Round(v * 20f) / 20f); UpdateSync(); },
                () => _suppressCallbacks,
                out _selectRoleMapScaleSlider, out _selectRoleMapScaleText);

            XingShuViewCardHelper.AddPercentSliderRow(mapHintCard, "MapPosX", "水平位置 X（左 → 右）", 160f,
                0f, 1f, SelectRoleMapNameHint.PosX,
                v => { SelectRoleMapNameHint.SetPosition(v, SelectRoleMapNameHint.PosY); UpdateSync(); },
                () => _suppressCallbacks,
                out _selectRoleMapXSlider, out _selectRoleMapXText);

            XingShuViewCardHelper.AddPercentSliderRow(mapHintCard, "MapPosY", "垂直位置 Y（上 → 下）", 214f,
                0f, 1f, SelectRoleMapNameHint.PosY,
                v => { SelectRoleMapNameHint.SetPosition(SelectRoleMapNameHint.PosX, v); UpdateSync(); },
                () => _suppressCallbacks,
                out _selectRoleMapYSlider, out _selectRoleMapYText);
        }

        internal void UpdateSync()
        {
            float scale = SelectRoleMapNameHint.Scale;
            float px = SelectRoleMapNameHint.PosX;
            float py = SelectRoleMapNameHint.PosY;
            bool locked = SelectRoleMapNameHint.Locked;
            _suppressCallbacks = true;
            if (_selectRoleMapToggle != null && _selectRoleMapToggle.isOn != SelectRoleMapNameHint.IsEnabled)
                _selectRoleMapToggle.isOn = SelectRoleMapNameHint.IsEnabled;
            if (_selectRoleMapScaleSlider != null && Mathf.Abs(_selectRoleMapScaleSlider.value - scale) > 0.005f)
                _selectRoleMapScaleSlider.value = scale;
            if (_selectRoleMapXSlider != null && Mathf.Abs(_selectRoleMapXSlider.value - px) > 0.005f)
                _selectRoleMapXSlider.value = px;
            if (_selectRoleMapYSlider != null && Mathf.Abs(_selectRoleMapYSlider.value - py) > 0.005f)
                _selectRoleMapYSlider.value = py;
            if (_selectRoleMapLockToggle != null && _selectRoleMapLockToggle.isOn != locked)
                _selectRoleMapLockToggle.isOn = locked;
            _suppressCallbacks = false;
            if (_selectRoleMapScaleText != null) _selectRoleMapScaleText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_selectRoleMapXText != null) _selectRoleMapXText.text = $"{Mathf.RoundToInt(px * 100f)}%";
            if (_selectRoleMapYText != null) _selectRoleMapYText.text = $"{Mathf.RoundToInt(py * 100f)}%";
        }
    }
}
