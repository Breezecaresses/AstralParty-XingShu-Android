using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace XingShuRuntime.Utilities
{
    internal enum LobbyBgMediaKind
    {
        Unknown = 0,
        Video = 1,
        Image = 2,
        CriUsm = 3,
        UnityBundle = 4,
        MotionStream = 5,
    }

    internal enum LobbyBgFillMode
    {
        Cover = 0,
        Contain = 1,
        Stretch = 2,
    }

    /// <summary>
    /// Pure, deterministic media classification, sorting, clamping, and cycling policy
    /// for <see cref="CustomLobbyBackground"/>. Kept free of UnityEngine/game dependencies
    /// so unit tests can compile and verify it directly.
    /// </summary>
    internal static class CustomLobbyBgPolicy
    {
        internal const float MinBrightness = 0.30f;
        internal const float MaxBrightness = 1.00f;
        internal const float DefaultBrightness = 1.00f;

        internal const float MinSpeed = 0.50f;
        internal const float MaxSpeed = 2.00f;
        internal const float DefaultSpeed = 1.00f;

        internal const float MinVolume = 0.00f;
        internal const float MaxVolume = 1.00f;
        internal const float DefaultVolume = 0.35f;

        internal static LobbyBgMediaKind ClassifyExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return LobbyBgMediaKind.Unknown;
            }
            string ext = Path.GetExtension(path.Trim());
            if (string.IsNullOrEmpty(ext))
            {
                return LobbyBgMediaKind.Unknown;
            }
            string lower = ext.ToLowerInvariant();
            switch (lower)
            {
                case ".mp4":
                case ".webm":
                case ".mov":
                case ".m4v":
                case ".wmv":
                    return LobbyBgMediaKind.Video;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".webp":
                case ".bmp":
                    return LobbyBgMediaKind.Image;
                case ".usm":
                    return LobbyBgMediaKind.CriUsm;
                case ".bundle":
                    return LobbyBgMediaKind.UnityBundle;
                case ".xsvid":
                    return LobbyBgMediaKind.MotionStream;
                default:
                    return LobbyBgMediaKind.Unknown;
            }
        }

        internal static string KindLabel(LobbyBgMediaKind kind)
        {
            switch (kind)
            {
                case LobbyBgMediaKind.Video: return "视频";
                case LobbyBgMediaKind.Image: return "静态图";
                case LobbyBgMediaKind.CriUsm: return "USM 动效";
                case LobbyBgMediaKind.UnityBundle: return "VKVLoop 包";
                case LobbyBgMediaKind.MotionStream: return "动态视频";
                default: return "未知";
            }
        }

        internal static List<string> SortMediaFiles(IEnumerable<string> paths)
        {
            var result = new List<string>();
            if (paths == null)
            {
                return result;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in paths)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }
                string trimmed = raw.Trim();
                string fileName = Path.GetFileName(trimmed);
                if (string.IsNullOrEmpty(fileName) || fileName[0] == '.' || fileName[0] == '~')
                {
                    continue;
                }
                if (ClassifyExtension(fileName) == LobbyBgMediaKind.Unknown)
                {
                    continue;
                }
                if (seen.Add(trimmed))
                {
                    result.Add(trimmed);
                }
            }
            result.Sort(CompareMediaPath);
            return result;
        }

        private static int CompareMediaPath(string a, string b)
        {
            string na = Path.GetFileName(a) ?? "";
            string nb = Path.GetFileName(b) ?? "";
            int c = StringComparer.OrdinalIgnoreCase.Compare(na, nb);
            return c != 0 ? c : StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        internal static void ComputeRenderTextureSize(int srcW, int srcH, bool isMobile, out int outW, out int outH)
        {
            if (srcW <= 0 || srcH <= 0)
            {
                outW = isMobile ? 1280 : 1920;
                outH = isMobile ? 720 : 1080;
                return;
            }
            int maxW = isMobile ? 1920 : 3840;
            int maxH = isMobile ? 1080 : 2160;
            double w = srcW;
            double h = srcH;
            if (w > maxW || h > maxH)
            {
                double scale = Math.Min((double)maxW / w, (double)maxH / h);
                w *= scale;
                h *= scale;
            }
            outW = ((int)Math.Round(w)) & ~1;
            outH = ((int)Math.Round(h)) & ~1;
            if (outW < 2) outW = 2;
            if (outH < 2) outH = 2;
        }

        internal static int PickNextIndex(int currentIndex, int count, int delta)
        {
            if (count <= 0)
            {
                return -1;
            }
            if (currentIndex < 0 || currentIndex >= count)
            {
                return delta >= 0 ? 0 : count - 1;
            }
            int shifted = (currentIndex + delta) % count;
            if (shifted < 0)
            {
                shifted += count;
            }
            return shifted;
        }

        internal static int PickRandomIndex(int currentIndex, int count, Func<int, int> nextInt)
        {
            if (count <= 0)
            {
                return -1;
            }
            if (count == 1)
            {
                return 0;
            }
            bool hasCurrent = currentIndex >= 0 && currentIndex < count;
            int pool = hasCurrent ? (count - 1) : count;
            int raw = nextInt != null ? nextInt(pool) : 0;
            if (raw < 0) raw = 0;
            if (raw >= pool) raw = pool - 1;
            if (hasCurrent && raw >= currentIndex)
            {
                raw += 1;
            }
            if (raw >= count) raw = count - 1;
            return raw;
        }

        internal static float ClampBrightness(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultBrightness;
            if (value < MinBrightness) return MinBrightness;
            if (value > MaxBrightness) return MaxBrightness;
            return value;
        }

        internal static float ClampSpeed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultSpeed;
            if (value < MinSpeed) return MinSpeed;
            if (value > MaxSpeed) return MaxSpeed;
            return value;
        }

        internal static float ClampVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultVolume;
            if (value < MinVolume) return MinVolume;
            if (value > MaxVolume) return MaxVolume;
            return value;
        }

        internal static LobbyBgFillMode NormalizeFillMode(int raw)
        {
            switch (raw)
            {
                case (int)LobbyBgFillMode.Contain: return LobbyBgFillMode.Contain;
                case (int)LobbyBgFillMode.Stretch: return LobbyBgFillMode.Stretch;
                default: return LobbyBgFillMode.Cover;
            }
        }

        internal static string FillModeLabel(LobbyBgFillMode mode)
        {
            switch (mode)
            {
                case LobbyBgFillMode.Contain: return "完整显示";
                case LobbyBgFillMode.Stretch: return "拉伸填充";
                default: return "等比铺满";
            }
        }

        internal static bool ContainsNonAsciiOrSpecial(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 127 || c == '[' || c == ']' || c == '(' || c == ')' || c == '%' || c == '#' || c == '?')
                {
                    return true;
                }
            }
            return false;
        }

        internal static string BuildVideoUrl(string fullPath, bool isAndroid, int stage)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return "";
            }
            if (!isAndroid || stage == 1)
            {
                return BuildFileUri(fullPath);
            }
            return fullPath.Replace('\\', '/');
        }

        internal static string BuildAndroidAsciiCacheFileName(string fullPath)
        {
            string input = fullPath ?? "";
            uint hash = 2166136261u;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= 16777619u;
            }
            uint hash2 = 5381u;
            for (int i = input.Length - 1; i >= 0; i--)
            {
                hash2 = ((hash2 << 5) + hash2) ^ input[i];
            }
            string ext = Path.GetExtension(input.Trim()).ToLowerInvariant();
            switch (ext)
            {
                case ".mp4":
                case ".webm":
                case ".mov":
                case ".m4v":
                case ".wmv":
                    break;
                default:
                    ext = ".mp4";
                    break;
            }
            return "xingshu_lobbybg_" + hash.ToString("x8", CultureInfo.InvariantCulture)
                + hash2.ToString("x8", CultureInfo.InvariantCulture) + ext;
        }

        internal static string BuildFileUri(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return "";
            }
            try
            {
                string abs = Path.GetFullPath(fullPath);
                return new Uri(abs).AbsoluteUri;
            }
            catch
            {
                string norm = fullPath.Replace('\\', '/');
                return norm.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? norm
                    : ("file://" + (norm.StartsWith("/") ? "" : "/") + Uri.EscapeUriString(norm));
            }
        }

        internal static string DescribeImageHeader(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
            {
                return "空文件";
            }
            if (bytes.Length >= 8
                && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                return "PNG";
            }
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "JPEG";
            }
            if (bytes.Length >= 12
                && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return "WebP";
            }
            if (bytes.Length >= 12
                && bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70)
            {
                return "HEIF/AVIF";
            }
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return "BMP";
            }
            if (bytes.Length >= 6
                && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            {
                return "GIF";
            }
            return "未知格式";
        }

        internal static bool IsLikelyUnsupportedByUnityLoadImage(byte[] bytes)
        {
            string fmt = DescribeImageHeader(bytes);
            return fmt == "WebP" || fmt == "HEIF/AVIF" || fmt == "BMP" || fmt == "GIF";
        }

        internal const int VideoAudioOutputModeDirectValue = 2;

        internal static bool ShouldMuteVanillaLobbyBgm(
            bool featureEnabled,
            bool muteVanillaBgm,
            bool homeVisible,
            bool subBgVisible)
        {
            return featureEnabled && muteVanillaBgm && (homeVisible || subBgVisible);
        }

        internal static void ProcessPcm16Chunk(
            byte[] src,
            int srcBytes,
            int channels,
            float speed,
            float startGain,
            float endGain,
            ref double fracFramePos,
            byte[] dst,
            out int dstBytes)
        {
            dstBytes = 0;
            if (src == null || dst == null || channels <= 0 || srcBytes <= 0 || dst.Length <= 0)
            {
                return;
            }
            int frameSize = channels * 2;
            int maxSrcFrames = Math.Min(srcBytes, src.Length) / frameSize;
            int maxDstFrames = dst.Length / frameSize;
            if (maxSrcFrames <= 0 || maxDstFrames <= 0)
            {
                return;
            }

            float clampedSpeed = ClampSpeed(speed);
            float g0 = ClampVolume(startGain);
            float g1 = ClampVolume(endGain);

            if (Math.Abs(clampedSpeed - 1f) < 0.015f)
            {
                int count = Math.Min(maxSrcFrames, maxDstFrames);
                fracFramePos = 0.0;
                for (int f = 0; f < count; f++)
                {
                    float g = g0 + (g1 - g0) * ((f + 0.5f) / count);
                    int baseIdx = f * frameSize;
                    for (int c = 0; c < channels; c++)
                    {
                        int idx = baseIdx + c * 2;
                        short s = (short)(src[idx] | (src[idx + 1] << 8));
                        int scaled = (int)Math.Round(s * g);
                        if (scaled > short.MaxValue) scaled = short.MaxValue;
                        else if (scaled < short.MinValue) scaled = short.MinValue;
                        short outS = (short)scaled;
                        dst[idx] = (byte)(outS & 0xFF);
                        dst[idx + 1] = (byte)((outS >> 8) & 0xFF);
                    }
                }
                dstBytes = count * frameSize;
                return;
            }

            double pos = fracFramePos >= 0.0 ? fracFramePos : 0.0;
            int outFrames = 0;
            while (outFrames < maxDstFrames)
            {
                int i0 = (int)pos;
                if (i0 >= maxSrcFrames)
                {
                    break;
                }
                int i1 = i0 + 1 < maxSrcFrames ? i0 + 1 : i0;
                double t = pos - i0;
                float g = g0 + (g1 - g0) * ((outFrames + 0.5f) / maxDstFrames);
                int b0 = i0 * frameSize;
                int b1 = i1 * frameSize;
                int bd = outFrames * frameSize;
                for (int c = 0; c < channels; c++)
                {
                    int o = c * 2;
                    short s0 = (short)(src[b0 + o] | (src[b0 + o + 1] << 8));
                    short s1 = (short)(src[b1 + o] | (src[b1 + o + 1] << 8));
                    double interp = s0 + (s1 - s0) * t;
                    int scaled = (int)Math.Round(interp * g);
                    if (scaled > short.MaxValue) scaled = short.MaxValue;
                    else if (scaled < short.MinValue) scaled = short.MinValue;
                    short outS = (short)scaled;
                    dst[bd + o] = (byte)(outS & 0xFF);
                    dst[bd + o + 1] = (byte)((outS >> 8) & 0xFF);
                }
                outFrames++;
                pos += clampedSpeed;
            }
            fracFramePos = pos >= maxSrcFrames ? (pos - maxSrcFrames) : 0.0;
            dstBytes = outFrames * frameSize;
        }

        internal static string BuildWindowsAudioCommandPayload(
            int generation,
            bool stop,
            bool quit,
            string fullPath,
            bool muted,
            float volume,
            float speed,
            bool paused,
            int seekSeq,
            double seekSeconds)
        {
            string b64 = string.IsNullOrEmpty(fullPath)
                ? ""
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath));
            float clampedVol = ClampVolume(volume);
            float clampedSpeed = ClampSpeed(speed);
            double safeSeek = seekSeconds > 0.0 && !double.IsNaN(seekSeconds) && !double.IsInfinity(seekSeconds)
                ? seekSeconds
                : 0.0;

            var sb = new StringBuilder(256);
            sb.Append("GEN=").Append(generation).Append('\n');
            sb.Append("STOP=").Append(stop ? "1" : "0").Append('\n');
            sb.Append("QUIT=").Append(quit ? "1" : "0").Append('\n');
            sb.Append("PATH_B64=").Append(b64).Append('\n');
            sb.Append("MUTED=").Append(muted ? "1" : "0").Append('\n');
            sb.Append("VOL=").Append(clampedVol.ToString("0.0000", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("SPEED=").Append(clampedSpeed.ToString("0.0000", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("PAUSED=").Append(paused ? "1" : "0").Append('\n');
            sb.Append("SEEK_SEQ=").Append(seekSeq).Append('\n');
            sb.Append("SEEK_SEC=").Append(safeSeek.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }

        internal static bool TryParseWindowsAudioState(
            string content,
            out int generation,
            out bool opened,
            out bool hasAudio,
            out double posSeconds,
            out double durationSeconds,
            out string error)
        {
            generation = 0;
            opened = false;
            hasAudio = false;
            posSeconds = 0.0;
            durationSeconds = 0.0;
            error = "";
            if (string.IsNullOrEmpty(content))
            {
                return false;
            }
            string trimmed = content.Trim();
            int p1 = trimmed.IndexOf('|');
            if (p1 < 0) return false;
            int p2 = trimmed.IndexOf('|', p1 + 1);
            if (p2 < 0) return false;
            int p3 = trimmed.IndexOf('|', p2 + 1);
            if (p3 < 0) return false;
            int p4 = trimmed.IndexOf('|', p3 + 1);
            if (p4 < 0) return false;
            int p5 = trimmed.IndexOf('|', p4 + 1);

            string genStr = trimmed.Substring(0, p1);
            string openStr = trimmed.Substring(p1 + 1, p2 - p1 - 1);
            string audioStr = trimmed.Substring(p2 + 1, p3 - p2 - 1);
            string posStr = trimmed.Substring(p3 + 1, p4 - p3 - 1);
            string durStr = p5 >= 0 ? trimmed.Substring(p4 + 1, p5 - p4 - 1) : trimmed.Substring(p4 + 1);

            if (!int.TryParse(genStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out generation))
            {
                return false;
            }
            opened = openStr == "1";
            hasAudio = audioStr == "1";
            if (!double.TryParse(posStr, NumberStyles.Float, CultureInfo.InvariantCulture, out posSeconds)
                || double.IsNaN(posSeconds) || double.IsInfinity(posSeconds) || posSeconds < 0.0)
            {
                posSeconds = 0.0;
            }
            if (!double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds)
                || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds < 0.0)
            {
                durationSeconds = 0.0;
            }
            error = p5 >= 0 ? trimmed.Substring(p5 + 1).Trim() : "";
            return true;
        }

        internal static byte[] BuildWindowsShortcutLnk(string envTargetPath, string arguments)
        {
            string target = envTargetPath ?? "";
            string args = arguments ?? "";

            using (var ms = new MemoryStream(1024))
            {
                // ShellLinkHeader (76 bytes)
                WriteUInt32LE(ms, 0x0000004Cu); // HeaderSize
                byte[] clsid = new Guid("00021401-0000-0000-C000-000000000046").ToByteArray();
                ms.Write(clsid, 0, clsid.Length);
                WriteUInt32LE(ms, 0x000002A0u); // HasArguments(0x20) | IsUnicode(0x80) | HasExpString(0x200)
                WriteUInt32LE(ms, 0x00000020u); // FILE_ATTRIBUTE_ARCHIVE
                WriteUInt64LE(ms, 0UL);         // CreationTime
                WriteUInt64LE(ms, 0UL);         // AccessTime
                WriteUInt64LE(ms, 0UL);         // WriteTime
                WriteUInt32LE(ms, 0u);          // FileSize
                WriteUInt32LE(ms, 0u);          // IconIndex
                WriteUInt32LE(ms, 7u);          // ShowCommand = SW_SHOWMINNOACTIVE
                WriteUInt16LE(ms, 0);           // HotKey
                WriteUInt16LE(ms, 0);           // Reserved1
                WriteUInt32LE(ms, 0u);          // Reserved2
                WriteUInt32LE(ms, 0u);          // Reserved3

                // StringData: COMMAND_LINE_ARGUMENTS (CountCharacters + UTF-16LE chars)
                int charCount = args.Length > ushort.MaxValue ? ushort.MaxValue : args.Length;
                string clippedArgs = charCount == args.Length ? args : args.Substring(0, charCount);
                WriteUInt16LE(ms, (ushort)charCount);
                byte[] argBytes = Encoding.Unicode.GetBytes(clippedArgs);
                ms.Write(argBytes, 0, argBytes.Length);

                // ExtraData: EnvironmentVariableDataBlock (788 bytes, signature 0xA0000001)
                WriteUInt32LE(ms, 788u);
                WriteUInt32LE(ms, 0xA0000001u);

                byte[] ansiBuf = new byte[260];
                byte[] ansiSrc = Encoding.ASCII.GetBytes(target);
                int ansiLen = ansiSrc.Length > 259 ? 259 : ansiSrc.Length;
                Buffer.BlockCopy(ansiSrc, 0, ansiBuf, 0, ansiLen);
                ms.Write(ansiBuf, 0, ansiBuf.Length);

                byte[] uniBuf = new byte[520];
                byte[] uniSrc = Encoding.Unicode.GetBytes(target);
                int uniLen = uniSrc.Length > 518 ? 518 : uniSrc.Length;
                Buffer.BlockCopy(uniSrc, 0, uniBuf, 0, uniLen);
                ms.Write(uniBuf, 0, uniBuf.Length);

                // Terminal block
                WriteUInt32LE(ms, 0u);
                return ms.ToArray();
            }
        }

        internal static string BuildWindowsVbsLauncher(
            string psExePath,
            string ps1Path,
            int parentPid,
            string cmdPath,
            string statePath,
            string vbsPath,
            string lnkPath)
        {
            string cmdLine = string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -STA -File \"{1}\" -ParentPid {2} -CmdFile \"{3}\" -StateFile \"{4}\" -VbsFile \"{5}\" -LnkFile \"{6}\"",
                psExePath ?? "",
                ps1Path ?? "",
                parentPid,
                cmdPath ?? "",
                statePath ?? "",
                vbsPath ?? "",
                lnkPath ?? "");
            return "CreateObject(\"WScript.Shell\").Run \"" + cmdLine.Replace("\"", "\"\"") + "\", 0, False\r\n";
        }

        private static void WriteUInt16LE(Stream s, ushort value)
        {
            s.WriteByte((byte)(value & 0xFF));
            s.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private static void WriteUInt32LE(Stream s, uint value)
        {
            s.WriteByte((byte)(value & 0xFF));
            s.WriteByte((byte)((value >> 8) & 0xFF));
            s.WriteByte((byte)((value >> 16) & 0xFF));
            s.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static void WriteUInt64LE(Stream s, ulong value)
        {
            WriteUInt32LE(s, (uint)(value & 0xFFFFFFFFUL));
            WriteUInt32LE(s, (uint)(value >> 32));
        }

        internal static double EstimatePlaybackPosition(
            double lastPosSeconds,
            double elapsedSeconds,
            float speed,
            double durationSeconds)
        {
            double pos = (double.IsNaN(lastPosSeconds) || double.IsInfinity(lastPosSeconds) || lastPosSeconds < 0.0)
                ? 0.0
                : lastPosSeconds;
            double dur = (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds < 0.0)
                ? 0.0
                : durationSeconds;
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0.0)
            {
                return dur > 0.05 && pos > dur ? dur : pos;
            }
            double est = pos + elapsedSeconds * ClampSpeed(speed);
            if (dur > 0.05 && est > dur)
            {
                est = est % dur;
            }
            return est;
        }

        internal const string SidecarFileName = "xingshu_lobby_bg.json";

        internal struct LobbyBgSidecarConfig
        {
            public bool Enabled;
            public string SelectedFile;
            public bool SyncSubPanels;
            public bool RandomOnLobby;
            public LobbyBgFillMode FillMode;
            public float Brightness;
            public float Speed;
            public bool Muted;
            public float Volume;
            public bool MuteVanillaBgm;
            public long UpdatedAt;
        }

        internal static string SerializeSidecarJson(LobbyBgSidecarConfig cfg)
        {
            string fillStr = cfg.FillMode == LobbyBgFillMode.Contain
                ? "contain"
                : cfg.FillMode == LobbyBgFillMode.Stretch
                    ? "stretch"
                    : "cover";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return "{\n"
                + "  \"enabled\": " + (cfg.Enabled ? "true" : "false") + ",\n"
                + "  \"selectedFile\": \"" + EscapeJsonString(cfg.SelectedFile) + "\",\n"
                + "  \"syncSubPanels\": " + (cfg.SyncSubPanels ? "true" : "false") + ",\n"
                + "  \"randomOnLobby\": " + (cfg.RandomOnLobby ? "true" : "false") + ",\n"
                + "  \"fillMode\": \"" + fillStr + "\",\n"
                + "  \"brightness\": " + ClampBrightness(cfg.Brightness).ToString("0.###", inv) + ",\n"
                + "  \"speed\": " + ClampSpeed(cfg.Speed).ToString("0.###", inv) + ",\n"
                + "  \"muted\": " + (cfg.Muted ? "true" : "false") + ",\n"
                + "  \"volume\": " + ClampVolume(cfg.Volume).ToString("0.###", inv) + ",\n"
                + "  \"muteVanillaBgm\": " + (cfg.MuteVanillaBgm ? "true" : "false") + ",\n"
                + "  \"updatedAt\": " + cfg.UpdatedAt.ToString(inv) + "\n"
                + "}\n";
        }

        internal static bool TryParseSidecarJson(string json, out LobbyBgSidecarConfig cfg)
        {
            cfg = new LobbyBgSidecarConfig
            {
                SyncSubPanels = true,
                FillMode = LobbyBgFillMode.Cover,
                Brightness = DefaultBrightness,
                Speed = DefaultSpeed,
                Muted = true,
                Volume = DefaultVolume,
            };
            if (string.IsNullOrEmpty(json) || json.IndexOf('{') < 0) return false;
            bool anyMatched = false;
            bool bVal;
            string sVal;
            float fVal;
            long lVal;
            if (TryReadJsonBool(json, "enabled", out bVal)) { cfg.Enabled = bVal; anyMatched = true; }
            if (TryReadJsonString(json, "selectedFile", out sVal)) { cfg.SelectedFile = sVal; anyMatched = true; }
            if (TryReadJsonBool(json, "syncSubPanels", out bVal)) { cfg.SyncSubPanels = bVal; anyMatched = true; }
            if (TryReadJsonBool(json, "randomOnLobby", out bVal)) { cfg.RandomOnLobby = bVal; anyMatched = true; }
            if (TryReadJsonString(json, "fillMode", out sVal))
            {
                anyMatched = true;
                if (string.Equals(sVal, "contain", StringComparison.OrdinalIgnoreCase)) cfg.FillMode = LobbyBgFillMode.Contain;
                else if (string.Equals(sVal, "stretch", StringComparison.OrdinalIgnoreCase)) cfg.FillMode = LobbyBgFillMode.Stretch;
                else cfg.FillMode = LobbyBgFillMode.Cover;
            }
            if (TryReadJsonFloat(json, "brightness", out fVal)) { cfg.Brightness = ClampBrightness(fVal); anyMatched = true; }
            if (TryReadJsonFloat(json, "speed", out fVal)) { cfg.Speed = ClampSpeed(fVal); anyMatched = true; }
            if (TryReadJsonBool(json, "muted", out bVal)) { cfg.Muted = bVal; anyMatched = true; }
            if (TryReadJsonFloat(json, "volume", out fVal)) { cfg.Volume = ClampVolume(fVal); anyMatched = true; }
            if (TryReadJsonBool(json, "muteVanillaBgm", out bVal)) { cfg.MuteVanillaBgm = bVal; anyMatched = true; }
            if (TryReadJsonLong(json, "updatedAt", out lVal)) { cfg.UpdatedAt = lVal; }
            return anyMatched;
        }

        public static float ComputeCompensatedPlaybackSpeed(
            float userSpeed,
            float nativeClockScale,
            float unityTimeScale,
            bool videoUsesUnscaledClock)
        {
            float safeUser = ClampSpeed(userSpeed);
            float denom = (!float.IsNaN(nativeClockScale) && !float.IsInfinity(nativeClockScale) && nativeClockScale > 0.01f)
                ? nativeClockScale
                : 1.0f;
            if (!videoUsesUnscaledClock)
            {
                float safeUnity = (!float.IsNaN(unityTimeScale) && !float.IsInfinity(unityTimeScale) && unityTimeScale > 0.01f)
                    ? unityTimeScale
                    : 1.0f;
                denom *= safeUnity;
            }
            if (float.IsNaN(denom) || float.IsInfinity(denom) || denom <= 0.01f)
            {
                denom = 1.0f;
            }
            float compensated = safeUser / denom;
            if (compensated < 0.05f) return 0.05f;
            if (compensated > 4.0f) return 4.0f;
            return compensated;
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else if (c >= ' ') sb.Append(c);
            }
            return sb.ToString();
        }

        private static int FindJsonValueStart(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int keyIdx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIdx < 0) return -1;
            int colon = json.IndexOf(':', keyIdx + pattern.Length);
            if (colon < 0) return -1;
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return i < json.Length ? i : -1;
        }

        private static bool TryReadJsonBool(string json, string key, out bool value)
        {
            value = false;
            int i = FindJsonValueStart(json, key);
            if (i < 0) return false;
            if (i + 4 <= json.Length && string.Compare(json, i, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }
            if (i + 5 <= json.Length && string.Compare(json, i, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = false;
                return true;
            }
            return false;
        }

        private static bool TryReadJsonFloat(string json, string key, out float value)
        {
            value = 0f;
            int i = FindJsonValueStart(json, key);
            if (i < 0) return false;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '+' || json[end] == '.'))
            {
                end++;
            }
            if (end <= i) return false;
            return float.TryParse(json.Substring(i, end - i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadJsonLong(string json, string key, out long value)
        {
            value = 0L;
            int i = FindJsonValueStart(json, key);
            if (i < 0) return false;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            {
                end++;
            }
            if (end <= i) return false;
            return long.TryParse(json.Substring(i, end - i), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadJsonString(string json, string key, out string value)
        {
            value = "";
            int i = FindJsonValueStart(json, key);
            if (i < 0 || json[i] != '"') return false;
            i++;
            var sb = new System.Text.StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c == '\\' && i < json.Length)
                {
                    char esc = json[i++];
                    if (esc == '"' || esc == '\\' || esc == '/') sb.Append(esc);
                    else if (esc == 'n') sb.Append('\n');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == 't') sb.Append('\t');
                    else sb.Append(esc);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return false;
        }

        internal struct XsvidFrameEntry
        {
            public long Offset;
            public int Length;
        }

        internal sealed class XsvidHeader
        {
            public int Width;
            public int Height;
            public int FpsMilli;
            public int FrameCount;
            public long AudioOffset;
            public int AudioLength;
            public XsvidFrameEntry[] Frames;
        }

        internal static bool TryParseXsvidIndex(Stream stream, out XsvidHeader header, out string error)
        {
            header = null;
            error = "";
            if (stream == null || !stream.CanRead || !stream.CanSeek)
            {
                error = "流不可读取或不可定位";
                return false;
            }
            try
            {
                long fileLen = stream.Length;
                if (fileLen < 40)
                {
                    error = "文件长度不足 40 字节头";
                    return false;
                }
                stream.Position = 0;
                byte[] hdr = ReadExactBytes(stream, 40);
                if (hdr == null)
                {
                    error = "读取文件头失败";
                    return false;
                }
                if (hdr[0] != (byte)'X' || hdr[1] != (byte)'S' || hdr[2] != (byte)'V' || hdr[3] != (byte)'D')
                {
                    error = "无效的 XSVD 魔数标识";
                    return false;
                }
                uint version = ReadUInt32LE(hdr, 4);
                uint width = ReadUInt32LE(hdr, 8);
                uint height = ReadUInt32LE(hdr, 12);
                uint fpsMilli = ReadUInt32LE(hdr, 16);
                uint frameCount = ReadUInt32LE(hdr, 20);
                ulong audioOffset = ReadUInt64LE(hdr, 24);
                uint audioLength = ReadUInt32LE(hdr, 32);

                if (version != 1)
                {
                    error = "不支持的 XSVD 版本: " + version;
                    return false;
                }
                if (width < 2 || width > 2048 || height < 2 || height > 2048)
                {
                    error = string.Format("分辨率超出范围: {0}x{1}", width, height);
                    return false;
                }
                if (fpsMilli < 1000 || fpsMilli > 60000)
                {
                    error = "帧率超出范围: " + fpsMilli;
                    return false;
                }
                if (frameCount < 1 || frameCount > 3600)
                {
                    error = "帧数超出范围: " + frameCount;
                    return false;
                }
                if (audioLength > 64u * 1024u * 1024u)
                {
                    error = "内嵌伴音超出大小限制";
                    return false;
                }
                if (audioLength > 0)
                {
                    if (audioOffset < 40 || audioOffset > (ulong)fileLen || (ulong)audioLength > (ulong)fileLen - audioOffset)
                    {
                        error = "伴音区间越界";
                        return false;
                    }
                }

                int indexBytes = (int)frameCount * 12;
                if (40L + indexBytes > fileLen)
                {
                    error = "帧索引表被截断";
                    return false;
                }
                byte[] idxBuf = ReadExactBytes(stream, indexBytes);
                if (idxBuf == null)
                {
                    error = "读取帧索引表失败";
                    return false;
                }

                var frames = new XsvidFrameEntry[frameCount];
                for (int i = 0; i < (int)frameCount; i++)
                {
                    int off = i * 12;
                    ulong fOff = ReadUInt64LE(idxBuf, off);
                    uint fLen = ReadUInt32LE(idxBuf, off + 8);
                    if (fLen < 16 || fLen > 4u * 1024u * 1024u || fOff < (ulong)(40 + indexBytes) || fOff > (ulong)fileLen || (ulong)fLen > (ulong)fileLen - fOff)
                    {
                        error = string.Format("第 {0} 帧偏移或长度越界", i);
                        return false;
                    }
                    frames[i] = new XsvidFrameEntry { Offset = (long)fOff, Length = (int)fLen };
                }

                header = new XsvidHeader
                {
                    Width = (int)width,
                    Height = (int)height,
                    FpsMilli = (int)fpsMilli,
                    FrameCount = (int)frameCount,
                    AudioOffset = (long)audioOffset,
                    AudioLength = (int)audioLength,
                    Frames = frames,
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static int ComputeMotionStreamFrameIndex(double elapsedSeconds, int fpsMilli, float speed, int frameCount)
        {
            if (frameCount <= 1 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0.0)
            {
                return 0;
            }
            double fps = (fpsMilli >= 1000 && fpsMilli <= 60000) ? (fpsMilli / 1000.0) : 18.0;
            double spd = (!float.IsNaN(speed) && !float.IsInfinity(speed) && speed > 0.05f) ? speed : 1.0;
            long totalFrames = (long)Math.Floor(elapsedSeconds * fps * spd);
            if (totalFrames <= 0) return 0;
            return (int)(totalFrames % frameCount);
        }

        private static byte[] ReadExactBytes(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n <= 0) return null;
                read += n;
            }
            return buf;
        }

        private static uint ReadUInt32LE(byte[] b, int offset)
        {
            return (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
        }

        private static ulong ReadUInt64LE(byte[] b, int offset)
        {
            uint lo = ReadUInt32LE(b, offset);
            uint hi = ReadUInt32LE(b, offset + 4);
            return ((ulong)hi << 32) | lo;
        }
    }
}
