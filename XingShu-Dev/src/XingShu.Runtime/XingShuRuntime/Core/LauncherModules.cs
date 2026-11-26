using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XingShuRuntime.Core
{
    // The launcher build enables only explicitly installed first-party modules.
    // Package files select a known feature, never an assembly or executable path.
    internal static class LauncherModules
    {
        internal static bool CanLoad(string typeName)
        {
#if XINGSHU_MODULAR_LAUNCHER
            if (_optionalTypes == null) Load();
            return !_optionalTypes.Contains(typeName) || _enabledTypes.Contains(typeName);
#else
            return true;
#endif
        }

#if XINGSHU_MODULAR_LAUNCHER
        private static readonly string[][] LegacyModuleAliases =
        {
            new[] { "monster-targeting", "monster-action" },
            new[] { "monster-cooldown", "monster-action" },
            new[] { "battle-card-points", "defense-hint" },
            new[] { "quick-thanks", "auto-social" },
            new[] { "avatar-praise", "auto-social" },
            new[] { "star-coin-shop", "auto-social" },
            new[] { "settlement-capture", "auto-social" },
        };

        private static HashSet<string> _optionalTypes;
        private static HashSet<string> _enabledTypes;

        private static void Load()
        {
            var optional = new HashSet<string>(StringComparer.Ordinal);
            var enabled = new HashSet<string>(StringComparer.Ordinal);
#if XINGSHU_ANDROID_PREVIEW
            var gameRoot = Application.persistentDataPath;
#else
            var gameRoot = Path.GetDirectoryName(Application.dataPath);
#endif
            var directory = Path.Combine(gameRoot ?? "", "xingshu/plugins");
            foreach (string[] module in LauncherModuleCatalog.Modules)
            {
                bool active = IsModuleOrLegacyInstalled(directory, module[0]);
                for (int index = 1; index < module.Length; index++)
                {
                    optional.Add(module[index]);
                    if (active) enabled.Add(module[index]);
                }
            }
            _enabledTypes = enabled;
            _optionalTypes = optional;
        }

        private static bool IsModuleOrLegacyInstalled(string directory, string canonicalId)
        {
            if (Installed(directory, canonicalId)) return true;
            foreach (string[] pair in LegacyModuleAliases)
            {
                if (pair.Length >= 2 && string.Equals(pair[1], canonicalId, StringComparison.Ordinal) &&
                    Installed(directory, pair[0]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Installed(string directory, string id)
        {
            try
            {
                var path = Path.Combine(directory, "xingshu.launcher." + id + ".module");
                if (!File.Exists(path)) return false;
                using (var stream = File.OpenRead(path))
                using (var reader = new StreamReader(stream))
                {
                    var buffer = new char[128];
                    int count = reader.ReadBlock(buffer, 0, buffer.Length);
                    return new string(buffer, 0, count) == "xingshu-module-v1:" + id + "\n";
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
#endif
    }
}
