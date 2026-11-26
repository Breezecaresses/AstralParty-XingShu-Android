$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$movePolicy = Join-Path $repoRoot "src\XingShu.Runtime\XingShuRuntime\Utilities\PlayerCombatStatusPolicy.cs"
$buffPolicy = Join-Path $repoRoot "src\XingShu.Runtime\XingShuRuntime\Utilities\PlayerStarBuffPolicy.cs"
$testSource = Join-Path $PSScriptRoot "MatchLifecycleTests\PlayerStarBuffTests.cs"

$cscCandidates = @(
    "D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "csc.exe not found"
}

$outDir = Join-Path $env:TEMP ("xingshu_star_buff_tests_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exePath = Join-Path $outDir "PlayerStarBuffTests.exe"

try {
    & $csc /nologo /target:exe /out:$exePath $movePolicy $buffPolicy $testSource
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
