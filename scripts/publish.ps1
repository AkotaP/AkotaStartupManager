[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version,

    [Parameter()]
    [string]$OutputDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repositoryRoot 'VERSION'
$solution = Join-Path $repositoryRoot 'AkotaStartupManager.slnx'
$appProject = Join-Path $repositoryRoot 'src\AkotaStartupManager.App\AkotaStartupManager.App.csproj'
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repositoryRoot $OutputDirectory
}
$stagingRoot = Join-Path $outputRoot '.staging'
$versionPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'

if (-not $PSBoundParameters.ContainsKey('Version')) {
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Version file was not found: $versionFile"
    }
    $Version = [System.IO.File]::ReadAllText($versionFile).Trim()
    $versionSource = $versionFile
} else {
    $Version = $Version.Trim()
    $versionSource = '-Version parameter'
}

if ([string]::IsNullOrWhiteSpace($Version) -or $Version -notmatch $versionPattern) {
    throw "Invalid semantic version '$Version' from $versionSource. Expected a value such as 1.2.3 or 1.2.3-beta.1."
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Remove-RuntimeData {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    foreach ($directory in @('Data', 'Logs')) {
        $path = Join-Path $PublishDirectory $directory
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

function Copy-ReleaseDocuments {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    foreach ($document in @('README.md', 'README.en.md', 'LICENSE', 'CHANGELOG.md')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot $document) -Destination $PublishDirectory
    }
}

$publishVariants = @(
    [PSCustomObject]@{
        Name = 'standalone'
        SelfContained = 'true'
    },
    [PSCustomObject]@{
        Name = 'runtime'
        SelfContained = 'false'
    }
)

Write-Host "Publishing Akota Startup Manager v$Version" -ForegroundColor Cyan
Write-Host "Version source: $versionSource"

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$artifacts = @()
try {
    Invoke-DotNet @('restore', $solution)
    Invoke-DotNet @('build', $solution, '--configuration', 'Release', '--no-restore', "-p:Version=$Version")
    Invoke-DotNet @('test', $solution, '--configuration', 'Release', '--no-build', '--no-restore')
    Invoke-DotNet @('restore', $appProject, '--runtime', 'win-x64', "-p:Version=$Version")

    foreach ($variant in $publishVariants) {
        $publishDirectory = Join-Path (Join-Path $stagingRoot $variant.Name) 'AkotaStartupManager'
        New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

        Invoke-DotNet @(
            'publish', $appProject,
            '--configuration', 'Release',
            '--runtime', 'win-x64',
            '--self-contained', $variant.SelfContained,
            '--no-restore',
            '-p:PublishSingleFile=false',
            "-p:Version=$Version",
            '--output', $publishDirectory
        )

        Remove-RuntimeData -PublishDirectory $publishDirectory
        Copy-ReleaseDocuments -PublishDirectory $publishDirectory

        $archiveBaseName = "AkotaStartupManager-v$Version-win-x64-$($variant.Name)"
        $archivePath = Join-Path $outputRoot "$archiveBaseName.zip"
        $checksumPath = "$archivePath.sha256"
        foreach ($path in @($archivePath, $checksumPath)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force
            }
        }

        Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksumLine = "$hash  $archiveBaseName.zip"
        [System.IO.File]::WriteAllText($checksumPath, "$checksumLine`r`n", [System.Text.UTF8Encoding]::new($false))

        $artifacts += [PSCustomObject]@{
            Variant = $variant.Name
            Archive = $archivePath
            Checksum = $checksumPath
            Hash = $hash
        }
    }
} finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Write-Host ''
Write-Host 'Release artifacts created:' -ForegroundColor Green
foreach ($artifact in $artifacts) {
    Write-Host "  [$($artifact.Variant)] $($artifact.Archive)"
    Write-Host "  [$($artifact.Variant)] $($artifact.Checksum)"
    Write-Host "  [$($artifact.Variant)] SHA-256: $($artifact.Hash)"
}
