using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// Standalone page view for Player Combat Status HUD (角色状态, Tab 16).
    /// </summary>
    internal sealed class XingShuPlayerCombatHudView
    {
        private readonly GameObject _root;
        private Toggle _playerHudEnabledToggle;
        private Toggle _playerHudHeadToggle;
        private Toggle _playerHudAvatarToggle;
        private Toggle _playerHudLockToggle;
        private Toggle _playerHudCdToggle;
        private Toggle _playerHudHpToggle;
        private Toggle _playerHudAtkDefToggle;
        private Toggle _playerHudMoveToggle;
        private Slider _playerHudScaleSlider;
        private Slider _playerHudOffXSlider;
        private Slider _playerHudOffYSlider;
        private Slider _playerHudHeadOffYSlider;
        private Text _playerHudScaleText;
        private Text _playerHudOffXText;
        private Text _playerHudOffYText;
        private Text _playerHudHeadOffYText;
        private bool _suppressCallbacks;

        internal GameObject Root => _root;

        internal XingShuPlayerCombatHudView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_PlayerCombatHud", parent.gameObject);
            BuildUI();
            UpdateSync();
        }

        private void BuildUI()
        {
            XingShuViewCardHelper.CreatePageScroll(_root, "PlayerCombatHudScrollView", out GameObject content);

            GameObject card = XingShuViewCardHelper.CreateCardContainer(content, "PlayerCombatStatusHudCard", 408f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "角色状态显示（头顶与头像栏）", 18, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            XingShuViewCardHelper.PlaceTopLeft(title.rectTransform, new Vector2(20f, -12f), new Vector2(450f, 26f));

            Text desc = XingShuUIFactory.CreateText(card, "Description",
                "角色头顶与怪物推演同层锁死不可拖动；左侧头像星级栏下方状态条可自由拖动及微调",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            XingShuViewCardHelper.PlaceStretchTop(desc.rectTransform, 40f, 20f, 40f);

            _playerHudEnabledToggle = XingShuUIFactory.CreateToggle(card, "EnablePlayerCombatHud", "显示状态", PlayerCombatStatusHud.IsEnabled,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetEnabled(value); });
            XingShuViewCardHelper.PlaceTopRight(_playerHudEnabledToggle.GetComponent<RectTransform>(), new Vector2(-20f, -10f), new Vector2(165f, 34f));

            _playerHudHeadToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudHeadToggle", "棋盘头顶(锁)", PlayerCombatStatusHud.ShowHead,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowHead(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudHeadToggle.GetComponent<RectTransform>(), new Vector2(20f, -64f), new Vector2(140f, 30f));

            _playerHudAvatarToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudAvatarToggle", "头像下方栏", PlayerCombatStatusHud.ShowAvatar,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowAvatar(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudAvatarToggle.GetComponent<RectTransform>(), new Vector2(168f, -64f), new Vector2(138f, 30f));

            _playerHudLockToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudLockToggle", "锁定头像栏", PlayerCombatStatusHud.Locked,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetLocked(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudLockToggle.GetComponent<RectTransform>(), new Vector2(314f, -64f), new Vector2(138f, 30f));

            Button resetBtn = XingShuViewCardHelper.CreateActionButton(card, "ResetPlayerHudLayout", "恢复默认",
                () => { PlayerCombatStatusHud.ResetLayout(); UpdateSync(); }, 104f, 30f, 12);
            XingShuViewCardHelper.PlaceTopRight(resetBtn.GetComponent<RectTransform>(), new Vector2(-20f, -64f), new Vector2(104f, 30f));

            _playerHudCdToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudCdToggle", "冷却 CD", PlayerCombatStatusHud.ShowCd,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowCd(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudCdToggle.GetComponent<RectTransform>(), new Vector2(20f, -102f), new Vector2(128f, 30f));

            _playerHudHpToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudHpToggle", "头顶 HP", PlayerCombatStatusHud.ShowHp,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowHp(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudHpToggle.GetComponent<RectTransform>(), new Vector2(158f, -102f), new Vector2(128f, 30f));

            _playerHudAtkDefToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudAtkDefToggle", "基础攻防", PlayerCombatStatusHud.ShowAtkDef,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowAtkDef(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudAtkDefToggle.GetComponent<RectTransform>(), new Vector2(296f, -102f), new Vector2(126f, 30f));

            _playerHudMoveToggle = XingShuUIFactory.CreateToggle(card, "PlayerHudMoveToggle", "额外移速", PlayerCombatStatusHud.ShowMove,
                value => { if (!_suppressCallbacks) PlayerCombatStatusHud.SetShowMove(value); });
            XingShuViewCardHelper.PlaceTopLeft(_playerHudMoveToggle.GetComponent<RectTransform>(), new Vector2(430f, -102f), new Vector2(126f, 30f));

            XingShuViewCardHelper.AddPercentSliderRow(card, "PlayerHudScale", "显示大小（60%–180%）", 142f,
                XingShuConfigKeys.PlayerCombatHudScaleMin, XingShuConfigKeys.PlayerCombatHudScaleMax,
                PlayerCombatStatusHud.Scale,
                v => { PlayerCombatStatusHud.SetScale(Mathf.Round(v * 20f) / 20f); UpdateSync(); },
                () => _suppressCallbacks,
                out _playerHudScaleSlider, out _playerHudScaleText);

            XingShuViewCardHelper.AddSliderRow(card, "PlayerHudOffX", "头像栏水平偏移 X（-160–160 px）", 198f,
                XingShuConfigKeys.PlayerCombatHudOffsetXMin, XingShuConfigKeys.PlayerCombatHudOffsetXMax,
                PlayerCombatStatusHud.OffsetX, $"{Mathf.RoundToInt(PlayerCombatStatusHud.OffsetX)} px",
                v => { PlayerCombatStatusHud.SetOffset(Mathf.Round(v), PlayerCombatStatusHud.OffsetY); UpdateSync(); },
                () => _suppressCallbacks,
                out _playerHudOffXSlider, out _playerHudOffXText);

            XingShuViewCardHelper.AddSliderRow(card, "PlayerHudOffY", "头像栏垂直偏移 Y（-160–160 px）", 254f,
                XingShuConfigKeys.PlayerCombatHudOffsetYMin, XingShuConfigKeys.PlayerCombatHudOffsetYMax,
                PlayerCombatStatusHud.OffsetY, $"{Mathf.RoundToInt(PlayerCombatStatusHud.OffsetY)} px",
                v => { PlayerCombatStatusHud.SetOffset(PlayerCombatStatusHud.OffsetX, Mathf.Round(v)); UpdateSync(); },
                () => _suppressCallbacks,
                out _playerHudOffYSlider, out _playerHudOffYText);

            XingShuViewCardHelper.AddSliderRow(card, "PlayerHudHeadOffY", "棋盘头顶高度偏移 Y（-120–120 px）", 310f,
                XingShuConfigKeys.PlayerCombatHudHeadOffsetYMin, XingShuConfigKeys.PlayerCombatHudHeadOffsetYMax,
                PlayerCombatStatusHud.HeadOffsetY, $"{Mathf.RoundToInt(PlayerCombatStatusHud.HeadOffsetY)} px",
                v => { PlayerCombatStatusHud.SetHeadOffsetY(Mathf.Round(v)); UpdateSync(); },
                () => _suppressCallbacks,
                out _playerHudHeadOffYSlider, out _playerHudHeadOffYText);
        }

        internal void UpdateSync()
        {
            float scale = PlayerCombatStatusHud.Scale;
            float offX = PlayerCombatStatusHud.OffsetX;
            float offY = PlayerCombatStatusHud.OffsetY;
            float headOffY = PlayerCombatStatusHud.HeadOffsetY;
            bool locked = PlayerCombatStatusHud.Locked;
            _suppressCallbacks = true;
            if (_playerHudEnabledToggle != null && _playerHudEnabledToggle.isOn != PlayerCombatStatusHud.IsEnabled)
                _playerHudEnabledToggle.isOn = PlayerCombatStatusHud.IsEnabled;
            if (_playerHudHeadToggle != null && _playerHudHeadToggle.isOn != PlayerCombatStatusHud.ShowHead)
                _playerHudHeadToggle.isOn = PlayerCombatStatusHud.ShowHead;
            if (_playerHudAvatarToggle != null && _playerHudAvatarToggle.isOn != PlayerCombatStatusHud.ShowAvatar)
                _playerHudAvatarToggle.isOn = PlayerCombatStatusHud.ShowAvatar;
            if (_playerHudScaleSlider != null && Mathf.Abs(_playerHudScaleSlider.value - scale) > 0.005f)
                _playerHudScaleSlider.value = scale;
            if (_playerHudOffXSlider != null && Mathf.Abs(_playerHudOffXSlider.value - offX) > 0.5f)
                _playerHudOffXSlider.value = offX;
            if (_playerHudOffYSlider != null && Mathf.Abs(_playerHudOffYSlider.value - offY) > 0.5f)
                _playerHudOffYSlider.value = offY;
            if (_playerHudHeadOffYSlider != null && Mathf.Abs(_playerHudHeadOffYSlider.value - headOffY) > 0.5f)
                _playerHudHeadOffYSlider.value = headOffY;
            if (_playerHudLockToggle != null && _playerHudLockToggle.isOn != locked)
                _playerHudLockToggle.isOn = locked;
            if (_playerHudCdToggle != null && _playerHudCdToggle.isOn != PlayerCombatStatusHud.ShowCd)
                _playerHudCdToggle.isOn = PlayerCombatStatusHud.ShowCd;
            if (_playerHudHpToggle != null && _playerHudHpToggle.isOn != PlayerCombatStatusHud.ShowHp)
                _playerHudHpToggle.isOn = PlayerCombatStatusHud.ShowHp;
            if (_playerHudAtkDefToggle != null && _playerHudAtkDefToggle.isOn != PlayerCombatStatusHud.ShowAtkDef)
                _playerHudAtkDefToggle.isOn = PlayerCombatStatusHud.ShowAtkDef;
            if (_playerHudMoveToggle != null && _playerHudMoveToggle.isOn != PlayerCombatStatusHud.ShowMove)
                _playerHudMoveToggle.isOn = PlayerCombatStatusHud.ShowMove;
            _suppressCallbacks = false;
            if (_playerHudScaleText != null) _playerHudScaleText.text = $"{Mathf.RoundToInt(scale * 100f)}%";
            if (_playerHudOffXText != null) _playerHudOffXText.text = $"{Mathf.RoundToInt(offX)} px";
            if (_playerHudOffYText != null) _playerHudOffYText.text = $"{Mathf.RoundToInt(offY)} px";
            if (_playerHudHeadOffYText != null) _playerHudHeadOffYText.text = $"{Mathf.RoundToInt(headOffY)} px";
        }
    }
}
