#if !XINGSHU_ANDROID_PREVIEW
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using XingShu.Runtime;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Windows-native companion audio player for CustomLobbyBackground.
    /// Zero native imports, zero COM marshaling, zero Process.Start calls (which IL2CPP stubs on Windows desktop).
    /// Launches a hidden STA Windows PowerShell helper hosting WPF System.Windows.Media.MediaPlayer via
    /// main-thread UnityEngine.Application.OpenURL on a pure-byte .lnk shortcut targeting %SystemRoot%\System32\wscript.exe,
    /// using FileShare.ReadWrite I/O, continuous 200ms state polling + smooth position extrapolation, and clean auto-cleanup.
    /// </summary>
    internal sealed class WindowsLobbyAudioPlayer : IDisposable
    {
        private const string ScriptFileName = "xingshu_lobby_audio_v4.ps1";
        private const int MaxLaunchAttempts = 4;
        private const double LaunchRetryCooldownSeconds = 8.0;
        private const double HelperHeartbeatTimeoutSeconds = 6.0;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private const string HelperScriptContent = @"param(
    [int]$ParentPid = 0,
    [string]$CmdFile = """",
    [string]$StateFile = """",
    [string]$VbsFile = """",
    [string]$LnkFile = """"
)

$ErrorActionPreference = 'SilentlyContinue'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$dispatcher = [System.Windows.Threading.Dispatcher]::CurrentDispatcher
$mp = New-Object System.Windows.Media.MediaPlayer
$utf8 = New-Object System.Text.UTF8Encoding($false)

$script:opened = $false
$script:hasOpenedOnce = $false
$script:hasAudio = $false
$script:isPlaying = $false
$script:appliedSpeed = -1.0
$script:duration = 0.0
$script:curPath = """"
$script:curGen = -1
$script:curMuted = $true
$script:curVol = 0.35
$script:curSpeed = 1.0
$script:curPaused = $true
$script:lastSeekSeq = -1
$script:lastCmdText = """"
$script:lastStateWrite = [DateTime]::MinValue
$script:pendingSeek = -1.0
$script:lastError = """"

function Read-TextShared([string]$p) {
    $fs = New-Object System.IO.FileStream($p, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $sr = New-Object System.IO.StreamReader($fs, $utf8)
        try {
            return $sr.ReadToEnd()
        } finally {
            $sr.Dispose()
        }
    } finally {
        $fs.Dispose()
    }
}

function Write-TextShared([string]$p, [string]$content) {
    $fs = New-Object System.IO.FileStream($p, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::ReadWrite)
    try {
        $bytes = $utf8.GetBytes($content)
        $fs.Write($bytes, 0, $bytes.Length)
        $fs.Flush()
    } finally {
        $fs.Dispose()
    }
}

function Write-State([bool]$force) {
    if ([string]::IsNullOrEmpty($StateFile)) { return }
    $now = [DateTime]::UtcNow
    if (-not $force -and ($now - $script:lastStateWrite).TotalMilliseconds -lt 350) { return }
    $script:lastStateWrite = $now
    try {
        $pos = 0.0
        if ($script:opened) {
            try { $pos = $mp.Position.TotalSeconds } catch {}
        }
        $inv = [System.Globalization.CultureInfo]::InvariantCulture
        $ha = if ($script:hasAudio) { ""1"" } else { ""0"" }
        $op = if ($script:opened) { ""1"" } else { ""0"" }
        $errClean = if ($script:lastError) { ($script:lastError -replace ""[\r\n]+"", "" "").Trim() } else { """" }
        $line = ""$($script:curGen)|$op|$ha|$($pos.ToString('F3', $inv))|$($script:duration.ToString('F3', $inv))|$errClean""
        Write-TextShared $StateFile $line
    } catch {}
}

function Apply-Playback() {
    if (-not $script:opened) { return }
    try {
        $mp.IsMuted = $script:curMuted
        $mp.Volume = if ($script:curMuted) { 0.0 } else { [Math]::Max(0.0, [Math]::Min(1.0, $script:curVol)) }
        $targetSpeed = [Math]::Max(0.5, [Math]::Min(2.0, $script:curSpeed))
        if ([Math]::Abs($targetSpeed - $script:appliedSpeed) -gt 0.005) {
            $mp.SpeedRatio = $targetSpeed
            $script:appliedSpeed = $targetSpeed
        }
        if ($script:curPaused) {
            if ($script:isPlaying) {
                $mp.Pause()
                $script:isPlaying = $false
            }
        } else {
            if (-not $script:isPlaying) {
                $mp.Play()
                $script:isPlaying = $true
            }
        }
    } catch {}
}

$mp.add_MediaOpened({
    $script:opened = $true
    $script:hasAudio = $mp.HasAudio
    $script:lastError = """"
    if ($mp.NaturalDuration.HasTimeSpan) {
        $script:duration = $mp.NaturalDuration.TimeSpan.TotalSeconds
    } else {
        $script:duration = 0.0
    }
    if ($script:pendingSeek -gt 0.05) {
        try { $mp.Position = [TimeSpan]::FromSeconds($script:pendingSeek) } catch {}
        $script:pendingSeek = -1.0
    }
    Apply-Playback
    Write-State $true
})

$mp.add_MediaEnded({
    if ($script:opened -and -not $script:curPaused) {
        try {
            $mp.Position = [TimeSpan]::Zero
            $mp.Play()
            $script:isPlaying = $true
        } catch {}
    } else {
        $script:isPlaying = $false
    }
})

$mp.add_MediaFailed({
    param($s, $e)
    $script:opened = $false
    $script:hasAudio = $false
    $script:isPlaying = $false
    try {
        if ($e -and $e.ErrorException) { $script:lastError = $e.ErrorException.Message }
        else { $script:lastError = ""MediaFailed"" }
    } catch { $script:lastError = ""MediaFailed"" }
    Write-State $true
})

$script:parentCheckCounter = 0
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(50)

function Safe-DeleteFile([string]$p) {
    try {
        if (-not [string]::IsNullOrEmpty($p) -and [System.IO.File]::Exists($p)) {
            [System.IO.File]::Delete($p)
        }
    } catch {}
}

function Shutdown-Helper() {
    try { $timer.Stop() } catch {}
    if ($script:hasOpenedOnce) {
        try { $mp.Stop() } catch {}
        try { $mp.Close() } catch {}
    }
    $script:isPlaying = $false
    $script:appliedSpeed = -1.0
    Safe-DeleteFile $CmdFile
    Safe-DeleteFile $StateFile
    Safe-DeleteFile $VbsFile
    Safe-DeleteFile $LnkFile
    try { $dispatcher.InvokeShutdown() } catch {}
}

$timer.add_Tick({
    $script:parentCheckCounter++
    if ($ParentPid -gt 0 -and ($script:parentCheckCounter % 10 -eq 0)) {
        try {
            $p = [System.Diagnostics.Process]::GetProcessById($ParentPid)
            if ($null -eq $p -or $p.HasExited) {
                Shutdown-Helper
                return
            }
        } catch {
            Shutdown-Helper
            return
        }
    }

    if (-not [string]::IsNullOrEmpty($CmdFile) -and [System.IO.File]::Exists($CmdFile)) {
        try {
            $text = Read-TextShared $CmdFile
            if (-not [string]::IsNullOrEmpty($text) -and $text -ne $script:lastCmdText) {
                $map = @{}
                foreach ($rawLine in ($text -split ""`n"")) {
                    $line = $rawLine.Trim()
                    $idx = $line.IndexOf('=')
                    if ($idx -gt 0) {
                        $map[$line.Substring(0, $idx)] = $line.Substring($idx + 1)
                    }
                }
                if (-not ($map['QUIT'] -eq '1' -or $map.ContainsKey('SEEK_SEC'))) {
                    return
                }
                $script:lastCmdText = $text
                if ($map['QUIT'] -eq '1') {
                    Shutdown-Helper
                    return
                }

                $inv = [System.Globalization.CultureInfo]::InvariantCulture
                $gen = 0
                [int]::TryParse($map['GEN'], [ref]$gen) | Out-Null
                $stop = ($map['STOP'] -eq '1')
                $muted = ($map['MUTED'] -ne '0')
                $vol = 0.35
                [double]::TryParse($map['VOL'], [System.Globalization.NumberStyles]::Float, $inv, [ref]$vol) | Out-Null
                $speed = 1.0
                [double]::TryParse($map['SPEED'], [System.Globalization.NumberStyles]::Float, $inv, [ref]$speed) | Out-Null
                $paused = ($map['PAUSED'] -eq '1')
                $seekSeq = 0
                [int]::TryParse($map['SEEK_SEQ'], [ref]$seekSeq) | Out-Null
                $seekSec = 0.0
                [double]::TryParse($map['SEEK_SEC'], [System.Globalization.NumberStyles]::Float, $inv, [ref]$seekSec) | Out-Null

                $script:curMuted = $muted
                $script:curVol = $vol
                $script:curSpeed = $speed
                $script:curPaused = $paused

                if ($stop) {
                    $script:curGen = $gen
                    $script:curPath = """"
                    $script:opened = $false
                    $script:hasAudio = $false
                    $script:isPlaying = $false
                    $script:appliedSpeed = -1.0
                    $script:duration = 0.0
                    $script:lastError = """"
                    $script:pendingSeek = -1.0
                    if ($script:hasOpenedOnce) {
                        try { $mp.Stop() } catch {}
                        try { $mp.Close() } catch {}
                        $script:hasOpenedOnce = $false
                    }
                    Write-State $true
                    return
                }

                $pathB64 = $map['PATH_B64']
                $targetPath = """"
                if (-not [string]::IsNullOrEmpty($pathB64)) {
                    $targetPath = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($pathB64))
                }

                if ($gen -ne $script:curGen -or $targetPath -ne $script:curPath) {
                    $script:curGen = $gen
                    $script:curPath = $targetPath
                    $script:opened = $false
                    $script:hasAudio = $false
                    $script:isPlaying = $false
                    $script:duration = 0.0
                    $script:lastError = """"
                    $script:lastSeekSeq = $seekSeq
                    $script:pendingSeek = if ($seekSec -gt 0.05) { $seekSec } else { -1.0 }
                    if ($script:hasOpenedOnce) {
                        try { $mp.Stop() } catch {}
                        try { $mp.Close() } catch {}
                    }
                    if (-not [string]::IsNullOrEmpty($targetPath) -and [System.IO.File]::Exists($targetPath)) {
                        $script:hasOpenedOnce = $true
                        if ($script:curPaused) {
                            $mp.IsMuted = $true
                            $mp.Volume = 0.0
                        } else {
                            $mp.IsMuted = $script:curMuted
                            $mp.Volume = if ($script:curMuted) { 0.0 } else { [Math]::Max(0.0, [Math]::Min(1.0, $script:curVol)) }
                        }
                        $targetSpeed = [Math]::Max(0.5, [Math]::Min(2.0, $script:curSpeed))
                        $mp.SpeedRatio = $targetSpeed
                        $script:appliedSpeed = $targetSpeed
                        $mp.Open((New-Object System.Uri($targetPath)))
                        $mp.Play()
                        $script:isPlaying = $true
                    } else {
                        $script:lastError = ""FileNotFound""
                    }
                    Write-State $true
                } else {
                    if ($seekSeq -ne $script:lastSeekSeq) {
                        $script:lastSeekSeq = $seekSeq
                        if ($script:opened) {
                            try { $mp.Position = [TimeSpan]::FromSeconds([Math]::Max(0.0, $seekSec)) } catch {}
                        } else {
                            $script:pendingSeek = $seekSec
                        }
                    }
                    Apply-Playback
                    Write-State $true
                }
            }
        } catch {
        }
    }

    Write-State $false
})

$timer.Start()
Write-State $true
[System.Windows.Threading.Dispatcher]::Run()
";

        private readonly object _lock = new object();
        private readonly string _scriptPath;
        private readonly string _cmdPath;
        private readonly string _statePath;
        private readonly string _vbsPath;
        private readonly string _lnkPath;
        private readonly int _selfPid;

        private Thread _worker;
        private DateTime _nextLaunchAllowedUtc = DateTime.MinValue;
        private int _launchAttempts;
        private bool _launchedOnce;
        private volatile string _pendingOpenUrlPath;
        private int _pendingOpenAttempt;

        private bool _disposed;
        private bool _dirty;
        private bool _prewarmRequested;
        private bool _active;
        private string _activePath = "";
        private int _generation;
        private bool _muted = true;
        private float _volume = CustomLobbyBgPolicy.DefaultVolume;
        private float _speed = CustomLobbyBgPolicy.DefaultSpeed;
        private bool _paused;
        private int _seekSeq;
        private double _seekSeconds;

        private volatile bool _hasAudio;
        private double _statePosSeconds;
        private double _stateDurationSeconds;
        private DateTime _stateSampleUtc = DateTime.MinValue;
        private int _loggedReadyGen = -1;
        private int _loggedErrorGen = -1;

        internal WindowsLobbyAudioPlayer()
        {
            int pid = 0;
            try
            {
                pid = Process.GetCurrentProcess().Id;
            }
            catch
            {
            }
            _selfPid = pid;
            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tempDir = Path.GetTempPath();
            _scriptPath = Path.Combine(tempDir, ScriptFileName);
            _cmdPath = Path.Combine(tempDir, $"xingshu_lobby_audio_{pid}_{tag}.cmd");
            _statePath = Path.Combine(tempDir, $"xingshu_lobby_audio_{pid}_{tag}.state");
            _vbsPath = Path.Combine(tempDir, $"xingshu_lobby_audio_{pid}_{tag}.vbs");
            _lnkPath = Path.Combine(tempDir, $"xingshu_lobby_audio_{pid}_{tag}.lnk");
        }

        internal bool HasAudio => _hasAudio;

        internal double CurrentTimeSeconds
        {
            get
            {
                lock (_lock)
                {
                    if (_active && !_paused && _hasAudio && _stateSampleUtc != DateTime.MinValue)
                    {
                        double elapsed = (DateTime.UtcNow - _stateSampleUtc).TotalSeconds;
                        return CustomLobbyBgPolicy.EstimatePlaybackPosition(
                            _statePosSeconds, elapsed, _speed, _stateDurationSeconds);
                    }
                    return _statePosSeconds;
                }
            }
        }

        internal void Prewarm()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _prewarmRequested = true;
                _dirty = true;
                EnsureWorkerThreadLocked();
                Monitor.PulseAll(_lock);
            }
        }

        internal void TickMainThread()
        {
            if (_pendingOpenUrlPath == null)
            {
                return;
            }

            string pathToOpen;
            int attempt;
            lock (_lock)
            {
                pathToOpen = _pendingOpenUrlPath;
                _pendingOpenUrlPath = null;
                attempt = _pendingOpenAttempt;
                if (_disposed || string.IsNullOrEmpty(pathToOpen))
                {
                    return;
                }
                _launchedOnce = true;
            }

            try
            {
                Application.OpenURL(pathToOpen);
                LogSafe($"[WindowsLobbyAudioPlayer] 已通过 Application.OpenURL 拉起伴随音频进程: attempt={attempt}, lnk={pathToOpen}");
            }
            catch (Exception ex)
            {
                LogSafe($"[WindowsLobbyAudioPlayer] Application.OpenURL 拉起异常: {ex.GetType().Name}: {ex.Message}");
            }
        }

        internal void StartFile(string fullPath, bool muted, float volume, float speed, bool paused, double startSeconds)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                Stop();
                return;
            }
            double safeStart = startSeconds > 0.0 ? startSeconds : 0.0;
            lock (_lock)
            {
                if (_disposed) return;
                _active = true;
                _activePath = fullPath;
                _generation++;
                _muted = muted;
                _volume = CustomLobbyBgPolicy.ClampVolume(volume);
                _speed = CustomLobbyBgPolicy.ClampSpeed(speed);
                _paused = paused;
                if (safeStart > 0.05)
                {
                    _seekSeq++;
                    _seekSeconds = safeStart;
                }
                else
                {
                    _seekSeconds = 0.0;
                }
                _hasAudio = false;
                _statePosSeconds = safeStart;
                _stateDurationSeconds = 0.0;
                _stateSampleUtc = DateTime.MinValue;
                _loggedReadyGen = -1;
                _loggedErrorGen = -1;
                _dirty = true;
                EnsureWorkerThreadLocked();
                Monitor.PulseAll(_lock);
            }
        }

        internal void SetAudioParams(bool muted, float volume, float speed)
        {
            float cv = CustomLobbyBgPolicy.ClampVolume(volume);
            float cs = CustomLobbyBgPolicy.ClampSpeed(speed);
            lock (_lock)
            {
                if (_disposed) return;
                if (_muted == muted && Math.Abs(_volume - cv) < 0.0005f && Math.Abs(_speed - cs) < 0.0005f)
                {
                    return;
                }
                if (_active && !_paused && _hasAudio && _stateSampleUtc != DateTime.MinValue)
                {
                    double elapsed = (DateTime.UtcNow - _stateSampleUtc).TotalSeconds;
                    _statePosSeconds = CustomLobbyBgPolicy.EstimatePlaybackPosition(
                        _statePosSeconds, elapsed, _speed, _stateDurationSeconds);
                    _stateSampleUtc = DateTime.UtcNow;
                }
                _muted = muted;
                _volume = cv;
                _speed = cs;
                if (_active)
                {
                    _dirty = true;
                    EnsureWorkerThreadLocked();
                    Monitor.PulseAll(_lock);
                }
            }
        }

        internal void SetPaused(bool paused)
        {
            lock (_lock)
            {
                if (_disposed || _paused == paused) return;
                if (_active && _hasAudio && paused && _stateSampleUtc != DateTime.MinValue)
                {
                    double elapsed = (DateTime.UtcNow - _stateSampleUtc).TotalSeconds;
                    _statePosSeconds = CustomLobbyBgPolicy.EstimatePlaybackPosition(
                        _statePosSeconds, elapsed, _speed, _stateDurationSeconds);
                    _stateSampleUtc = DateTime.MinValue;
                }
                else if (_active && _hasAudio && !paused)
                {
                    _stateSampleUtc = DateTime.UtcNow;
                }
                _paused = paused;
                if (_active)
                {
                    _dirty = true;
                    EnsureWorkerThreadLocked();
                    Monitor.PulseAll(_lock);
                }
            }
        }

        internal void RequestSeek(double seconds)
        {
            double clamped = seconds > 0.0 ? seconds : 0.0;
            lock (_lock)
            {
                if (_disposed) return;
                _seekSeq++;
                _seekSeconds = clamped;
                _statePosSeconds = clamped;
                _stateSampleUtc = _paused ? DateTime.MinValue : DateTime.UtcNow;
                if (_active)
                {
                    _dirty = true;
                    EnsureWorkerThreadLocked();
                    Monitor.PulseAll(_lock);
                }
            }
        }

        internal void Stop()
        {
            lock (_lock)
            {
                if (!_active && !_prewarmRequested) return;
                _active = false;
                _activePath = "";
                _generation++;
                _hasAudio = false;
                _statePosSeconds = 0.0;
                _stateDurationSeconds = 0.0;
                _stateSampleUtc = DateTime.MinValue;
                if (_launchedOnce || _worker != null)
                {
                    _dirty = true;
                    Monitor.PulseAll(_lock);
                }
            }
        }

        public void Dispose()
        {
            bool sendQuit;
            string quitPayload = "";
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _active = false;
                _prewarmRequested = false;
                _hasAudio = false;
                _pendingOpenUrlPath = null;
                _stateSampleUtc = DateTime.MinValue;
                _generation++;
                sendQuit = _launchedOnce || _worker != null;
                if (sendQuit)
                {
                    quitPayload = CustomLobbyBgPolicy.BuildWindowsAudioCommandPayload(
                        _generation, true, true, "", true, 0f, 1f, true, _seekSeq, 0.0);
                }
                _dirty = true;
                Monitor.PulseAll(_lock);
            }

            if (sendQuit && !string.IsNullOrEmpty(quitPayload))
            {
                try
                {
                    WriteAllTextShared(_cmdPath, quitPayload, Utf8NoBom);
                }
                catch
                {
                }
            }
        }

        private void EnsureWorkerThreadLocked()
        {
            if (_worker != null) return;
            Thread t = new Thread(SyncLoop)
            {
                IsBackground = true,
                Name = "XingShu_LobbyAudioSync"
            };
            _worker = t;
            t.Start();
        }

        private void SyncLoop()
        {
            string lastWrittenCmd = null;
            while (true)
            {
                bool disposed;
                bool active;
                bool prewarm;
                int expectedGen;
                string cmdPayload;

                lock (_lock)
                {
                    if (!_dirty && !_disposed)
                    {
                        Monitor.Wait(_lock, 200);
                    }
                    disposed = _disposed;
                    _dirty = false;
                    active = _active;
                    prewarm = _prewarmRequested;
                    expectedGen = _generation;
                    cmdPayload = CustomLobbyBgPolicy.BuildWindowsAudioCommandPayload(
                        expectedGen,
                        !active,
                        disposed,
                        active ? _activePath : "",
                        _muted,
                        _volume,
                        _speed,
                        _paused || !active,
                        _seekSeq,
                        _seekSeconds);
                }

                if (disposed)
                {
                    return;
                }

                bool needHelper = active || prewarm;
                if (needHelper)
                {
                    if (!string.Equals(cmdPayload, lastWrittenCmd, StringComparison.Ordinal))
                    {
                        try
                        {
                            WriteAllTextShared(_cmdPath, cmdPayload, Utf8NoBom);
                            lastWrittenCmd = cmdPayload;
                        }
                        catch
                        {
                        }
                    }

                    bool helperAlive = IsHelperAlive();
                    if (!helperAlive && DateTime.UtcNow >= _nextLaunchAllowedUtc && _launchAttempts < MaxLaunchAttempts)
                    {
                        QueueHelperLaunch();
                    }

                    PollHelperState(expectedGen, active);
                }
            }
        }

        private bool IsHelperAlive()
        {
            try
            {
                if (!File.Exists(_statePath))
                {
                    return false;
                }
                DateTime mtime = File.GetLastWriteTimeUtc(_statePath);
                return (DateTime.UtcNow - mtime).TotalSeconds < HelperHeartbeatTimeoutSeconds;
            }
            catch
            {
                return false;
            }
        }

        private void QueueHelperLaunch()
        {
            try
            {
                EnsureHelperScriptWritten();

                string psExe = ResolvePowerShellExe();
                string vbsText = CustomLobbyBgPolicy.BuildWindowsVbsLauncher(
                    psExe, _scriptPath, _selfPid, _cmdPath, _statePath, _vbsPath, _lnkPath);
                WriteAllTextShared(_vbsPath, vbsText, Encoding.Unicode);

                byte[] lnkBytes = CustomLobbyBgPolicy.BuildWindowsShortcutLnk(
                    @"%SystemRoot%\System32\wscript.exe",
                    "//B //Nologo \"" + _vbsPath + "\"");
                File.WriteAllBytes(_lnkPath, lnkBytes);

                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }
                    _launchAttempts++;
                    _nextLaunchAllowedUtc = DateTime.UtcNow.AddSeconds(LaunchRetryCooldownSeconds);
                    _pendingOpenAttempt = _launchAttempts;
                    _pendingOpenUrlPath = _lnkPath;
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _launchAttempts++;
                    _nextLaunchAllowedUtc = DateTime.UtcNow.AddSeconds(LaunchRetryCooldownSeconds);
                }
                LogSafe($"[WindowsLobbyAudioPlayer] 生成伴随启动脚本失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string ResolvePowerShellExe()
        {
            try
            {
                string sysRoot = Environment.GetEnvironmentVariable("SystemRoot");
                if (!string.IsNullOrEmpty(sysRoot))
                {
                    string candidate = Path.Combine(sysRoot, @"System32\WindowsPowerShell\v1.0\powershell.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }
            return "powershell.exe";
        }

        private void EnsureHelperScriptWritten()
        {
            bool needWrite = true;
            try
            {
                if (File.Exists(_scriptPath))
                {
                    string existing = ReadAllTextShared(_scriptPath);
                    if (string.Equals(existing, HelperScriptContent, StringComparison.Ordinal))
                    {
                        needWrite = false;
                    }
                }
            }
            catch
            {
                needWrite = true;
            }
            if (needWrite)
            {
                WriteAllTextShared(_scriptPath, HelperScriptContent, Utf8NoBom);
            }
        }

        private void PollHelperState(int expectedGen, bool active)
        {
            try
            {
                if (!File.Exists(_statePath)) return;
                string raw = ReadAllTextShared(_statePath);
                if (!CustomLobbyBgPolicy.TryParseWindowsAudioState(
                    raw,
                    out int stateGen,
                    out bool opened,
                    out bool hasAudio,
                    out double posSec,
                    out double durSec,
                    out string error))
                {
                    return;
                }

                if (IsHelperAlive())
                {
                    lock (_lock)
                    {
                        _launchAttempts = 0;
                    }
                }

                if (!active || stateGen != expectedGen)
                {
                    return;
                }

                bool logReady = false;
                bool logErr = false;
                lock (_lock)
                {
                    if (_disposed || !_active || _generation != stateGen)
                    {
                        return;
                    }
                    _hasAudio = opened && hasAudio;
                    _statePosSeconds = posSec;
                    _stateDurationSeconds = durSec;
                    _stateSampleUtc = _paused ? DateTime.MinValue : DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(error) && _loggedErrorGen != stateGen)
                    {
                        _loggedErrorGen = stateGen;
                        logErr = true;
                    }
                    else if (opened && _loggedReadyGen != stateGen)
                    {
                        _loggedReadyGen = stateGen;
                        logReady = true;
                    }
                }

                if (logErr)
                {
                    LogSafe($"[WindowsLobbyAudioPlayer] 音频解码失败 (gen={stateGen}): {error}");
                }
                else if (logReady)
                {
                    LogSafe($"[WindowsLobbyAudioPlayer] 音频就绪 (gen={stateGen}): hasAudio={hasAudio}, duration={durSec:F2}s");
                }
            }
            catch
            {
            }
        }

        private static string ReadAllTextShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Utf8NoBom))
            {
                return sr.ReadToEnd();
            }
        }

        private static void WriteAllTextShared(string path, string content, Encoding encoding)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                byte[] preamble = encoding.GetPreamble();
                if (preamble != null && preamble.Length > 0)
                {
                    fs.Write(preamble, 0, preamble.Length);
                }
                byte[] payload = encoding.GetBytes(content ?? "");
                if (payload.Length > 0)
                {
                    fs.Write(payload, 0, payload.Length);
                }
                fs.Flush();
            }
        }

        private static void LogSafe(string message)
        {
            try
            {
                XingShuRuntimeHost.LogLifecycle(message);
            }
            catch
            {
            }
        }
    }
}
#endif
