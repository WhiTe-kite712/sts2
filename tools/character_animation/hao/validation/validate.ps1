param(
    [string]$Python = 'python',
    [string]$Engine = 'E:\steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe',
    [string]$SpineDll = 'E:\steam\steamapps\common\Slay the Spire 2\libspine_godot.windows.template_release.x86_64.dll'
)
$ErrorActionPreference = 'Stop'
$probeDirectory = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$sourceDirectory = Join-Path (Split-Path $probeDirectory) 'source'
$probeRuntimeDirectory = Join-Path $probeDirectory 'runtime'
$probeRoamingDirectory = Join-Path $probeRuntimeDirectory 'AppData\Roaming'
$probeLocalDirectory = Join-Path $probeRuntimeDirectory 'AppData\Local'
New-Item -ItemType Directory -Path $probeRoamingDirectory,$probeLocalDirectory -Force | Out-Null
$previousProbeEnvironment = @{
    APPDATA = $env:APPDATA
    LOCALAPPDATA = $env:LOCALAPPDATA
    DOTNET_BUNDLE_EXTRACT_BASE_DIR = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
}

function New-ProbePack {
    & $Python (Join-Path $probeDirectory 'pack_probe.py') --assets-root $sourceDirectory --spine-dll $SpineDll
    if ($LASTEXITCODE -ne 0) { throw "Probe pack failed: $LASTEXITCODE" }
}
function Invoke-NativeProbe([string]$Script, [string]$Log) {
    $probeProcess = Start-Process -FilePath $Engine -ArgumentList @(
        '--headless', '--main-pack', ('"' + (Join-Path $probeDirectory 'spine-probe.pck') + '"'),
        '--script', $Script, '--log-file', ('"' + (Join-Path $probeDirectory $Log) + '"')
    ) -WorkingDirectory $probeDirectory -WindowStyle Hidden -PassThru `
      -RedirectStandardOutput (Join-Path $probeDirectory ($Log + '.stdout')) `
      -RedirectStandardError (Join-Path $probeDirectory ($Log + '.stderr'))
    if (-not $probeProcess.WaitForExit(30000)) {
        Stop-Process -Id $probeProcess.Id
        throw "Native probe timed out after 30 seconds; see $Log"
    }
    $probeProcess.Refresh()
    if ($probeProcess.ExitCode -ne 0) { throw "Native probe failed: $($probeProcess.ExitCode); see $Log" }
}

try {
    $env:APPDATA = $probeRoamingDirectory
    $env:LOCALAPPDATA = $probeLocalDirectory
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $probeRuntimeDirectory 'bundle'
    New-ProbePack
    Invoke-NativeProbe 'res://texture.gd' 'texture.log'
    New-ProbePack
    $probeStartedUtc = [DateTime]::UtcNow
    Invoke-NativeProbe 'res://probe.gd' 'spine-probe.log'
    $probeResultFile = Join-Path $probeDirectory 'validation.json'
    if (-not (Test-Path -LiteralPath $probeResultFile)) { throw 'Native probe did not produce validation.json.' }
    if ((Get-Item -LiteralPath $probeResultFile).LastWriteTimeUtc -lt $probeStartedUtc) { throw 'Native probe did not refresh validation.json; an old report cannot satisfy this run.' }
    $probeResult = Get-Content -LiteralPath $probeResultFile -Raw | ConvertFrom-Json
    if (-not $probeResult.ok) { throw 'The native validation result is not successful.' }
    if (-not $probeResult.runtime_resource_loader.ok) { throw 'Native Godot resource wrappers were not verified.' }
    $currentSourceHashes = @{
        skeleton = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $sourceDirectory 'hao.spine-json')).Hash.ToLowerInvariant()
        atlas = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $sourceDirectory 'hao.atlas')).Hash.ToLowerInvariant()
        png = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $sourceDirectory 'hao.png')).Hash.ToLowerInvariant()
    }
    foreach ($sourceKey in @('skeleton','atlas','png')) {
        if ($currentSourceHashes[$sourceKey] -ne $probeResult.source_sha256.$sourceKey) { throw "Source $sourceKey changed after validation; rerun." }
    }
    & $Python (Join-Path $probeDirectory 'check_direction.py')
    if ($LASTEXITCODE -ne 0) { throw "Native strike direction check failed: $LASTEXITCODE" }
    & $Python (Join-Path $probeDirectory 'check_elbow.py')
    if ($LASTEXITCODE -ne 0) { throw "Native main elbow-right check failed: $LASTEXITCODE" }
    Write-Output 'PASS: native Spine source, textures, animations, optional mesh/deform, and Godot resource loading.'
} finally {
    $env:APPDATA = $previousProbeEnvironment.APPDATA
    $env:LOCALAPPDATA = $previousProbeEnvironment.LOCALAPPDATA
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $previousProbeEnvironment.DOTNET_BUNDLE_EXTRACT_BASE_DIR
}
