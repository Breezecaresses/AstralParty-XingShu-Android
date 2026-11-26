param([Parameter(Mandatory)][string]$InputDll, [Parameter(Mandatory)][string]$OutputDll, [string]$HooksDll)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (!$HooksDll) { $HooksDll = Join-Path $taskRoot 'XingShu-Dev/build/bin/Release/XingShu.Runtime.dll' }

$resolvedInput = (Resolve-Path -LiteralPath $InputDll).Path
$resolvedHooks = (Resolve-Path -LiteralPath $HooksDll).Path
$resolvedOutput = [IO.Path]::GetFullPath($OutputDll)
$outDir = Split-Path -Parent $resolvedOutput
if (!(Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$patcherExe = Join-Path $outDir 'ModImgLoaderPatcher.exe'
$cecil = Join-Path $taskRoot 'XingShu-Dev/tools/lib/Mono.Cecil.dll'
$cecilRocks = Join-Path $taskRoot 'XingShu-Dev/tools/lib/Mono.Cecil.Rocks.dll'
$compiler = 'D:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'

& $compiler /nologo "/out:$patcherExe" "/reference:$cecil" "/reference:$cecilRocks" (Join-Path $PSScriptRoot 'patch_modimg_loader.cs')
if ($LASTEXITCODE -ne 0) { throw 'ModImgLoaderPatcher compilation failed' }

Copy-Item -LiteralPath $cecil -Destination (Join-Path $outDir 'Mono.Cecil.dll') -Force
Copy-Item -LiteralPath $cecilRocks -Destination (Join-Path $outDir 'Mono.Cecil.Rocks.dll') -Force

& $patcherExe $resolvedInput $resolvedHooks $resolvedOutput $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'UI.TextureLoader modimg hook patch failed' }
