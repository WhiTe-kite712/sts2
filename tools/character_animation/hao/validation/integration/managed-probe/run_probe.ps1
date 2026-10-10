param(
 [string]$Engine='E:\steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe',
 [string]$ModDll='C:\Users\Amazi\Desktop\Spire2Mod\Git Clone\sts2\MySts2Mod\.godot\mono\temp\bin\Debug\MySts2Mod.dll',
 [string]$ModPck='C:\Users\Amazi\Desktop\Spire2Mod\Git Clone\sts2\MySts2Mod\.godot\mono\temp\bin\Debug\MySts2Mod.pck',
 [string]$Python='C:\Users\Amazi\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe',
 [int]$TimeoutSeconds=25
)
$ErrorActionPreference='Stop'
$probeDirectory=(Resolve-Path -LiteralPath $PSScriptRoot).Path
$gameDataDirectory=Join-Path (Split-Path $Engine) 'data_sts2_windows_x86_64'
$baseLibPath='C:\Users\Amazi\.nuget\packages\alchyr.sts2.baselib\3.4.7\lib\net9.0\BaseLib.dll'
$spineLibrary=Join-Path (Split-Path $Engine) 'libspine_godot.windows.template_release.x86_64.dll'
& dotnet restore (Join-Path $probeDirectory 'SpineIntegrationProbe.csproj') --configfile (Join-Path $probeDirectory 'NuGet.Config') --ignore-failed-sources
if($LASTEXITCODE -ne 0){throw 'Offline probe restore failed'}
& dotnet build (Join-Path $probeDirectory 'SpineIntegrationProbe.csproj') --no-restore --configuration Debug
if($LASTEXITCODE -ne 0){throw 'Probe build failed'}
& $Python (Join-Path $probeDirectory 'prepare_probe.py') --mod-dll $ModDll --mod-pck $ModPck --game-data $gameDataDirectory --baselib-dll $baseLibPath --spine-dll $spineLibrary
if($LASTEXITCODE -ne 0){throw 'Probe pack failed'}
$probeRuntimeDirectory=Join-Path $probeDirectory 'runtime'
$probeRoamingDirectory=Join-Path $probeRuntimeDirectory 'AppData\Roaming'
$probeLocalDirectory=Join-Path $probeRuntimeDirectory 'AppData\Local'
New-Item -ItemType Directory -Path $probeRoamingDirectory,$probeLocalDirectory -Force | Out-Null
$previousProbeEnvironment=@{APPDATA=$env:APPDATA;LOCALAPPDATA=$env:LOCALAPPDATA;DOTNET_BUNDLE_EXTRACT_BASE_DIR=$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR}
try{
 $env:APPDATA=$probeRoamingDirectory
 $env:LOCALAPPDATA=$probeLocalDirectory
 $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=Join-Path $probeRuntimeDirectory 'bundle'
 $resultFile=Join-Path $probeDirectory 'factory-result.json'
 $startedUtc=[DateTime]::UtcNow
 $process=Start-Process -FilePath $Engine -ArgumentList @('--headless','--path',('"' + $probeDirectory + '"'),'--main-pack',('"' + (Join-Path $probeDirectory 'factory-probe.pck') + '"'),'--log-file',('"' + (Join-Path $probeDirectory 'factory-probe.log') + '"')) -WorkingDirectory $probeDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $probeDirectory 'factory-probe.stdout') -RedirectStandardError (Join-Path $probeDirectory 'factory-probe.stderr')
 if(-not $process.WaitForExit($TimeoutSeconds*1000)){
   Stop-Process -Id $process.Id
   throw 'Isolated C# host did not finish within the bounded startup/playback deadline.'
 }
 $process.Refresh()
 if(-not(Test-Path -LiteralPath $resultFile)){throw 'Managed probe did not create a result. Host assembly bootstrap unavailable; see factory-probe.log.'}
 if((Get-Item -LiteralPath $resultFile).LastWriteTimeUtc -lt $startedUtc){throw 'Stale probe result cannot satisfy this run.'}
 $result=Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
 if($process.ExitCode -ne 0 -or -not $result.ok){throw ('Actual factory probe failed at ' + $result.stage)}
 Write-Output 'PASS: actual compiled CodingFarmerCombatVisuals + NCreatureVisuals + game CreatureAnimator playback.'
} finally{
 $env:APPDATA=$previousProbeEnvironment.APPDATA
 $env:LOCALAPPDATA=$previousProbeEnvironment.LOCALAPPDATA
 $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=$previousProbeEnvironment.DOTNET_BUNDLE_EXTRACT_BASE_DIR
}

