using System;
using System.Collections.Generic;
using System.IO;
using FairyGUI;
using UnityEngine;
using XingShuRuntime.Core;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Resolves FairyGUI external card/portrait URLs against loose PNG/JPG textures in the configured MODIMG folder.
    /// Exposed publicly so injected hooks inside AstralParty.Runtime can resolve textures without reflection or access faults.
    /// </summary>
    public static class ModImgTextureLoader
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, string> FileIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, NTexture> TextureCache = new Dictionary<string, NTexture>(StringComparer.Ordinal);

        private static bool _indexInitialized;
        private static string _indexedDir = "";
        private static long _indexedDirWriteTicks = -1;
        private static long _lastStatCheckUtcTicks;
        private static int _hitCount;
        private static string _lastMatchedKey = "";
        private static string _lastAttemptedUrl = "";
        private static long _lastStatusWriteUtcTicks;

        public static void InvalidateCache()
        {
            lock (Sync)
            {
                _indexInitialized = false;
                _indexedDir = "";
                _indexedDirWriteTicks = -1;
                _lastStatCheckUtcTicks = 0;
                TextureCache.Clear();
                FileIndex.Clear();
            }
            DynamicCardVideoManager.InvalidateCache();
        }

        public static NTexture TryResolveForLoader(GLoader loader, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                DynamicCardVideoManager.ClearLoader(loader);
                return null;
            }
            if (url.StartsWith("ui://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                DynamicCardVideoManager.ClearLoader(loader);
                return null;
            }

            if (loader != null)
            {
                NTexture dynamicTex = DynamicCardVideoManager.TryResolve(loader, url);
                if (dynamicTex != null)
                {
                    lock (Sync)
                    {
                        _hitCount++;
                        _lastAttemptedUrl = url;
                        _lastMatchedKey = "dynamic:" + url;
                    }
                    return dynamicTex;
                }
            }
            else
            {
                DynamicCardVideoManager.ClearLoader(null);
            }

            return TryResolve(url);
        }

        public static NTexture TryResolve(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }
            if (url.StartsWith("ui://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                if (!LauncherModules.CanLoad("StaticPortraitGUI") || !StaticPortraitGUI.IsEnabled)
                {
                    return null;
                }

                lock (Sync)
                {
                    _lastAttemptedUrl = url;
                }

                string modImgDir = ResolveModImgDirectory();
                if (string.IsNullOrEmpty(modImgDir))
                {
                    return null;
                }

                lock (Sync)
                {
                    EnsureIndexLocked(modImgDir);

                    NTexture existing;
                    if (TextureCache.TryGetValue(url, out existing))
                    {
                        if (existing != null && !existing.disposed && existing.nativeTexture != null)
                        {
                            if (string.Equals(existing.nativeTexture.name, url, StringComparison.Ordinal))
                            {
                                _hitCount++;
                                _lastMatchedKey = url;
                                MaybeWriteStatusLocked(modImgDir, force: false);
                                return existing;
                            }
                        }
                        TextureCache.Remove(url);
                    }

                    string filePath = LookupFilePathLocked(url);
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        return null;
                    }

                    byte[] bytes = File.ReadAllBytes(filePath);
                    if (bytes == null || bytes.Length < 16)
                    {
                        return null;
                    }

                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, bytes, false))
                    {
                        UnityEngine.Object.Destroy(tex);
                        return null;
                    }

                    // FairyGUI.GLoader.onExternalLoadSuccess rejects any NTexture whose nativeTexture.name != url.
                    tex.name = url;
                    tex.wrapMode = TextureWrapMode.Clamp;

                    NTexture nt = new NTexture(tex);
                    nt.destroyMethod = DestroyMethod.None;
                    nt.AddRef();
                    TextureCache[url] = nt;

                    _hitCount++;
                    _lastMatchedKey = url;
                    MaybeWriteStatusLocked(modImgDir, force: _hitCount == 1);
                    return nt;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveModImgDirectory()
        {
            string resolved = "";
            if (StaticPortraitGUI.Instance != null)
            {
                string rawInst = StaticPortraitGUI.Instance.ModImgPath ?? "";
                if (XingShuMobileUIHelper.IsMobile && (rawInst.Trim() == "../modimg" || rawInst.Trim() == "..\\modimg"))
                {
                    rawInst = "modimg";
                }
                resolved = StaticPortraitGUI.Instance.ResolvePathInternal(
                    rawInst,
                    XingShuMobileUIHelper.IsMobile ? "modimg" : StaticPortraitGUI.Instance.DefaultModImg
                );
            }
            else
            {
                string basePref = XingShuConfigService.GetString(
                    XingShuConfigKeys.StaticPortraitBasePath,
                    XingShuConfigKeys.StaticPortraitBasePathDefault
                );
                string modImgPref = XingShuConfigService.GetString(
                    XingShuConfigKeys.StaticPortraitModImg,
                    XingShuConfigKeys.StaticPortraitModImgDefault
                );
                string defaultModImg = XingShuMobileUIHelper.IsMobile ? "modimg" : "../modimg";
                string raw = string.IsNullOrEmpty(modImgPref) ? defaultModImg : modImgPref.Trim();
                if (XingShuMobileUIHelper.IsMobile && (raw == "../modimg" || raw == "..\\modimg"))
                {
                    raw = "modimg";
                }
                raw = raw.TrimEnd('/', '\\');

                if (StaticPortraitGUI.IsPathRootedSafe(raw))
                {
                    try
                    {
                        resolved = Path.GetFullPath(raw).Replace('\\', '/');
                    }
                    catch
                    {
                        resolved = raw.Replace('\\', '/');
                    }
                }
                else
                {
                    string effectiveBase = StaticPortraitGUI.GetEffectiveBasePath(basePref);
                    string trimmed = raw.TrimStart('/', '\\');
                    try
                    {
                        resolved = Path.GetFullPath(Path.Combine(effectiveBase, trimmed)).Replace('\\', '/');
                    }
                    catch
                    {
                        resolved = (effectiveBase + "/" + trimmed).Replace('\\', '/');
                    }
                }
            }

            if (XingShuMobileUIHelper.IsMobile && (string.IsNullOrEmpty(resolved) || !Directory.Exists(resolved)))
            {
                string persistentModImg = Path.Combine(Application.persistentDataPath ?? "", "modimg").Replace('\\', '/');
                if (!string.IsNullOrEmpty(Application.persistentDataPath) && Directory.Exists(persistentModImg))
                {
                    resolved = persistentModImg;
                }
            }
            return resolved;
        }

        private static void EnsureIndexLocked(string modImgDir)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            bool dirChanged = !string.Equals(_indexedDir, modImgDir, StringComparison.Ordinal);
            if (_indexInitialized && !dirChanged && (nowTicks - _lastStatCheckUtcTicks) < TimeSpan.FromSeconds(3).Ticks)
            {
                return;
            }
            _lastStatCheckUtcTicks = nowTicks;

            bool exists = Directory.Exists(modImgDir);
            long writeTicks = exists ? Directory.GetLastWriteTimeUtc(modImgDir).Ticks : -1;
            if (_indexInitialized && !dirChanged && writeTicks == _indexedDirWriteTicks)
            {
                return;
            }

            _indexedDir = modImgDir;
            _indexedDirWriteTicks = writeTicks;
            _indexInitialized = true;
            FileIndex.Clear();
            TextureCache.Clear();

            if (!exists)
            {
                MaybeWriteStatusLocked(modImgDir, force: true);
                return;
            }

            foreach (string path in Directory.GetFiles(modImgDir))
            {
                string ext = Path.GetExtension(path);
                if (!string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string stem = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(stem) && !FileIndex.ContainsKey(stem))
                {
                    FileIndex[stem] = path;
                }
            }

            MaybeWriteStatusLocked(modImgDir, force: true);
        }

        private static string LookupFilePathLocked(string url)
        {
            if (FileIndex.Count == 0)
            {
                return null;
            }

            string clean = url.Replace('\\', '/');
            int queryIdx = clean.IndexOfAny(new[] { '?', '#' });
            if (queryIdx >= 0)
            {
                clean = clean.Substring(0, queryIdx);
            }
            int slashIdx = clean.LastIndexOf('/');
            if (slashIdx >= 0)
            {
                clean = clean.Substring(slashIdx + 1);
            }

            string stem = Path.GetFileNameWithoutExtension(clean);
            if (string.IsNullOrEmpty(stem))
            {
                stem = clean;
            }
            if (string.IsNullOrEmpty(stem))
            {
                return null;
            }

            string match;
            if (FileIndex.TryGetValue(stem, out match))
            {
                return match;
            }
            if (stem.EndsWith("_sfw", StringComparison.OrdinalIgnoreCase))
            {
                string baseStem = stem.Substring(0, stem.Length - 4);
                if (FileIndex.TryGetValue(baseStem, out match))
                {
                    return match;
                }
            }
            return null;
        }

        private static void MaybeWriteStatusLocked(string modImgDir, bool force)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            if (!force && (nowTicks - _lastStatusWriteUtcTicks) < TimeSpan.FromSeconds(5).Ticks)
            {
                return;
            }
            _lastStatusWriteUtcTicks = nowTicks;
            try
            {
                string baseRoot = StaticPortraitGUI.GetEffectiveBasePath("");
                if (string.IsNullOrEmpty(baseRoot) || !Directory.Exists(baseRoot))
                {
                    return;
                }
                string statusPath = Path.Combine(baseRoot, "xingshu_modimg_status.txt");
                string content = "indexed=" + FileIndex.Count
                    + "\nhits=" + _hitCount
                    + "\nlast=" + (_lastMatchedKey ?? "")
                    + "\nlast_url=" + (_lastAttemptedUrl ?? "")
                    + "\ndir=" + (modImgDir ?? "")
                    + "\nutc=" + DateTime.UtcNow.ToString("o") + "\n";
                File.WriteAllText(statusPath, content);
            }
            catch
            {
            }
        }
    }
}
