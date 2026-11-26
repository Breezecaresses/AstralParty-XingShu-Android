using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using FairyGUI;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace XingShuRuntime.UI
{
    internal enum XingShuNativeSprite
    {
        TabBg,
        TabPink,
        TabGold,
        TabSelected,
        TabDot,
        DarkStripes,
        DarkPattern,
        LightPattern,
        RoundedLightCard,
        RoundedDarkFrame,
        TitleTag,
        InputField,
        DropdownArrow,
        ToggleOff,
        ToggleOn,
        SliderBg,
        SliderFill,
        SliderTrail,
        SliderStar,
        BtnSmall,
        BtnSmallOk,
        BtnLarge,
        BtnLargeOk,
        UidPill,
        ReturnBg,
        ReturnBtn,
        StarBuff
    }

    /// <summary>
    /// 按需加载并提取游戏原版 FairyGUI "Setting" / "Common" 图集切片，供双端 UGUI 悬浮球与功能中心复用。
    /// 绝不修改或释放游戏自身拥有的 UIPackage 与底层图集 Texture。
    /// </summary>
    internal static class XingShuNativeGameUI
    {
        private struct SpriteSpec
        {
            internal readonly string PackageName;
            internal readonly string ItemName;
            internal readonly bool WantTiled;
            internal SpriteSpec(string pkg, string item, bool tiled = false)
            {
                PackageName = pkg;
                ItemName = item;
                WantTiled = tiled;
            }
        }

        private struct PendingBinding
        {
            internal Image Target;
            internal XingShuNativeSprite Id;
            internal Image.Type Type;
            internal bool PreserveAspect;
            internal Color? ReadyColor;
            internal Action<Sprite> CustomCallback;
        }

        private static readonly Dictionary<XingShuNativeSprite, SpriteSpec> _specs = new Dictionary<XingShuNativeSprite, SpriteSpec>
        {
            { XingShuNativeSprite.TabBg,         new SpriteSpec("Setting", "页签-底") },
            { XingShuNativeSprite.TabPink,       new SpriteSpec("Setting", "页签-默认") },
            { XingShuNativeSprite.TabGold,       new SpriteSpec("Setting", "页签-黄") },
            { XingShuNativeSprite.TabSelected,   new SpriteSpec("Setting", "页签-选中") },
            { XingShuNativeSprite.TabDot,        new SpriteSpec("Setting", "页签-圆") },
            { XingShuNativeSprite.DarkStripes,   new SpriteSpec("Setting", "深底-条纹") },
            { XingShuNativeSprite.DarkPattern,   new SpriteSpec("Setting", "深底-花纹", tiled: true) },
            { XingShuNativeSprite.LightPattern,  new SpriteSpec("Setting", "浅底-花纹", tiled: true) },
            { XingShuNativeSprite.TitleTag,      new SpriteSpec("Setting", "标题框") },
            { XingShuNativeSprite.InputField,    new SpriteSpec("Setting", "输入框") },
            { XingShuNativeSprite.DropdownArrow, new SpriteSpec("Setting", "下拉箭头") },
            { XingShuNativeSprite.ToggleOff,     new SpriteSpec("Setting", "关") },
            { XingShuNativeSprite.ToggleOn,      new SpriteSpec("Setting", "开") },
            { XingShuNativeSprite.SliderBg,      new SpriteSpec("Setting", "音量条-底") },
            { XingShuNativeSprite.SliderFill,    new SpriteSpec("Setting", "音量条") },
            { XingShuNativeSprite.SliderTrail,   new SpriteSpec("Setting", "音量条-星星拖尾") },
            { XingShuNativeSprite.SliderStar,    new SpriteSpec("Setting", "音量条-星星") },
            { XingShuNativeSprite.BtnSmall,      new SpriteSpec("Setting", "小按钮") },
            { XingShuNativeSprite.BtnSmallOk,    new SpriteSpec("Setting", "小按钮-确认") },
            { XingShuNativeSprite.BtnLarge,      new SpriteSpec("Setting", "大按钮") },
            { XingShuNativeSprite.BtnLargeOk,    new SpriteSpec("Setting", "大按钮-确认") },
            { XingShuNativeSprite.UidPill,       new SpriteSpec("Setting", "UID") },
            { XingShuNativeSprite.ReturnBg,      new SpriteSpec("Common", "返回-底") },
            { XingShuNativeSprite.ReturnBtn,     new SpriteSpec("Common", "返回") },
        };

        private static readonly Dictionary<string, XingShuNativeSprite> _logicalMap = new Dictionary<string, XingShuNativeSprite>(StringComparer.OrdinalIgnoreCase)
        {
            { "sidebar_tab_normal.png",   XingShuNativeSprite.TabPink },
            { "sidebar_tab_selected.png", XingShuNativeSprite.TabGold },
            { "sidebar_tab_hover.png",    XingShuNativeSprite.TabSelected },
            { "button_primary.png",       XingShuNativeSprite.BtnLarge },
            { "button_selected.png",      XingShuNativeSprite.BtnSmallOk },
            { "slider_track.png",         XingShuNativeSprite.SliderBg },
            { "slider_fill.png",          XingShuNativeSprite.SliderFill },
            { "slider_handle.png",        XingShuNativeSprite.SliderStar },
            { "toggle_track_off.png",     XingShuNativeSprite.ToggleOff },
            { "toggle_track_on.png",      XingShuNativeSprite.ToggleOn },
            { "hotkey_badge.png",         XingShuNativeSprite.UidPill },
            { "card_base.png",            XingShuNativeSprite.RoundedLightCard },
            { "card_feature_header.png",  XingShuNativeSprite.RoundedLightCard },
            { "window_frame.png",         XingShuNativeSprite.RoundedDarkFrame },
        };

        private static readonly Dictionary<XingShuNativeSprite, Sprite> _cache = new Dictionary<XingShuNativeSprite, Sprite>();
        private static readonly List<Sprite> _ownedSprites = new List<Sprite>();
        private static readonly List<Texture2D> _ownedTextures = new List<Texture2D>();
        private static readonly List<PendingBinding> _pendingBindings = new List<PendingBinding>();
        private static readonly List<Action> _readyCallbacks = new List<Action>();
        private static readonly HashSet<string> _loggedItems = new HashSet<string>();

        private static bool _loading;
        private static bool _ready;
        private static bool _loggedSummary;
        private static float _retryAt;
        private static float _loadStartedAt;
        private static float _nextTickAt;
        private static float _nextHealthCheckAt;
        private static int _generation;

        private static readonly FieldInfo _pkgTexturesField = typeof(UIPackage).GetField("_textures", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool IsReady => _ready;

        private static bool IsRealAtlasTexture(Texture tex)
        {
            return tex != null
                && tex != Texture2D.whiteTexture
                && tex.width >= 32
                && tex.height >= 32
                && !string.Equals(tex.name, "White Texture", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ArePackageAtlasesLoaded(UIPackage pkg, out string atlasSummary)
        {
            atlasSummary = "";
            if (pkg == null) return false;
            List<PackageItem> items = pkg.GetItems();
            if (items == null || items.Count == 0) return false;

            Dictionary<string, Texture> textures = null;
            if (_pkgTexturesField != null)
            {
                try { textures = _pkgTexturesField.GetValue(pkg) as Dictionary<string, Texture>; }
                catch { textures = null; }
            }

            int atlasCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PackageItem item = items[i];
                if (item == null || item.type != PackageItemType.Atlas) continue;
                atlasCount++;

                string key = pkg.name + "_" + Path.GetFileNameWithoutExtension(item.file ?? string.Empty);
                Texture realTex = null;

                if (textures != null)
                {
                    if (!textures.TryGetValue(key, out realTex) || !IsRealAtlasTexture(realTex))
                    {
                        return false;
                    }
                    // Self-heal if another caller triggered GetItemAsset before async atlas load completed and poisoned item.texture with 1x1 "White Texture"
                    if (item.texture != null && !IsRealAtlasTexture(item.texture.nativeTexture))
                    {
                        try { item.texture.Reload(realTex, null); } catch { }
                    }
                }
                else
                {
                    if (item.texture == null || !IsRealAtlasTexture(item.texture.nativeTexture))
                    {
                        return false;
                    }
                    realTex = item.texture.nativeTexture;
                }

                if (atlasSummary.Length == 0 && realTex != null)
                {
                    atlasSummary = $"{realTex.width}x{realTex.height}";
                }
            }

            return atlasCount > 0;
        }

        private static bool IsCacheHealthy()
        {
            XingShuNativeSprite[] probe =
            {
                XingShuNativeSprite.TabPink,
                XingShuNativeSprite.TabGold,
                XingShuNativeSprite.TitleTag,
                XingShuNativeSprite.ToggleOff,
                XingShuNativeSprite.ToggleOn,
                XingShuNativeSprite.SliderFill,
                XingShuNativeSprite.SliderStar,
                XingShuNativeSprite.RoundedDarkFrame,
                XingShuNativeSprite.RoundedLightCard,
                XingShuNativeSprite.ReturnBtn
            };
            for (int i = 0; i < probe.Length; i++)
            {
                if (!_cache.TryGetValue(probe[i], out Sprite sp) || sp == null || sp.texture == null)
                {
                    return false;
                }
            }
            return true;
        }

        internal static void TickEnsureLoaded()
        {
            float now = Time.unscaledTime;
            if (_ready)
            {
                if (now < _nextHealthCheckAt) return;
                _nextHealthCheckAt = now + 2.0f;
                if (!IsCacheHealthy())
                {
                    XingShuUIAssetService.LogUIRuntime("[XingShu NativeGameUI WARNING] Cached native sprites invalidated after scene transition, re-extracting.");
                    _cache.Clear();
                    _ready = false;
                    _loggedSummary = false;
                    EnsureLoaded(null);
                }
                return;
            }

            if (now < _nextTickAt) return;
            _nextTickAt = now + 0.35f;
            EnsureLoaded(null);
        }

        internal static void EnsureLoaded(Action onReady)
        {
            if (onReady != null)
            {
                if (_ready)
                {
                    try { onReady(); } catch { }
                    return;
                }
                if (!_readyCallbacks.Contains(onReady))
                {
                    _readyCallbacks.Add(onReady);
                }
            }

            if (_ready) return;

            // 绝不抢先加载 "Common"！必须等游戏自身的 CommonUIManager.RegisterCommonPackage() 完成全部 38 个组件注册且图集全部加载完成，否则主大厅会崩溃或提取出 1x1 占位白块。
            UIPackage commonPkg = UIPackage.GetByName("Common");
            if (commonPkg == null || !ArePackageAtlasesLoaded(commonPkg, out _))
            {
                return;
            }

            // 先检查 _loading，绝不能在 AddPackageAsync("Setting") 尚在异步下载 Setting_atlas* 时提前触发 PopulateFromPackages。
            if (_loading && (Time.unscaledTime - _loadStartedAt) < 15f) return;

            UIPackage settingPkg = UIPackage.GetByName("Setting");
            if (settingPkg != null)
            {
                if (ArePackageAtlasesLoaded(settingPkg, out _))
                {
                    PopulateFromPackages(settingPkg, commonPkg);
                }
                return;
            }

            if (Time.unscaledTime < _retryAt) return;
            LoadSettingOnlyAsync(_generation).Forget();
        }

        private static async UniTaskVoid LoadSettingOnlyAsync(int generation)
        {
            _loading = true;
            _loadStartedAt = Time.unscaledTime;
            _retryAt = Time.unscaledTime + 3f;
            try
            {
                // 延迟一帧，确保 CommonUIManager.RegisterCommonPackage() 在 await AddPackageAsync("Common") 后的同步 Bind 语句已执行完毕
                await UniTask.Yield();
                if (generation != _generation) return;

                UIPackage commonPkg = UIPackage.GetByName("Common");
                if (commonPkg == null || !ArePackageAtlasesLoaded(commonPkg, out _)) return;

                UIPackage settingPkg = UIPackage.GetByName("Setting");
                if (settingPkg == null)
                {
                    settingPkg = await UIPackage.AddPackageAsync("Setting", string.Empty);
                }

                if (generation != _generation || settingPkg == null) return;

                // 若游戏自身恰好同步启动了 AddPackageAsync("Setting") 导致 AddPackageAsync 提前返回半成品包，等待图集纹理真正进入 _textures（最多约 3 秒）
                for (int attempt = 0; attempt < 30 && !ArePackageAtlasesLoaded(settingPkg, out _); attempt++)
                {
                    await UniTask.Delay(100, ignoreTimeScale: true);
                    if (generation != _generation) return;
                    settingPkg = UIPackage.GetByName("Setting");
                    if (settingPkg == null) return;
                }

                if (generation != _generation) return;

                UIPackage readyCommon = UIPackage.GetByName("Common") ?? commonPkg;
                if (settingPkg != null && ArePackageAtlasesLoaded(settingPkg, out _) && ArePackageAtlasesLoaded(readyCommon, out _))
                {
                    PopulateFromPackages(settingPkg, readyCommon);
                }
            }
            catch (Exception ex)
            {
                XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI WARNING] 加载原生 Setting 图包失败，3s 后重试: {ex.Message}");
            }
            finally
            {
                if (generation == _generation)
                {
                    _loading = false;
                }
            }
        }

        private static void PopulateFromPackages(UIPackage settingPkg, UIPackage commonPkg)
        {
            if (settingPkg == null || commonPkg == null) return;
            if (!ArePackageAtlasesLoaded(settingPkg, out string settingAtlasSize) || !ArePackageAtlasesLoaded(commonPkg, out string commonAtlasSize))
            {
                return;
            }

            int expectedSetting = 0;
            int expectedCommon = 0;
            int settingCount = 0;
            int commonCount = 0;

            foreach (var kv in _specs)
            {
                if (kv.Value.PackageName == "Setting") expectedSetting++; else expectedCommon++;
                XingShuNativeSprite id = kv.Key;
                if (_cache.TryGetValue(id, out Sprite existing) && existing != null && existing.texture != null)
                {
                    if (kv.Value.PackageName == "Setting") settingCount++; else commonCount++;
                    continue;
                }

                UIPackage pkg = kv.Value.PackageName == "Setting" ? settingPkg : commonPkg;
                if (pkg == null) continue;

                Sprite extracted = ExtractSprite(pkg, id, kv.Value.ItemName, kv.Value.WantTiled);
                if (extracted != null)
                {
                    _cache[id] = extracted;
                    if (kv.Value.PackageName == "Setting") settingCount++; else commonCount++;
                }
            }

            // 烘焙半透圆角浅纹卡片与深纹底框（按花纹 Alpha 混入实底色，防止透明区变为黑底）
            if ((!_cache.TryGetValue(XingShuNativeSprite.RoundedLightCard, out Sprite cachedLight) || cachedLight == null || cachedLight.texture == null) &&
                _cache.TryGetValue(XingShuNativeSprite.LightPattern, out Sprite lp) && lp != null && lp.texture != null)
            {
                Sprite roundedCard = BakeRoundedPatternCard(
                    lp.texture,
                    192,
                    192,
                    12f,
                    0.985f,
                    new Color(0.955f, 0.960f, 0.975f, 1f),
                    0.24f,
                    new Color(1f, 1f, 1f, 1f),
                    new Color(0.84f, 0.87f, 0.92f, 0.96f),
                    new Vector4(16f, 16f, 16f, 16f),
                    "XINGSHU_Native_RoundedLightCard");
                if (roundedCard != null) _cache[XingShuNativeSprite.RoundedLightCard] = roundedCard;
            }

            if ((!_cache.TryGetValue(XingShuNativeSprite.RoundedDarkFrame, out Sprite cachedDark) || cachedDark == null || cachedDark.texture == null) &&
                _cache.TryGetValue(XingShuNativeSprite.DarkPattern, out Sprite dp) && dp != null && dp.texture != null)
            {
                Sprite darkFrame = BakeRoundedPatternCard(
                    dp.texture,
                    256,
                    256,
                    20f,
                    0.95f,
                    new Color(0.15f, 0.16f, 0.20f, 1f),
                    0.40f,
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 1f, 1f, 0.14f),
                    new Vector4(24f, 24f, 24f, 24f),
                    "XINGSHU_Native_RoundedDarkFrame");
                if (darkFrame != null) _cache[XingShuNativeSprite.RoundedDarkFrame] = darkFrame;
            }

            bool allReady = settingCount >= expectedSetting
                && commonCount >= expectedCommon
                && _cache.TryGetValue(XingShuNativeSprite.RoundedLightCard, out Sprite rlc) && rlc != null && rlc.texture != null
                && _cache.TryGetValue(XingShuNativeSprite.RoundedDarkFrame, out Sprite rdf) && rdf != null && rdf.texture != null;

            if (allReady)
            {
                _ready = true;
                _nextHealthCheckAt = Time.unscaledTime + 2.0f;
                if (!_loggedSummary)
                {
                    _loggedSummary = true;
                    XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI] Ready: Setting={settingCount}/{expectedSetting}({settingAtlasSize}) Common={commonCount}/{expectedCommon}({commonAtlasSize}) RoundedCards=1");
                }
            }

            FlushBindings(allReady);
        }

        private static void FlushBindings(bool fireReadyCallbacks)
        {
            for (int i = _pendingBindings.Count - 1; i >= 0; i--)
            {
                PendingBinding b = _pendingBindings[i];
                if (_cache.TryGetValue(b.Id, out Sprite sp) && sp != null && sp.texture != null)
                {
                    ApplyBinding(b, sp);
                    _pendingBindings.RemoveAt(i);
                }
            }

            if (fireReadyCallbacks && _readyCallbacks.Count > 0)
            {
                var callbacks = new List<Action>(_readyCallbacks);
                _readyCallbacks.Clear();
                for (int i = 0; i < callbacks.Count; i++)
                {
                    try { callbacks[i]?.Invoke(); } catch { }
                }
            }
        }

        private static void ApplyBinding(PendingBinding b, Sprite sp)
        {
            if (b.CustomCallback != null)
            {
                try { b.CustomCallback(sp); } catch { }
                return;
            }
            if (b.Target == null) return;
            b.Target.sprite = sp;
            b.Target.preserveAspect = b.PreserveAspect;
            b.Target.type = b.Type == Image.Type.Sliced && sp.border == Vector4.zero ? Image.Type.Simple : b.Type;
            if (b.ReadyColor.HasValue)
            {
                b.Target.color = b.ReadyColor.Value;
            }
        }

        internal static void Bind(Image target, XingShuNativeSprite id, Image.Type imageType = Image.Type.Sliced, bool preserveAspect = false, Color? readyColor = null)
        {
            if (target == null) return;
            if (_cache.TryGetValue(id, out Sprite sp) && sp != null)
            {
                ApplyBinding(new PendingBinding
                {
                    Target = target,
                    Id = id,
                    Type = imageType,
                    PreserveAspect = preserveAspect,
                    ReadyColor = readyColor
                }, sp);
                return;
            }

            _pendingBindings.Add(new PendingBinding
            {
                Target = target,
                Id = id,
                Type = imageType,
                PreserveAspect = preserveAspect,
                ReadyColor = readyColor
            });
            EnsureLoaded(null);
        }

        internal static void BindCallback(XingShuNativeSprite id, Action<Sprite> callback)
        {
            if (callback == null) return;
            if (_cache.TryGetValue(id, out Sprite sp) && sp != null)
            {
                try { callback(sp); } catch { }
                return;
            }
            _pendingBindings.Add(new PendingBinding { Id = id, CustomCallback = callback });
            EnsureLoaded(null);
        }

        internal static Sprite GetIfReady(XingShuNativeSprite id)
        {
            return _cache.TryGetValue(id, out Sprite sp) && sp != null ? sp : null;
        }

        internal static void ClearPendingBindingsUnder(GameObject root)
        {
            if (root == null) return;
            Transform rootTf = root.transform;
            for (int i = _pendingBindings.Count - 1; i >= 0; i--)
            {
                Image target = _pendingBindings[i].Target;
                if (target != null && target.transform.IsChildOf(rootTf))
                {
                    _pendingBindings.RemoveAt(i);
                }
            }
        }

        internal static bool TryGetMappedSprite(string assetName, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(assetName)) return false;
            if (!_logicalMap.TryGetValue(assetName, out XingShuNativeSprite nativeId)) return false;

            EnsureLoaded(null);
            if (_cache.TryGetValue(nativeId, out Sprite cached) && cached != null)
            {
                sprite = cached;
                return true;
            }
            return false;
        }

        internal static void BindLogicalIfMapped(Image target, string assetName, Image.Type imageType = Image.Type.Sliced, bool preserveAspect = false, Color? readyColor = null)
        {
            if (target == null || string.IsNullOrEmpty(assetName)) return;
            if (_logicalMap.TryGetValue(assetName, out XingShuNativeSprite nativeId))
            {
                Bind(target, nativeId, imageType, preserveAspect, readyColor);
            }
        }

        internal static bool IsNativeAssetName(string assetName)
        {
            return !string.IsNullOrEmpty(assetName) && _logicalMap.ContainsKey(assetName);
        }

        private static Sprite ExtractSprite(UIPackage pkg, XingShuNativeSprite id, string itemName, bool wantTiled)
        {
            try
            {
                PackageItem item = pkg.GetItemByName(itemName);
                if (item == null)
                {
                    if (_loggedItems.Add(itemName))
                        XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI] Item not found: {pkg.name}/{itemName}");
                    return null;
                }

                NTexture nt = pkg.GetItemAsset(item) as NTexture ?? item.texture;
                if (nt == null || nt.disposed || nt.nativeTexture == null)
                {
                    return null;
                }

                Texture srcTex = nt.nativeTexture;
                if (!IsRealAtlasTexture(srcTex))
                {
                    if (_loggedItems.Add(itemName + "_placeholder"))
                        XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI WARNING] Rejected placeholder texture for {pkg.name}/{itemName}: {(srcTex == null ? "null" : $"{srcTex.name}({srcTex.width}x{srcTex.height})")}");
                    return null;
                }

                int atlasW = srcTex.width;
                int atlasH = srcTex.height;

                // 计算九宫格 [Left, Bottom, Right, Top]（FairyGUI 为左上原点）
                Vector4 border = Vector4.zero;
                if (item.scale9Grid.HasValue && item.width > 0 && item.height > 0)
                {
                    Rect s9 = item.scale9Grid.Value;
                    float left = Mathf.Clamp(s9.x, 0f, item.width - 1f);
                    float top = Mathf.Clamp(s9.y, 0f, item.height - 1f);
                    float right = Mathf.Clamp(item.width - s9.xMax, 0f, item.width - left - 1f);
                    float bottom = Mathf.Clamp(item.height - s9.yMax, 0f, item.height - top - 1f);
                    border = new Vector4(left, bottom, right, top);
                }

                // 物理 UV 宽高（FairyGUI 当 rotated == true 时交换过 uvRect.width / height）
                // 统一转为独立拷贝纹理：避免游戏切换场景卸载 Setting 图集后造成原贴图悬空变白块
                float physUvW = nt.rotated ? nt.uvRect.height : nt.uvRect.width;
                float physUvH = nt.rotated ? nt.uvRect.width : nt.uvRect.height;
                int physW = Mathf.Max(1, Mathf.RoundToInt(physUvW * atlasW));
                int physH = Mathf.Max(1, Mathf.RoundToInt(physUvH * atlasH));
                if ((item.width >= 8 && physW < 4) || (item.height >= 8 && physH < 4))
                {
                    if (_loggedItems.Add(itemName + "_tiny"))
                        XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI WARNING] Rejected tiny slice for {pkg.name}/{itemName}: phys={physW}x{physH}, item={item.width}x{item.height}");
                    return null;
                }

                Texture2D cropped = CropAndUnrotateViaRenderTexture(
                    srcTex,
                    new Vector2(physUvW, physUvH),
                    new Vector2(nt.uvRect.x, nt.uvRect.y),
                    physW,
                    physH,
                    nt.rotated,
                    wantTiled,
                    "XINGSHU_NativeTex_" + id);
                if (cropped == null || cropped.width < 2 || cropped.height < 2) return null;
                if ((item.width >= 8 && cropped.width < 4) || (item.height >= 8 && cropped.height < 4))
                {
                    UnityEngine.Object.Destroy(cropped);
                    return null;
                }

                cropped.hideFlags = HideFlags.HideAndDontSave;
                _ownedTextures.Add(cropped);
                Vector4 finalBorder = wantTiled ? Vector4.zero : ClampBorder(border, cropped.width, cropped.height);
                Sprite sp = Sprite.Create(cropped, new Rect(0f, 0f, cropped.width, cropped.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, finalBorder);
                sp.name = "XINGSHU_Native_" + id;
                sp.hideFlags = HideFlags.HideAndDontSave;
                _ownedSprites.Add(sp);
                return sp;
            }
            catch (Exception ex)
            {
                if (_loggedItems.Add(itemName + "_err"))
                    XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI ERROR] Extract {itemName}: {ex.Message}");
                return null;
            }
        }

        private static Vector4 ClampBorder(Vector4 border, float w, float h)
        {
            if (border == Vector4.zero) return Vector4.zero;
            float l = Mathf.Clamp(border.x, 0f, Mathf.Max(0f, w * 0.45f));
            float b = Mathf.Clamp(border.y, 0f, Mathf.Max(0f, h * 0.45f));
            float r = Mathf.Clamp(border.z, 0f, Mathf.Max(0f, w * 0.45f));
            float t = Mathf.Clamp(border.w, 0f, Mathf.Max(0f, h * 0.45f));
            return new Vector4(l, b, r, t);
        }

        private static Texture2D CropAndUnrotateViaRenderTexture(
            Texture atlas,
            Vector2 scale,
            Vector2 offset,
            int physW,
            int physH,
            bool rotated,
            bool repeatWrap,
            string texName)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(physW, physH, 0, RenderTextureFormat.ARGB32);
            Texture2D temp = null;
            Texture2D finalTex = null;
            try
            {
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(atlas, rt, scale, offset);
                RenderTexture.active = rt;
                temp = new Texture2D(physW, physH, TextureFormat.RGBA32, false);
                temp.ReadPixels(new Rect(0f, 0f, physW, physH), 0, 0, false);
                temp.Apply(false, false);

                if (!rotated)
                {
                    temp.name = texName;
                    temp.filterMode = FilterMode.Bilinear;
                    temp.wrapMode = repeatWrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    temp.hideFlags = HideFlags.HideAndDontSave;
                    finalTex = temp;
                    temp = null;
                    return finalTex;
                }

                // 依据 FairyGUI.NTexture.GetUV 验证公式还原顺时针旋转图块：srcX = (physW - 1) - outY, srcY = outX
                int outW = physH;
                int outH = physW;
                Color32[] src = temp.GetPixels32();
                Color32[] dst = new Color32[outW * outH];
                for (int outY = 0; outY < outH; outY++)
                {
                    int srcX = (physW - 1) - outY;
                    int rowOff = outY * outW;
                    for (int outX = 0; outX < outW; outX++)
                    {
                        dst[rowOff + outX] = src[outX * physW + srcX];
                    }
                }

                finalTex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
                finalTex.name = texName;
                finalTex.filterMode = FilterMode.Bilinear;
                finalTex.wrapMode = repeatWrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                finalTex.hideFlags = HideFlags.HideAndDontSave;
                finalTex.SetPixels32(dst);
                finalTex.Apply(false, false);
                return finalTex;
            }
            finally
            {
                if (temp != null) UnityEngine.Object.Destroy(temp);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Sprite BakeRoundedPatternCard(
            Texture2D tileTex,
            int outW,
            int outH,
            float radius,
            float baseAlpha,
            Color baseFill,
            float patternAlpha,
            Color tint,
            Color borderColor,
            Vector4 border,
            string spriteName)
        {
            try
            {
                int tw = tileTex.width;
                int th = tileTex.height;
                if (tw <= 0 || th <= 0) return null;

                Color32[] tilePixels = tileTex.GetPixels32();
                Color32[] dst = new Color32[outW * outH];

                for (int y = 0; y < outH; y++)
                {
                    int ty = y % th;
                    int tileRow = ty * tw;
                    int dstRow = y * outW;
                    float py = y + 0.5f;
                    float dy = Mathf.Min(py, outH - py);

                    for (int x = 0; x < outW; x++)
                    {
                        float px = x + 0.5f;
                        float dx = Mathf.Min(px, outW - px);

                        float edgeDist;
                        if (dx < radius && dy < radius)
                        {
                            float vx = radius - dx;
                            float vy = radius - dy;
                            edgeDist = radius - Mathf.Sqrt(vx * vx + vy * vy);
                        }
                        else
                        {
                            edgeDist = Mathf.Min(dx, dy);
                        }

                        if (edgeDist <= -0.5f)
                        {
                            dst[dstRow + x] = new Color32(0, 0, 0, 0);
                            continue;
                        }

                        float maskAlpha = Mathf.Clamp01(edgeDist + 0.5f);
                        Color src = tilePixels[tileRow + (x % tw)];
                        float pa = src.a * patternAlpha;
                        bool isBorderBand = edgeDist >= 0f && edgeDist <= 1.35f;

                        Color col = new Color(
                            Mathf.Lerp(baseFill.r, src.r * tint.r, pa),
                            Mathf.Lerp(baseFill.g, src.g * tint.g, pa),
                            Mathf.Lerp(baseFill.b, src.b * tint.b, pa),
                            baseAlpha * maskAlpha);
                        if (isBorderBand && borderColor.a > 0.01f)
                        {
                            float bf = borderColor.a * Mathf.Clamp01(1.35f - edgeDist);
                            col.r = Mathf.Lerp(col.r, borderColor.r, bf);
                            col.g = Mathf.Lerp(col.g, borderColor.g, bf);
                            col.b = Mathf.Lerp(col.b, borderColor.b, bf);
                            col.a = Mathf.Max(col.a, bf * maskAlpha);
                        }
                        dst[dstRow + x] = col;
                    }
                }

                Texture2D outTex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
                outTex.name = spriteName + "_Tex";
                outTex.filterMode = FilterMode.Bilinear;
                outTex.wrapMode = TextureWrapMode.Clamp;
                outTex.hideFlags = HideFlags.HideAndDontSave;
                outTex.SetPixels32(dst);
                outTex.Apply(false, true);
                _ownedTextures.Add(outTex);

                Sprite sp = Sprite.Create(outTex, new Rect(0f, 0f, outW, outH), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                sp.name = spriteName;
                sp.hideFlags = HideFlags.HideAndDontSave;
                _ownedSprites.Add(sp);
                return sp;
            }
            catch (Exception ex)
            {
                XingShuUIAssetService.LogUIRuntime($"[XingShu NativeGameUI WARNING] BakeRoundedPatternCard {spriteName}: {ex.Message}");
                return null;
            }
        }

        internal static void Cleanup()
        {
            _generation++;
            _loading = false;
            _ready = false;
            _retryAt = 0f;
            _loadStartedAt = 0f;
            _nextTickAt = 0f;
            _pendingBindings.Clear();
            _readyCallbacks.Clear();
            _cache.Clear();

            for (int i = 0; i < _ownedSprites.Count; i++)
            {
                if (_ownedSprites[i] != null) UnityEngine.Object.Destroy(_ownedSprites[i]);
            }
            _ownedSprites.Clear();

            for (int i = 0; i < _ownedTextures.Count; i++)
            {
                if (_ownedTextures[i] != null) UnityEngine.Object.Destroy(_ownedTextures[i]);
            }
            _ownedTextures.Clear();
        }
    }
}
