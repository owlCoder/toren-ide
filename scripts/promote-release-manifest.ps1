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
    [bool] $ReleaseGatesConfirmed,

    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ReleaseGatesConfirmed) {
    throw 'Release gates were not explicitly confirmed.'
}

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
if ($candidate.schemaVersion -ne 1) {
    throw "Unsupported candidate manifest schema version '$($candidate.schemaVersion)'."
}
if ($candidate.product -ne 'Toren IDE') {
    throw "Candidate product '$($candidate.product)' is not 'Toren IDE'."
}
if ($candidate.version -ne $ExpectedVersion) {
    throw "Candidate version '$($candidate.version)' does not match '$ExpectedVersion'."
}
if ($candidate.channel -ne $ExpectedChannel) {
    throw "Candidate channel '$($candidate.channel)' does not match '$ExpectedChannel'."
}
if ($candidate.sourceCommit -ne $ExpectedSourceCommit) {
    throw "Candidate source commit '$($candidate.sourceCommit)' does not match '$ExpectedSourceCommit'."
}

$expectedCandidatePolicies = [ordered]@{
    'linux-x64' = $false
    'win-x64' = $true
    'osx-x64' = $true
    'osx-arm64' = $true
}
$candidateArtifacts = @($candidate.artifacts)
if ($candidateArtifacts.Count -ne $expectedCandidatePolicies.Count) {
    throw "Candidate manifest must contain exactly $($expectedCandidatePolicies.Count) artifacts, found $($candidateArtifacts.Count)."
}

foreach ($runtimeIdentifier in $expectedCandidatePolicies.Keys) {
    $entries = @($candidateArtifacts | Where-Object runtimeIdentifier -eq $runtimeIdentifier)
    if ($entries.Count -ne 1) {
        throw "Candidate manifest must contain exactly one '$runtimeIdentifier' artifact."
    }

    $entry = $entries[0]
    if ([string]::IsNullOrWhiteSpace($entry.fileName)) {
        throw "Candidate '$runtimeIdentifier' artifact must include a file name."
    }
    if ($entry.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Candidate '$runtimeIdentifier' artifact does not contain a valid SHA-256 hash."
    }
    if ($entry.packageValidated -ne $true) {
        throw "Candidate '$runtimeIdentifier' artifact was not package-validated."
    }

    $expectedSigning = $expectedCandidatePolicies[$runtimeIdentifier]
    if ($entry.requiresPlatformSigning -ne $expectedSigning) {
        throw "Candidate '$runtimeIdentifier' signing policy does not match the release contract."
    }
    if ($entry.distributionReady -ne $false) {
        throw "Candidate '$runtimeIdentifier' must not be marked distribution-ready before promotion."
    }
}

if (Test-Path -LiteralPath $OutputDirectory) {
    $existingOutput = @(Get-ChildItem -LiteralPath $OutputDirectory -Force)
    if ($existingOutput.Count -ne 0) {
        throw "Promotion output directory must be empty: $OutputDirectory"
    }
}
else {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

function Copy-CandidateArtifact {
    param([Parameter(Mandatory)][string] $RuntimeIdentifier)

    $entry = @($candidateArtifacts | Where-Object runtimeIdentifier -eq $RuntimeIdentifier)[0]
    $artifactDirectory = Join-Path $PackageArtifactsRoot "Toren-IDE-$RuntimeIdentifier"
    $packagePath = Join-Path $artifactDirectory $entry.fileName
    $checksumPath = "$packagePath.sha256"
    $actualHash = Get-VerifiedHash -PackagePath $packagePath -ChecksumPath $checksumPath
    if ($actualHash -ne $entry.sha256) {
        throw "Candidate manifest hash does not match '$RuntimeIdentifier' package bytes."
    }

    $destinationPath = Join-Path $OutputDirectory $entry.fileName
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

    $attestations = @(Get-ChildItem -LiteralPath $ArtifactRoot -File -Filter '*.release.json')
    if ($attestations.Count -ne 1) {
        throw "Expected exactly one signed macOS attestation for '$RuntimeIdentifier', found $($attestations.Count)."
    }

    $sourcePath = $packages[0].FullName
    $sourceChecksum = "$sourcePath.sha256"
    $actualHash = Get-VerifiedHash -PackagePath $sourcePath -ChecksumPath $sourceChecksum
    $attestation = Get-Content -LiteralPath $attestations[0].FullName -Raw | ConvertFrom-Json

    if ($attestation.schemaVersion -ne 1) {
        throw "Signed '$RuntimeIdentifier' attestation has unsupported schema version '$($attestation.schemaVersion)'."
    }
    if ($attestation.product -ne 'Toren IDE') {
        throw "Signed '$RuntimeIdentifier' attestation product '$($attestation.product)' is not 'Toren IDE'."
    }
    if ($attestation.version -ne $ExpectedVersion) {
        throw "Signed '$RuntimeIdentifier' version '$($attestation.version)' does not match '$ExpectedVersion'."
    }
    if ($attestation.runtimeIdentifier -ne $RuntimeIdentifier) {
        throw "Signed artifact attestation RID '$($attestation.runtimeIdentifier)' does not match '$RuntimeIdentifier'."
    }
    if ($attestation.sourceCommit -ne $ExpectedSourceCommit) {
        throw "Signed '$RuntimeIdentifier' source commit '$($attestation.sourceCommit)' does not match '$ExpectedSourceCommit'."
    }
    if ($attestation.sha256 -ne $actualHash) {
        throw "Signed '$RuntimeIdentifier' attestation hash does not match package bytes."
    }
    if ($attestation.developerIdSigned -ne $true -or $attestation.notarized -ne $true -or $attestation.stapled -ne $true) {
        throw "Signed '$RuntimeIdentifier' attestation does not confirm signing, notarization, and stapling."
    }

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
    releaseGatesConfirmed = $true
    promotedAtUtc = [DateTime]::UtcNow.ToString('O')
    artifacts = $artifacts
}

$manifestPath = Join-Path $OutputDirectory 'Toren-IDE-release-manifest.json'
$releaseManifest | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
Write-Host "Distribution manifest written to $manifestPath"
