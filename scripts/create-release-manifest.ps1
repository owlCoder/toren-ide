[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ArtifactsRoot,

    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [ValidateSet('preview', 'stable')]
    [string] $Channel,

    [Parameter(Mandatory)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ArtifactsRoot -PathType Container)) {
    throw "Artifact root does not exist: $ArtifactsRoot"
}

$versionPattern = if ($Channel -eq 'stable') {
    '^\d+\.\d+\.\d+$'
}
else {
    '^\d+\.\d+\.\d+-preview\.\d+$'
}

if ($Version -notmatch $versionPattern) {
    throw "Version '$Version' is not valid for the '$Channel' channel."
}

$artifactDirectories = Get-ChildItem -LiteralPath $ArtifactsRoot -Directory |
    Where-Object { $_.Name -like 'Toren-IDE-*' } |
    Sort-Object Name

if ($artifactDirectories.Count -eq 0) {
    throw "No Toren package artifacts were found under: $ArtifactsRoot"
}

$artifacts = foreach ($directory in $artifactDirectories) {
    $runtimeIdentifier = $directory.Name.Substring('Toren-IDE-'.Length)
    $packageFiles = @(
        Get-ChildItem -LiteralPath $directory.FullName -File |
            Where-Object { $_.Name -notlike '*.sha256' }
    )

    if ($packageFiles.Count -ne 1) {
        throw "Expected exactly one package file in '$($directory.FullName)', found $($packageFiles.Count)."
    }

    $package = $packageFiles[0]
    $checksumPath = "$($package.FullName).sha256"
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Checksum file is missing for '$($package.Name)'."
    }

    $checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($checksumText -notmatch '^(?<hash>[0-9a-fA-F]{64})\s+') {
        throw "Checksum file for '$($package.Name)' does not contain a SHA-256 hash."
    }

    $expectedHash = $Matches['hash'].ToLowerInvariant()
    $actualHash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Checksum verification failed for '$($package.Name)'."
    }

    # MVP 1.0 requires Developer ID signing/notarization for public macOS
    # distribution. Windows Authenticode signing is not an MVP release gate.
    $requiresPlatformSigning =
        $runtimeIdentifier.StartsWith('osx-', [StringComparison]::Ordinal)

    [ordered]@{
        runtimeIdentifier = $runtimeIdentifier
        fileName = $package.Name
        sha256 = $actualHash
        packageValidated = $true
        requiresPlatformSigning = $requiresPlatformSigning
        distributionReady = $false
    }
}

$sourceCommit = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $null
}
else {
    $env:GITHUB_SHA
}

$manifest = [ordered]@{
    schemaVersion = 1
    product = 'Toren IDE'
    version = $Version
    channel = $Channel
    sourceCommit = $sourceCommit
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    artifacts = @($artifacts)
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Release-candidate manifest written to $OutputPath"
