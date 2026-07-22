[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [Parameter()]
    [string]$OutputDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'AkotaStartupManager.slnx'
$appProject = Join-Path $repositoryRoot 'src\AkotaStartupManager.App\AkotaStartupManager.App.csproj'
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repositoryRoot $OutputDirectory
}
$stagingRoot = Join-Path $outputRoot '.staging'
$publishDirectory = Join-Path $stagingRoot 'AkotaStartupManager'
$archiveBaseName = "AkotaStartupManager-v$Version-win-x64"
$archivePath = Join-Path $outputRoot "$archiveBaseName.zip"
$checksumPath = "$archivePath.sha256"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Publishing Akota Startup Manager v$Version" -ForegroundColor Cyan

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}
if (Test-Path $checksumPath) {
    Remove-Item $checksumPath -Force
}

Invoke-DotNet @('restore', $solution)
Invoke-DotNet @('build', $solution, '--configuration', 'Release', '--no-restore', "-p:Version=$Version")
Invoke-DotNet @('test', $solution, '--configuration', 'Release', '--no-build', '--no-restore')
Invoke-DotNet @('restore', $appProject, '--runtime', 'win-x64')
Invoke-DotNet @(
    'publish', $appProject,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--no-restore',
    '-p:PublishSingleFile=false',
    "-p:Version=$Version",
    '--output', $publishDirectory
)

$runtimeDirectories = @('Data', 'Logs')
foreach ($directory in $runtimeDirectories) {
    $path = Join-Path $publishDirectory $directory
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }
}

$releaseDocuments = @('README.md', 'README.en.md', 'LICENSE', 'CHANGELOG.md')
foreach ($document in $releaseDocuments) {
    Copy-Item (Join-Path $repositoryRoot $document) $publishDirectory
}

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$hash  $archiveBaseName.zip"
[System.IO.File]::WriteAllText($checksumPath, "$checksumLine`r`n", [System.Text.UTF8Encoding]::new($false))

Remove-Item $stagingRoot -Recurse -Force

Write-Host ''
Write-Host 'Release artifacts created:' -ForegroundColor Green
Write-Host "  $archivePath"
Write-Host "  $checksumPath"
Write-Host "  SHA-256: $hash"
