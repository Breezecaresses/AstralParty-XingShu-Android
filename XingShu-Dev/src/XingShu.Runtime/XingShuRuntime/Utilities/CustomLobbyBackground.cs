using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FairyGUI;
using UI;
using UnityEngine;
using XingShuRuntime.Core;
using XingShuRuntime.Features;
using XingShuRuntime.UI;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Independent modular lobby background replacement for Astral Party 3.3.0+.
    /// Attaches a non-destructive FairyGUI GLoader above HomePanel.Com_BGSet.loader_Video
    /// (and optionally BackgroundPanel.loader_BG) supporting local MP4/WebM/MOV videos,
    /// PNG/JPG images, CriWare .usm movies, and 3.3.0 VKVLoop .bundle assets without
    /// modifying game files.
    /// </summary>
    internal sealed class CustomLobbyBackground : MonoBehaviour
    {
        internal static CustomLobbyBackground Instance { get; private set; }

        private const string LoaderName = "XingShu_CustomLobbyBg";
        private const string VideoGoName = "[XingShu] LobbyVideoPlayer";
        private const float AutoRescanInterval = 2.5f;
        private const float VideoPrepareTimeoutSeconds = 8.0f;

        private enum CriSwitchPhase
        {
            Idle = 0,
            WaitingStop = 1,
        }

        // Persisted settings
        private bool _enabled = XingShuConfigKeys.CustomLobbyBgEnabledDefault;
        private bool _syncSubPanels = XingShuConfigKeys.CustomLobbyBgSyncSubPanelsDefault;
        private bool _randomOnLobby = XingShuConfigKeys.CustomLobbyBgRandomOnLobbyDefault;
        private string _selectedFile = XingShuConfigKeys.CustomLobbyBgSelectedFileDefault;
        private string _folderPath = XingShuConfigKeys.CustomLobbyBgFolderPathDefault;
        private LobbyBgFillMode _fillMode = LobbyBgFillMode.Cover;
        private float _brightness = XingShuConfigKeys.CustomLobbyBgBrightnessDefault;
        private float _speed = XingShuConfigKeys.CustomLobbyBgSpeedDefault;
        private bool _muted = XingShuConfigKeys.CustomLobbyBgMutedDefault;
        private float _volume = XingShuConfigKeys.CustomLobbyBgVolumeDefault;
        private bool _muteVanillaBgm = XingShuConfigKeys.CustomLobbyBgMuteVanillaBgmDefault;

        // Media catalog state
        private readonly List<string> _mediaFiles = new List<string>();
        private int _currentIndex = -1;
        private double _lastScanTime = -99.0;
        private bool _defaultFolderCreated;

        // Status readout for UI
        internal string StatusMsg { get; private set; } = "";
        internal string ActiveMediaLabel { get; private set; } = "未加载自定义素材";
        internal LobbyBgMediaKind ActiveMediaKind { get; private set; } = LobbyBgMediaKind.Unknown;

        // Live FairyGUI overlay + media resources
        private GLoader _homeLoader;
        private GLoader _subLoader;
        private GGraph _attachedHomeGraph;
        private GGraph _attachedSubGraph;
        private NTexture _activeNTexture;
        private Texture2D _loadedImageTex;
        private RenderTexture _videoRt;
        private AssetBundle _loadedCriBundle;
        private Component _videoPlayer;
        private GameObject _videoGo;

        // Runtime playback / suppression state
        private bool _videoPreparing;
        private double _videoPrepareStartedAt;
        private bool _videoStarted;
        private double _videoStartedAt;
        private bool _pausedForAway;
        private bool _wasHomeVisible;
        private bool _suppressedHomeCri;
        private bool _suppressedSubCri;
        private bool _criOverrideApplied;
        private bool _vanillaBgmSuppressed;
        private double _lastSidecarCheckTime = -99.0;
        private DateTime _lastSidecarWriteUtc = DateTime.MinValue;
        private bool _suppressSidecarWrite;
        private CriSwitchPhase _criPhase = CriSwitchPhase.Idle;
        private string _pendingCriPath = "";
        private LobbyBgMediaKind _pendingCriKind = LobbyBgMediaKind.Unknown;
        private double _pendingCriStartedAt;
        private string _activeVideoFullPath = "";
        private string _videoPreparedPath = "";
        private int _videoRetryStage;
        private double _lastAutoLoadAttemptAt = -99.0;
        private bool _autoLoadFailedForCurrentFile;
        private double _lastAudioDriftCheckAt = -99.0;
        private bool _initialAudioAligned;
        private bool _videoUsesUnscaledClock;
        private float _lastAppliedNativeScale = -1f;
        private float _lastAppliedUnityTimeScale = -1f;
#if !XINGSHU_ANDROID_PREVIEW
        private readonly WindowsLobbyAudioPlayer _companionAudio = new WindowsLobbyAudioPlayer();
#else
        private readonly AndroidLobbyAudioPlayer _companionAudio = new AndroidLobbyAudioPlayer();
#endif

        // Cached reflection handles (Android IL2CPP stripped-API safe)
        private static bool _reflInit;
        private static FieldInfo _cachedPanelDictField;
        private static MethodInfo _showStartVideoMethod;
        private static MethodInfo _bgRefreshMethod;
        private static PropertyInfo _criPlayerProp;
        private static MethodInfo _criPlayerPauseMethod;
        private static MethodInfo _criPlayerStopMethod;
        private static MethodInfo _criPlayerStartMethod;
        private static MethodInfo _criPlayerLoopMethod;
        private static MethodInfo _criPlayerSetSpeedMethod;
        private static MethodInfo _criPlayerSetVolumeMethod;
        private static MethodInfo _criPlayerSetFileMethod;
        private static PropertyInfo _criPlayerStatusProp;
        private static MethodInfo _criCtrlSetAssetMethod;
        private static PropertyInfo _vpIsLoopingProp;
        private static PropertyInfo _vpAudioModeProp;
        private static PropertyInfo _vpTimeUpdateModeProp;
        private static PropertyInfo _vpIsPreparedProp;
        private static PropertyInfo _vpTextureProp;
        private static Type _vpType;
        private static PropertyInfo _vpSourceProp;
        private static PropertyInfo _vpUrlProp;
        private static PropertyInfo _vpRenderModeProp;
        private static PropertyInfo _vpTargetTextureProp;
        private static PropertyInfo _vpAspectRatioProp;
        private static PropertyInfo _vpPlayOnAwakeProp;
        private static PropertyInfo _vpPlaybackSpeedProp;
        private static PropertyInfo _vpWidthProp;
        private static PropertyInfo _vpHeightProp;
        private static PropertyInfo _vpIsPlayingProp;
        private static PropertyInfo _vpIsPausedProp;
        private static PropertyInfo _vpLengthProp;
        private static PropertyInfo _vpTimeProp;
        private static MethodInfo _vpStopMethod;
        private static MethodInfo _vpPrepareMethod;
        private static MethodInfo _vpPlayMethod;
        private static MethodInfo _vpPauseMethod;
        private static Type _criCtrlType;
        private static Type _criUsmAssetType;

        internal static void Install(GameObject host)
        {
            if (host != null && host.GetComponent<CustomLobbyBackground>() == null)
            {
                host.AddComponent<CustomLobbyBackground>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            try
            {
                InitReflection();
                LoadConfig();
                EnsureDefaultFolderOnce();
                TryLoadSidecarIfNewer(force: true, out _);
                ScanFilesInternal(applyIfChanged: false);
                if (_enabled && _mediaFiles.Count > 0)
                {
                    ApplySelectedOrFirst(forceReload: false);
                }
            }
            catch (Exception ex)
            {
                StatusMsg = $"初始化大厅背景组件异常({ex.GetType().Name}): {ex.Message}";
                RuntimeCompatibilityHost.LogLifecycle("[CustomLobbyBackground.Awake] " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            try
            {
                RestoreVanillaLobbyBgm();
                TeardownActiveMedia(restoreVanilla: true);
                DestroyOverlayLoaders();
                _companionAudio.Dispose();
            }
            catch
            {
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            try
            {
                RestoreVanillaLobbyBgm();
                _companionAudio.Dispose();
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------
        // Public Configuration Properties & Actions
        // ------------------------------------------------------------------

        internal bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgEnabled, value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                if (!value)
                {
                    RestoreVanillaLobbyBgm();
                    TeardownActiveMedia(restoreVanilla: true);
                    StatusMsg = "已暂停自定义大厅背景，恢复原版大厅";
                }
                else
                {
                    _lastAutoLoadAttemptAt = -99.0;
                    _autoLoadFailedForCurrentFile = false;
                    ScanFilesInternal(applyIfChanged: false);
                    ApplySelectedOrFirst(forceReload: true);
                }
            }
        }

        internal bool SyncSubPanels
        {
            get => _syncSubPanels;
            set
            {
                if (_syncSubPanels == value) return;
                _syncSubPanels = value;
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgSyncSubPanels, value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                if (!value)
                {
                    RestoreSubPanelVanilla();
                    SetLoaderVisible(_subLoader, false);
                }
            }
        }

        internal bool RandomOnLobby
        {
            get => _randomOnLobby;
            set
            {
                if (_randomOnLobby == value) return;
                _randomOnLobby = value;
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgRandomOnLobby, value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
            }
        }

        internal string FolderPathInput
        {
            get => _folderPath;
            set
            {
                string next = value != null ? value.Trim() : "";
                if (string.Equals(_folderPath, next, StringComparison.Ordinal)) return;
                _folderPath = next;
                XingShuConfigService.SetString(XingShuConfigKeys.CustomLobbyBgFolderPath, next);
                XingShuConfigService.Save();
                ScanFilesInternal(applyIfChanged: true);
            }
        }

        internal LobbyBgFillMode FillMode
        {
            get => _fillMode;
            set
            {
                if (_fillMode == value) return;
                _fillMode = value;
                XingShuConfigService.SetInt(XingShuConfigKeys.CustomLobbyBgFillMode, (int)value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                ApplyOverlayVisualStyle();
            }
        }

        internal float Brightness
        {
            get => _brightness;
            set
            {
                float clamped = CustomLobbyBgPolicy.ClampBrightness(value);
                if (Mathf.Abs(_brightness - clamped) < 0.005f) return;
                _brightness = clamped;
                XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgBrightness, clamped);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                ApplyOverlayVisualStyle();
            }
        }

        internal float Speed
        {
            get => _speed;
            set
            {
                float clamped = CustomLobbyBgPolicy.ClampSpeed(value);
                if (Mathf.Abs(_speed - clamped) < 0.005f) return;
                _speed = clamped;
                XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgSpeed, clamped);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                ApplyVideoPlaybackSettings();
            }
        }

        internal bool Muted
        {
            get => _muted;
            set
            {
                if (_muted == value) return;
                _muted = value;
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuted, value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                ApplyVideoPlaybackSettings();
            }
        }

        internal float Volume
        {
            get => _volume;
            set
            {
                float clamped = CustomLobbyBgPolicy.ClampVolume(value);
                if (Mathf.Abs(_volume - clamped) < 0.005f) return;
                _volume = clamped;
                XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgVolume, clamped);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                ApplyVideoPlaybackSettings();
            }
        }

        internal bool SupportsAudioControl => true;

        internal bool MuteVanillaBgm
        {
            get => _muteVanillaBgm;
            set
            {
                if (_muteVanillaBgm == value) return;
                _muteVanillaBgm = value;
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuteVanillaBgm, value);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                if (!value)
                {
                    RestoreVanillaLobbyBgm();
                }
            }
        }

        internal int FileCount => _mediaFiles.Count;

        internal int CurrentIndex => _currentIndex;

        internal string CurrentFileName =>
            (_currentIndex >= 0 && _currentIndex < _mediaFiles.Count)
                ? Path.GetFileName(_mediaFiles[_currentIndex])
                : "";

        internal IList<string> GetFileNamesSnapshot()
        {
            var names = new List<string>(_mediaFiles.Count);
            for (int i = 0; i < _mediaFiles.Count; i++)
            {
                names.Add(Path.GetFileName(_mediaFiles[i]));
            }
            return names;
        }

        internal string GetDefaultFolder()
        {
            if (XingShuMobileUIHelper.IsMobile)
            {
                return Path.Combine(Application.persistentDataPath ?? "", "lobbybg").Replace('\\', '/');
            }
            string dataPath = Application.dataPath ?? "";
            try
            {
                string parentDir = Path.GetDirectoryName(dataPath.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(parentDir))
                {
                    return Path.Combine(parentDir, "lobbybg").Replace('\\', '/');
                }
            }
            catch
            {
            }
            return Path.Combine(dataPath, "lobbybg").Replace('\\', '/');
        }

        internal string ResolveMediaDirectory()
        {
            if (string.IsNullOrEmpty(_folderPath))
            {
                return GetDefaultFolder();
            }
            string trimmed = _folderPath.Trim();
            try
            {
                if (Path.IsPathRooted(trimmed))
                {
                    return trimmed.Replace('\\', '/');
                }
                string dataDir = (Application.dataPath ?? "").TrimEnd('/', '\\');
                string parentDir = Path.GetDirectoryName(dataDir);
                string baseRoot = XingShuMobileUIHelper.IsMobile
                    ? (Application.persistentDataPath ?? "")
                    : (!string.IsNullOrEmpty(parentDir) ? parentDir : (Application.dataPath ?? ""));
                return Path.GetFullPath(Path.Combine(baseRoot, trimmed)).Replace('\\', '/');
            }
            catch
            {
                return GetDefaultFolder();
            }
        }

        internal bool EnsureDirectoryExists(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Directory.Exists(dir);
            }
            catch (Exception ex)
            {
                StatusMsg = $"创建目录失败: {ex.Message}";
                return false;
            }
        }

#if !XINGSHU_ANDROID_PREVIEW
        internal void OpenMediaDirectory()
        {
            string dir = ResolveMediaDirectory();
            if (!EnsureDirectoryExists(dir)) return;
            try
            {
                Application.OpenURL(CustomLobbyBgPolicy.BuildFileUri(dir));
                StatusMsg = $"已打开目录: {dir}";
            }
            catch (Exception ex)
            {
                StatusMsg = $"无法打开目录: {ex.Message}";
            }
        }
#endif

        internal void RefreshFiles()
        {
            _lastAutoLoadAttemptAt = -99.0;
            _autoLoadFailedForCurrentFile = false;
            ScanFilesInternal(applyIfChanged: true);
        }

        internal void SelectIndex(int index)
        {
            if (index < 0 || index >= _mediaFiles.Count) return;
            _lastAutoLoadAttemptAt = -99.0;
            _autoLoadFailedForCurrentFile = false;
            _currentIndex = index;
            SaveSelectedFile(_mediaFiles[index]);
            if (_enabled)
            {
                LoadMediaAt(index);
            }
            else
            {
                ActiveMediaLabel = Path.GetFileName(_mediaFiles[index]);
                StatusMsg = $"已选中: {ActiveMediaLabel}（请开启总开关生效）";
            }
        }

        internal void CycleNext(int delta)
        {
            if (_mediaFiles.Count == 0)
            {
                ScanFilesInternal(applyIfChanged: false);
            }
            if (_mediaFiles.Count == 0)
            {
                StatusMsg = $"目录内暂无可用素材: {ResolveMediaDirectory()}";
                return;
            }
            int next = CustomLobbyBgPolicy.PickNextIndex(_currentIndex, _mediaFiles.Count, delta);
            SelectIndex(next);
        }

        internal void SelectRandom()
        {
            if (_mediaFiles.Count == 0)
            {
                ScanFilesInternal(applyIfChanged: false);
            }
            if (_mediaFiles.Count == 0)
            {
                StatusMsg = $"目录内暂无可用素材: {ResolveMediaDirectory()}";
                return;
            }
            int next = CustomLobbyBgPolicy.PickRandomIndex(_currentIndex, _mediaFiles.Count, n => UnityEngine.Random.Range(0, n));
            SelectIndex(next);
        }

        internal void ResetDisplayDefaults()
        {
            _fillMode = LobbyBgFillMode.Cover;
            _brightness = XingShuConfigKeys.CustomLobbyBgBrightnessDefault;
            _speed = XingShuConfigKeys.CustomLobbyBgSpeedDefault;
            _muted = XingShuConfigKeys.CustomLobbyBgMutedDefault;
            _volume = XingShuConfigKeys.CustomLobbyBgVolumeDefault;
            _muteVanillaBgm = XingShuConfigKeys.CustomLobbyBgMuteVanillaBgmDefault;
            XingShuConfigService.SetInt(XingShuConfigKeys.CustomLobbyBgFillMode, (int)_fillMode);
            XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgBrightness, _brightness);
            XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgSpeed, _speed);
            XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuted, _muted);
            XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgVolume, _volume);
                XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuteVanillaBgm, _muteVanillaBgm);
                XingShuConfigService.Save();
                SyncSidecarToDisk();
                RestoreVanillaLobbyBgm();
            ApplyOverlayVisualStyle();
            ApplyVideoPlaybackSettings();
            StatusMsg = "已恢复画面与音量默认参数";
        }

        // ------------------------------------------------------------------
        // Lifecycle & Frame Synchronization
        // ------------------------------------------------------------------

        private void Update()
        {
            try
            {
                _companionAudio.TickMainThread();
                double now = ThinkingClock.RealSeconds();
                if (now - _lastSidecarCheckTime >= 2.0)
                {
                    _lastSidecarCheckTime = now;
                    TryLoadSidecarIfNewer(force: false, out _);
                }
                if (now - _lastScanTime >= AutoRescanInterval)
                {
                    _lastScanTime = now;
                    // Auto-pick when new files appear or active media is not yet loaded
                    if (_mediaFiles.Count == 0 || (XingShuUICenter.IsOpen && !_autoLoadFailedForCurrentFile) || (_enabled && ActiveMediaKind == LobbyBgMediaKind.Unknown && !_autoLoadFailedForCurrentFile))
                    {
                        ScanFilesInternal(applyIfChanged: _enabled && !_autoLoadFailedForCurrentFile && (_mediaFiles.Count == 0 || ActiveMediaKind == LobbyBgMediaKind.Unknown));
                    }
                }
                if (_enabled && _mediaFiles.Count > 0 && ActiveMediaKind == LobbyBgMediaKind.Unknown
                    && !_videoPreparing && _criPhase == CriSwitchPhase.Idle && !_autoLoadFailedForCurrentFile
                    && (now - _lastAutoLoadAttemptAt >= 3.0))
                {
                    _lastAutoLoadAttemptAt = now;
                    ApplySelectedOrFirst(forceReload: false);
                }

                float curNativeScale = ReadNativeClockScale();
                float curUnityScale = Time.timeScale;
                if (Math.Abs(curNativeScale - _lastAppliedNativeScale) > 0.01f ||
                    Math.Abs(curUnityScale - _lastAppliedUnityTimeScale) > 0.01f)
                {
                    ApplyVideoPlaybackSettings();
                }

                HomePanel home = FindHomePanel();
                BackgroundPanel rawBgPanel = FindBackgroundPanel();
                bool homeVisible = IsPanelVisibleOnStage(home, out UIHomePanel uiHome);
                bool rawBgVisible = IsPanelVisibleOnStage(rawBgPanel, out UIBackgroundPanel rawUiBg);
                BackgroundPanel bgPanel = _syncSubPanels ? rawBgPanel : null;
                UIBackgroundPanel uiBg = _syncSubPanels ? rawUiBg : null;
                bool bgVisible = _syncSubPanels && rawBgVisible;

                bool enteredHome = homeVisible && !_wasHomeVisible;
                SyncVanillaLobbyBgm(homeVisible, rawBgVisible, enteredHome);

                if (enteredHome && _enabled && _randomOnLobby && _mediaFiles.Count > 1)
                {
                    int next = CustomLobbyBgPolicy.PickRandomIndex(_currentIndex, _mediaFiles.Count, n => UnityEngine.Random.Range(0, n));
                    if (next >= 0 && next != _currentIndex)
                    {
                        _currentIndex = next;
                        SaveSelectedFile(_mediaFiles[next]);
                        LoadMediaAt(next);
                    }
                }
                _wasHomeVisible = homeVisible;

                if (!_enabled || _currentIndex < 0 || _currentIndex >= _mediaFiles.Count)
                {
                    if (_suppressedHomeCri || _suppressedSubCri)
                    {
                        RestoreAllVanillaCri(home, bgPanel);
                    }
                    SetLoaderVisible(_homeLoader, false);
                    SetLoaderVisible(_subLoader, false);
                    return;
                }

                TickPendingCriSwitch(home);
                TickVideoPlayback();

                bool anyLobbySurfaceVisible = homeVisible || bgVisible;
                UpdateVideoPowerState(anyLobbySurfaceVisible);

                GGraph homeGraph = uiHome != null && uiHome.Com_BGSet != null ? uiHome.Com_BGSet.loader_Video : null;
                GGraph subGraph = uiBg != null ? uiBg.loader_BG : null;

                bool overlayActive = (ActiveMediaKind == LobbyBgMediaKind.Image || ActiveMediaKind == LobbyBgMediaKind.Video) && _activeNTexture != null;
                if (overlayActive)
                {
                    if (homeGraph != null && !homeGraph.isDisposed)
                    {
                        EnsureOverlayAttached(ref _homeLoader, ref _attachedHomeGraph, homeGraph, "Home");
                        SyncOverlayTransform(_homeLoader, homeGraph, homeVisible);
                        SuppressVanillaCri(homeGraph, true, ref _suppressedHomeCri);
                    }
                    else
                    {
                        SetLoaderVisible(_homeLoader, false);
                    }

                    if (bgVisible && subGraph != null && !subGraph.isDisposed)
                    {
                        EnsureOverlayAttached(ref _subLoader, ref _attachedSubGraph, subGraph, "Sub");
                        SyncOverlayTransform(_subLoader, subGraph, true);
                        SuppressVanillaCri(subGraph, true, ref _suppressedSubCri);
                    }
                    else
                    {
                        if (_suppressedSubCri)
                        {
                            RestoreSubPanelVanilla();
                        }
                        SetLoaderVisible(_subLoader, false);
                    }
                }
                else
                {
                    SetLoaderVisible(_homeLoader, false);
                    SetLoaderVisible(_subLoader, false);
                    if (_suppressedHomeCri && homeGraph != null)
                    {
                        SuppressVanillaCri(homeGraph, false, ref _suppressedHomeCri);
                    }
                    if (_suppressedSubCri)
                    {
                        RestoreSubPanelVanilla();
                    }
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(StatusMsg) || ActiveMediaKind == LobbyBgMediaKind.Unknown)
                {
                    StatusMsg = $"运行异常({ex.GetType().Name}): {ex.Message}";
                }
                RuntimeCompatibilityHost.LogLifecycle("[CustomLobbyBackground.Update] " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Config Persistence & Directory Scan
        // ------------------------------------------------------------------

        private void LoadConfig()
        {
            _enabled = XingShuConfigService.GetBool(XingShuConfigKeys.CustomLobbyBgEnabled, XingShuConfigKeys.CustomLobbyBgEnabledDefault);
            _syncSubPanels = XingShuConfigService.GetBool(XingShuConfigKeys.CustomLobbyBgSyncSubPanels, XingShuConfigKeys.CustomLobbyBgSyncSubPanelsDefault);
            _randomOnLobby = XingShuConfigService.GetBool(XingShuConfigKeys.CustomLobbyBgRandomOnLobby, XingShuConfigKeys.CustomLobbyBgRandomOnLobbyDefault);
            _selectedFile = XingShuConfigService.GetString(XingShuConfigKeys.CustomLobbyBgSelectedFile, XingShuConfigKeys.CustomLobbyBgSelectedFileDefault) ?? "";
            _folderPath = XingShuConfigService.GetString(XingShuConfigKeys.CustomLobbyBgFolderPath, XingShuConfigKeys.CustomLobbyBgFolderPathDefault) ?? "";
            _fillMode = CustomLobbyBgPolicy.NormalizeFillMode(XingShuConfigService.GetInt(XingShuConfigKeys.CustomLobbyBgFillMode, XingShuConfigKeys.CustomLobbyBgFillModeDefault));
            _brightness = CustomLobbyBgPolicy.ClampBrightness(XingShuConfigService.GetFloat(XingShuConfigKeys.CustomLobbyBgBrightness, XingShuConfigKeys.CustomLobbyBgBrightnessDefault));
            _speed = CustomLobbyBgPolicy.ClampSpeed(XingShuConfigService.GetFloat(XingShuConfigKeys.CustomLobbyBgSpeed, XingShuConfigKeys.CustomLobbyBgSpeedDefault));
            _muted = XingShuConfigService.GetBool(XingShuConfigKeys.CustomLobbyBgMuted, XingShuConfigKeys.CustomLobbyBgMutedDefault);
            _volume = CustomLobbyBgPolicy.ClampVolume(XingShuConfigService.GetFloat(XingShuConfigKeys.CustomLobbyBgVolume, XingShuConfigKeys.CustomLobbyBgVolumeDefault));
            _muteVanillaBgm = XingShuConfigService.GetBool(XingShuConfigKeys.CustomLobbyBgMuteVanillaBgm, XingShuConfigKeys.CustomLobbyBgMuteVanillaBgmDefault);
        }

        private void EnsureDefaultFolderOnce()
        {
            if (_defaultFolderCreated || !string.IsNullOrEmpty(_folderPath)) return;
            _defaultFolderCreated = true;
            try
            {
                string defDir = GetDefaultFolder();
                if (!Directory.Exists(defDir))
                {
                    Directory.CreateDirectory(defDir);
                }
            }
            catch
            {
            }
        }

        private void SaveSelectedFile(string fullPath)
        {
            string fileName = string.IsNullOrEmpty(fullPath) ? "" : Path.GetFileName(fullPath);
            _selectedFile = fileName;
            XingShuConfigService.SetString(XingShuConfigKeys.CustomLobbyBgSelectedFile, fileName);
            XingShuConfigService.Save();
            SyncSidecarToDisk();
        }

        private string GetSidecarPath()
        {
            string dir = ResolveMediaDirectory();
            return string.IsNullOrEmpty(dir) ? "" : Path.Combine(dir, CustomLobbyBgPolicy.SidecarFileName);
        }

        private void SyncSidecarToDisk()
        {
            if (_suppressSidecarWrite) return;
            try
            {
                string dir = ResolveMediaDirectory();
                if (string.IsNullOrEmpty(dir)) return;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, CustomLobbyBgPolicy.SidecarFileName);
                var sc = new CustomLobbyBgPolicy.LobbyBgSidecarConfig
                {
                    Enabled = _enabled,
                    SelectedFile = _selectedFile ?? "",
                    SyncSubPanels = _syncSubPanels,
                    RandomOnLobby = _randomOnLobby,
                    FillMode = _fillMode,
                    Brightness = _brightness,
                    Speed = _speed,
                    Muted = _muted,
                    Volume = _volume,
                    MuteVanillaBgm = _muteVanillaBgm,
                    UpdatedAt = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds,
                };
                File.WriteAllText(path, CustomLobbyBgPolicy.SerializeSidecarJson(sc), new System.Text.UTF8Encoding(false));
                _lastSidecarWriteUtc = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
            }
        }

        private bool TryLoadSidecarIfNewer(bool force, out bool changed)
        {
            changed = false;
            try
            {
                string path = GetSidecarPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                DateTime writeUtc = File.GetLastWriteTimeUtc(path);
                if (!force && writeUtc <= _lastSidecarWriteUtc) return false;

                string raw = File.ReadAllText(path, System.Text.Encoding.UTF8);
                if (!CustomLobbyBgPolicy.TryParseSidecarJson(raw, out var sc))
                {
                    _lastSidecarWriteUtc = writeUtc;
                    return false;
                }

                _lastSidecarWriteUtc = writeUtc;
                bool fileOrEnableChanged = sc.Enabled != _enabled
                    || !string.Equals(sc.SelectedFile ?? "", _selectedFile ?? "", StringComparison.OrdinalIgnoreCase);

                _suppressSidecarWrite = true;
                try
                {
                    _enabled = sc.Enabled;
                    _selectedFile = sc.SelectedFile ?? "";
                    _syncSubPanels = sc.SyncSubPanels;
                    _randomOnLobby = sc.RandomOnLobby;
                    _fillMode = sc.FillMode;
                    _brightness = CustomLobbyBgPolicy.ClampBrightness(sc.Brightness);
                    _speed = CustomLobbyBgPolicy.ClampSpeed(sc.Speed);
                    _muted = sc.Muted;
                    _volume = CustomLobbyBgPolicy.ClampVolume(sc.Volume);
                    _muteVanillaBgm = sc.MuteVanillaBgm;

                    XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgEnabled, _enabled);
                    XingShuConfigService.SetString(XingShuConfigKeys.CustomLobbyBgSelectedFile, _selectedFile);
                    XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgSyncSubPanels, _syncSubPanels);
                    XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgRandomOnLobby, _randomOnLobby);
                    XingShuConfigService.SetInt(XingShuConfigKeys.CustomLobbyBgFillMode, (int)_fillMode);
                    XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgBrightness, _brightness);
                    XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgSpeed, _speed);
                    XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuted, _muted);
                    XingShuConfigService.SetFloat(XingShuConfigKeys.CustomLobbyBgVolume, _volume);
                    XingShuConfigService.SetBool(XingShuConfigKeys.CustomLobbyBgMuteVanillaBgm, _muteVanillaBgm);
                    XingShuConfigService.Save();
                }
                finally
                {
                    _suppressSidecarWrite = false;
                }

                if (!_enabled)
                {
                    RestoreVanillaLobbyBgm();
                    TeardownActiveMedia(restoreVanilla: true);
                    StatusMsg = "已由启动器设为使用默认大厅背景";
                }
                else
                {
                    ScanFilesInternal(applyIfChanged: false);
                    if (fileOrEnableChanged || (ActiveMediaKind == LobbyBgMediaKind.Unknown && !_autoLoadFailedForCurrentFile))
                    {
                        _lastAutoLoadAttemptAt = -99.0;
                        _autoLoadFailedForCurrentFile = false;
                        ApplySelectedOrFirst(forceReload: true);
                    }
                    else
                    {
                        ApplyOverlayVisualStyle();
                        ApplyVideoPlaybackSettings();
                    }
                }
                changed = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ScanFilesInternal(bool applyIfChanged)
        {
            string dir = ResolveMediaDirectory();
            List<string> nextList = new List<string>();
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    string[] rawFiles = Directory.GetFiles(dir);
                    nextList = CustomLobbyBgPolicy.SortMediaFiles(rawFiles);
                }
                catch (Exception ex)
                {
                    StatusMsg = $"读取目录失败: {ex.Message}";
                }
            }

            string prevActive = (_currentIndex >= 0 && _currentIndex < _mediaFiles.Count) ? _mediaFiles[_currentIndex] : "";
            _mediaFiles.Clear();
            _mediaFiles.AddRange(nextList);

            int matched = -1;
            if (!string.IsNullOrEmpty(_selectedFile))
            {
                for (int i = 0; i < _mediaFiles.Count; i++)
                {
                    if (string.Equals(Path.GetFileName(_mediaFiles[i]), _selectedFile, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(_mediaFiles[i], _selectedFile, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = i;
                        break;
                    }
                }
            }
            if (matched < 0 && _mediaFiles.Count > 0)
            {
                matched = 0;
            }
            _currentIndex = matched;

            if (_mediaFiles.Count == 0)
            {
                ActiveMediaLabel = "目录暂无素材（保留原版）";
                ActiveMediaKind = LobbyBgMediaKind.Unknown;
                if (_enabled && !string.IsNullOrEmpty(prevActive))
                {
                    TeardownActiveMedia(restoreVanilla: true);
                }
                if (Directory.Exists(dir))
                {
                    StatusMsg = $"未检测到素材，请将 MP4 / PNG / JPG / USM 放入 {dir}";
                }
                return;
            }

            if (applyIfChanged && _enabled && _currentIndex >= 0)
            {
                string nextActive = _mediaFiles[_currentIndex];
                if (!string.Equals(prevActive, nextActive, StringComparison.OrdinalIgnoreCase) || ActiveMediaKind == LobbyBgMediaKind.Unknown)
                {
                    LoadMediaAt(_currentIndex);
                }
            }
        }

        private void ApplySelectedOrFirst(bool forceReload)
        {
            if (_mediaFiles.Count == 0)
            {
                ActiveMediaLabel = "目录暂无素材（保留原版）";
                StatusMsg = $"目录暂无可用素材: {ResolveMediaDirectory()}";
                return;
            }
            if (_currentIndex < 0 || _currentIndex >= _mediaFiles.Count)
            {
                _currentIndex = 0;
            }
            if (forceReload || ActiveMediaKind == LobbyBgMediaKind.Unknown)
            {
                LoadMediaAt(_currentIndex);
            }
        }

        // ------------------------------------------------------------------
        // Media Loading & Switching
        // ------------------------------------------------------------------

        private void LoadMediaAt(int index)
        {
            if (index < 0 || index >= _mediaFiles.Count) return;
            string fullPath = _mediaFiles[index];
            if (!File.Exists(fullPath))
            {
                _autoLoadFailedForCurrentFile = true;
                StatusMsg = $"文件不存在: {fullPath}";
                return;
            }

            LobbyBgMediaKind kind = CustomLobbyBgPolicy.ClassifyExtension(fullPath);
            string fileName = Path.GetFileName(fullPath);
            bool hadCriOverride = _criOverrideApplied;
            try
            {
                TeardownActiveMedia(restoreVanilla: false);

                switch (kind)
                {
                    case LobbyBgMediaKind.Image:
                        if (hadCriOverride) RestoreHomeVanillaCri(FindHomePanel());
                        LoadImageFile(fullPath, fileName);
                        break;
                    case LobbyBgMediaKind.Video:
                        if (hadCriOverride) RestoreHomeVanillaCri(FindHomePanel());
                        LoadVideoFile(fullPath, fileName);
                        break;
                    case LobbyBgMediaKind.CriUsm:
                    case LobbyBgMediaKind.UnityBundle:
                        BeginCriOverride(fullPath, fileName, kind);
                        break;
                    default:
                        _autoLoadFailedForCurrentFile = true;
                        StatusMsg = $"不支持的文件格式: {fileName}";
                        if (hadCriOverride) RestoreHomeVanillaCri(FindHomePanel());
                        break;
                }
            }
            catch (Exception ex)
            {
                _autoLoadFailedForCurrentFile = true;
                ActiveMediaLabel = $"加载异常: {fileName}";
                StatusMsg = $"加载失败({ex.GetType().Name}): {ex.Message}";
                try { TeardownActiveMedia(restoreVanilla: true); } catch { }
                RuntimeCompatibilityHost.LogLifecycle("[CustomLobbyBackground.LoadMediaAt] " + ex);
            }
        }

        private void LoadImageFile(string fullPath, string fileName)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes == null || bytes.Length < 16)
                {
                    ActiveMediaLabel = $"加载失败: {fileName}（数据为空或损坏）";
                    StatusMsg = $"图片数据过小或损坏: {fileName}";
                    return;
                }

                string detectedFmt = CustomLobbyBgPolicy.DescribeImageHeader(bytes);
                bool needsNativeFallback = CustomLobbyBgPolicy.IsLikelyUnsupportedByUnityLoadImage(bytes);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool loaded = !needsNativeFallback && ImageConversion.LoadImage(tex, bytes, false);
                bool looksLikeErrorPlaceholder = loaded && tex.width == 8 && tex.height == 8 && bytes.Length > 512;

                string diagError = "";
                if (!loaded || looksLikeErrorPlaceholder)
                {
                    if (Application.platform == RuntimePlatform.Android
                        && TryDecodeImageViaAndroidBitmap(fullPath, out byte[] transcodedPng, out diagError))
                    {
                        Destroy(tex);
                        tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        loaded = ImageConversion.LoadImage(tex, transcodedPng, false);
                        looksLikeErrorPlaceholder = loaded && tex.width == 8 && tex.height == 8;
                    }
                }

                if (!loaded || looksLikeErrorPlaceholder)
                {
                    Destroy(tex);
                    ActiveMediaLabel = $"无法解码: {fileName} ({detectedFmt})";
                    StatusMsg = string.IsNullOrEmpty(diagError)
                        ? $"无法解码图片 ({detectedFmt}): {fileName}（请改用标准 PNG / JPG）"
                        : $"无法解码图片 ({detectedFmt}): {fileName} - {diagError}";
                    return;
                }

                tex.name = "XingShu_LobbyBg_" + fileName;
                tex.wrapMode = TextureWrapMode.Clamp;
                _loadedImageTex = tex;

                NTexture nt = new NTexture(tex);
                nt.destroyMethod = DestroyMethod.None;
                nt.AddRef();
                _activeNTexture = nt;
                ActiveMediaKind = LobbyBgMediaKind.Image;
                ActiveMediaLabel = $"{fileName} ({tex.width}x{tex.height} 图片)";
                BindNTextureToLoaders(nt);
                StatusMsg = $"已应用静态背景: {fileName} ({tex.width}×{tex.height})";
            }
            catch (Exception ex)
            {
                ActiveMediaLabel = $"加载失败: {fileName}";
                StatusMsg = $"加载图片失败: {ex.Message}";
                TeardownActiveMedia(restoreVanilla: true);
            }
        }

        private static bool TryDecodeImageViaAndroidBitmap(string fullPath, out byte[] pngBytes, out string diagError)
        {
            pngBytes = null;
            diagError = "";
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(fullPath))
            {
                return false;
            }
            try
            {
                using (var bf = new AndroidJavaClass("android.graphics.BitmapFactory"))
                using (var bmp = bf.CallStatic<AndroidJavaObject>("decodeFile", fullPath))
                {
                    if (bmp == null)
                    {
                        diagError = "BitmapFactory.decodeFile 返回空";
                        return false;
                    }
                    return EncodeAndroidBitmapToPng(bmp, out pngBytes, out diagError);
                }
            }
            catch (Exception ex)
            {
                diagError = ex.Message;
                return false;
            }
        }

        private static bool EncodeAndroidBitmapToPng(AndroidJavaObject bmp, out byte[] pngBytes, out string diagError)
        {
            pngBytes = null;
            diagError = "";
            if (bmp == null)
            {
                diagError = "位图为空";
                return false;
            }
            try
            {
                int w = bmp.Call<int>("getWidth");
                int h = bmp.Call<int>("getHeight");
                if (w <= 0 || h <= 0)
                {
                    diagError = "图片尺寸无效";
                    return false;
                }

                AndroidJavaObject scaled = null;
                try
                {
                    AndroidJavaObject target = bmp;
                    const int MaxDim = 2048;
                    if (w > MaxDim || h > MaxDim)
                    {
                        float ratio = Math.Min((float)MaxDim / w, (float)MaxDim / h);
                        int nw = Math.Max(2, Mathf.RoundToInt(w * ratio));
                        int nh = Math.Max(2, Mathf.RoundToInt(h * ratio));
                        using (var bmpClass = new AndroidJavaClass("android.graphics.Bitmap"))
                        {
                            scaled = bmpClass.CallStatic<AndroidJavaObject>("createScaledBitmap", bmp, nw, nh, true);
                        }
                        if (scaled != null) target = scaled;
                    }

                    using (var baos = new AndroidJavaObject("java.io.ByteArrayOutputStream"))
                    using (var fmtClass = new AndroidJavaClass("android.graphics.Bitmap$CompressFormat"))
                    using (var pngFmt = fmtClass.GetStatic<AndroidJavaObject>("PNG"))
                    {
                        bool compressed = target.Call<bool>("compress", pngFmt, 100, baos);
                        if (!compressed)
                        {
                            diagError = "Android PNG 重编码失败";
                            return false;
                        }
                        using (var byteArrObj = baos.Call<AndroidJavaObject>("toByteArray"))
                        using (var b64Class = new AndroidJavaClass("android.util.Base64"))
                        {
                            string b64 = b64Class.CallStatic<string>("encodeToString", byteArrObj, 2); // Base64.NO_WRAP = 2
                            if (string.IsNullOrEmpty(b64))
                            {
                                diagError = "Base64 输出为空";
                                return false;
                            }
                            pngBytes = Convert.FromBase64String(b64);
                            return pngBytes != null && pngBytes.Length >= 16;
                        }
                    }
                }
                finally
                {
                    if (scaled != null) scaled.Dispose();
                }
            }
            catch (Exception ex)
            {
                diagError = ex.Message;
                return false;
            }
        }

        private static bool InvokeAndroidJniReflected(AndroidJavaObject target, string methodName, string[] paramClassNames, IntPtr[] argObjPtrs)
        {
            if (target == null) return false;
            IntPtr clsClass = IntPtr.Zero, clsObject = IntPtr.Zero, clsMethod = IntPtr.Zero;
            IntPtr targetCls = IntPtr.Zero, jName = IntPtr.Zero, pArray = IntPtr.Zero, methodObj = IntPtr.Zero, aArray = IntPtr.Zero, retObj = IntPtr.Zero;
            try
            {
                clsClass = AndroidJNI.FindClass("java/lang/Class");
                clsObject = AndroidJNI.FindClass("java/lang/Object");
                clsMethod = AndroidJNI.FindClass("java/lang/reflect/Method");
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) AndroidJNI.ExceptionClear();
                if (clsClass == IntPtr.Zero || clsObject == IntPtr.Zero || clsMethod == IntPtr.Zero) return false;

                IntPtr midGetMethod = AndroidJNI.GetMethodID(clsClass, "getMethod", "(Ljava/lang/String;[Ljava/lang/Class;)Ljava/lang/reflect/Method;");
                IntPtr midInvoke = AndroidJNI.GetMethodID(clsMethod, "invoke", "(Ljava/lang/Object;[Ljava/lang/Object;)Ljava/lang/Object;");
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) AndroidJNI.ExceptionClear();
                if (midGetMethod == IntPtr.Zero || midInvoke == IntPtr.Zero) return false;

                IntPtr rawTarget = target.GetRawObject();
                if (rawTarget == IntPtr.Zero) return false;
                targetCls = AndroidJNI.GetObjectClass(rawTarget);
                if (targetCls == IntPtr.Zero) return false;

                jName = AndroidJNI.NewString(methodName);
                int pCount = paramClassNames != null ? paramClassNames.Length : 0;
                pArray = AndroidJNI.NewObjectArray(pCount, clsClass, IntPtr.Zero);
                if (pCount > 0)
                {
                    using (var clsUtil = new AndroidJavaClass("java.lang.Class"))
                    {
                        for (int i = 0; i < pCount; i++)
                        {
                            using (var pc = clsUtil.CallStatic<AndroidJavaObject>("forName", paramClassNames[i]))
                            {
                                AndroidJNI.SetObjectArrayElement(pArray, i, pc.GetRawObject());
                            }
                        }
                    }
                }

                var gmArgs = new jvalue[2];
                gmArgs[0].l = jName;
                gmArgs[1].l = pArray;
                methodObj = AndroidJNI.CallObjectMethod(targetCls, midGetMethod, gmArgs);
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) { AndroidJNI.ExceptionClear(); return false; }
                if (methodObj == IntPtr.Zero) return false;

                int aCount = argObjPtrs != null ? argObjPtrs.Length : 0;
                aArray = AndroidJNI.NewObjectArray(aCount, clsObject, IntPtr.Zero);
                for (int i = 0; i < aCount; i++)
                {
                    AndroidJNI.SetObjectArrayElement(aArray, i, argObjPtrs[i]);
                }

                var invArgs = new jvalue[2];
                invArgs[0].l = rawTarget;
                invArgs[1].l = aArray;
                retObj = AndroidJNI.CallObjectMethod(methodObj, midInvoke, invArgs);
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) { AndroidJNI.ExceptionClear(); return false; }
                return true;
            }
            catch
            {
                if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero) AndroidJNI.ExceptionClear();
                return false;
            }
            finally
            {
                if (retObj != IntPtr.Zero) AndroidJNI.DeleteLocalRef(retObj);
                if (aArray != IntPtr.Zero) AndroidJNI.DeleteLocalRef(aArray);
                if (methodObj != IntPtr.Zero) AndroidJNI.DeleteLocalRef(methodObj);
                if (pArray != IntPtr.Zero) AndroidJNI.DeleteLocalRef(pArray);
                if (jName != IntPtr.Zero) AndroidJNI.DeleteLocalRef(jName);
                if (targetCls != IntPtr.Zero) AndroidJNI.DeleteLocalRef(targetCls);
                if (clsMethod != IntPtr.Zero) AndroidJNI.DeleteLocalRef(clsMethod);
                if (clsObject != IntPtr.Zero) AndroidJNI.DeleteLocalRef(clsObject);
                if (clsClass != IntPtr.Zero) AndroidJNI.DeleteLocalRef(clsClass);
            }
        }

        private bool TryFallbackAndroidVideoPoster(string fullPath, string fileName, string vpCaps)
        {
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                _autoLoadFailedForCurrentFile = true;
                ActiveMediaLabel = $"不支持视频播放: {fileName}";
                StatusMsg = $"当前客户端未内置 VideoPlayer({vpCaps})，请改用图片或 .usm / .bundle";
                TeardownActiveMedia(restoreVanilla: true);
                return false;
            }

            string diagError = "";
            byte[] pngBytes = null;
            AndroidJavaObject mmr = null;
            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                mmr = new AndroidJavaObject("android.media.MediaMetadataRetriever");
                pathPtr = AndroidJNI.NewString(fullPath);
                if (!InvokeAndroidJniReflected(mmr, "setDataSource", new[] { "java.lang.String" }, new[] { pathPtr }))
                {
                    diagError = "MediaMetadataRetriever.setDataSource 失败";
                }
                else
                {
                    using (var frameBmp = mmr.Call<AndroidJavaObject>("getFrameAtTime", 0L))
                    {
                        if (frameBmp == null)
                        {
                            diagError = "无法提取视频首帧";
                        }
                        else if (!EncodeAndroidBitmapToPng(frameBmp, out pngBytes, out diagError))
                        {
                            if (string.IsNullOrEmpty(diagError)) diagError = "首帧转码 PNG 失败";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diagError = ex.Message;
            }
            finally
            {
                if (pathPtr != IntPtr.Zero) AndroidJNI.DeleteLocalRef(pathPtr);
                if (mmr != null)
                {
                    try { InvokeAndroidJniReflected(mmr, "release", null, null); } catch { }
                    try { mmr.Dispose(); } catch { }
                }
            }

            if (pngBytes == null || pngBytes.Length < 16)
            {
                _autoLoadFailedForCurrentFile = true;
                ActiveMediaLabel = $"视频引擎不可用: {fileName}";
                StatusMsg = $"未内置 VideoPlayer({vpCaps})，且首帧回退失败: {diagError}";
                TeardownActiveMedia(restoreVanilla: true);
                return false;
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, pngBytes, false))
            {
                Destroy(tex);
                _autoLoadFailedForCurrentFile = true;
                ActiveMediaLabel = $"视频引擎不可用: {fileName}";
                StatusMsg = $"未内置 VideoPlayer({vpCaps})，首帧纹理生成失败";
                TeardownActiveMedia(restoreVanilla: true);
                return false;
            }

            tex.name = "XingShu_LobbyBg_Poster_" + fileName;
            tex.wrapMode = TextureWrapMode.Clamp;
            _loadedImageTex = tex;
            NTexture nt = new NTexture(tex);
            nt.destroyMethod = DestroyMethod.None;
            nt.AddRef();
            _activeNTexture = nt;
            ActiveMediaKind = LobbyBgMediaKind.Image;
            ActiveMediaLabel = $"{fileName} ({tex.width}x{tex.height} 首帧兼容)";
            BindNTextureToLoaders(nt);
            StatusMsg = $"客户端裁剪了 VideoPlayer({vpCaps})，已自动应用视频高清首帧（动态背景请用 .usm / .bundle 或静态图）";
            return true;
        }

        private void LoadVideoFile(string fullPath, string fileName)
        {
            _activeVideoFullPath = fullPath;
            _videoPreparedPath = fullPath;
            _videoRetryStage = 0;
            BeginVideoPrepareStage(fullPath, fileName, 0);
        }

        private void BeginVideoPrepareStage(string fullPath, string fileName, int stage)
        {
            try
            {
                if (!IsVideoPlayerApiUsable())
                {
                    TryFallbackAndroidVideoPoster(fullPath, fileName, DescribeVideoPlayerCaps());
                    return;
                }
                EnsureVideoPlayer();
                if (_videoPlayer == null)
                {
                    TryFallbackAndroidVideoPoster(fullPath, fileName, DescribeVideoPlayerCaps() + ",comp=0");
                    return;
                }

                _activeVideoFullPath = fullPath;
                _companionAudio.Prewarm();
                InvokeVp(_vpStopMethod);
                ConfigureVideoAudioBeforePrepare();

                bool isAndroid = Application.platform == RuntimePlatform.Android;
                string effectivePath = fullPath;
                if (isAndroid && stage >= 2)
                {
                    effectivePath = EnsureAndroidAsciiCacheCopy(fullPath);
                }
                _videoPreparedPath = effectivePath;
                string videoUrl = CustomLobbyBgPolicy.BuildVideoUrl(effectivePath, isAndroid, stage);

                int initW = XingShuMobileUIHelper.IsMobile ? 1280 : 1920;
                int initH = XingShuMobileUIHelper.IsMobile ? 720 : 1080;
                if (_videoRt == null || _videoRt.width != initW || _videoRt.height != initH)
                {
                    ReleaseRenderTexture();
                    _videoRt = new RenderTexture(initW, initH, 0, RenderTextureFormat.ARGB32);
                    _videoRt.name = "XingShu_LobbyBg_RT";
                    _videoRt.Create();
                }

                SetVpEnumProp(_vpSourceProp, "Url", 1);
                SetVpProp(_vpUrlProp, videoUrl);
                SetVpEnumProp(_vpRenderModeProp, "RenderTexture", 2);
                SetVpProp(_vpTargetTextureProp, _videoRt);
                SetVpEnumProp(_vpAspectRatioProp, "NoScaling", 0);
                SetVpProp(_vpPlayOnAwakeProp, false);
                SetVpProp(_vpPlaybackSpeedProp, _speed);
                ApplyVideoPlaybackSettings();

                _videoRetryStage = stage;
                _videoPreparing = true;
                _videoStarted = false;
                _initialAudioAligned = false;
                _pausedForAway = false;
                _videoPrepareStartedAt = ThinkingClock.RealSeconds();
                ActiveMediaKind = LobbyBgMediaKind.Video;
                ActiveMediaLabel = stage == 0
                    ? $"{fileName} (准备视频中…)"
                    : $"{fileName} (兼容路径重试 {stage + 1}/3…)";
                StatusMsg = stage == 0
                    ? $"正在加载视频: {fileName}…"
                    : $"正在切换兼容路径重试 ({stage + 1}/3): {fileName}…";

                InvokeVp(_vpPrepareMethod);
            }
            catch (Exception ex)
            {
                if (Application.platform == RuntimePlatform.Android && stage < 2 && IsVideoPlayerApiUsable())
                {
                    BeginVideoPrepareStage(fullPath, fileName, stage + 1);
                    return;
                }
                _autoLoadFailedForCurrentFile = true;
                ActiveMediaLabel = $"加载失败: {fileName}";
                StatusMsg = $"初始化视频失败({ex.GetType().Name}): {ex.Message}";
                TeardownActiveMedia(restoreVanilla: true);
            }
        }

        private static string EnsureAndroidAsciiCacheCopy(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return sourcePath;
            }
            try
            {
                string cacheDir = Application.temporaryCachePath;
                if (string.IsNullOrEmpty(cacheDir))
                {
                    return sourcePath;
                }
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
                string cacheName = CustomLobbyBgPolicy.BuildAndroidAsciiCacheFileName(sourcePath);
                string dstPath = Path.Combine(cacheDir, cacheName);
                FileInfo srcInfo = new FileInfo(sourcePath);
                if (File.Exists(dstPath))
                {
                    FileInfo dstInfo = new FileInfo(dstPath);
                    if (dstInfo.Length == srcInfo.Length && dstInfo.LastWriteTimeUtc >= srcInfo.LastWriteTimeUtc)
                    {
                        return dstPath;
                    }
                }
                File.Copy(sourcePath, dstPath, overwrite: true);
                return dstPath;
            }
            catch
            {
                return sourcePath;
            }
        }

        private void EnsureVideoPlayer()
        {
            if (_vpType == null) return;
            if (_videoGo == null)
            {
                _videoGo = new GameObject(VideoGoName);
                _videoGo.transform.SetParent(transform, false);
            }
            if (_videoPlayer == null)
            {
                _videoPlayer = _videoGo.GetComponent(_vpType) ?? _videoGo.AddComponent(_vpType);
            }
        }

        private void TickVideoPlayback()
        {
            if (ActiveMediaKind != LobbyBgMediaKind.Video || _videoPlayer == null)
            {
                return;
            }

            if (_videoPreparing)
            {
                int vw = 0;
                int vh = 0;
                bool isPrepared = false;
                try
                {
                    vw = GetVpInt(_vpWidthProp);
                    vh = GetVpInt(_vpHeightProp);
                    isPrepared = GetVpBool(_vpIsPreparedProp);
                    if (isPrepared && (vw <= 0 || vh <= 0) && _vpTextureProp != null)
                    {
                        Texture vpTex = _vpTextureProp.GetValue(_videoPlayer, null) as Texture;
                        if (vpTex != null && vpTex.width > 0 && vpTex.height > 0)
                        {
                            vw = vpTex.width;
                            vh = vpTex.height;
                        }
                    }
                }
                catch
                {
                }

                if ((vw > 0 && vh > 0) || isPrepared)
                {
                    _videoPreparing = false;
                    CustomLobbyBgPolicy.ComputeRenderTextureSize(vw, vh, XingShuMobileUIHelper.IsMobile, out int rtW, out int rtH);
                    if (_videoRt == null || _videoRt.width != rtW || _videoRt.height != rtH)
                    {
                        ReleaseRenderTexture();
                        _videoRt = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
                        _videoRt.name = "XingShu_LobbyBg_RT";
                        _videoRt.Create();
                    }

                    SetVpProp(_vpTargetTextureProp, _videoRt);
                    SetVpProp(_vpPlaybackSpeedProp, _speed);
                    ApplyVideoPlaybackSettings();
                    InvokeVp(_vpPlayMethod);
                    ApplyVideoPlaybackSettings();
                    _videoStarted = true;
                    _initialAudioAligned = false;
                    _videoStartedAt = ThinkingClock.RealSeconds();
                    _lastAudioDriftCheckAt = ThinkingClock.RealSeconds();
                    string audioPath = !string.IsNullOrEmpty(_videoPreparedPath) ? _videoPreparedPath : _activeVideoFullPath;
                    _companionAudio.StartFile(audioPath, _muted, _volume, _speed, _pausedForAway, 0.0);

                    ReleaseActiveNTexture();
                    NTexture nt = new NTexture(_videoRt);
                    nt.destroyMethod = DestroyMethod.None;
                    nt.AddRef();
                    _activeNTexture = nt;
                    BindNTextureToLoaders(nt);

                    string fn = CurrentFileName;
                    ActiveMediaLabel = $"{fn} ({rtW}x{rtH} 视频)";
                    StatusMsg = $"已应用动态视频背景: {fn} ({rtW}×{rtH})";
                    return;
                }

                bool isAndroid = Application.platform == RuntimePlatform.Android;
                float stageTimeout = isAndroid ? 4.5f : VideoPrepareTimeoutSeconds;
                if (ThinkingClock.RealSeconds() - _videoPrepareStartedAt > stageTimeout)
                {
                    string fn = CurrentFileName;
                    if (isAndroid && _videoRetryStage < 2 && !string.IsNullOrEmpty(_activeVideoFullPath))
                    {
                        BeginVideoPrepareStage(_activeVideoFullPath, fn, _videoRetryStage + 1);
                        return;
                    }
                    _videoPreparing = false;
                    _autoLoadFailedForCurrentFile = true;
                    StatusMsg = $"视频解码超时或编码不受支持: {fn}（建议使用 H.264 MP4）";
                    TeardownActiveMedia(restoreVanilla: true);
                }
                return;
            }

            // Software loop guard (essential on Android IL2CPP where VideoPlayer.isLooping may be stripped)
            if (_videoStarted && !_pausedForAway)
            {
                bool playing = false;
                bool paused = false;
                try
                {
                    playing = GetVpBool(_vpIsPlayingProp);
                    paused = GetVpBool(_vpIsPausedProp);
                }
                catch
                {
                }
                if (!playing && !paused)
                {
                    double len = 0;
                    double cur = 0;
                    try
                    {
                        len = GetVpDouble(_vpLengthProp);
                        cur = GetVpDouble(_vpTimeProp);
                    }
                    catch
                    {
                    }
                    bool reachedEnd = (len > 0.05 && cur >= len - 0.15) || (ThinkingClock.RealSeconds() - _videoStartedAt > 0.35);
                    if (reachedEnd)
                    {
                        try
                        {
                            SetVpProp(_vpTimeProp, 0.0);
                            InvokeVp(_vpPlayMethod);
                            _videoStartedAt = ThinkingClock.RealSeconds();
                            _companionAudio.RequestSeek(0.0);
                        }
                        catch
                        {
                        }
                    }
                }
                else if (playing && _companionAudio.HasAudio)
                {
                    if (!_initialAudioAligned)
                    {
                        _initialAudioAligned = true;
                        _lastAudioDriftCheckAt = ThinkingClock.RealSeconds();
                        try
                        {
                            double vt = GetVpDouble(_vpTimeProp);
                            double at = _companionAudio.CurrentTimeSeconds;
                            if (vt >= 0.0 && Math.Abs(at - vt) > 0.55)
                            {
                                _companionAudio.RequestSeek(vt);
                            }
                        }
                        catch
                        {
                        }
                    }
                    else if (ThinkingClock.RealSeconds() - _lastAudioDriftCheckAt >= 3.0)
                    {
                        _lastAudioDriftCheckAt = ThinkingClock.RealSeconds();
                        try
                        {
                            double vt = GetVpDouble(_vpTimeProp);
                            double at = _companionAudio.CurrentTimeSeconds;
                            if (vt >= 0.0 && Math.Abs(at - vt) > 1.25)
                            {
                                _companionAudio.RequestSeek(vt);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void UpdateVideoPowerState(bool anyLobbyVisible)
        {
            if (ActiveMediaKind != LobbyBgMediaKind.Video || _videoPlayer == null || _videoPreparing || !_videoStarted)
            {
                return;
            }
            if (!anyLobbyVisible && !_pausedForAway)
            {
                _pausedForAway = true;
                try { InvokeVp(_vpPauseMethod); } catch { }
                try { _companionAudio.SetPaused(true); } catch { }
            }
            else if (anyLobbyVisible && _pausedForAway)
            {
                _pausedForAway = false;
                try
                {
                    double vt = 0.0;
                    try { vt = GetVpDouble(_vpTimeProp); } catch { }
                    InvokeVp(_vpPlayMethod);
                    ApplyVideoPlaybackSettings();
                    _videoStartedAt = ThinkingClock.RealSeconds();
                    _initialAudioAligned = true;
                    _lastAudioDriftCheckAt = ThinkingClock.RealSeconds();
                    _companionAudio.RequestSeek(vt);
                    _companionAudio.SetPaused(false);
                }
                catch
                {
                }
            }
        }

        private void ConfigureVideoAudioBeforePrepare()
        {
            if (_videoPlayer == null) return;
            try
            {
                // Always silence Unity's own VideoPlayer audio backend (game sets AudioManager.m_DisableAudio=true
                // and Windows backend rejects Direct audio output); audio is driven by _companionAudio instead.
                if (_vpAudioModeProp != null)
                {
                    object noneMode;
                    try
                    {
                        noneMode = Enum.Parse(_vpAudioModeProp.PropertyType, "None", false);
                    }
                    catch
                    {
                        noneMode = Enum.ToObject(_vpAudioModeProp.PropertyType, 0);
                    }
                    _vpAudioModeProp.SetValue(_videoPlayer, noneMode, null);
                }

                _videoUsesUnscaledClock = false;
                if (_vpTimeUpdateModeProp != null)
                {
                    try
                    {
                        object unscaledMode = Enum.Parse(_vpTimeUpdateModeProp.PropertyType, "UnscaledGameTime", false);
                        _vpTimeUpdateModeProp.SetValue(_videoPlayer, unscaledMode, null);
                        _videoUsesUnscaledClock = true;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static float ReadNativeClockScale()
        {
            float s = XingShuSpeedEngineCoordinator.ActiveNativeClockScale;
            return s > 0.01f ? s : 1.0f;
        }

        private void ApplyVideoPlaybackSettings()
        {
            _companionAudio.SetAudioParams(_muted, _volume, _speed);

            float nativeScale = ReadNativeClockScale();
            float unityScale = Time.timeScale;
            _lastAppliedNativeScale = nativeScale;
            _lastAppliedUnityTimeScale = unityScale;

            float compensatedVideoSpeed = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(
                _speed, nativeScale, unityScale, _videoUsesUnscaledClock);
            float compensatedCriSpeed = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(
                _speed, nativeScale, unityScale, true);

            if (_videoPlayer != null)
            {
                SetVpProp(_vpPlaybackSpeedProp, compensatedVideoSpeed);
                SetVpProp(_vpIsLoopingProp, true);
            }

            if (_criOverrideApplied)
            {
                try
                {
                    HomePanel home = FindHomePanel();
                    GGraph graph = home != null && home.UIObject != null && home.UIObject.Com_BGSet != null
                        ? home.UIObject.Com_BGSet.loader_Video
                        : null;
                    if (graph != null && !graph.isDisposed && TryGetCriControllerAndPlayer(graph, out _, out object player) && player != null)
                    {
                        _criPlayerSetSpeedMethod?.Invoke(player, new object[] { compensatedCriSpeed });
                        _criPlayerSetVolumeMethod?.Invoke(player, new object[] { _muted ? 0f : _volume });
                    }
                }
                catch
                {
                }
            }
        }

        private void SyncVanillaLobbyBgm(bool homeVisible, bool subBgVisible, bool enteredHome)
        {
            bool shouldMute = CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(_enabled, _muteVanillaBgm, homeVisible, subBgVisible);
            if (shouldMute)
            {
                if (!_vanillaBgmSuppressed || enteredHome)
                {
                    TrySetVanillaBgmRtpc(0f);
                    _vanillaBgmSuppressed = true;
                }
            }
            else if (_vanillaBgmSuppressed)
            {
                RestoreVanillaLobbyBgm();
            }
        }

        private void RestoreVanillaLobbyBgm()
        {
            if (!_vanillaBgmSuppressed)
            {
                return;
            }
            _vanillaBgmSuppressed = false;
            try
            {
                float saved = global::Core.GameSettings.GetBGMVolume();
                TrySetVanillaBgmRtpc(saved);
            }
            catch
            {
            }
        }

        private static void TrySetVanillaBgmRtpc(float value)
        {
            try
            {
                global::Core.AudioManager audio = global::Tools.SimpleSingletonProvider<global::Core.AudioManager>.inst;
                if (audio != null)
                {
                    audio.SetRTPCValue(global::Core.LocalStore.BGMVolumeLabel, value);
                }
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------
        // CriWare (.usm & 3.3.0 VKVLoop .bundle) Override
        // ------------------------------------------------------------------

        private void BeginCriOverride(string fullPath, string fileName, LobbyBgMediaKind kind)
        {
            _pendingCriPath = fullPath;
            _pendingCriKind = kind;
            _pendingCriStartedAt = ThinkingClock.RealSeconds();
            _criPhase = CriSwitchPhase.WaitingStop;
            ActiveMediaKind = kind;
            ActiveMediaLabel = $"{fileName} ({CustomLobbyBgPolicy.KindLabel(kind)})";

            HomePanel home = FindHomePanel();
            GGraph graph = home != null && home.UIObject != null && home.UIObject.Com_BGSet != null
                ? home.UIObject.Com_BGSet.loader_Video
                : null;
            if (graph != null && !graph.isDisposed)
            {
                if (TryGetCriControllerAndPlayer(graph, out _, out object player) && player != null)
                {
                    try { _criPlayerStopMethod?.Invoke(player, null); } catch { }
                }
                StatusMsg = $"正在切换 CriWare 大厅动效: {fileName}…";
            }
            else
            {
                StatusMsg = $"已选中 {fileName}，回到游戏主大厅后自动切换";
            }
        }

        private void TickPendingCriSwitch(HomePanel home)
        {
            if (_criPhase != CriSwitchPhase.WaitingStop || string.IsNullOrEmpty(_pendingCriPath))
            {
                return;
            }
            if (home == null || home.UIObject == null || home.UIObject.Com_BGSet == null)
            {
                return;
            }
            GGraph graph = home.UIObject.Com_BGSet.loader_Video;
            if (graph == null || graph.isDisposed) return;

            if (!TryGetCriControllerAndPlayer(graph, out Component ctrl, out object player) || ctrl == null || player == null)
            {
                if (ThinkingClock.RealSeconds() - _pendingCriStartedAt > 5.0)
                {
                    _criPhase = CriSwitchPhase.Idle;
                    StatusMsg = "未找到大厅 CriWare 播放器组件，请改用 .mp4 或 .png 素材";
                }
                return;
            }

            // Wait until status == 0 (Stop) or timeout after 1.5s
            int statusInt = -1;
            try
            {
                object st = _criPlayerStatusProp?.GetValue(player, null);
                if (st != null) statusInt = Convert.ToInt32(st);
            }
            catch
            {
            }
            if (statusInt != 0 && (ThinkingClock.RealSeconds() - _pendingCriStartedAt) < 1.5)
            {
                return;
            }

            _criPhase = CriSwitchPhase.Idle;
            string path = _pendingCriPath;
            string fn = Path.GetFileName(path);

            try
            {
                float criSpeed = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(_speed, ReadNativeClockScale(), Time.timeScale, true);
                if (_pendingCriKind == LobbyBgMediaKind.CriUsm)
                {
                    if (!InvokeCriSetFile(player, path))
                    {
                        StatusMsg = $"当前客户端不支持直接加载本地 .usm，请改用 MP4 视频: {fn}";
                        return;
                    }
                    _criPlayerLoopMethod?.Invoke(player, new object[] { true });
                    _criPlayerSetSpeedMethod?.Invoke(player, new object[] { criSpeed });
                    _criPlayerSetVolumeMethod?.Invoke(player, new object[] { _muted ? 0f : _volume });
                    _criPlayerPauseMethod?.Invoke(player, new object[] { false });
                    _criPlayerStartMethod?.Invoke(player, null);
                    EnsureGraphRendererVisible(graph);
                    _criOverrideApplied = true;
                    StatusMsg = $"已应用 CriWare USM 大厅背景: {fn}";
                }
                else if (_pendingCriKind == LobbyBgMediaKind.UnityBundle)
                {
                    ReleaseCriBundle();
                    AssetBundle ab = AssetBundle.LoadFromFile(path);
                    if (ab == null)
                    {
                        StatusMsg = $"Bundle 加载失败（可能与游戏已加载容器重名，请改放 .usm / .mp4 或用桌面端工具安装）: {fn}";
                        return;
                    }
                    _loadedCriBundle = ab;
                    UnityEngine.Object[] assets = ab.LoadAllAssets();
                    UnityEngine.Object usmAsset = null;
                    if (assets != null && _criUsmAssetType != null)
                    {
                        for (int i = 0; i < assets.Length; i++)
                        {
                            if (assets[i] != null && _criUsmAssetType.IsAssignableFrom(assets[i].GetType()))
                            {
                                usmAsset = assets[i];
                                break;
                            }
                        }
                    }
                    if (usmAsset == null || _criCtrlSetAssetMethod == null)
                    {
                        ReleaseCriBundle();
                        StatusMsg = $"未在 .bundle 中找到 3.3.0 CriManaUsmAsset（旧包需先经星枢 Hub 适配）: {fn}";
                        return;
                    }
                    _criCtrlSetAssetMethod.Invoke(ctrl, new object[] { Path.GetFileNameWithoutExtension(fn), usmAsset });
                    _criPlayerLoopMethod?.Invoke(player, new object[] { true });
                    _criPlayerSetSpeedMethod?.Invoke(player, new object[] { criSpeed });
                    _criPlayerSetVolumeMethod?.Invoke(player, new object[] { _muted ? 0f : _volume });
                    _criPlayerPauseMethod?.Invoke(player, new object[] { false });
                    _criPlayerStartMethod?.Invoke(player, null);
                    EnsureGraphRendererVisible(graph);
                    _criOverrideApplied = true;
                    StatusMsg = $"已应用 VKVLoop 动效包: {fn}";
                }
            }
            catch (Exception ex)
            {
                StatusMsg = $"应用 CriWare 背景失败: {ex.Message}";
            }
        }

        private static bool InvokeCriSetFile(object player, string filePath)
        {
            if (player == null || _criPlayerSetFileMethod == null) return false;
            ParameterInfo[] ps = _criPlayerSetFileMethod.GetParameters();
            if (ps.Length == 2)
            {
                _criPlayerSetFileMethod.Invoke(player, new object[] { null, filePath });
                return true;
            }
            if (ps.Length == 3)
            {
                object setMode = Enum.ToObject(ps[2].ParameterType, 0); // SetMode.New
                _criPlayerSetFileMethod.Invoke(player, new object[] { null, filePath, setMode });
                return true;
            }
            return false;
        }

        private static void EnsureGraphRendererVisible(GGraph graph)
        {
            try
            {
                graph.visible = true;
                MeshRenderer mr = graph.shape != null && graph.shape.graphics != null
                    ? graph.shape.graphics.meshRenderer
                    : null;
                if (mr != null) mr.enabled = true;
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------------
        // FairyGUI Overlay Attachment, Mirroring & Vanilla Suppression
        // ------------------------------------------------------------------

        private void BindNTextureToLoaders(NTexture nt)
        {
            if (_homeLoader != null && !_homeLoader.isDisposed)
            {
                _homeLoader.texture = nt;
                ApplyStyleToLoader(_homeLoader);
            }
            if (_subLoader != null && !_subLoader.isDisposed)
            {
                _subLoader.texture = nt;
                ApplyStyleToLoader(_subLoader);
            }
        }

        private void ApplyOverlayVisualStyle()
        {
            if (_homeLoader != null && !_homeLoader.isDisposed) ApplyStyleToLoader(_homeLoader);
            if (_subLoader != null && !_subLoader.isDisposed) ApplyStyleToLoader(_subLoader);
        }

        private void ApplyStyleToLoader(GLoader loader)
        {
            if (loader == null || loader.isDisposed) return;
            loader.align = AlignType.Center;
            loader.verticalAlign = VertAlignType.Middle;
            switch (_fillMode)
            {
                case LobbyBgFillMode.Contain:
                    loader.fill = FillType.Scale;
                    break;
                case LobbyBgFillMode.Stretch:
                    loader.fill = FillType.ScaleFree;
                    break;
                default:
                    loader.fill = FillType.ScaleNoBorder;
                    break;
            }
            loader.color = new Color(_brightness, _brightness, _brightness, 1f);
        }

        private void EnsureOverlayAttached(ref GLoader loader, ref GGraph attachedTo, GGraph targetGraph, string tag)
        {
            if (loader != null && !loader.isDisposed && attachedTo == targetGraph && loader.parent == targetGraph.parent)
            {
                if (loader.texture != _activeNTexture && _activeNTexture != null)
                {
                    loader.texture = _activeNTexture;
                }
                return;
            }

            if (loader != null && !loader.isDisposed)
            {
                try { loader.Dispose(); } catch { }
            }
            loader = new GLoader
            {
                name = LoaderName + "_" + tag,
                touchable = false,
                visible = true,
            };
            targetGraph.AddAfterMe(loader);
            attachedTo = targetGraph;
            if (_activeNTexture != null)
            {
                loader.texture = _activeNTexture;
            }
            ApplyStyleToLoader(loader);
        }

        private void SyncOverlayTransform(GLoader loader, GGraph targetGraph, bool shouldShow)
        {
            if (loader == null || loader.isDisposed || targetGraph == null || targetGraph.isDisposed) return;
            float w = targetGraph.width;
            float h = targetGraph.height;
            if ((w <= 1f || h <= 1f) && targetGraph.parent != null)
            {
                w = targetGraph.parent.width;
                h = targetGraph.parent.height;
            }
            loader.pivotAsAnchor = targetGraph.pivotAsAnchor;
            loader.SetPivot(targetGraph.pivotX, targetGraph.pivotY, targetGraph.pivotAsAnchor);
            loader.SetXY(targetGraph.x, targetGraph.y);
            loader.SetSize(Mathf.Max(1f, w), Mathf.Max(1f, h));
            loader.SetScale(targetGraph.scaleX, targetGraph.scaleY);
            loader.rotation = targetGraph.rotation;
            loader.alpha = targetGraph.alpha > 0.01f ? targetGraph.alpha : 1f;
            loader.visible = shouldShow && _activeNTexture != null;
            ApplyStyleToLoader(loader);
        }

        private static void SetLoaderVisible(GLoader loader, bool visible)
        {
            if (loader != null && !loader.isDisposed && loader.visible != visible)
            {
                loader.visible = visible;
            }
        }

        private static bool TryGetCriControllerAndPlayer(GGraph graph, out Component controller, out object player)
        {
            controller = null;
            player = null;
            if (graph == null || graph.isDisposed || graph.shape == null) return false;
            GameObject go = graph.shape.gameObject;
            if (go == null) return false;
            controller = _criCtrlType != null
                ? go.GetComponent(_criCtrlType)
                : go.GetComponent("CriManaMovieControllerForGGraph");
            if (controller == null) return false;
            if (_criPlayerProp != null)
            {
                try { player = _criPlayerProp.GetValue(controller, null); } catch { }
            }
            return true;
        }

        private void SuppressVanillaCri(GGraph graph, bool suppress, ref bool flag)
        {
            if (graph == null || graph.isDisposed) return;
            try
            {
                MeshRenderer mr = graph.shape != null && graph.shape.graphics != null
                    ? graph.shape.graphics.meshRenderer
                    : null;
                if (mr != null && mr.enabled == suppress)
                {
                    mr.enabled = !suppress;
                }
                if (TryGetCriControllerAndPlayer(graph, out _, out object player) && player != null)
                {
                    _criPlayerPauseMethod?.Invoke(player, new object[] { suppress });
                }
                flag = suppress;
            }
            catch
            {
            }
        }

        private void RestoreAllVanillaCri(HomePanel home, BackgroundPanel bgPanel)
        {
            if (_suppressedHomeCri)
            {
                RestoreHomeVanillaCri(home);
            }
            if (_suppressedSubCri)
            {
                if (bgPanel != null && bgPanel.UIObject != null)
                {
                    SuppressVanillaCri(bgPanel.UIObject.loader_BG, false, ref _suppressedSubCri);
                }
                _suppressedSubCri = false;
            }
        }

        private void RestoreHomeVanillaCri(HomePanel home)
        {
            if (home != null && home.UIObject != null && home.UIObject.Com_BGSet != null)
            {
                GGraph graph = home.UIObject.Com_BGSet.loader_Video;
                SuppressVanillaCri(graph, false, ref _suppressedHomeCri);
                if (_criOverrideApplied && _showStartVideoMethod != null)
                {
                    _criOverrideApplied = false;
                    try { _showStartVideoMethod.Invoke(home, null); } catch { }
                }
            }
            _suppressedHomeCri = false;
            _criOverrideApplied = false;
        }

        private void RestoreSubPanelVanilla()
        {
            BackgroundPanel bgPanel = FindBackgroundPanel();
            if (bgPanel != null && bgPanel.UIObject != null && bgPanel.UIObject.loader_BG != null)
            {
                SuppressVanillaCri(bgPanel.UIObject.loader_BG, false, ref _suppressedSubCri);
                if (_bgRefreshMethod != null)
                {
                    try { _bgRefreshMethod.Invoke(bgPanel, null); } catch { }
                }
            }
            _suppressedSubCri = false;
        }

        // ------------------------------------------------------------------
        // Teardown Helpers
        // ------------------------------------------------------------------

        private void TeardownActiveMedia(bool restoreVanilla)
        {
            _videoPreparing = false;
            _videoStarted = false;
            _initialAudioAligned = false;
            _pausedForAway = false;
            _criPhase = CriSwitchPhase.Idle;
            _pendingCriPath = "";
            _activeVideoFullPath = "";
            _videoPreparedPath = "";
            _videoRetryStage = 0;
            ActiveMediaKind = LobbyBgMediaKind.Unknown;

            try { _companionAudio.Stop(); } catch { }

            if (_videoPlayer != null)
            {
                InvokeVp(_vpStopMethod);
                SetVpProp(_vpTargetTextureProp, null);
            }

            if (_homeLoader != null && !_homeLoader.isDisposed)
            {
                _homeLoader.visible = false;
                _homeLoader.texture = null;
            }
            if (_subLoader != null && !_subLoader.isDisposed)
            {
                _subLoader.visible = false;
                _subLoader.texture = null;
            }

            ReleaseActiveNTexture();
            ReleaseRenderTexture();
            ReleaseImageTex();
            ReleaseCriBundle();

            if (restoreVanilla)
            {
                if (!_enabled)
                {
                    RestoreVanillaLobbyBgm();
                }
                ActiveMediaLabel = "原版默认大厅";
                RestoreAllVanillaCri(FindHomePanel(), FindBackgroundPanel());
            }
        }

        private void ReleaseActiveNTexture()
        {
            if (_activeNTexture != null)
            {
                try
                {
                    _activeNTexture.ReleaseRef();
                    _activeNTexture.Dispose();
                }
                catch
                {
                }
                _activeNTexture = null;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (_videoRt != null)
            {
                try
                {
                    _videoRt.Release();
                    Destroy(_videoRt);
                }
                catch
                {
                }
                _videoRt = null;
            }
        }

        private void ReleaseImageTex()
        {
            if (_loadedImageTex != null)
            {
                try { Destroy(_loadedImageTex); } catch { }
                _loadedImageTex = null;
            }
        }

        private void ReleaseCriBundle()
        {
            if (_loadedCriBundle != null)
            {
                try { _loadedCriBundle.Unload(true); } catch { }
                _loadedCriBundle = null;
            }
        }

        private void DestroyOverlayLoaders()
        {
            if (_homeLoader != null)
            {
                try { if (!_homeLoader.isDisposed) _homeLoader.Dispose(); } catch { }
                _homeLoader = null;
            }
            if (_subLoader != null)
            {
                try { if (!_subLoader.isDisposed) _subLoader.Dispose(); } catch { }
                _subLoader = null;
            }
            _attachedHomeGraph = null;
            _attachedSubGraph = null;
        }

        // ------------------------------------------------------------------
        // Panel Discovery & Reflection Initialization
        // ------------------------------------------------------------------

        private static HomePanel FindHomePanel()
        {
            UIManager mgr = UIManager.inst;
            if (mgr == null) return null;
            if (mgr.currentPanel is HomePanel hp) return hp;
            if (_cachedPanelDictField != null)
            {
                try
                {
                    var dict = _cachedPanelDictField.GetValue(mgr) as Dictionary<UIPanelType, IBasePanel>;
                    if (dict != null && dict.TryGetValue(UIPanelType.Home, out IBasePanel panel))
                    {
                        return panel as HomePanel;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private static BackgroundPanel FindBackgroundPanel()
        {
            UIManager mgr = UIManager.inst;
            return mgr != null ? mgr.backgroundPanel as BackgroundPanel : null;
        }

        private static bool IsPanelVisibleOnStage<TUi>(BasePanel<TUi> panel, out TUi uiObj) where TUi : GComponent
        {
            uiObj = null;
            if (panel == null || !panel.IsOpen()) return false;
            uiObj = panel.UIObject;
            return uiObj != null && !uiObj.isDisposed && uiObj.visible && uiObj.onStage;
        }

        private static void InitReflection()
        {
            if (_reflInit) return;
            _reflInit = true;

            const BindingFlags anyInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _cachedPanelDictField = typeof(UIManager).GetField("_cachedPanelDict", anyInst);
            _showStartVideoMethod = typeof(HomePanel).GetMethod("ShowStartVideo", anyInst);
            _bgRefreshMethod = typeof(BackgroundPanel).GetMethod("InitComponents", anyInst);

            // Stripped-safe VideoPlayer properties/methods on Android IL2CPP (zero direct IL dependencies)
            try
            {
                Type vpType = Type.GetType("UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule", false);
                if (vpType == null)
                {
                    Assembly[] loadedVp = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < loadedVp.Length; i++)
                    {
                        vpType = loadedVp[i].GetType("UnityEngine.Video.VideoPlayer", false, false);
                        if (vpType != null) break;
                    }
                }
                _vpType = vpType;
                if (vpType != null)
                {
                    _vpSourceProp = vpType.GetProperty("source", anyInst);
                    _vpUrlProp = vpType.GetProperty("url", anyInst);
                    _vpRenderModeProp = vpType.GetProperty("renderMode", anyInst);
                    _vpTargetTextureProp = vpType.GetProperty("targetTexture", anyInst);
                    _vpAspectRatioProp = vpType.GetProperty("aspectRatio", anyInst);
                    _vpPlayOnAwakeProp = vpType.GetProperty("playOnAwake", anyInst);
                    _vpPlaybackSpeedProp = vpType.GetProperty("playbackSpeed", anyInst);
                    _vpWidthProp = vpType.GetProperty("width", anyInst);
                    _vpHeightProp = vpType.GetProperty("height", anyInst);
                    _vpIsPlayingProp = vpType.GetProperty("isPlaying", anyInst);
                    _vpIsPausedProp = vpType.GetProperty("isPaused", anyInst);
                    _vpLengthProp = vpType.GetProperty("length", anyInst);
                    _vpTimeProp = vpType.GetProperty("time", anyInst);
                    _vpIsLoopingProp = vpType.GetProperty("isLooping", anyInst);
                    _vpAudioModeProp = vpType.GetProperty("audioOutputMode", anyInst);
                    _vpTimeUpdateModeProp = vpType.GetProperty("timeUpdateMode", anyInst);
                    _vpIsPreparedProp = vpType.GetProperty("isPrepared", anyInst);
                    _vpTextureProp = vpType.GetProperty("texture", anyInst);
                    _vpStopMethod = vpType.GetMethod("Stop", anyInst, null, Type.EmptyTypes, null);
                    _vpPrepareMethod = vpType.GetMethod("Prepare", anyInst, null, Type.EmptyTypes, null);
                    _vpPlayMethod = vpType.GetMethod("Play", anyInst, null, Type.EmptyTypes, null);
                    _vpPauseMethod = vpType.GetMethod("Pause", anyInst, null, Type.EmptyTypes, null);
                }
            }
            catch
            {
            }

            // CriWare runtime reflection (avoids hard compile dependency on CriMw DLLs)
            try
            {
                Type ctrlType = typeof(HomePanel).Assembly.GetType("UI.CriManaMovieControllerForGGraph");
                _criCtrlType = ctrlType;
                if (ctrlType != null)
                {
                    _criCtrlSetAssetMethod = ctrlType.GetMethod("SetAsset", anyInst);
                    _criPlayerProp = ctrlType.GetProperty("player", anyInst);
                    if (_criPlayerProp != null)
                    {
                        Type playerType = _criPlayerProp.PropertyType;
                        _criPlayerPauseMethod = playerType.GetMethod("Pause", anyInst);
                        _criPlayerStopMethod = playerType.GetMethod("Stop", anyInst);
                        _criPlayerStartMethod = playerType.GetMethod("Start", anyInst);
                        _criPlayerLoopMethod = playerType.GetMethod("Loop", anyInst);
                        _criPlayerSetSpeedMethod = playerType.GetMethod("SetSpeed", anyInst);
                        _criPlayerSetVolumeMethod = playerType.GetMethod("SetVolume", anyInst);
                        _criPlayerStatusProp = playerType.GetProperty("status", anyInst);
                        MethodInfo[] methods = playerType.GetMethods(anyInst);
                        for (int i = 0; i < methods.Length; i++)
                        {
                            if (methods[i].Name == "SetFile" && methods[i].GetParameters().Length >= 2)
                            {
                                _criPlayerSetFileMethod = methods[i];
                                break;
                            }
                        }
                    }
                }
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < loaded.Length; i++)
                {
                    Type t = loaded[i].GetType("CriWare.Assets.CriManaUsmAsset", false, false);
                    if (t != null)
                    {
                        _criUsmAssetType = t;
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsVideoPlayerApiUsable()
        {
            return _vpType != null
                && _vpUrlProp != null
                && _vpTargetTextureProp != null
                && _vpPrepareMethod != null
                && _vpPlayMethod != null;
        }

        private static string DescribeVideoPlayerCaps()
        {
            if (_vpType == null) return "vp=0";
            return $"vp=1,u={(_vpUrlProp != null ? 1 : 0)},t={(_vpTargetTextureProp != null ? 1 : 0)},pr={(_vpPrepareMethod != null ? 1 : 0)},pl={(_vpPlayMethod != null ? 1 : 0)},rm={(_vpRenderModeProp != null ? 1 : 0)}";
        }

        private void SetVpProp(PropertyInfo prop, object value)
        {
            if (_videoPlayer == null || prop == null || !prop.CanWrite) return;
            try { prop.SetValue(_videoPlayer, value, null); } catch { }
        }

        private void SetVpEnumProp(PropertyInfo prop, string enumName, int fallbackInt)
        {
            if (_videoPlayer == null || prop == null || !prop.CanWrite) return;
            try
            {
                object val;
                try { val = Enum.Parse(prop.PropertyType, enumName, false); }
                catch { val = Enum.ToObject(prop.PropertyType, fallbackInt); }
                prop.SetValue(_videoPlayer, val, null);
            }
            catch
            {
            }
        }

        private bool GetVpBool(PropertyInfo prop)
        {
            if (_videoPlayer == null || prop == null || !prop.CanRead) return false;
            try
            {
                object v = prop.GetValue(_videoPlayer, null);
                return v is bool b && b;
            }
            catch { return false; }
        }

        private int GetVpInt(PropertyInfo prop)
        {
            if (_videoPlayer == null || prop == null || !prop.CanRead) return 0;
            try
            {
                object v = prop.GetValue(_videoPlayer, null);
                return v != null ? Convert.ToInt32(v) : 0;
            }
            catch { return 0; }
        }

        private double GetVpDouble(PropertyInfo prop)
        {
            if (_videoPlayer == null || prop == null || !prop.CanRead) return 0.0;
            try
            {
                object v = prop.GetValue(_videoPlayer, null);
                return v != null ? Convert.ToDouble(v) : 0.0;
            }
            catch { return 0.0; }
        }

        private void InvokeVp(MethodInfo method)
        {
            if (_videoPlayer == null || method == null) return;
            try { method.Invoke(_videoPlayer, null); } catch { }
        }
    }
}
