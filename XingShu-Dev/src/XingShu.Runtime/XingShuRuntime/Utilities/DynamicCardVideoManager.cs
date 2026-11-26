using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FairyGUI;
using UI;
using UnityEngine;
using UnityEngine.Video;
using XingShu.Runtime;
using XingShuRuntime.Core;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Native, DRM-free dynamic hero card / portrait video loader for Astral Party 3.3.0+.
    /// Resolves URLs such as UT_Hero_Card_306 against standard VUT_Hero_Card_306.{mp4,webm,mov,usm,bundle}
    /// files in &lt;GameRoot&gt;/xingshu_dynamic_cards (PC) or &lt;persistentDataPath&gt;/xingshu_dynamic_cards (Android),
    /// without patched game DLLs or third-party obfuscated payloads.
    /// </summary>
    internal sealed class DynamicCardVideoManager : MonoBehaviour
    {
        private const string FolderName = "xingshu_dynamic_cards";
        private const float RescanIntervalSeconds = 3.0f;
        private const float VideoPrepareTimeoutSeconds = 8.0f;

        private enum MediaKind
        {
            None = 0,
            Video = 1,
            CriUsm = 2,
            CriBundle = 3,
        }

        private sealed class LoaderBinding
        {
            public GLoader Loader;
            public string Url;
            public string FilePath;
            public MediaKind Kind;
            public bool Failed;
            public bool HasBeenOnStage;
            public float CreatedAt;

            // UnityEngine.VideoPlayer state
            public GameObject VideoGo;
            public VideoPlayer VideoPlayer;
            public RenderTexture RenderTex;
            public NTexture TargetNTexture;
            public bool VideoPreparing;
            public bool VideoStarted;
            public float VideoStartTime;

            // CriWare sibling GGraph state (.usm / CriManaUsmAsset .bundle)
            public GGraph CriGraph;
            public Component CriController;
            public object CriPlayer;
            public Material CriMaterial;
            public AssetBundle CriBundle;
            public Texture2D PlaceholderTex;
            public NTexture PlaceholderNTexture;
            public float LastWidth;
            public float LastHeight;
            public bool CriStarted;
        }

        private static DynamicCardVideoManager _instance;
        private static readonly object IndexSync = new object();
        private static readonly Dictionary<string, string> FileIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoggedEncryptedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _indexInitialized;
        private static string _indexedDir = "";
        private static long _indexedDirWriteTicks = -1;
        private static long _lastStatCheckUtcTicks;

        private readonly List<LoaderBinding> _bindings = new List<LoaderBinding>();

        // Cached reflection handles (IL2CPP-safe)
        private static bool _reflInit;
        private static FieldInfo _criMaterialField;
        private static Type _criCtrlType;
        private static Type _criTargetType;
        private static Type _criUsmAssetType;
        private static PropertyInfo _criCtrlMaterialProp;
        private static PropertyInfo _criCtrlTargetProp;
        private static PropertyInfo _criCtrlMaxFrameDropProp;
        private static FieldInfo _criCtrlRenderModeField;
        private static MethodInfo _criCtrlManualSetupMethod;
        private static MethodInfo _criCtrlSetAssetMethod;
        private static PropertyInfo _criPlayerProp;
        private static MethodInfo _criPlayerStopMethod;
        private static MethodInfo _criPlayerStartMethod;
        private static MethodInfo _criPlayerPauseMethod;
        private static MethodInfo _criPlayerLoopMethod;
        private static MethodInfo _criPlayerSetVolumeMethod;
        private static MethodInfo _criPlayerSetFileMethod;
        private static MethodInfo _criPlayerManualInitMethod;
        private static PropertyInfo _vpIsLoopingProp;
        private static PropertyInfo _vpAudioModeProp;
        private static MethodInfo _vpSetMuteMethod;

        internal static void InvalidateCache()
        {
            lock (IndexSync)
            {
                _indexInitialized = false;
                _indexedDir = "";
                _indexedDirWriteTicks = -1;
                _lastStatCheckUtcTicks = 0;
                FileIndex.Clear();
            }
        }

        internal static void ClearAll()
        {
            if (_instance != null)
            {
                _instance.DisposeAllBindings();
            }
        }

        internal static void ClearLoader(GLoader loader)
        {
            if (loader == null || _instance == null)
            {
                return;
            }
            _instance.DisposeBindingForLoader(loader);
        }

        internal static NTexture TryResolve(GLoader loader, string url)
        {
            if (loader == null || string.IsNullOrEmpty(url))
            {
                return null;
            }
            if (url.StartsWith("ui://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                ClearLoader(loader);
                return null;
            }

            try
            {
                if (!LauncherModules.CanLoad("StaticPortraitGUI") || !StaticPortraitGUI.IsEnabled)
                {
                    ClearLoader(loader);
                    return null;
                }

                string stem = ExtractUrlStem(url);
                if (string.IsNullOrEmpty(stem))
                {
                    ClearLoader(loader);
                    return null;
                }

                // Never override non-card generic UI if no matching dynamic card file exists.
                string dir = ResolveDynamicCardsDirectory();
                if (string.IsNullOrEmpty(dir))
                {
                    ClearLoader(loader);
                    return null;
                }

                string filePath;
                MediaKind kind;
                lock (IndexSync)
                {
                    EnsureIndexLocked(dir);
                    if (!TryLookupDynamicFileLocked(stem, out filePath, out kind))
                    {
                        ClearLoader(loader);
                        return null;
                    }
                }

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    ClearLoader(loader);
                    return null;
                }

                if (ContainsEncryptedSignature(filePath, kind))
                {
                    LogEncryptedSkipOnce(filePath);
                    ClearLoader(loader);
                    return null;
                }

                DynamicCardVideoManager mgr = EnsureManager();
                if (mgr == null)
                {
                    ClearLoader(loader);
                    return null;
                }

                return mgr.AttachBinding(loader, url, stem, filePath, kind);
            }
            catch (Exception ex)
            {
                RuntimeCompatibilityHost.LogLifecycle("[DynamicCardVideoManager.TryResolve] " + ex.Message);
                ClearLoader(loader);
                return null;
            }
        }

        internal static string ResolveDynamicCardsDirectory()
        {
            try
            {
                string customBase = XingShuConfigService.GetString(
                    XingShuConfigKeys.StaticPortraitBasePath,
                    XingShuConfigKeys.StaticPortraitBasePathDefault
                );
                if (!string.IsNullOrEmpty(customBase) && !string.IsNullOrEmpty(customBase.Trim()))
                {
                    string customCandidate = Path.Combine(customBase.Trim(), FolderName).Replace('\\', '/');
                    if (Directory.Exists(customCandidate))
                    {
                        return customCandidate;
                    }
                }

                if (XingShuMobileUIHelper.IsMobile)
                {
                    string persistent = Application.persistentDataPath ?? "";
                    if (!string.IsNullOrEmpty(persistent))
                    {
                        return Path.Combine(persistent, FolderName).Replace('\\', '/');
                    }
                }
                else
                {
                    string dataPath = Application.dataPath ?? "";
                    string parentDir = !string.IsNullOrEmpty(dataPath)
                        ? Path.GetDirectoryName(dataPath.TrimEnd('/', '\\'))
                        : "";
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        return Path.Combine(parentDir, FolderName).Replace('\\', '/');
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        private static DynamicCardVideoManager EnsureManager()
        {
            if (_instance != null)
            {
                return _instance;
            }
            GameObject host = XingShuRuntimeHost.EnsureHost();
            if (host == null)
            {
                return null;
            }
            DynamicCardVideoManager existing = host.GetComponent<DynamicCardVideoManager>();
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }
            return host.AddComponent<DynamicCardVideoManager>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            InitReflection();
        }

        private void OnDestroy()
        {
            DisposeAllBindings();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void LateUpdate()
        {
            if (_bindings.Count == 0)
            {
                return;
            }

            if (!LauncherModules.CanLoad("StaticPortraitGUI") || !StaticPortraitGUI.IsEnabled)
            {
                DisposeAllBindings();
                return;
            }

            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                LoaderBinding b = _bindings[i];
                if (!IsBindingValid(b))
                {
                    DisposeBinding(b);
                    _bindings.RemoveAt(i);
                    continue;
                }

                if (b.Kind == MediaKind.Video)
                {
                    TickVideoBinding(b);
                }
                else if (b.Kind == MediaKind.CriUsm || b.Kind == MediaKind.CriBundle)
                {
                    TickCriBinding(b);
                }
            }
        }

        private static bool IsBindingValid(LoaderBinding b)
        {
            if (b == null || b.Failed || b.Loader == null || b.Loader.isDisposed)
            {
                return false;
            }
            if (!string.Equals(b.Loader.url, b.Url, StringComparison.Ordinal))
            {
                return false;
            }
            if (b.Loader.image != null && b.Loader.image.cachedTransform == null)
            {
                return false;
            }
            return true;
        }

        private NTexture AttachBinding(GLoader loader, string url, string stem, string filePath, MediaKind kind)
        {
            DisposeBindingForLoader(loader);

            LoaderBinding b = new LoaderBinding
            {
                Loader = loader,
                Url = url,
                FilePath = filePath,
                Kind = kind,
                HasBeenOnStage = loader.onStage,
                CreatedAt = Time.unscaledTime,
            };

            if (kind == MediaKind.Video)
            {
                if (!StartVideoBinding(b))
                {
                    DisposeBinding(b);
                    return null;
                }
                _bindings.Add(b);
                return b.TargetNTexture;
            }

            if (kind == MediaKind.CriUsm || kind == MediaKind.CriBundle)
            {
                if (!StartCriPlaceholder(b))
                {
                    DisposeBinding(b);
                    return null;
                }
                _bindings.Add(b);
                // If parent is already ready, try attaching CriWare sibling immediately; otherwise LateUpdate picks it up.
                TryAttachAndStartCri(b, stem);
                if (b.Failed)
                {
                    _bindings.Remove(b);
                    DisposeBinding(b);
                    return null;
                }
                return b.PlaceholderNTexture;
            }

            return null;
        }

        private bool StartVideoBinding(LoaderBinding b)
        {
            try
            {
                RenderTexture rt = new RenderTexture(512, 768, 0, RenderTextureFormat.ARGB32);
                rt.name = b.Url;
                rt.Create();

                GameObject go = new GameObject("[XingShu] DynamicCard_" + Path.GetFileNameWithoutExtension(b.FilePath));
                go.transform.SetParent(transform, false);
                VideoPlayer vp = go.AddComponent<VideoPlayer>();
                vp.playOnAwake = false;
                vp.source = VideoSource.Url;
                vp.url = CustomLobbyBgPolicy.BuildFileUri(b.FilePath);
                vp.renderMode = VideoRenderMode.RenderTexture;
                vp.aspectRatio = VideoAspectRatio.NoScaling;
                vp.targetTexture = rt;
                ConfigureVideoPlayer(vp);

                NTexture nt = new NTexture(rt);
                nt.destroyMethod = DestroyMethod.None;
                nt.AddRef();

                b.VideoGo = go;
                b.VideoPlayer = vp;
                b.RenderTex = rt;
                b.TargetNTexture = nt;
                b.VideoPreparing = true;
                b.VideoStarted = false;
                b.VideoStartTime = Time.unscaledTime;

                vp.Prepare();
                return true;
            }
            catch (Exception ex)
            {
                RuntimeCompatibilityHost.LogLifecycle("[DynamicCardVideoManager.StartVideo] " + ex.Message);
                return false;
            }
        }

        private void TickVideoBinding(LoaderBinding b)
        {
            if (b.VideoPlayer == null)
            {
                b.Failed = true;
                return;
            }

            if (b.Loader.onStage)
            {
                b.HasBeenOnStage = true;
            }
            else if (b.HasBeenOnStage || (Time.unscaledTime - b.CreatedAt > 5.0f))
            {
                DisposeBindingForLoader(b.Loader);
                return;
            }

            if (b.VideoPreparing)
            {
                int vw = 0;
                int vh = 0;
                try
                {
                    vw = (int)b.VideoPlayer.width;
                    vh = (int)b.VideoPlayer.height;
                }
                catch
                {
                }

                if (vw > 0 && vh > 0)
                {
                    b.VideoPreparing = false;
                    ComputeCardRenderTextureSize(vw, vh, XingShuMobileUIHelper.IsMobile, out int rtW, out int rtH);
                    if (b.RenderTex == null || b.RenderTex.width != rtW || b.RenderTex.height != rtH)
                    {
                        RenderTexture oldRt = b.RenderTex;
                        RenderTexture newRt = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
                        newRt.name = b.Url;
                        newRt.Create();
                        b.VideoPlayer.targetTexture = newRt;
                        b.RenderTex = newRt;
                        if (b.TargetNTexture != null)
                        {
                            b.TargetNTexture.Reload(newRt, null);
                        }
                        if (oldRt != null)
                        {
                            try
                            {
                                oldRt.Release();
                                Destroy(oldRt);
                            }
                            catch
                            {
                            }
                        }
                    }

                    ConfigureVideoPlayer(b.VideoPlayer);
                    b.VideoPlayer.Play();
                    b.VideoStarted = true;
                    b.VideoStartTime = Time.unscaledTime;
                    return;
                }

                if (Time.unscaledTime - b.VideoStartTime > VideoPrepareTimeoutSeconds)
                {
                    RuntimeCompatibilityHost.LogLifecycle("[DynamicCardVideoManager] 视频解码超时: " + b.FilePath);
                    b.Failed = true;
                }
                return;
            }

            if (b.VideoStarted)
            {
                bool visible = b.Loader.visible;
                try
                {
                    bool playing = b.VideoPlayer.isPlaying;
                    bool paused = b.VideoPlayer.isPaused;
                    if (!visible)
                    {
                        if (playing) b.VideoPlayer.Pause();
                        return;
                    }
                    if (paused)
                    {
                        b.VideoPlayer.Play();
                        b.VideoStartTime = Time.unscaledTime;
                        return;
                    }
                    if (!playing && (Time.unscaledTime - b.VideoStartTime > 0.35f))
                    {
                        b.VideoPlayer.time = 0.0;
                        b.VideoPlayer.Play();
                        b.VideoStartTime = Time.unscaledTime;
                    }
                }
                catch
                {
                }
            }
        }

        private bool StartCriPlaceholder(LoaderBinding b)
        {
            if (_criCtrlType == null || _criPlayerProp == null)
            {
                return false;
            }
            try
            {
                Texture2D placeholder = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                placeholder.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
                placeholder.Apply(false, false);
                placeholder.name = b.Url;

                NTexture nt = new NTexture(placeholder);
                nt.destroyMethod = DestroyMethod.None;
                nt.AddRef();

                b.PlaceholderTex = placeholder;
                b.PlaceholderNTexture = nt;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TickCriBinding(LoaderBinding b)
        {
            if (!b.CriStarted)
            {
                TryAttachAndStartCri(b, ExtractUrlStem(b.Url));
                return;
            }

            if (b.CriGraph == null || b.CriGraph.isDisposed || b.Loader.parent == null || b.Loader.parent.isDisposed)
            {
                b.Failed = true;
                return;
            }

            if (b.Loader.onStage)
            {
                b.HasBeenOnStage = true;
            }
            else if (b.HasBeenOnStage || (Time.unscaledTime - b.CreatedAt > 5.0f))
            {
                DisposeBindingForLoader(b.Loader);
                return;
            }

            SyncSiblingGraphTransform(b);
            if (b.CriPlayer != null)
            {
                bool shouldShow = b.Loader.visible && b.Loader.onStage;
                try
                {
                    _criPlayerPauseMethod?.Invoke(b.CriPlayer, new object[] { !shouldShow });
                }
                catch
                {
                }
            }
        }

        private void TryAttachAndStartCri(LoaderBinding b, string stem)
        {
            if (b.CriStarted || b.Failed)
            {
                return;
            }
            if (b.Loader.parent == null || b.Loader.parent.isDisposed)
            {
                return;
            }

            try
            {
                GComponent parent = b.Loader.parent;
                int loaderIndex = parent.GetChildIndex(b.Loader);
                if (loaderIndex < 0)
                {
                    return;
                }

                float w = Mathf.Max(2f, b.Loader.width > 1f ? b.Loader.width : (b.Loader.sourceWidth > 1 ? b.Loader.sourceWidth : 512f));
                float h = Mathf.Max(2f, b.Loader.height > 1f ? b.Loader.height : (b.Loader.sourceHeight > 1 ? b.Loader.sourceHeight : 768f));

                GGraph graph = new GGraph
                {
                    name = "XingShu_DynamicCard_" + stem,
                    touchable = false,
                    visible = b.Loader.visible,
                };
                parent.AddChildAt(graph, Mathf.Min(parent.numChildren, loaderIndex + 1));
                graph.DrawRect(w, h, 0, Color.clear, Color.white);
                b.CriGraph = graph;
                b.LastWidth = w;
                b.LastHeight = h;
                SyncSiblingGraphTransform(b);

                GameObject go = graph.shape != null ? graph.shape.gameObject : null;
                if (go == null)
                {
                    b.Failed = true;
                    return;
                }

                Component ctrl = go.AddComponent(_criCtrlType);
                if (ctrl == null)
                {
                    b.Failed = true;
                    return;
                }
                b.CriController = ctrl;

                object player = null;
                if (b.Kind == MediaKind.CriBundle)
                {
                    if (_criUsmAssetType == null || _criCtrlSetAssetMethod == null)
                    {
                        b.Failed = true;
                        return;
                    }
                    AssetBundle ab = AssetBundle.LoadFromFile(b.FilePath);
                    if (ab == null)
                    {
                        b.Failed = true;
                        return;
                    }
                    b.CriBundle = ab;

                    UnityEngine.Object[] assets = ab.LoadAllAssets();
                    UnityEngine.Object usmAsset = null;
                    if (assets != null)
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
                    if (usmAsset == null || IsEncryptedCriAssetObject(usmAsset))
                    {
                        if (usmAsset != null)
                        {
                            LogEncryptedSkipOnce(b.FilePath);
                        }
                        b.Failed = true;
                        return;
                    }

                    _criCtrlSetAssetMethod.Invoke(ctrl, new object[] { "V" + stem, usmAsset });
                    player = _criPlayerProp.GetValue(ctrl, null);
                }
                else
                {
                    player = _criPlayerProp.GetValue(ctrl, null);
                    if (player == null)
                    {
                        _criPlayerManualInitMethod?.Invoke(ctrl, null);
                        player = _criPlayerProp.GetValue(ctrl, null);
                    }
                    if (!InvokeCriSetFile(player, b.FilePath))
                    {
                        b.Failed = true;
                        return;
                    }
                }

                if (player == null)
                {
                    b.Failed = true;
                    return;
                }
                b.CriPlayer = player;

                Material baseMat = _criMaterialField != null ? _criMaterialField.GetValue(null) as Material : null;
                if (baseMat == null)
                {
                    Shader shader = Shader.Find("CriMana/SofdecPrimeYuv");
                    if (shader != null)
                    {
                        baseMat = new Material(shader);
                    }
                }
                if (baseMat != null && _criCtrlMaterialProp != null)
                {
                    Material matInstance = new Material(baseMat);
                    b.CriMaterial = matInstance;
                    _criCtrlMaterialProp.SetValue(ctrl, matInstance, null);
                }

                MeshRenderer mr = graph.shape != null && graph.shape.graphics != null
                    ? graph.shape.graphics.meshRenderer
                    : null;
                if (mr != null && _criTargetType != null && _criCtrlTargetProp != null)
                {
                    object target = Activator.CreateInstance(_criTargetType, new object[] { mr });
                    _criCtrlTargetProp.SetValue(ctrl, target, null);
                }

                _criCtrlManualSetupMethod?.Invoke(ctrl, null);
                if (graph.shape != null && graph.shape.graphics != null && b.CriMaterial != null)
                {
                    graph.shape.graphics.material = b.CriMaterial;
                }
                if (_criCtrlRenderModeField != null)
                {
                    object always = Enum.ToObject(_criCtrlRenderModeField.FieldType, 0);
                    _criCtrlRenderModeField.SetValue(ctrl, always);
                }
                if (_criCtrlMaxFrameDropProp != null)
                {
                    object two = Enum.ToObject(_criCtrlMaxFrameDropProp.PropertyType, 2);
                    _criCtrlMaxFrameDropProp.SetValue(ctrl, two, null);
                }
                if (mr != null)
                {
                    mr.enabled = true;
                }

                graph.color = Color.white;
                graph.visible = b.Loader.visible;
                _criPlayerLoopMethod?.Invoke(player, new object[] { true });
                _criPlayerSetVolumeMethod?.Invoke(player, new object[] { 0f });
                _criPlayerPauseMethod?.Invoke(player, new object[] { false });
                _criPlayerStartMethod?.Invoke(player, null);
                b.CriStarted = true;
            }
            catch (Exception ex)
            {
                RuntimeCompatibilityHost.LogLifecycle("[DynamicCardVideoManager.TryAttachCri] " + ex.Message);
                b.Failed = true;
            }
        }

        private static void SyncSiblingGraphTransform(LoaderBinding b)
        {
            if (b.CriGraph == null || b.CriGraph.isDisposed || b.Loader == null || b.Loader.isDisposed)
            {
                return;
            }
            GLoader l = b.Loader;
            GGraph g = b.CriGraph;
            g.pivotAsAnchor = l.pivotAsAnchor;
            g.SetPivot(l.pivotX, l.pivotY, l.pivotAsAnchor);
            g.SetXY(l.x, l.y);
            float w = Mathf.Max(2f, l.width > 1f ? l.width : b.LastWidth);
            float h = Mathf.Max(2f, l.height > 1f ? l.height : b.LastHeight);
            if (Mathf.Abs(w - b.LastWidth) > 0.5f || Mathf.Abs(h - b.LastHeight) > 0.5f)
            {
                b.LastWidth = w;
                b.LastHeight = h;
                g.DrawRect(w, h, 0, Color.clear, Color.white);
                if (g.shape != null && g.shape.graphics != null && b.CriMaterial != null)
                {
                    g.shape.graphics.material = b.CriMaterial;
                    if (g.shape.graphics.meshRenderer != null)
                    {
                        g.shape.graphics.meshRenderer.enabled = true;
                    }
                }
            }
            g.SetScale(l.scaleX, l.scaleY);
            g.rotation = l.rotation;
            g.alpha = l.alpha;
            g.sortingOrder = l.sortingOrder;
            g.visible = l.visible && l.onStage;
        }

        private void DisposeBindingForLoader(GLoader loader)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (object.ReferenceEquals(_bindings[i].Loader, loader))
                {
                    DisposeBinding(_bindings[i]);
                    _bindings.RemoveAt(i);
                }
            }
        }

        private void DisposeAllBindings()
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                DisposeBinding(_bindings[i]);
            }
            _bindings.Clear();
        }

        private static void DisposeBinding(LoaderBinding b)
        {
            if (b == null) return;

            if (b.VideoPlayer != null)
            {
                try
                {
                    b.VideoPlayer.Stop();
                    b.VideoPlayer.targetTexture = null;
                }
                catch { }
            }
            if (b.VideoGo != null)
            {
                try { Destroy(b.VideoGo); } catch { }
                b.VideoGo = null;
            }
            if (b.TargetNTexture != null)
            {
                try
                {
                    b.TargetNTexture.ReleaseRef();
                    b.TargetNTexture.Dispose();
                }
                catch { }
                b.TargetNTexture = null;
            }
            if (b.RenderTex != null)
            {
                try
                {
                    b.RenderTex.Release();
                    Destroy(b.RenderTex);
                }
                catch { }
                b.RenderTex = null;
            }
            if (b.CriPlayer != null)
            {
                try { _criPlayerStopMethod?.Invoke(b.CriPlayer, null); } catch { }
                b.CriPlayer = null;
            }
            if (b.CriGraph != null)
            {
                try
                {
                    if (!b.CriGraph.isDisposed) b.CriGraph.Dispose();
                }
                catch { }
                b.CriGraph = null;
            }
            if (b.CriMaterial != null)
            {
                try { Destroy(b.CriMaterial); } catch { }
                b.CriMaterial = null;
            }
            if (b.CriBundle != null)
            {
                try { b.CriBundle.Unload(true); } catch { }
                b.CriBundle = null;
            }
            if (b.PlaceholderNTexture != null)
            {
                try
                {
                    b.PlaceholderNTexture.ReleaseRef();
                    b.PlaceholderNTexture.Dispose();
                }
                catch { }
                b.PlaceholderNTexture = null;
            }
            if (b.PlaceholderTex != null)
            {
                try { Destroy(b.PlaceholderTex); } catch { }
                b.PlaceholderTex = null;
            }
        }

        private static void ConfigureVideoPlayer(VideoPlayer vp)
        {
            try
            {
                if (_vpIsLoopingProp != null) _vpIsLoopingProp.SetValue(vp, true, null);
            }
            catch { }
            try
            {
                if (_vpAudioModeProp != null)
                {
                    object directMode = Enum.ToObject(_vpAudioModeProp.PropertyType, 1);
                    _vpAudioModeProp.SetValue(vp, directMode, null);
                }
                _vpSetMuteMethod?.Invoke(vp, new object[] { (ushort)0, true });
            }
            catch { }
        }

        private static void ComputeCardRenderTextureSize(int srcW, int srcH, bool isMobile, out int outW, out int outH)
        {
            if (srcW <= 0 || srcH <= 0)
            {
                outW = isMobile ? 512 : 768;
                outH = isMobile ? 768 : 1152;
                return;
            }
            int maxDim = isMobile ? 720 : 1280;
            double w = srcW;
            double h = srcH;
            if (w > maxDim || h > maxDim)
            {
                double scale = Math.Min((double)maxDim / w, (double)maxDim / h);
                w *= scale;
                h *= scale;
            }
            outW = ((int)Math.Round(w)) & ~1;
            outH = ((int)Math.Round(h)) & ~1;
            if (outW < 2) outW = 2;
            if (outH < 2) outH = 2;
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
                object setMode = Enum.ToObject(ps[2].ParameterType, 0);
                _criPlayerSetFileMethod.Invoke(player, new object[] { null, filePath, setMode });
                return true;
            }
            return false;
        }

        private static void EnsureIndexLocked(string dir)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            bool dirChanged = !string.Equals(_indexedDir, dir, StringComparison.Ordinal);
            if (_indexInitialized && !dirChanged && (nowTicks - _lastStatCheckUtcTicks) < TimeSpan.FromSeconds(RescanIntervalSeconds).Ticks)
            {
                return;
            }
            _lastStatCheckUtcTicks = nowTicks;

            bool exists = Directory.Exists(dir);
            long writeTicks = exists ? Directory.GetLastWriteTimeUtc(dir).Ticks : -1;
            if (_indexInitialized && !dirChanged && writeTicks == _indexedDirWriteTicks)
            {
                return;
            }

            _indexedDir = dir;
            _indexedDirWriteTicks = writeTicks;
            _indexInitialized = true;
            FileIndex.Clear();

            if (!exists) return;

            foreach (string path in Directory.GetFiles(dir))
            {
                MediaKind kind = ClassifyCandidateFile(path);
                if (kind == MediaKind.None) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(stem)) continue;

                // Prioritize MP4/WEBM/MOV > USM > Bundle when multiple formats exist for the same key.
                if (!FileIndex.TryGetValue(stem, out string prev) || GetPriority(kind) > GetPriority(ClassifyCandidateFile(prev)))
                {
                    FileIndex[stem] = path;
                }
            }
        }

        private static int GetPriority(MediaKind k)
        {
            switch (k)
            {
                case MediaKind.Video: return 3;
                case MediaKind.CriUsm: return 2;
                case MediaKind.CriBundle: return 1;
                default: return 0;
            }
        }

        private static bool TryLookupDynamicFileLocked(string urlStem, out string path, out MediaKind kind)
        {
            path = null;
            kind = MediaKind.None;
            if (FileIndex.Count == 0 || string.IsNullOrEmpty(urlStem))
            {
                return false;
            }

            string candidateKey = "V" + urlStem;
            if (FileIndex.TryGetValue(candidateKey, out path) || FileIndex.TryGetValue(urlStem, out path))
            {
                kind = ClassifyCandidateFile(path);
                return kind != MediaKind.None;
            }

            if (urlStem.EndsWith("_sfw", StringComparison.OrdinalIgnoreCase))
            {
                string cleanStem = urlStem.Substring(0, urlStem.Length - 4);
                string cleanKey = "V" + cleanStem;
                if (FileIndex.TryGetValue(cleanKey, out path) || FileIndex.TryGetValue(cleanStem, out path))
                {
                    kind = ClassifyCandidateFile(path);
                    return kind != MediaKind.None;
                }
            }
            return false;
        }

        private static MediaKind ClassifyCandidateFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return MediaKind.None;
            string ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
            {
                string lower = ext.ToLowerInvariant();
                if (lower == ".mp4" || lower == ".webm" || lower == ".mov" || lower == ".m4v")
                {
                    return MediaKind.Video;
                }
                if (lower == ".usm")
                {
                    return MediaKind.CriUsm;
                }
                if (lower == ".bundle")
                {
                    return MediaKind.CriBundle;
                }
                return MediaKind.None;
            }

            // Extensionless files (e.g. copied from SkinVideoMod data/VUT_Hero_Card_306)
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] header = new byte[8];
                    int read = fs.Read(header, 0, header.Length);
                    if (read >= 7 && header[0] == 'U' && header[1] == 'n' && header[2] == 'i' && header[3] == 't' && header[4] == 'y' && header[5] == 'F' && header[6] == 'S')
                    {
                        return MediaKind.CriBundle;
                    }
                    if (read >= 4 && header[0] == 'C' && header[1] == 'R' && header[2] == 'I' && header[3] == 'D')
                    {
                        return MediaKind.CriUsm;
                    }
                }
            }
            catch
            {
            }
            return MediaKind.None;
        }

        private static bool ContainsEncryptedSignature(string path, MediaKind kind)
        {
            if (kind != MediaKind.CriUsm && kind != MediaKind.CriBundle)
            {
                return false;
            }
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] probe = new byte[Math.Min(65536, (int)Math.Min(fs.Length, 65536L))];
                    int read = fs.Read(probe, 0, probe.Length);
                    if (ContainsAscii(probe, read, "APENC01"))
                    {
                        return true;
                    }
                    if (kind == MediaKind.CriUsm && (read < 4 || probe[0] != 'C' || probe[1] != 'R' || probe[2] != 'I' || probe[3] != 'D'))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static bool IsEncryptedCriAssetObject(UnityEngine.Object assetObj)
        {
            if (assetObj == null) return true;
            try
            {
                Type t = assetObj.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                PropertyInfo implProp = t.GetProperty("Implementation", flags);
                object impl = implProp != null ? implProp.GetValue(assetObj, null) : null;
                object target = impl ?? assetObj;
                foreach (FieldInfo fi in target.GetType().GetFields(flags))
                {
                    if (fi.FieldType == typeof(byte[]))
                    {
                        byte[] arr = fi.GetValue(target) as byte[];
                        if (arr != null && arr.Length >= 7 && ContainsAscii(arr, Math.Min(arr.Length, 1024), "APENC01"))
                        {
                            return true;
                        }
                    }
                }
                foreach (PropertyInfo pi in target.GetType().GetProperties(flags))
                {
                    if (pi.PropertyType == typeof(byte[]) && pi.GetIndexParameters().Length == 0)
                    {
                        byte[] arr = pi.GetValue(target, null) as byte[];
                        if (arr != null && arr.Length >= 7 && ContainsAscii(arr, Math.Min(arr.Length, 1024), "APENC01"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool ContainsAscii(byte[] buf, int len, string needle)
        {
            if (buf == null || string.IsNullOrEmpty(needle) || len < needle.Length) return false;
            byte[] pat = Encoding.ASCII.GetBytes(needle);
            int limit = len - pat.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pat.Length; j++)
                {
                    if (buf[i + j] != pat[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        private static void LogEncryptedSkipOnce(string path)
        {
            lock (LoggedEncryptedPaths)
            {
                if (!LoggedEncryptedPaths.Add(path))
                {
                    return;
                }
            }
            string msg = "[XingShu DynamicCard] 跳过受第三方加密保护的 APENC01 文件: " + Path.GetFileName(path);
            Debug.LogWarning(msg);
            RuntimeCompatibilityHost.LogLifecycle(msg);
        }

        private static string ExtractUrlStem(string url)
        {
            string clean = url.Replace('\\', '/');
            int queryIdx = clean.IndexOfAny(new[] { '?', '#' });
            if (queryIdx >= 0) clean = clean.Substring(0, queryIdx);
            int slashIdx = clean.LastIndexOf('/');
            if (slashIdx >= 0) clean = clean.Substring(slashIdx + 1);
            string stem = Path.GetFileNameWithoutExtension(clean);
            return string.IsNullOrEmpty(stem) ? clean : stem;
        }

        private static void InitReflection()
        {
            if (_reflInit) return;
            _reflInit = true;

            const BindingFlags anyInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            const BindingFlags anyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            Type vpType = typeof(VideoPlayer);
            _vpIsLoopingProp = vpType.GetProperty("isLooping", anyInst);
            _vpAudioModeProp = vpType.GetProperty("audioOutputMode", anyInst);
            _vpSetMuteMethod = vpType.GetMethod("SetDirectAudioMute", anyInst);

            try
            {
                Assembly gameAsm = typeof(HomePanel).Assembly;
                Type movieMgrType = gameAsm.GetType("UI.CriMovieManager", false, false);
                if (movieMgrType != null)
                {
                    _criMaterialField = movieMgrType.GetField("CriMaterial", anyStatic);
                }

                Type ctrlType = gameAsm.GetType("UI.CriManaMovieControllerForGGraph", false, false);
                _criCtrlType = ctrlType;
                _criTargetType = gameAsm.GetType("UI.FManaMovieMaterialFGUIGraphTarget", false, false);
                if (ctrlType != null)
                {
                    _criCtrlSetAssetMethod = ctrlType.GetMethod("SetAsset", anyInst);
                    _criCtrlMaterialProp = ctrlType.GetProperty("material", anyInst);
                    _criCtrlTargetProp = ctrlType.GetProperty("Target", anyInst);
                    _criCtrlMaxFrameDropProp = ctrlType.GetProperty("maxFrameDrop", anyInst);
                    _criCtrlRenderModeField = ctrlType.GetField("renderMode", anyInst);
                    _criCtrlManualSetupMethod = ctrlType.GetMethod("PlayerManualSetup", anyInst);
                    _criPlayerManualInitMethod = ctrlType.GetMethod("PlayerManualInitialize", anyInst);
                    _criPlayerProp = ctrlType.GetProperty("player", anyInst);
                    if (_criPlayerProp != null)
                    {
                        Type pt = _criPlayerProp.PropertyType;
                        _criPlayerStopMethod = pt.GetMethod("Stop", anyInst);
                        _criPlayerStartMethod = pt.GetMethod("Start", anyInst);
                        _criPlayerPauseMethod = pt.GetMethod("Pause", anyInst);
                        _criPlayerLoopMethod = pt.GetMethod("Loop", anyInst);
                        _criPlayerSetVolumeMethod = pt.GetMethod("SetVolume", anyInst);
                        MethodInfo[] methods = pt.GetMethods(anyInst);
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
    }
}
