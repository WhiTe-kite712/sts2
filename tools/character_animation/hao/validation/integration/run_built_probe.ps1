param([string]$Python = 'C:\Users\Amazi\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
$ErrorActionPreference = 'Stop'
$probeDirectory = $PSScriptRoot
& $Python (Join-Path $probeDirectory 'pack_built_probe.py')
if ($LASTEXITCODE -ne 0) { throw 'Cannot prepare final-PCK probe' }
$oldProbeEnvironment = @{ APPDATA=$env:APPDATA; LOCALAPPDATA=$env:LOCALAPPDATA; DOTNET_BUNDLE_EXTRACT_BASE_DIR=$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR }
try {
    $env:APPDATA = Join-Path $probeDirectory 'built-probe-runtime\AppData\Roaming'
    $env:LOCALAPPDATA = Join-Path $probeDirectory 'built-probe-runtime\AppData\Local'
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $probeDirectory 'built-probe-runtime\bundle'
    New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA -Force | Out-Null
    $startedUtc = [DateTime]::UtcNow
    $process = Start-Process -FilePath 'E:\steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe' -WindowStyle Hidden -PassThru `
      -WorkingDirectory $probeDirectory -RedirectStandardOutput (Join-Path $probeDirectory 'built_pack.stdout.log') `
      -RedirectStandardError (Join-Path $probeDirectory 'built_pack.stderr.log') -ArgumentList @(
        '--headless','--main-pack',('"'+(Join-Path $probeDirectory 'built_pack_probe.pck')+'"'),
        '--script','res://built_pack_probe.gd','--log-file',('"'+(Join-Path $probeDirectory 'built_pack.log')+'"'))
    if (-not $process.WaitForExit(60000)) { Stop-Process -Id $process.Id; throw 'Built PCK probe timed out' }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Built PCK probe exited $($process.ExitCode)" }
    $reportPath = Join-Path $probeDirectory 'built_pack_result.json'
    if ((Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -lt $startedUtc) { throw 'No fresh native result' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $report.ok) { throw 'Final built PCK native checks failed' }
    $packHashNow = (Get-FileHash -LiteralPath $report.pack -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($packHashNow -ne $report.pack_sha256) { throw 'Compiled mod PCK changed during the probe' }
    Write-Output 'PASS final compiled PCK: native Spine resource, converted CTEX, five animations, cape and point angles.'
} finally {
    $env:APPDATA = $oldProbeEnvironment.APPDATA
    $env:LOCALAPPDATA = $oldProbeEnvironment.LOCALAPPDATA
    $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $oldProbeEnvironment.DOTNET_BUNDLE_EXTRACT_BASE_DIR
}
