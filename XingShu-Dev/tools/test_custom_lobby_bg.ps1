$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$policySource = Join-Path $repoRoot "src\XingShu.Runtime\XingShuRuntime\Utilities\CustomLobbyBgPolicy.cs"
$testSource = Join-Path $PSScriptRoot "MatchLifecycleTests\CustomLobbyBgTests.cs"

$cscCandidates = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "csc.exe not found"
}

$outDir = Join-Path $env:TEMP ("xingshu_lobby_bg_tests_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exePath = Join-Path $outDir "CustomLobbyBgTests.exe"

try {
    & $csc /nologo /target:exe /out:$exePath $policySource $testSource
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed with exit code $LASTEXITCODE"
    }
    & $exePath
    if ($LASTEXITCODE -ne 0) {
        throw "Test execution failed with exit code $LASTEXITCODE"
    }
}
finally {
    if (Test-Path $outDir) {
        Remove-Item -Recurse -Force $outDir -ErrorAction SilentlyContinue
    }
}
