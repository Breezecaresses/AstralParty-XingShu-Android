using System;
using System.Collections.Generic;
using System.Text;
using XingShuRuntime.Utilities;

namespace XingShu.Tests
{
    public static class CustomLobbyBgTests
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }

        private static void AssertClose(float actual, float expected, string message)
        {
            if (Math.Abs(actual - expected) > 0.0005f)
            {
                throw new InvalidOperationException(
                    string.Format("Assertion failed: {0} (expected={1}, actual={2})", message, expected, actual));
            }
        }

        private static void TestClassifyAndKindLabel()
        {
            Assert(CustomLobbyBgPolicy.ClassifyExtension("clip.mp4") == LobbyBgMediaKind.Video, ".mp4 -> Video");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("CLIP.WEBM") == LobbyBgMediaKind.Video, ".WEBM -> Video");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("demo.mov") == LobbyBgMediaKind.Video, ".mov -> Video");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("demo.m4v") == LobbyBgMediaKind.Video, ".m4v -> Video");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("demo.wmv") == LobbyBgMediaKind.Video, ".wmv -> Video");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("bg.png") == LobbyBgMediaKind.Image, ".png -> Image");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("bg.jpg") == LobbyBgMediaKind.Image, ".jpg -> Image");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("bg.jpeg") == LobbyBgMediaKind.Image, ".jpeg -> Image");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("bg.webp") == LobbyBgMediaKind.Image, ".webp -> Image");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("bg.BMP") == LobbyBgMediaKind.Image, ".BMP -> Image");

            byte[] webpHeader = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x20, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 };
            byte[] heicHeader = new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 };
            byte[] pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
            Assert(CustomLobbyBgPolicy.DescribeImageHeader(webpHeader) == "WebP", "detect WebP");
            Assert(CustomLobbyBgPolicy.IsLikelyUnsupportedByUnityLoadImage(webpHeader), "WebP needs fallback");
            Assert(CustomLobbyBgPolicy.DescribeImageHeader(heicHeader) == "HEIF/AVIF", "detect HEIC");
            Assert(CustomLobbyBgPolicy.IsLikelyUnsupportedByUnityLoadImage(heicHeader), "HEIC needs fallback");
            Assert(CustomLobbyBgPolicy.DescribeImageHeader(pngHeader) == "PNG", "detect PNG");
            Assert(!CustomLobbyBgPolicy.IsLikelyUnsupportedByUnityLoadImage(pngHeader), "PNG native");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("movie.usm") == LobbyBgMediaKind.CriUsm, ".usm -> CriUsm");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("vkv.bundle") == LobbyBgMediaKind.UnityBundle, ".bundle -> UnityBundle");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("loop.xsvid") == LobbyBgMediaKind.MotionStream, ".xsvid -> MotionStream");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("readme.txt") == LobbyBgMediaKind.Unknown, ".txt -> Unknown");
            Assert(CustomLobbyBgPolicy.ClassifyExtension("") == LobbyBgMediaKind.Unknown, "empty -> Unknown");
            Assert(CustomLobbyBgPolicy.ClassifyExtension(null) == LobbyBgMediaKind.Unknown, "null -> Unknown");

            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.Video) == "视频", "Video label");
            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.Image) == "静态图", "Image label");
            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.CriUsm) == "USM 动效", "CriUsm label");
            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.UnityBundle) == "VKVLoop 包", "Bundle label");
            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.MotionStream) == "动态视频", "MotionStream label");
            Assert(CustomLobbyBgPolicy.KindLabel(LobbyBgMediaKind.Unknown) == "未知", "Unknown label");
        }

        private static void TestSortMediaFiles()
        {
            List<string> empty = CustomLobbyBgPolicy.SortMediaFiles(null);
            Assert(empty.Count == 0, "null list -> empty");

            List<string> sorted = CustomLobbyBgPolicy.SortMediaFiles(new[]
            {
                "",
                null,
                @"C:\dir\.hidden.mp4",
                @"C:\dir\~temp.png",
                @"C:\dir\notes.txt",
                @"C:\dir\zeta.mp4",
                @"C:\dir\alpha.PNG",
                @"C:\dir\alpha.PNG",
                @"C:\dir\beta.usm",
                @"C:\dir\gamma.bundle",
            });

            Assert(sorted.Count == 4, "filtered supported unique files count == 4");
            Assert(sorted[0].EndsWith("alpha.PNG"), "index 0 alpha.PNG");
            Assert(sorted[1].EndsWith("beta.usm"), "index 1 beta.usm");
            Assert(sorted[2].EndsWith("gamma.bundle"), "index 2 gamma.bundle");
            Assert(sorted[3].EndsWith("zeta.mp4"), "index 3 zeta.mp4");
        }

        private static void TestComputeRenderTextureSize()
        {
            int dw, dh;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(0, 0, false, out dw, out dh);
            Assert(dw == 1920 && dh == 1080, "desktop fallback 1920x1080");

            int mw, mh;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(-1, 500, true, out mw, out mh);
            Assert(mw == 1280 && mh == 720, "mobile fallback 1280x720");

            int pcW, pcH;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(2560, 1440, false, out pcW, out pcH);
            Assert(pcW == 2560 && pcH == 1440, "desktop preserves <=4K dimensions");

            int capW, capH;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(7680, 4320, false, out capW, out capH);
            Assert(capW == 3840 && capH == 2160, "desktop caps 8K to 3840x2160");

            int mobCapW, mobCapH;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(2560, 1440, true, out mobCapW, out mobCapH);
            Assert(mobCapW == 1920 && mobCapH == 1080, "mobile caps 1440p to 1920x1080");

            int evenW, evenH;
            CustomLobbyBgPolicy.ComputeRenderTextureSize(3, 3, false, out evenW, out evenH);
            Assert(evenW % 2 == 0 && evenH % 2 == 0 && evenW >= 2 && evenH >= 2, "even positive sizes");
        }

        private static void TestIndexSelection()
        {
            Assert(CustomLobbyBgPolicy.PickNextIndex(0, 0, 1) == -1, "empty next -> -1");
            Assert(CustomLobbyBgPolicy.PickNextIndex(-1, 3, 1) == 0, "unselected +1 -> 0");
            Assert(CustomLobbyBgPolicy.PickNextIndex(-1, 3, -1) == 2, "unselected -1 -> last");
            Assert(CustomLobbyBgPolicy.PickNextIndex(2, 3, 1) == 0, "wrap forward");
            Assert(CustomLobbyBgPolicy.PickNextIndex(0, 3, -1) == 2, "wrap backward");

            Assert(CustomLobbyBgPolicy.PickRandomIndex(0, 0, _ => 0) == -1, "empty random -> -1");
            Assert(CustomLobbyBgPolicy.PickRandomIndex(0, 1, _ => 0) == 0, "single random -> 0");

            for (int current = 0; current < 4; current++)
            {
                for (int r = 0; r < 3; r++)
                {
                    int picked = CustomLobbyBgPolicy.PickRandomIndex(current, 4, _ => r);
                    Assert(picked >= 0 && picked < 4 && picked != current, "random avoids repeating current");
                }
            }
        }

        private static void TestClampersAndFillModeAndUri()
        {
            AssertClose(CustomLobbyBgPolicy.ClampBrightness(float.NaN), CustomLobbyBgPolicy.DefaultBrightness, "brightness NaN");
            AssertClose(CustomLobbyBgPolicy.ClampBrightness(0.05f), CustomLobbyBgPolicy.MinBrightness, "brightness min");
            AssertClose(CustomLobbyBgPolicy.ClampBrightness(2.5f), CustomLobbyBgPolicy.MaxBrightness, "brightness max");
            AssertClose(CustomLobbyBgPolicy.ClampBrightness(0.75f), 0.75f, "brightness mid");

            AssertClose(CustomLobbyBgPolicy.ClampSpeed(float.PositiveInfinity), CustomLobbyBgPolicy.DefaultSpeed, "speed inf");
            AssertClose(CustomLobbyBgPolicy.ClampSpeed(0.1f), CustomLobbyBgPolicy.MinSpeed, "speed min");
            AssertClose(CustomLobbyBgPolicy.ClampSpeed(5.0f), CustomLobbyBgPolicy.MaxSpeed, "speed max");
            AssertClose(CustomLobbyBgPolicy.ClampSpeed(1.25f), 1.25f, "speed mid");

            AssertClose(CustomLobbyBgPolicy.ClampVolume(float.NegativeInfinity), CustomLobbyBgPolicy.DefaultVolume, "vol -inf");
            AssertClose(CustomLobbyBgPolicy.ClampVolume(-1f), CustomLobbyBgPolicy.MinVolume, "vol min");
            AssertClose(CustomLobbyBgPolicy.ClampVolume(3f), CustomLobbyBgPolicy.MaxVolume, "vol max");
            AssertClose(CustomLobbyBgPolicy.ClampVolume(0.6f), 0.6f, "vol mid");

            Assert(CustomLobbyBgPolicy.NormalizeFillMode(0) == LobbyBgFillMode.Cover, "fill 0 -> Cover");
            Assert(CustomLobbyBgPolicy.NormalizeFillMode(1) == LobbyBgFillMode.Contain, "fill 1 -> Contain");
            Assert(CustomLobbyBgPolicy.NormalizeFillMode(2) == LobbyBgFillMode.Stretch, "fill 2 -> Stretch");
            Assert(CustomLobbyBgPolicy.NormalizeFillMode(99) == LobbyBgFillMode.Cover, "fill invalid -> Cover");

            Assert(CustomLobbyBgPolicy.FillModeLabel(LobbyBgFillMode.Cover) == "等比铺满", "Cover label");
            Assert(CustomLobbyBgPolicy.FillModeLabel(LobbyBgFillMode.Contain) == "完整显示", "Contain label");
            Assert(CustomLobbyBgPolicy.FillModeLabel(LobbyBgFillMode.Stretch) == "拉伸填充", "Stretch label");

            Assert(CustomLobbyBgPolicy.BuildFileUri("") == "", "empty uri");
            string uri = CustomLobbyBgPolicy.BuildFileUri(@"E:\工作流\lobbybg\my video.mp4");
            Assert(uri.StartsWith("file:///") && uri.EndsWith("my%20video.mp4"), "file uri encoded: " + uri);

            Assert(CustomLobbyBgPolicy.ContainsNonAsciiOrSpecial("video_「新宿决战」雨爱雄_0.mp4"), "CJK/bracket flagged");
            Assert(!CustomLobbyBgPolicy.ContainsNonAsciiOrSpecial("bg_01.mp4"), "plain ASCII not flagged");
            string androidStage0 = CustomLobbyBgPolicy.BuildVideoUrl("/storage/emulated/0/Android/data/com.feimo.astralparty/files/lobbybg/video_「新宿决战」雨爱雄_0.mp4", true, 0);
            Assert(androidStage0 == "/storage/emulated/0/Android/data/com.feimo.astralparty/files/lobbybg/video_「新宿决战」雨爱雄_0.mp4", "android stage 0 uses raw path");
            string androidStage1 = CustomLobbyBgPolicy.BuildVideoUrl("/storage/emulated/0/lobbybg/a.mp4", true, 1);
            Assert(androidStage1.StartsWith("file:///") && androidStage1.EndsWith("/storage/emulated/0/lobbybg/a.mp4"), "android stage 1 uses file uri: " + androidStage1);
            string asciiCache = CustomLobbyBgPolicy.BuildAndroidAsciiCacheFileName("/storage/emulated/0/lobbybg/video_「新宿决战」雨爱雄_0.mp4");
            Assert(asciiCache.StartsWith("xingshu_lobbybg_") && asciiCache.EndsWith(".mp4") && !CustomLobbyBgPolicy.ContainsNonAsciiOrSpecial(asciiCache), "ascii cache name: " + asciiCache);
        }

        private static void TestAudioAndVanillaBgmPolicy()
        {
            Assert(CustomLobbyBgPolicy.VideoAudioOutputModeDirectValue == 2, "VideoAudioOutputMode.Direct must be 2");
            Assert(!CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(false, true, true, false), "feature disabled -> keep vanilla BGM");
            Assert(!CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(true, false, true, true), "toggle disabled -> keep vanilla BGM");
            Assert(!CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(true, true, false, false), "in battle / away -> restore vanilla BGM");
            Assert(CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(true, true, true, false), "home visible -> mute vanilla BGM");
            Assert(CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(true, true, false, true), "sub lobby visible -> mute vanilla BGM");
            Assert(CustomLobbyBgPolicy.ShouldMuteVanillaLobbyBgm(true, true, true, true), "both visible -> mute vanilla BGM");
        }

        private static void TestPcm16Processing()
        {
            byte[] src = new byte[8 * 4]; // 8 stereo frames
            for (int f = 0; f < 8; f++)
            {
                short l = (short)(2000 * (f + 1));
                short r = (short)(-2000 * (f + 1));
                int o = f * 4;
                src[o] = (byte)(l & 0xFF); src[o + 1] = (byte)((l >> 8) & 0xFF);
                src[o + 2] = (byte)(r & 0xFF); src[o + 3] = (byte)((r >> 8) & 0xFF);
            }

            byte[] dst = new byte[src.Length * 3];
            double frac = 0.0;
            int outLen;

            // Zero gain -> all zero silent bytes
            CustomLobbyBgPolicy.ProcessPcm16Chunk(src, src.Length, 2, 1.0f, 0f, 0f, ref frac, dst, out outLen);
            Assert(outLen == src.Length, "1.0x length preserved");
            for (int i = 0; i < outLen; i++)
            {
                Assert(dst[i] == 0, "zero gain produces silent PCM byte at " + i);
            }

            // 0.5x gain halves sample values cleanly
            frac = 0.0;
            CustomLobbyBgPolicy.ProcessPcm16Chunk(src, src.Length, 2, 1.0f, 0.5f, 0.5f, ref frac, dst, out outLen);
            short halfL = (short)(dst[0] | (dst[1] << 8));
            short halfR = (short)(dst[2] | (dst[3] << 8));
            Assert(halfL == 1000 && halfR == -1000, "0.5x gain halves sample: " + halfL + "," + halfR);

            // 2.0x speed halves frame count (~4 frames = 16 bytes)
            frac = 0.0;
            CustomLobbyBgPolicy.ProcessPcm16Chunk(src, src.Length, 2, 2.0f, 1.0f, 1.0f, ref frac, dst, out outLen);
            Assert(outLen == 16, "2.0x speed halves output bytes: " + outLen);

            // 0.5x speed doubles frame count (~16 frames = 64 bytes)
            frac = 0.0;
            CustomLobbyBgPolicy.ProcessPcm16Chunk(src, src.Length, 2, 0.5f, 1.0f, 1.0f, ref frac, dst, out outLen);
            Assert(outLen == 64, "0.5x speed doubles output bytes: " + outLen);
        }

        private static void TestWindowsAudioProtocol()
        {
            string zhPath = @"E:\工作流\大厅素材\星引擎_40301496754-1-192.mp4";
            string cmd = CustomLobbyBgPolicy.BuildWindowsAudioCommandPayload(
                generation: 3,
                stop: false,
                quit: false,
                fullPath: zhPath,
                muted: false,
                volume: 1.5f,   // should clamp to 1.0
                speed: 0.25f,   // should clamp to 0.5
                paused: false,
                seekSeq: 2,
                seekSeconds: 15.25);

            string expectedB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(zhPath));
            Assert(cmd.Contains("GEN=3\n"), "cmd GEN");
            Assert(cmd.Contains("STOP=0\n"), "cmd STOP");
            Assert(cmd.Contains("QUIT=0\n"), "cmd QUIT");
            Assert(cmd.Contains("PATH_B64=" + expectedB64 + "\n"), "cmd Chinese UTF-8 base64");
            Assert(cmd.Contains("MUTED=0\n"), "cmd MUTED=0");
            Assert(cmd.Contains("VOL=1.0000\n"), "cmd clamped volume");
            Assert(cmd.Contains("SPEED=0.5000\n"), "cmd clamped speed");
            Assert(cmd.Contains("PAUSED=0\n"), "cmd PAUSED=0");
            Assert(cmd.Contains("SEEK_SEQ=2\n"), "cmd SEEK_SEQ");
            Assert(cmd.Contains("SEEK_SEC=15.250\n"), "cmd SEEK_SEC");

            string stopQuit = CustomLobbyBgPolicy.BuildWindowsAudioCommandPayload(
                generation: 4, stop: true, quit: true, fullPath: null,
                muted: true, volume: -0.5f, speed: 5.0f, paused: true, seekSeq: 0, seekSeconds: -3.0);
            Assert(stopQuit.Contains("STOP=1\n") && stopQuit.Contains("QUIT=1\n"), "stop/quit flags");
            Assert(stopQuit.Contains("PATH_B64=\n"), "empty path base64");
            Assert(stopQuit.Contains("VOL=0.0000\n") && stopQuit.Contains("SPEED=2.0000\n"), "clamped extremes");
            Assert(stopQuit.Contains("SEEK_SEC=0.000\n"), "negative seek clamped to 0");

            int gen;
            bool opened;
            bool hasAudio;
            double pos;
            double dur;
            string err;

            Assert(CustomLobbyBgPolicy.TryParseWindowsAudioState(
                "3|1|1|15.125|109.000|", out gen, out opened, out hasAudio, out pos, out dur, out err), "parse playing state");
            Assert(gen == 3 && opened && hasAudio && Math.Abs(pos - 15.125) < 0.001 && Math.Abs(dur - 109.0) < 0.001 && err == "",
                "playing state fields");

            Assert(CustomLobbyBgPolicy.TryParseWindowsAudioState(
                "5|0|0|0.000|0.000|Codec missing | detail error", out gen, out opened, out hasAudio, out pos, out dur, out err), "parse error with pipe");
            Assert(gen == 5 && !opened && !hasAudio && err == "Codec missing | detail error", "error preserves pipe and spaces");

            Assert(!CustomLobbyBgPolicy.TryParseWindowsAudioState("", out gen, out opened, out hasAudio, out pos, out dur, out err), "empty invalid");
            Assert(!CustomLobbyBgPolicy.TryParseWindowsAudioState("bad|1|1|0|0|", out gen, out opened, out hasAudio, out pos, out dur, out err), "non-int gen invalid");
            Assert(!CustomLobbyBgPolicy.TryParseWindowsAudioState("1|1|1|0", out gen, out opened, out hasAudio, out pos, out dur, out err), "too few fields invalid");
        }

        private static uint ReadUInt32LE(byte[] buf, int offset)
        {
            return (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
        }

        private static void TestWindowsShortcutAndVbsBuilders()
        {
            string args = "//B //Nologo \"C:\\Temp\\launch 1.vbs\"";
            byte[] lnk = CustomLobbyBgPolicy.BuildWindowsShortcutLnk("%SystemRoot%\\System32\\wscript.exe", args);
            byte[] argsBytes = Encoding.Unicode.GetBytes(args);
            int expectedLen = 76 + 2 + argsBytes.Length + 788 + 4;
            Assert(lnk.Length == expectedLen, "lnk exact byte length: " + lnk.Length + " vs " + expectedLen);
            Assert(ReadUInt32LE(lnk, 0) == 0x4Cu, "lnk HeaderSize=0x4C");

            byte[] clsid = new byte[16];
            Array.Copy(lnk, 4, clsid, 0, 16);
            Assert(new Guid(clsid) == new Guid("00021401-0000-0000-C000-000000000046"), "lnk ShellLink CLSID");
            Assert(ReadUInt32LE(lnk, 20) == 0x2A0u, "lnk LinkFlags HasArguments|IsUnicode|HasExpString");
            Assert(ReadUInt32LE(lnk, 24) == 0x20u, "lnk FileAttributes ARCHIVE");
            Assert(ReadUInt32LE(lnk, 60) == 7u, "lnk ShowCommand SW_SHOWMINNOACTIVE");

            ushort argChars = (ushort)(lnk[76] | (lnk[77] << 8));
            Assert(argChars == args.Length, "lnk arg char count");
            Assert(Encoding.Unicode.GetString(lnk, 78, argsBytes.Length) == args, "lnk UTF-16LE args");

            int expOffset = 78 + argsBytes.Length;
            Assert(ReadUInt32LE(lnk, expOffset) == 788u, "lnk EnvDataBlock Size=788");
            Assert(ReadUInt32LE(lnk, expOffset + 4) == 0xA0000001u, "lnk EnvDataBlock sig=0xA0000001");
            Assert(ReadUInt32LE(lnk, lnk.Length - 4) == 0u, "lnk terminal 4 zero bytes");

            string vbs = CustomLobbyBgPolicy.BuildWindowsVbsLauncher(
                @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe",
                @"C:\Temp\xingshu_lobby_audio_v3.ps1",
                12345,
                @"C:\Temp\a.cmd",
                @"C:\Temp\a.state",
                @"C:\Temp\a.vbs",
                @"C:\Temp\a.lnk");
            Assert(vbs.StartsWith("CreateObject(\"WScript.Shell\").Run \"\"\"C:\\WINDOWS"), "vbs header");
            Assert(vbs.Contains("-WindowStyle Hidden -STA -File \"\"C:\\Temp\\xingshu_lobby_audio_v3.ps1\"\" -ParentPid 12345"), "vbs ps1+pid");
            Assert(vbs.Contains("-VbsFile \"\"C:\\Temp\\a.vbs\"\" -LnkFile \"\"C:\\Temp\\a.lnk\"\""), "vbs cleanup args");
            Assert(vbs.TrimEnd().EndsWith("\", 0, False"), "vbs hidden async suffix");
        }

        private static void TestPlaybackPositionExtrapolation()
        {
            double p0 = CustomLobbyBgPolicy.EstimatePlaybackPosition(10.0, 0.0, 1.0f, 109.0);
            Assert(Math.Abs(p0 - 10.0) < 0.001, "0 elapsed holds position");

            double p1 = CustomLobbyBgPolicy.EstimatePlaybackPosition(10.0, 0.5, 1.0f, 109.0);
            Assert(Math.Abs(p1 - 10.5) < 0.001, "1.0x advance: " + p1);

            double p2 = CustomLobbyBgPolicy.EstimatePlaybackPosition(10.0, 2.0, 1.5f, 109.0);
            Assert(Math.Abs(p2 - 13.0) < 0.001, "1.5x advance: " + p2);

            double pWrap = CustomLobbyBgPolicy.EstimatePlaybackPosition(108.5, 1.0, 1.0f, 109.0);
            Assert(Math.Abs(pWrap - 0.5) < 0.001, "loop wrap: " + pWrap);

            double pGuard = CustomLobbyBgPolicy.EstimatePlaybackPosition(double.NaN, -3.0, 5.0f, -1.0);
            Assert(Math.Abs(pGuard - 0.0) < 0.001, "NaN/negative guard");
        }

        private static void TestSidecarConfigJsonRoundtrip()
        {
            var src = new CustomLobbyBgPolicy.LobbyBgSidecarConfig
            {
                Enabled = true,
                SelectedFile = "my_bg\"01\\test.mp4",
                SyncSubPanels = false,
                RandomOnLobby = true,
                FillMode = LobbyBgFillMode.Contain,
                Brightness = 0.725f,
                Speed = 1.25f,
                Muted = false,
                Volume = 0.65f,
                MuteVanillaBgm = true,
                UpdatedAt = 1795612000123L,
            };
            string json = CustomLobbyBgPolicy.SerializeSidecarJson(src);
            CustomLobbyBgPolicy.LobbyBgSidecarConfig parsed;
            Assert(CustomLobbyBgPolicy.TryParseSidecarJson(json, out parsed), "parse sidecar json");
            Assert(parsed.Enabled == true && parsed.SelectedFile == src.SelectedFile, "sidecar enabled/file");
            Assert(parsed.SyncSubPanels == false && parsed.RandomOnLobby == true, "sidecar booleans");
            Assert(parsed.FillMode == LobbyBgFillMode.Contain, "sidecar fill mode");
            Assert(Math.Abs(parsed.Brightness - 0.725f) < 0.001f && Math.Abs(parsed.Speed - 1.25f) < 0.001f, "sidecar floats");
            Assert(!parsed.Muted && Math.Abs(parsed.Volume - 0.65f) < 0.001f && parsed.MuteVanillaBgm, "sidecar audio");
            Assert(parsed.UpdatedAt == 1795612000123L, "sidecar updatedAt");

            Assert(!CustomLobbyBgPolicy.TryParseSidecarJson("{}", out parsed), "empty object rejected");
        }

        private static void TestNativeClockSpeedCompensation()
        {
            float s1 = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(1.0f, 1.0f, 1.0f, true);
            Assert(Math.Abs(s1 - 1.0f) < 0.001f, "1x native -> 1.0f");

            float s2 = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(1.0f, 2.0f, 1.0f, true);
            Assert(Math.Abs(s2 - 0.5f) < 0.001f, "2x native -> 0.5f");

            float s5 = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(1.5f, 5.0f, 1.0f, true);
            Assert(Math.Abs(s5 - 0.3f) < 0.001f, "1.5x user at 5x native -> 0.3f");

            float s10Min = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(0.5f, 10.0f, 1.0f, true);
            Assert(Math.Abs(s10Min - 0.05f) < 0.001f, "0.5x user at 10x native clamped >= 0.05f");

            float scaledManaged = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(1.0f, 1.0f, 2.5f, false);
            Assert(Math.Abs(scaledManaged - 0.4f) < 0.001f, "2.5x unityTimeScale fallback -> 0.4f");

            float nanGuard = CustomLobbyBgPolicy.ComputeCompensatedPlaybackSpeed(1.25f, float.NaN, 0f, false);
            Assert(Math.Abs(nanGuard - 1.25f) < 0.001f, "NaN/0 clock guard preserves user speed");
        }

        private static void WriteU32(System.IO.MemoryStream ms, uint v)
        {
            ms.WriteByte((byte)(v & 0xff));
            ms.WriteByte((byte)((v >> 8) & 0xff));
            ms.WriteByte((byte)((v >> 16) & 0xff));
            ms.WriteByte((byte)((v >> 24) & 0xff));
        }

        private static void WriteU64(System.IO.MemoryStream ms, ulong v)
        {
            WriteU32(ms, (uint)(v & 0xffffffffu));
            WriteU32(ms, (uint)(v >> 32));
        }

        private static void TestXsvidIndexParserAndFrameClock()
        {
            byte[] f0 = new byte[24];
            f0[0] = 0xFF; f0[1] = 0xD8; f0[23] = 0xD9;
            byte[] f1 = new byte[32];
            f1[0] = 0xFF; f1[1] = 0xD8; f1[31] = 0xD9;
            byte[] aud = new byte[] { 1, 2, 3, 4, 5, 6 };

            long f0Off = 40 + 2 * 12;
            long f1Off = f0Off + f0.Length;
            long audOff = f1Off + f1.Length;

            using (var ms = new System.IO.MemoryStream())
            {
                ms.WriteByte((byte)'X'); ms.WriteByte((byte)'S'); ms.WriteByte((byte)'V'); ms.WriteByte((byte)'D');
                WriteU32(ms, 1u);
                WriteU32(ms, 960u);
                WriteU32(ms, 540u);
                WriteU32(ms, 18000u);
                WriteU32(ms, 2u);
                WriteU64(ms, (ulong)audOff);
                WriteU32(ms, (uint)aud.Length);
                WriteU32(ms, 0u);
                WriteU64(ms, (ulong)f0Off); WriteU32(ms, (uint)f0.Length);
                WriteU64(ms, (ulong)f1Off); WriteU32(ms, (uint)f1.Length);
                ms.Write(f0, 0, f0.Length);
                ms.Write(f1, 0, f1.Length);
                ms.Write(aud, 0, aud.Length);

                CustomLobbyBgPolicy.XsvidHeader hdr;
                string err;
                Assert(CustomLobbyBgPolicy.TryParseXsvidIndex(ms, out hdr, out err), "valid xsvid parses: " + err);
                Assert(hdr.Width == 960 && hdr.Height == 540 && hdr.FpsMilli == 18000, "xsvid dimensions & fps");
                Assert(hdr.FrameCount == 2 && hdr.Frames[0].Offset == f0Off && hdr.Frames[1].Length == f1.Length, "xsvid frame table");
                Assert(hdr.AudioOffset == audOff && hdr.AudioLength == aud.Length, "xsvid audio bounds");
            }

            using (var bad = new System.IO.MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5 }))
            {
                CustomLobbyBgPolicy.XsvidHeader hdr;
                string err;
                Assert(!CustomLobbyBgPolicy.TryParseXsvidIndex(bad, out hdr, out err), "short stream rejected");
            }

            Assert(CustomLobbyBgPolicy.ComputeMotionStreamFrameIndex(0.0, 20000, 1.0f, 10) == 0, "t=0 -> frame 0");
            Assert(CustomLobbyBgPolicy.ComputeMotionStreamFrameIndex(0.25, 20000, 1.0f, 10) == 5, "t=0.25@20fps -> frame 5");
            Assert(CustomLobbyBgPolicy.ComputeMotionStreamFrameIndex(0.55, 20000, 1.0f, 10) == 1, "loop wraps -> frame 1");
        }

        public static int Main()
        {
            TestClassifyAndKindLabel();
            TestSortMediaFiles();
            TestComputeRenderTextureSize();
            TestIndexSelection();
            TestClampersAndFillModeAndUri();
            TestAudioAndVanillaBgmPolicy();
            TestPcm16Processing();
            TestWindowsAudioProtocol();
            TestWindowsShortcutAndVbsBuilders();
            TestPlaybackPositionExtrapolation();
            TestSidecarConfigJsonRoundtrip();
            TestNativeClockSpeedCompensation();
            TestXsvidIndexParserAndFrameClock();
            Console.WriteLine("CustomLobbyBgTests PASS");
            return 0;
        }
    }
}
