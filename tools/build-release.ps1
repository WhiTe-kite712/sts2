[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$Sts2Path,
    [string]$Dotnet = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'MySts2Mod\MySts2Mod.csproj'
$sourceManifest = Join-Path $repoRoot 'MySts2Mod\MySts2Mod.json'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if ($destination.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $destination.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be outside the source repository.'
}
if (!(Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project file not found: $projectPath"
}

$manifest = Get-Content -LiteralPath $sourceManifest -Raw | ConvertFrom-Json
if ($manifest.id -ne 'CodeFarmer') {
    throw 'The source manifest ID must be CodeFarmer.'
}
$version = [string]$manifest.version
if ($version -notmatch '^[A-Za-z0-9._-]+$') {
    throw 'The manifest version is empty or contains unsupported filename characters.'
}

$archivePath = Join-Path $destination "CodeFarmer-$version.zip"
$checksumPath = Join-Path $destination "CodeFarmer-$version.sha256"
$packagePath = Join-Path $destination 'CodeFarmer'
foreach ($targetPath in @($archivePath, $checksumPath, $packagePath)) {
    if (Test-Path -LiteralPath $targetPath) {
        throw "Release output already exists; choose another OutputDirectory: $targetPath"
    }
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
$buildPath = Join-Path $destination ('build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildPath | Out-Null

$buildArgs = @(
    'build', $projectPath,
    '-c', 'ExportRelease',
    '-p:DeployToGame=false',
    '--output', $buildPath
)
if ($Sts2Path) {
    $gameDirectory = [IO.Path]::GetFullPath($Sts2Path)
    if (!(Test-Path -LiteralPath $gameDirectory -PathType Container)) {
        throw "Game directory not found: $gameDirectory"
    }
    $buildArgs += "-p:Sts2Path=$gameDirectory"
}

Push-Location -LiteralPath $repoRoot
try {
    & $Dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE. Outputs remain in $buildPath"
    }
}
finally {
    Pop-Location
}

$requiredFiles = @('CodeFarmer.dll', 'CodeFarmer.pck', 'CodeFarmer.json')
foreach ($fileName in $requiredFiles) {
    $builtFile = Join-Path $buildPath $fileName
    if (!(Test-Path -LiteralPath $builtFile -PathType Leaf) -or (Get-Item -LiteralPath $builtFile).Length -eq 0) {
        throw "Missing or empty release file: $builtFile"
    }
}
$builtManifest = Get-Content -LiteralPath (Join-Path $buildPath 'CodeFarmer.json') -Raw | ConvertFrom-Json
if ($builtManifest.id -ne 'CodeFarmer' -or $builtManifest.version -ne $version) {
    throw 'Built manifest ID/version does not match the source manifest.'
}

New-Item -ItemType Directory -Path $packagePath | Out-Null
foreach ($fileName in $requiredFiles) {
    Copy-Item -LiteralPath (Join-Path $buildPath $fileName) -Destination (Join-Path $packagePath $fileName)
}
Compress-Archive -LiteralPath $packagePath -DestinationPath $archivePath -CompressionLevel Optimal

$checksumLines = foreach ($fileName in $requiredFiles) {
    $hash = Get-FileHash -LiteralPath (Join-Path $packagePath $fileName) -Algorithm SHA256
    '{0}  CodeFarmer/{1}' -f $hash.Hash.ToLowerInvariant(), $fileName
}
$zipHash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
$checksumLines += '{0}  {1}' -f $zipHash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($archivePath)
$checksumLines | Set-Content -LiteralPath $checksumPath -Encoding utf8

Write-Host "Release package: $archivePath"
Write-Host "Checksums: $checksumPath"
Write-Host 'Only CodeFarmer.dll, CodeFarmer.pck and CodeFarmer.json are included.'
Write-Host 'No game deployment or game startup was performed. Build success does not replace runtime validation.'
