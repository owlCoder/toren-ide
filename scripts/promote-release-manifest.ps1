[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CandidateManifestPath,

    [Parameter(Mandatory)]
    [string] $PackageArtifactsRoot,

    [Parameter(Mandatory)]
    [string] $SignedMacX64Root,

    [Parameter(Mandatory)]
    [string] $SignedMacArm64Root,

    [Parameter(Mandatory)]
    [string] $ExpectedVersion,

    [Parameter(Mandatory)]
    [ValidateSet('preview', 'stable')]
    [string] $ExpectedChannel,

    [Parameter(Mandatory)]
    [string] $ExpectedSourceCommit,

    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-VerifiedHash {
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath,

        [Parameter(Mandatory)]
        [string] $ChecksumPath
    )

    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        throw "Package file does not exist: $PackagePath"
    }

    if (-not (Test-Path -LiteralPath $ChecksumPath -PathType Leaf)) {
        throw "Checksum file does not exist: $ChecksumPath"
    }

    $checksumText = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
    if ($checksumText -notmatch '^(?<hash>[0-9a-fA-F]{64})\s+') {
        throw "Checksum file '$ChecksumPath' does not contain a SHA-256 hash."
    }

    $expectedHash = $Matches['hash'].ToLowerInvariant()
    $actualHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Checksum verification failed for '$PackagePath'."
    }

    return $actualHash
}

if (-not (Test-Path -LiteralPath $CandidateManifestPath -PathType Leaf)) {
    throw "Candidate manifest does not exist: $CandidateManifestPath"
}

$candidate = Get-Content -LiteralPath $CandidateManifestPath -Raw | ConvertFrom-Json
if ($candidate.version -ne $ExpectedVersion) {
    throw "Candidate version '$($candidate.version)' does not match '$ExpectedVersion'."
}
if ($candidate.channel -ne $ExpectedChannel) {
    throw "Candidate channel '$($candidate.channel)' does not match '$ExpectedChannel'."
}
if ($candidate.sourceCommit -ne $ExpectedSourceCommit) {
    throw "Candidate source commit '$($candidate.sourceCommit)' does not match '$ExpectedSourceCommit'."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Copy-CandidateArtifact {
    param([Parameter(Mandatory)][string] $RuntimeIdentifier)

    $entry = @($candidate.artifacts | Where-Object runtimeIdentifier -eq $RuntimeIdentifier)
    if ($entry.Count -ne 1) {
        throw "Candidate manifest must contain exactly one '$RuntimeIdentifier' artifact."
    }

    $artifactDirectory = Join-Path $PackageArtifactsRoot "Toren-IDE-$RuntimeIdentifier"
    $packagePath = Join-Path $artifactDirectory $entry[0].fileName
    $checksumPath = "$packagePath.sha256"
    $actualHash = Get-VerifiedHash -PackagePath $packagePath -ChecksumPath $checksumPath
    if ($actualHash -ne $entry[0].sha256) {
        throw "Candidate manifest hash does not match '$RuntimeIdentifier' package bytes."
    }

    $destinationPath = Join-Path $OutputDirectory $entry[0].fileName
    Copy-Item -LiteralPath $packagePath -Destination $destinationPath -Force
    "$actualHash  $([IO.Path]::GetFileName($destinationPath))" |
        Set-Content -LiteralPath "$destinationPath.sha256" -Encoding utf8NoBOM -NoNewline

    return [ordered]@{
        runtimeIdentifier = $RuntimeIdentifier
        fileName = [IO.Path]::GetFileName($destinationPath)
        sha256 = $actualHash
        platformSigningValidated = $false
        distributionReady = $true
    }
}

function Copy-SignedMacArtifact {
    param(
        [Parameter(Mandatory)][string] $RuntimeIdentifier,
        [Parameter(Mandatory)][string] $ArtifactRoot
    )

    $packages = @(Get-ChildItem -LiteralPath $ArtifactRoot -File -Filter '*.zip')
    if ($packages.Count -ne 1) {
        throw "Expected exactly one signed macOS ZIP for '$RuntimeIdentifier', found $($packages.Count)."
    }

    $sourcePath = $packages[0].FullName
    $sourceChecksum = "$sourcePath.sha256"
    $actualHash = Get-VerifiedHash -PackagePath $sourcePath -ChecksumPath $sourceChecksum
    $destinationName = "Toren-IDE-$RuntimeIdentifier.zip"
    $destinationPath = Join-Path $OutputDirectory $destinationName
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
    "$actualHash  $destinationName" |
        Set-Content -LiteralPath "$destinationPath.sha256" -Encoding utf8NoBOM -NoNewline

    return [ordered]@{
        runtimeIdentifier = $RuntimeIdentifier
        fileName = $destinationName
        sha256 = $actualHash
        platformSigningValidated = $true
        distributionReady = $true
    }
}

$artifacts = @(
    Copy-CandidateArtifact -RuntimeIdentifier 'linux-x64'
    Copy-CandidateArtifact -RuntimeIdentifier 'win-x64'
    Copy-SignedMacArtifact -RuntimeIdentifier 'osx-x64' -ArtifactRoot $SignedMacX64Root
    Copy-SignedMacArtifact -RuntimeIdentifier 'osx-arm64' -ArtifactRoot $SignedMacArm64Root
)

$releaseManifest = [ordered]@{
    schemaVersion = 1
    product = 'Toren IDE'
    version = $ExpectedVersion
    channel = $ExpectedChannel
    sourceCommit = $ExpectedSourceCommit
    promotedAtUtc = [DateTime]::UtcNow.ToString('O')
    artifacts = $artifacts
}

$manifestPath = Join-Path $OutputDirectory 'Toren-IDE-release-manifest.json'
$releaseManifest | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
Write-Host "Distribution manifest written to $manifestPath"
