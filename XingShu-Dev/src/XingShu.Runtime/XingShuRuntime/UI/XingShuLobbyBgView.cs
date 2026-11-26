using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;

namespace XingShuRuntime.UI
{
    /// <summary>
    /// 功能中心 - 自定义大厅背景设置页面 (Tab 10)
    /// 委托 <see cref="CustomLobbyBackground"/> 管理本地 MP4/WebM/PNG/JPG/USM/VKVLoop 大厅背景。
    /// </summary>
    internal sealed class XingShuLobbyBgView
    {
        private readonly GameObject _root;
        private Text _statusIndicatorText;
        private Text _activeMediaText;
        private Text _inlineStatusText;
        private Text _countBadgeText;
        private Text _statusMsgText;

        private Toggle _enableToggle;
        private Toggle _syncSubToggle;
        private Toggle _randomOnLobbyToggle;

        private GameObject _fileListContainer;
        private LayoutElement _fileListCardLe;
        private int _renderedFileVersion = -1;
        private string _lastRenderedListKey = "";
        private int _lastSelectedIdx = -2;
        private readonly List<Button> _fileRowButtons = new List<Button>();
        private readonly List<Image> _fileRowBgs = new List<Image>();

        private Button _fillCoverBtn;
        private Button _fillContainBtn;
        private Button _fillStretchBtn;
        private Slider _brightnessSlider;
        private Text _brightnessText;
        private Slider _speedSlider;
        private Text _speedText;
        private Toggle _mutedToggle;
        private Toggle _muteVanillaBgmToggle;
        private Slider _volumeSlider;
        private Text _volumeText;

        private InputField _folderInput;
        private Text _dirStatusText;
        private Text _dirResolvedText;
        private Button _createDirBtn;

        private bool _suppressCallbacks;
        private float _lastDirCheckTime = -10f;
        private string _lastObservedStatus = null;

        internal GameObject Root => _root;

        internal XingShuLobbyBgView(Transform parent)
        {
            _root = XingShuUIFactory.CreateUIObject("Page_LobbyBg", parent.gameObject);
            RectTransform rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            BuildUI();
            SyncFromRuntime(forceListRebuild: true);
        }

        private void BuildUI()
        {
            XingShuUIFactory.CreateVerticalScrollView(_root, "LobbyBgScrollView", out GameObject contentArea);
            VerticalLayoutGroup vlg = contentArea.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = 14f;
                vlg.padding = new RectOffset(0, 10, 0, 24);
            }

            BuildHeaderCard(contentArea);
            BuildSwitchCard(contentArea);
            BuildMediaControlCard(contentArea);
            BuildDisplayAudioCard(contentArea);
            BuildFolderCard(contentArea);
            BuildStatusFooterCard(contentArea);
        }

        private GameObject CreateCardContainer(GameObject parent, string name, float height)
        {
            GameObject cardGo = XingShuUIFactory.CreateUIObject(name, parent);
            LayoutElement le = cardGo.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f;

            Sprite cardSprite = XingShuUIAssetService.GetSprite("card_base.png");
            Image cardBg = cardGo.AddComponent<Image>();
            cardBg.sprite = cardSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = XingShuUIAssetService.IsFallbackSprite(cardSprite) ? XingShuUITheme.Card : Color.white;
            return cardGo;
        }

        private void BuildHeaderCard(GameObject parent)
        {
            GameObject headerGo = XingShuUIFactory.CreateUIObject("FeatureHeaderCard", parent);
            LayoutElement le = headerGo.AddComponent<LayoutElement>();
            le.preferredHeight = 76f;
            le.minHeight = 76f;
            le.flexibleHeight = 0f;

            Sprite cfhSprite = XingShuUIAssetService.GetSprite("card_feature_header.png");
            Image headerBg = headerGo.AddComponent<Image>();
            headerBg.sprite = cfhSprite;
            headerBg.type = Image.Type.Sliced;
            headerBg.color = XingShuUIAssetService.IsFallbackSprite(cfhSprite) ? XingShuUITheme.CardFeatureBase : Color.white;

            Text titleTxt = XingShuUIFactory.CreateText(headerGo, "Title", "自定义大厅背景", 20, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceRect(titleTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 10f), new Vector2(360f, 26f));

            Text subTxt = XingShuUIFactory.CreateText(headerGo, "Subtitle", "替换主大厅视频/静态图/USM，完整兼容隐藏 UI 与拖动缩放", 13, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            PlaceRect(subTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -14f), new Vector2(560f, 22f));

            _statusIndicatorText = XingShuUIFactory.CreateText(headerGo, "StatusIndicator", "● 已启用", 15, XingShuUITheme.Positive, TextAnchor.MiddleRight, FontStyle.Bold);
            PlaceRect(_statusIndicatorText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(150f, 26f));
        }

        private void BuildSwitchCard(GameObject parent)
        {
            GameObject card = CreateCardContainer(parent, "Card_Switch", 154f);
            Text title = XingShuUIFactory.CreateText(card, "Title", "1. 运行开关与轮换策略", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopFill(title.rectTransform, -14f, 22f);

            CustomLobbyBackground bg = CustomLobbyBackground.Instance;
            _enableToggle = XingShuUIFactory.CreateToggle(card, "Toggle_Enable", "启用自定义大厅背景（关闭立即恢复原版）", bg != null && bg.Enabled, v =>
            {
                if (_suppressCallbacks || CustomLobbyBackground.Instance == null) return;
                CustomLobbyBackground.Instance.Enabled = v;
                SyncFromRuntime(forceListRebuild: false);
            });
            FormatToggleRow(_enableToggle, -44f);

            _syncSubToggle = XingShuUIFactory.CreateToggle(card, "Toggle_SyncSub", "同步替换二级界面背景（选角色 / 房间 / 商店等视频底图）", bg != null && bg.SyncSubPanels, v =>
            {
                if (_suppressCallbacks || CustomLobbyBackground.Instance == null) return;
                CustomLobbyBackground.Instance.SyncSubPanels = v;
                SyncFromRuntime(forceListRebuild: false);
            });
            FormatToggleRow(_syncSubToggle, -80f);

            _randomOnLobbyToggle = XingShuUIFactory.CreateToggle(card, "Toggle_RandomLobby", "每次返回大厅自动随机切换下一个背景素材", bg != null && bg.RandomOnLobby, v =>
            {
                if (_suppressCallbacks || CustomLobbyBackground.Instance == null) return;
                CustomLobbyBackground.Instance.RandomOnLobby = v;
            });
            FormatToggleRow(_randomOnLobbyToggle, -116f);
        }

        private void BuildMediaControlCard(GameObject parent)
        {
            GameObject card = CreateCardContainer(parent, "Card_MediaList", 190f);
            _fileListCardLe = card.GetComponent<LayoutElement>();

            Text title = XingShuUIFactory.CreateText(card, "Title", "2. 素材切换与列表", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(-160f, 22f));

            _countBadgeText = XingShuUIFactory.CreateText(card, "CountBadge", "共 0 项", 13, XingShuUITheme.SecondaryText, TextAnchor.MiddleRight, FontStyle.Bold);
            PlaceRect(_countBadgeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(140f, 22f));

            _activeMediaText = XingShuUIFactory.CreateText(card, "ActiveLabel", "当前素材：—", 13, XingShuUITheme.CyanAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopFill(_activeMediaText.rectTransform, -42f, 22f);

            _inlineStatusText = XingShuUIFactory.CreateText(card, "InlineStatus", "", 12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            _inlineStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceTopFill(_inlineStatusText.rectTransform, -64f, 20f);

            // 4 Quick Action Buttons
            Sprite btnSec = XingShuUIAssetService.GetSprite("button_secondary.png");
            Sprite btnPri = XingShuUIAssetService.GetSprite("button_primary.png");

            GameObject btnRow = XingShuUIFactory.CreateUIObject("BtnRow", card);
            RectTransform brRt = btnRow.GetComponent<RectTransform>();
            brRt.anchorMin = new Vector2(0f, 1f);
            brRt.anchorMax = new Vector2(1f, 1f);
            brRt.pivot = new Vector2(0.5f, 1f);
            brRt.anchoredPosition = new Vector2(0f, -90f);
            brRt.sizeDelta = new Vector2(-40f, 34f);

            HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            XingShuUIFactory.CreateButton(btnRow, "Btn_Prev", "‹ 上一个", btnSec, Color.white, XingShuUITheme.PrimaryText, () =>
            {
                CustomLobbyBackground.Instance?.CycleNext(-1);
                SyncFromRuntime(forceListRebuild: false);
            }, 13);
            XingShuUIFactory.CreateButton(btnRow, "Btn_Next", "下一个 ›", btnSec, Color.white, XingShuUITheme.PrimaryText, () =>
            {
                CustomLobbyBackground.Instance?.CycleNext(1);
                SyncFromRuntime(forceListRebuild: false);
            }, 13);
            XingShuUIFactory.CreateButton(btnRow, "Btn_Random", "随机切换", btnSec, Color.white, XingShuUITheme.PrimaryText, () =>
            {
                CustomLobbyBackground.Instance?.SelectRandom();
                SyncFromRuntime(forceListRebuild: false);
            }, 13);
            XingShuUIFactory.CreateButton(btnRow, "Btn_Refresh", "刷新扫描", btnPri, Color.white, new Color(0.06f, 0.08f, 0.12f, 1f), () =>
            {
                CustomLobbyBackground.Instance?.RefreshFiles();
                SyncFromRuntime(forceListRebuild: true);
            }, 13);

            // Dynamic list container inside Card 2
            _fileListContainer = XingShuUIFactory.CreateUIObject("FileListItems", card);
            RectTransform flRt = _fileListContainer.GetComponent<RectTransform>();
            flRt.anchorMin = new Vector2(0f, 1f);
            flRt.anchorMax = new Vector2(1f, 1f);
            flRt.pivot = new Vector2(0.5f, 1f);
            flRt.anchoredPosition = new Vector2(0f, -134f);
            flRt.sizeDelta = new Vector2(-40f, 40f);

            VerticalLayoutGroup flLayout = _fileListContainer.AddComponent<VerticalLayoutGroup>();
            flLayout.spacing = 6f;
            flLayout.childAlignment = TextAnchor.UpperCenter;
            flLayout.childControlWidth = true;
            flLayout.childControlHeight = false;
            flLayout.childForceExpandWidth = true;
            flLayout.childForceExpandHeight = false;
        }

        private void BuildDisplayAudioCard(GameObject parent)
        {
            bool hasAudio = CustomLobbyBackground.Instance != null && CustomLobbyBackground.Instance.SupportsAudioControl;
            float height = hasAudio ? 326f : 264f;
            GameObject card = CreateCardContainer(parent, "Card_DisplayAudio", height);

            Text title = XingShuUIFactory.CreateText(card, "Title", "3. 画面适配与播放控制", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(-140f, 22f));

            Button resetBtn = XingShuUIFactory.CreateButton(card, "Btn_ResetDisplay", "恢复默认",
                XingShuUIAssetService.GetSprite("button_secondary.png"), Color.white, XingShuUITheme.PrimaryText, () =>
                {
                    CustomLobbyBackground.Instance?.ResetDisplayDefaults();
                    SyncFromRuntime(forceListRebuild: false);
                }, 12);
            PlaceRect(resetBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -10f), new Vector2(104f, 28f));

            // Fill mode buttons
            Text fillLbl = XingShuUIFactory.CreateText(card, "FillLabel", "缩放填充方式：", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            PlaceRect(fillLbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(118f, 30f));

            GameObject fillRow = XingShuUIFactory.CreateUIObject("FillBtnRow", card);
            RectTransform frRt = fillRow.GetComponent<RectTransform>();
            frRt.anchorMin = new Vector2(0f, 1f);
            frRt.anchorMax = new Vector2(1f, 1f);
            frRt.pivot = new Vector2(0f, 1f);
            frRt.anchoredPosition = new Vector2(140f, -46f);
            frRt.sizeDelta = new Vector2(-160f, 30f);
            HorizontalLayoutGroup frHlg = fillRow.AddComponent<HorizontalLayoutGroup>();
            frHlg.spacing = 8f;
            frHlg.childControlWidth = true;
            frHlg.childControlHeight = true;
            frHlg.childForceExpandWidth = true;
            frHlg.childForceExpandHeight = true;

            Sprite btnSec = XingShuUIAssetService.GetSprite("button_secondary.png");
            _fillCoverBtn = XingShuUIFactory.CreateButton(fillRow, "Fill_Cover", "等比铺满", btnSec, Color.white, XingShuUITheme.PrimaryText, () => SetFill(LobbyBgFillMode.Cover), 12);
            _fillContainBtn = XingShuUIFactory.CreateButton(fillRow, "Fill_Contain", "完整显示", btnSec, Color.white, XingShuUITheme.PrimaryText, () => SetFill(LobbyBgFillMode.Contain), 12);
            _fillStretchBtn = XingShuUIFactory.CreateButton(fillRow, "Fill_Stretch", "拉伸填充", btnSec, Color.white, XingShuUITheme.PrimaryText, () => SetFill(LobbyBgFillMode.Stretch), 12);

            CustomLobbyBackground bg = CustomLobbyBackground.Instance;
            float initBright = bg != null ? bg.Brightness : XingShuConfigKeys.CustomLobbyBgBrightnessDefault;
            float initSpeed = bg != null ? bg.Speed : XingShuConfigKeys.CustomLobbyBgSpeedDefault;

            AddSliderRow(card, "Brightness", "画面亮度（30%–100%）", 86f,
                XingShuConfigKeys.CustomLobbyBgBrightnessMin, XingShuConfigKeys.CustomLobbyBgBrightnessMax, initBright,
                v => $"{Mathf.RoundToInt(v * 100f)}%",
                v =>
                {
                    if (CustomLobbyBackground.Instance != null)
                        CustomLobbyBackground.Instance.Brightness = Mathf.Round(v * 20f) / 20f;
                },
                out _brightnessSlider, out _brightnessText);

            AddSliderRow(card, "Speed", "视频播放倍速（0.5x–2.0x）", 142f,
                XingShuConfigKeys.CustomLobbyBgSpeedMin, XingShuConfigKeys.CustomLobbyBgSpeedMax, initSpeed,
                v => $"{v:F2}x",
                v =>
                {
                    if (CustomLobbyBackground.Instance != null)
                        CustomLobbyBackground.Instance.Speed = Mathf.Round(v * 10f) / 10f;
                },
                out _speedSlider, out _speedText);

            bool initMuteVanillaBgm = bg != null && bg.MuteVanillaBgm;
            _muteVanillaBgmToggle = XingShuUIFactory.CreateToggle(card, "Toggle_MuteVanillaBgm", "关闭原版大厅背景音乐（仅大厅生效，进对局自动恢复）", initMuteVanillaBgm, v =>
            {
                if (_suppressCallbacks || CustomLobbyBackground.Instance == null) return;
                CustomLobbyBackground.Instance.MuteVanillaBgm = v;
            });
            FormatToggleRow(_muteVanillaBgmToggle, -198f);

            if (hasAudio)
            {
                bool initMuted = bg != null ? bg.Muted : XingShuConfigKeys.CustomLobbyBgMutedDefault;
                float initVol = bg != null ? bg.Volume : XingShuConfigKeys.CustomLobbyBgVolumeDefault;
                _mutedToggle = XingShuUIFactory.CreateToggle(card, "Toggle_Mute", "静音自定义视频声音（关闭后播放视频背景音乐）", initMuted, v =>
                {
                    if (_suppressCallbacks || CustomLobbyBackground.Instance == null) return;
                    CustomLobbyBackground.Instance.Muted = v;
                });
                FormatToggleRow(_mutedToggle, -230f);

                AddSliderRow(card, "Volume", "背景视频音量（关闭上方静音后生效）", 262f,
                    XingShuConfigKeys.CustomLobbyBgVolumeMin, XingShuConfigKeys.CustomLobbyBgVolumeMax, initVol,
                    v => $"{Mathf.RoundToInt(v * 100f)}%",
                    v =>
                    {
                        if (CustomLobbyBackground.Instance != null)
                            CustomLobbyBackground.Instance.Volume = Mathf.Round(v * 20f) / 20f;
                    },
                    out _volumeSlider, out _volumeText);
            }
            else
            {
                Text mobileAudioNote = XingShuUIFactory.CreateText(card, "AudioNote", "当前平台视频播放器采用静音轨模式（仅可关闭原版大厅音乐）", 12, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
                PlaceTopFill(mobileAudioNote.rectTransform, -232f, 20f);
            }
        }

        private void BuildFolderCard(GameObject parent)
        {
            GameObject card = CreateCardContainer(parent, "Card_Folder", 204f);

            Text title = XingShuUIFactory.CreateText(card, "Title", "4. 素材目录与格式说明", 15, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceTopFill(title.rectTransform, -14f, 22f);

            _dirStatusText = XingShuUIFactory.CreateText(card, "DirStatus", "● 目录检查中…", 12, XingShuUITheme.Positive, TextAnchor.MiddleLeft);
            PlaceRect(_dirStatusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f), new Vector2(-120f, 20f));

            _createDirBtn = XingShuUIFactory.CreateButton(card, "Btn_CreateDir", "创建目录",
                XingShuUIAssetService.GetSprite("button_secondary.png"), Color.white, XingShuUITheme.PrimaryText, () =>
                {
                    if (CustomLobbyBackground.Instance != null)
                    {
                        CustomLobbyBackground.Instance.EnsureDirectoryExists(CustomLobbyBackground.Instance.ResolveMediaDirectory());
                        CustomLobbyBackground.Instance.RefreshFiles();
                        SyncFromRuntime(forceListRebuild: true);
                    }
                }, 12);
            PlaceRect(_createDirBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -38f), new Vector2(88f, 26f));

            string initFolder = CustomLobbyBackground.Instance != null ? CustomLobbyBackground.Instance.FolderPathInput : "";
            _folderInput = XingShuUIFactory.CreateInputField(card, "Input_Folder", "留空默认使用游戏目录 lobbybg（支持绝对/相对路径）",
                initFolder, "CustomLobbyBg.Folder", null, null, 13);
            PlaceRect(_folderInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -66f), new Vector2(-40f, 32f));

            GameObject dirBtns = XingShuUIFactory.CreateUIObject("DirBtnRow", card);
            RectTransform dbRt = dirBtns.GetComponent<RectTransform>();
            dbRt.anchorMin = new Vector2(0f, 1f);
            dbRt.anchorMax = new Vector2(1f, 1f);
            dbRt.pivot = new Vector2(0.5f, 1f);
            dbRt.anchoredPosition = new Vector2(0f, -106f);
            dbRt.sizeDelta = new Vector2(-40f, 32f);
            HorizontalLayoutGroup dbHlg = dirBtns.AddComponent<HorizontalLayoutGroup>();
            dbHlg.spacing = 10f;
            dbHlg.childControlWidth = true;
            dbHlg.childControlHeight = true;
            dbHlg.childForceExpandWidth = true;
            dbHlg.childForceExpandHeight = true;

            Sprite btnPri = XingShuUIAssetService.GetSprite("button_primary.png");
            Sprite btnSec = XingShuUIAssetService.GetSprite("button_secondary.png");

            XingShuUIFactory.CreateButton(dirBtns, "Btn_ApplyDir", "应用路径并扫描", btnPri, Color.white, new Color(0.06f, 0.08f, 0.12f, 1f), () =>
            {
                if (CustomLobbyBackground.Instance != null && _folderInput != null)
                {
                    CustomLobbyBackground.Instance.FolderPathInput = _folderInput.text ?? "";
                    SyncFromRuntime(forceListRebuild: true);
                }
            }, 12);

            XingShuUIFactory.CreateButton(dirBtns, "Btn_DefaultDir", "恢复默认目录", btnSec, Color.white, XingShuUITheme.PrimaryText, () =>
            {
                if (CustomLobbyBackground.Instance != null)
                {
                    if (_folderInput != null) _folderInput.text = "";
                    CustomLobbyBackground.Instance.FolderPathInput = "";
                    SyncFromRuntime(forceListRebuild: true);
                }
            }, 12);

#if !XINGSHU_ANDROID_PREVIEW
            if (!XingShuMobileUIHelper.IsMobile)
            {
                XingShuUIFactory.CreateButton(dirBtns, "Btn_OpenDir", "打开素材文件夹", btnSec, Color.white, XingShuUITheme.PrimaryText, () =>
                {
                    CustomLobbyBackground.Instance?.OpenMediaDirectory();
                    SyncFromRuntime(forceListRebuild: false);
                }, 12);
            }
#endif

            _dirResolvedText = XingShuUIFactory.CreateText(card, "ResolvedDir", "当前目录: ...", 11, XingShuUITheme.MutedText, TextAnchor.MiddleLeft);
            PlaceTopFill(_dirResolvedText.rectTransform, -146f, 20f);

            Text formatTip = XingShuUIFactory.CreateText(card, "FormatTip",
                "支持格式：MP4 / WebM / MOV / M4V 视频、PNG / JPG / WebP / BMP 静态图、.usm 及 3.3.0 VKVLoop .bundle",
                12, XingShuUITheme.SecondaryText, TextAnchor.MiddleLeft);
            PlaceTopFill(formatTip.rectTransform, -172f, 20f);
        }

        private void BuildStatusFooterCard(GameObject parent)
        {
            GameObject card = CreateCardContainer(parent, "Card_StatusFooter", 54f);
            _statusMsgText = XingShuUIFactory.CreateText(card, "StatusMsg", "就绪", 13, XingShuUITheme.PrimaryText, TextAnchor.MiddleCenter);
            RectTransform rt = _statusMsgText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18f, 6f);
            rt.offsetMax = new Vector2(-18f, -6f);
        }

        private void SetFill(LobbyBgFillMode mode)
        {
            if (CustomLobbyBackground.Instance == null) return;
            CustomLobbyBackground.Instance.FillMode = mode;
            SyncFromRuntime(forceListRebuild: false);
        }

        private void AddSliderRow(
            GameObject card,
            string id,
            string label,
            float top,
            float min,
            float max,
            float initial,
            Func<float, string> formatter,
            Action<float> onChanged,
            out Slider slider,
            out Text valueText)
        {
            Text lbl = XingShuUIFactory.CreateText(card, id + "Label", label, 12, XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
            PlaceRect(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -top), new Vector2(260f, 20f));

            valueText = XingShuUIFactory.CreateText(card, id + "Value", formatter(initial), 12, XingShuUITheme.NumericAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            PlaceRect(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -top), new Vector2(90f, 20f));

            Text capText = valueText;
            slider = XingShuUIFactory.CreateSlider(card, id + "Slider", min, max, initial, v =>
            {
                if (_suppressCallbacks) return;
                onChanged(v);
                if (capText != null) capText.text = formatter(v);
            });
            RectTransform sRt = slider.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.anchoredPosition = new Vector2(0f, -(top + 22f));
            sRt.sizeDelta = new Vector2(-40f, 26f);
        }

        internal void UpdateSync()
        {
            SyncFromRuntime(forceListRebuild: false);
            if (Time.unscaledTime - _lastDirCheckTime > 1.2f)
            {
                _lastDirCheckTime = Time.unscaledTime;
                RefreshDirectoryStatus();
            }
        }

        private void SyncFromRuntime(bool forceListRebuild)
        {
            CustomLobbyBackground bg = CustomLobbyBackground.Instance;
            if (bg == null) return;

            _suppressCallbacks = true;
            try
            {
                if (_enableToggle != null && _enableToggle.isOn != bg.Enabled)
                    XingShuUIFactory.SetToggleStateWithoutNotify(_enableToggle, bg.Enabled);
                if (_syncSubToggle != null && _syncSubToggle.isOn != bg.SyncSubPanels)
                    XingShuUIFactory.SetToggleStateWithoutNotify(_syncSubToggle, bg.SyncSubPanels);
                if (_randomOnLobbyToggle != null && _randomOnLobbyToggle.isOn != bg.RandomOnLobby)
                    XingShuUIFactory.SetToggleStateWithoutNotify(_randomOnLobbyToggle, bg.RandomOnLobby);
                if (_muteVanillaBgmToggle != null && _muteVanillaBgmToggle.isOn != bg.MuteVanillaBgm)
                    XingShuUIFactory.SetToggleStateWithoutNotify(_muteVanillaBgmToggle, bg.MuteVanillaBgm);
                if (_mutedToggle != null && _mutedToggle.isOn != bg.Muted)
                    XingShuUIFactory.SetToggleStateWithoutNotify(_mutedToggle, bg.Muted);

                if (_brightnessSlider != null && Mathf.Abs(_brightnessSlider.value - bg.Brightness) > 0.005f)
                    _brightnessSlider.value = bg.Brightness;
                if (_brightnessText != null)
                    _brightnessText.text = $"{Mathf.RoundToInt(bg.Brightness * 100f)}%";

                if (_speedSlider != null && Mathf.Abs(_speedSlider.value - bg.Speed) > 0.01f)
                    _speedSlider.value = bg.Speed;
                if (_speedText != null)
                    _speedText.text = $"{bg.Speed:F2}x";

                if (_volumeSlider != null && Mathf.Abs(_volumeSlider.value - bg.Volume) > 0.005f)
                    _volumeSlider.value = bg.Volume;
                if (_volumeText != null)
                    _volumeText.text = $"{Mathf.RoundToInt(bg.Volume * 100f)}%";
            }
            finally
            {
                _suppressCallbacks = false;
            }

            if (_statusIndicatorText != null)
            {
                _statusIndicatorText.text = bg.Enabled ? "● 已启用" : "○ 已暂停";
                _statusIndicatorText.color = bg.Enabled ? XingShuUITheme.Positive : XingShuUITheme.SecondaryText;
            }

            if (_activeMediaText != null)
            {
                _activeMediaText.text = $"当前素材：{bg.ActiveMediaLabel}";
            }

            if (_countBadgeText != null)
            {
                _countBadgeText.text = bg.FileCount > 0
                    ? $"第 {Math.Max(1, bg.CurrentIndex + 1)} / {bg.FileCount} 项"
                    : "共 0 项";
            }

            string msg = bg.StatusMsg ?? "";
            if (msg != _lastObservedStatus)
            {
                _lastObservedStatus = msg;
                if (_statusMsgText != null)
                {
                    _statusMsgText.text = string.IsNullOrEmpty(msg) ? "就绪" : msg;
                }
                if (_inlineStatusText != null)
                {
                    _inlineStatusText.text = string.IsNullOrEmpty(msg) ? "点击下方素材行可立即应用替换" : $"状态：{msg}";
                    bool isErr = msg.Contains("无法解码") || msg.Contains("失败") || msg.Contains("不支持") || msg.Contains("不存在");
                    _inlineStatusText.color = isErr
                        ? XingShuUITheme.Danger
                        : (msg.Contains("已应用") ? XingShuUITheme.Positive : XingShuUITheme.SecondaryText);
                }
            }

            UpdateFillButtonVisuals(bg.FillMode);
            RebuildOrHighlightFileList(bg, forceListRebuild);
        }

        private void UpdateFillButtonVisuals(LobbyBgFillMode mode)
        {
            SetBtnHighlight(_fillCoverBtn, mode == LobbyBgFillMode.Cover);
            SetBtnHighlight(_fillContainBtn, mode == LobbyBgFillMode.Contain);
            SetBtnHighlight(_fillStretchBtn, mode == LobbyBgFillMode.Stretch);
        }

        private static void SetBtnHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            Text txt = btn.GetComponentInChildren<Text>();
            if (XingShuGlassSkin.IsActive)
            {
                XingShuGlassSkin.ApplySelectableButton(img, txt, active);
                return;
            }
            if (img != null)
            {
                img.color = active ? new Color(1f, 0.86f, 0.38f, 1f) : Color.white;
            }
        }

        private void RebuildOrHighlightFileList(CustomLobbyBackground bg, bool force)
        {
            if (_fileListContainer == null) return;
            IList<string> names = bg.GetFileNamesSnapshot();
            string listKey = string.Join("|", names);
            if (!force && listKey == _lastRenderedListKey && _renderedFileVersion == bg.FileCount)
            {
                if (_lastSelectedIdx != bg.CurrentIndex)
                {
                    _lastSelectedIdx = bg.CurrentIndex;
                    UpdateFileRowHighlights(bg.CurrentIndex);
                }
                return;
            }

            _lastRenderedListKey = listKey;
            _renderedFileVersion = bg.FileCount;
            _lastSelectedIdx = bg.CurrentIndex;
            _fileRowButtons.Clear();
            _fileRowBgs.Clear();

            for (int i = _fileListContainer.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_fileListContainer.transform.GetChild(i).gameObject);
            }

            if (names.Count == 0)
            {
                GameObject emptyObj = XingShuUIFactory.CreateText(_fileListContainer, "EmptyHint",
                    "目录暂无素材：请点击下方「打开素材文件夹」或将文件放入 lobbybg 目录后点击「刷新扫描」",
                    12, XingShuUITheme.MutedText, TextAnchor.MiddleCenter).gameObject;
                emptyObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
                if (XingShuMobileUIHelper.IsMobile)
                {
                    LayoutElement ele = emptyObj.GetComponent<LayoutElement>() ?? emptyObj.AddComponent<LayoutElement>();
                    ele.minHeight = 48f;
                    ele.preferredHeight = -1f;
                    ele.flexibleWidth = 1f;
                    UpdateFileCardHeight(-1f);
                }
                else
                {
                    UpdateFileCardHeight(192f);
                }
                return;
            }

            bool isMobile = XingShuMobileUIHelper.IsMobile;
            float rowHeight = isMobile ? 54f : 34f;
            float mScale = isMobile ? XingShuMobilePageLayout.TextScale : 1f;
            Sprite rowBgSprite = XingShuUIAssetService.GetSprite("button_secondary.png");
            for (int i = 0; i < names.Count; i++)
            {
                int capturedIdx = i;
                string fn = names[i];
                LobbyBgMediaKind kind = CustomLobbyBgPolicy.ClassifyExtension(fn);
                string badge = CustomLobbyBgPolicy.KindLabel(kind);

                GameObject rowGo = XingShuUIFactory.CreateUIObject("MediaRow_" + i, _fileListContainer);
                rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, rowHeight);
                if (isMobile)
                {
                    LayoutElement rowLe = rowGo.AddComponent<LayoutElement>();
                    rowLe.minHeight = rowHeight;
                    rowLe.preferredHeight = rowHeight;
                    rowLe.flexibleWidth = 1f;
                    rowLe.flexibleHeight = 0f;
                }

                Image rowImg = rowGo.AddComponent<Image>();
                rowImg.sprite = rowBgSprite ?? XingShuUIAssetService.WhiteSprite;
                rowImg.type = (rowBgSprite != null && rowBgSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                rowImg.color = Color.white;

                Button rowBtn = rowGo.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                rowBtn.onClick.AddListener(delegate
                {
                    CustomLobbyBackground.Instance?.SelectIndex(capturedIdx);
                    SyncFromRuntime(forceListRebuild: false);
                });

                Text nameTxt = XingShuUIFactory.CreateText(
                    rowGo, "Name", $"{i + 1}. {fn}",
                    isMobile ? Mathf.RoundToInt(20 * mScale) : 13,
                    XingShuUITheme.PrimaryText, TextAnchor.MiddleLeft);
                nameTxt.raycastTarget = false;
                if (isMobile)
                {
                    nameTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
                    nameTxt.verticalOverflow = VerticalWrapMode.Overflow;
                }
                RectTransform nRt = nameTxt.rectTransform;
                nRt.anchorMin = Vector2.zero;
                nRt.anchorMax = Vector2.one;
                nRt.offsetMin = new Vector2(14f, 0f);
                nRt.offsetMax = new Vector2(isMobile ? -116f : -108f, 0f);

                Text tagTxt = XingShuUIFactory.CreateText(
                    rowGo, "Kind", badge,
                    isMobile ? Mathf.RoundToInt(18 * mScale) : 12,
                    XingShuUITheme.SecondaryText, TextAnchor.MiddleRight, FontStyle.Bold);
                tagTxt.raycastTarget = false;
                RectTransform tRt = tagTxt.rectTransform;
                tRt.anchorMin = new Vector2(1f, 0f);
                tRt.anchorMax = new Vector2(1f, 1f);
                tRt.pivot = new Vector2(1f, 0.5f);
                tRt.anchoredPosition = new Vector2(-14f, 0f);
                tRt.sizeDelta = new Vector2(isMobile ? 96f : 88f, 0f);

                _fileRowButtons.Add(rowBtn);
                _fileRowBgs.Add(rowImg);
            }

            if (isMobile)
            {
                UpdateFileCardHeight(-1f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_fileListContainer.GetComponent<RectTransform>());
                if (_fileListCardLe != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_fileListCardLe.GetComponent<RectTransform>());
                }
            }
            else
            {
                float listHeight = names.Count * 40f;
                UpdateFileCardHeight(150f + listHeight);
            }
            UpdateFileRowHighlights(bg.CurrentIndex);
        }

        private void UpdateFileCardHeight(float totalHeight)
        {
            if (_fileListCardLe != null)
            {
                _fileListCardLe.preferredHeight = totalHeight;
                _fileListCardLe.minHeight = totalHeight < 0f ? 0f : totalHeight;
            }
        }

        private void UpdateFileRowHighlights(int activeIdx)
        {
            bool glass = XingShuGlassSkin.IsActive;
            for (int i = 0; i < _fileRowBgs.Count; i++)
            {
                Image img = _fileRowBgs[i];
                if (img == null) continue;
                bool active = (i == activeIdx);
                if (glass)
                {
                    XingShuGlassSkin.ApplySelectableButton(img, null, active);
                }
                else
                {
                    img.color = active ? new Color(1f, 0.88f, 0.45f, 1f) : new Color(0.95f, 0.96f, 0.99f, 0.92f);
                }
            }
        }

        private void RefreshDirectoryStatus()
        {
            CustomLobbyBackground bg = CustomLobbyBackground.Instance;
            if (bg == null) return;
            string resolved = bg.ResolveMediaDirectory();
            bool exists = false;
            try { exists = Directory.Exists(resolved); } catch { }

            if (_dirStatusText != null)
            {
                _dirStatusText.text = exists
                    ? $"● 目录有效（共检测到 {bg.FileCount} 个可用背景素材）"
                    : "○ 目录尚不存在，可点击右侧按钮创建";
                _dirStatusText.color = exists ? XingShuUITheme.Positive : XingShuUITheme.Danger;
            }
            if (_createDirBtn != null)
            {
                _createDirBtn.gameObject.SetActive(!exists);
            }
            if (_dirResolvedText != null)
            {
                _dirResolvedText.text = $"当前解析目录：{resolved}";
            }
        }

        private static void FormatToggleRow(Toggle toggle, float topY)
        {
            RectTransform rt = toggle.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, topY);
            rt.sizeDelta = new Vector2(-40f, 28f);

            Transform labelT = toggle.transform.Find("Label");
            if (labelT != null)
            {
                RectTransform lRt = labelT.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero;
                lRt.anchorMax = Vector2.one;
                lRt.pivot = new Vector2(0f, 0.5f);
                lRt.offsetMin = Vector2.zero;
                lRt.offsetMax = new Vector2(-46f, 0f);
            }
            Transform bgT = toggle.transform.Find("Background");
            if (bgT != null)
            {
                RectTransform bRt = bgT.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(1f, 0.5f);
                bRt.anchorMax = new Vector2(1f, 0.5f);
                bRt.pivot = new Vector2(1f, 0.5f);
                bRt.anchoredPosition = Vector2.zero;
                bRt.sizeDelta = new Vector2(32f, 32f);
            }
        }

        private static void PlaceTopFill(RectTransform rt, float topY, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, topY);
            rt.sizeDelta = new Vector2(-40f, height);
        }

        private static void PlaceRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
