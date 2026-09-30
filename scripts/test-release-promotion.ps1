[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$version = '1.2.3-preview.4'
$channel = 'preview'
$sourceCommit = '0123456789abcdef0123456789abcdef01234567'
$promotionRunId = '987654321'
$promotionRunAttempt = '1'
$root = Join-Path ([IO.Path]::GetTempPath()) "toren-release-promotion-$([Guid]::NewGuid().ToString('N'))"
$packageRoot = Join-Path $root 'packages'
$signedX64Root = Join-Path $root 'signed-osx-x64'
$signedArm64Root = Join-Path $root 'signed-osx-arm64'
$outputRoot = Join-Path $root 'output'
$candidateManifestPath = Join-Path $root 'candidate.json'

function New-TestPackage {
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][string] $FileName,
        [Parameter(Mandatory)][string] $Content
    )

    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $packagePath = Join-Path $Directory $FileName
    [IO.File]::WriteAllText($packagePath, $Content, [Text.UTF8Encoding]::new($false))
    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $FileName" | Set-Content -LiteralPath "$packagePath.sha256" -Encoding utf8NoBOM -NoNewline
    return [ordered]@{
        path = $packagePath
        hash = $hash
    }
}

function New-SignedMacFixture {
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][string] $RuntimeIdentifier,
        [Parameter(Mandatory)][string] $Content
    )

    $package = New-TestPackage -Directory $Directory -FileName 'Toren IDE.zip' -Content $Content
    $attestation = [ordered]@{
        schemaVersion = 1
        product = 'Toren IDE'
        version = $version
        runtimeIdentifier = $RuntimeIdentifier
        sourceCommit = $sourceCommit
        sha256 = $package.hash
        developerIdSigned = $true
        notarized = $true
        stapled = $true
    }
    $attestation | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $Directory 'Toren IDE.release.json') -Encoding utf8NoBOM
    return $package
}

function Write-CandidateManifest {
    param([Parameter(Mandatory)] $Manifest)

    $Manifest | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $candidateManifestPath -Encoding utf8NoBOM
}

function Invoke-Promotion {
    param(
        [Parameter(Mandatory)][bool] $ReleaseGatesConfirmed,
        [Parameter(Mandatory)][string] $Destination
    )

    & (Join-Path $PSScriptRoot 'promote-release-manifest.ps1') `
        -CandidateManifestPath $candidateManifestPath `
        -PackageArtifactsRoot $packageRoot `
        -SignedMacX64Root $signedX64Root `
        -SignedMacArm64Root $signedArm64Root `
        -ExpectedVersion $version `
        -ExpectedChannel $channel `
        -ExpectedSourceCommit $sourceCommit `
        -PromotionRunId $promotionRunId `
        -PromotionRunAttempt $promotionRunAttempt `
        -PromotionDryRun $true `
        -ReleaseGatesConfirmed $ReleaseGatesConfirmed `
        -OutputDirectory $Destination
}

try {
    New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

    $linux = New-TestPackage `
        -Directory (Join-Path $packageRoot 'Toren-IDE-linux-x64') `
        -FileName 'Toren-IDE-linux-x64.tar.gz' `
        -Content 'linux-package'
    $windows = New-TestPackage `
        -Directory (Join-Path $packageRoot 'Toren-IDE-win-x64') `
        -FileName 'Toren-IDE-win-x64.zip' `
        -Content 'windows-package'
    $macX64Candidate = New-TestPackage `
        -Directory (Join-Path $packageRoot 'Toren-IDE-osx-x64') `
        -FileName 'Toren-IDE-osx-x64.zip' `
        -Content 'unsigned-macos-x64'
    $macArm64Candidate = New-TestPackage `
        -Directory (Join-Path $packageRoot 'Toren-IDE-osx-arm64') `
        -FileName 'Toren-IDE-osx-arm64.zip' `
        -Content 'unsigned-macos-arm64'

    $generatedManifestPath = Join-Path $root 'generated-candidate.json'
    $previousGithubSha = $env:GITHUB_SHA
    try {
        $env:GITHUB_SHA = $sourceCommit
        & (Join-Path $PSScriptRoot 'create-release-manifest.ps1') `
            -ArtifactsRoot $packageRoot `
            -Version $version `
            -Channel $channel `
            -OutputPath $generatedManifestPath
    }
    finally {
        $env:GITHUB_SHA = $previousGithubSha
    }

    $generatedCandidate = Get-Content -LiteralPath $generatedManifestPath -Raw | ConvertFrom-Json
    $generatedWindows = @($generatedCandidate.artifacts | Where-Object runtimeIdentifier -eq 'win-x64')
    if ($generatedWindows.Count -ne 1 -or $generatedWindows[0].requiresPlatformSigning -ne $false) {
        throw 'Candidate manifest creator must not require Windows platform signing for MVP 1.0.'
    }
    $generatedMac = @($generatedCandidate.artifacts | Where-Object runtimeIdentifier -like 'osx-*')
    if ($generatedMac.Count -ne 2 -or @($generatedMac | Where-Object requiresPlatformSigning -ne $true).Count -ne 0) {
        throw 'Candidate manifest creator must require platform signing for both macOS RIDs.'
    }

    $candidate = [ordered]@{
        schemaVersion = 1
        product = 'Toren IDE'
        version = $version
        channel = $channel
        sourceCommit = $sourceCommit
        generatedAtUtc = [DateTime]::UtcNow.ToString('O')
        artifacts = @(
            [ordered]@{
                runtimeIdentifier = 'linux-x64'
                fileName = 'Toren-IDE-linux-x64.tar.gz'
                sha256 = $linux.hash
                packageValidated = $true
                requiresPlatformSigning = $false
                distributionReady = $false
            },
            [ordered]@{
                runtimeIdentifier = 'win-x64'
                fileName = 'Toren-IDE-win-x64.zip'
                sha256 = $windows.hash
                packageValidated = $true
                requiresPlatformSigning = $false
                distributionReady = $false
            },
            [ordered]@{
                runtimeIdentifier = 'osx-x64'
                fileName = 'Toren-IDE-osx-x64.zip'
                sha256 = $macX64Candidate.hash
                packageValidated = $true
                requiresPlatformSigning = $true
                distributionReady = $false
            },
            [ordered]@{
                runtimeIdentifier = 'osx-arm64'
                fileName = 'Toren-IDE-osx-arm64.zip'
                sha256 = $macArm64Candidate.hash
                packageValidated = $true
                requiresPlatformSigning = $true
                distributionReady = $false
            }
        )
    }
    Write-CandidateManifest -Manifest $candidate

    $null = New-SignedMacFixture `
        -Directory $signedX64Root `
        -RuntimeIdentifier 'osx-x64' `
        -Content 'signed-macos-x64'
    $null = New-SignedMacFixture `
        -Directory $signedArm64Root `
        -RuntimeIdentifier 'osx-arm64' `
        -Content 'signed-macos-arm64'

    $confirmationRejected = $false
    try {
        Invoke-Promotion `
            -ReleaseGatesConfirmed $false `
            -Destination (Join-Path $root 'unconfirmed-output')
    }
    catch {
        if ($_.Exception.Message -eq 'Release gates were not explicitly confirmed.') {
            $confirmationRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $confirmationRejected) {
        throw 'Promotion helper accepted distribution-ready output without release-gate confirmation.'
    }

    $candidate.artifacts[0].distributionReady = $true
    Write-CandidateManifest -Manifest $candidate
    $prematurePromotionRejected = $false
    try {
        Invoke-Promotion `
            -ReleaseGatesConfirmed $true `
            -Destination (Join-Path $root 'premature-output')
    }
    catch {
        if ($_.Exception.Message -like "*must not be marked distribution-ready before promotion*") {
            $prematurePromotionRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $prematurePromotionRejected) {
        throw 'Promotion helper accepted a candidate artifact already marked distribution-ready.'
    }
    $candidate.artifacts[0].distributionReady = $false
    Write-CandidateManifest -Manifest $candidate

    $staleOutput = Join-Path $root 'stale-output'
    New-Item -ItemType Directory -Force -Path $staleOutput | Out-Null
    Set-Content -LiteralPath (Join-Path $staleOutput 'old-package.zip') -Value 'stale' -Encoding utf8NoBOM
    $staleOutputRejected = $false
    try {
        Invoke-Promotion `
            -ReleaseGatesConfirmed $true `
            -Destination $staleOutput
    }
    catch {
        if ($_.Exception.Message -like 'Promotion output directory must be empty:*') {
            $staleOutputRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $staleOutputRejected) {
        throw 'Promotion helper accepted a non-empty destination that could leak stale release artifacts.'
    }

    Invoke-Promotion -ReleaseGatesConfirmed $true -Destination $outputRoot

    $releaseManifestPath = Join-Path $outputRoot 'Toren-IDE-release-manifest.json'
    $release = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json
    if ($release.version -ne $version -or $release.channel -ne $channel -or $release.sourceCommit -ne $sourceCommit) {
        throw 'Promoted release identity does not match the expected fixture identity.'
    }
    if ($release.releaseGatesConfirmed -ne $true) {
        throw 'Promoted release manifest does not record explicit release-gate confirmation.'
    }
    if ($release.promotionRunId -ne $promotionRunId -or $release.promotionRunAttempt -ne $promotionRunAttempt -or $release.promotionDryRun -ne $true) {
        throw 'Promoted release manifest does not preserve dry-run workflow provenance.'
    }

    $artifacts = @($release.artifacts)
    if ($artifacts.Count -ne 4) {
        throw "Expected four promoted artifacts, found $($artifacts.Count)."
    }
    if (@($artifacts | Where-Object distributionReady -ne $true).Count -ne 0) {
        throw 'Every promoted artifact must be marked distribution-ready.'
    }

    $signedMacArtifacts = @($artifacts | Where-Object runtimeIdentifier -like 'osx-*')
    if ($signedMacArtifacts.Count -ne 2 -or @($signedMacArtifacts | Where-Object platformSigningValidated -ne $true).Count -ne 0) {
        throw 'Both promoted macOS artifacts must carry validated platform-signing state.'
    }

    $badAttestationPath = Join-Path $signedArm64Root 'Toren IDE.release.json'
    $badAttestation = Get-Content -LiteralPath $badAttestationPath -Raw | ConvertFrom-Json
    $badAttestation.sourceCommit = 'ffffffffffffffffffffffffffffffffffffffff'
    $badAttestation | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $badAttestationPath -Encoding utf8NoBOM

    $rejected = $false
    try {
        Invoke-Promotion `
            -ReleaseGatesConfirmed $true `
            -Destination (Join-Path $root 'negative-output')
    }
    catch {
        if ($_.Exception.Message -like "*source commit*does not match*") {
            $rejected = $true
        }
        else {
            throw
        }
    }

    if (-not $rejected) {
        throw 'Promotion helper accepted a signed artifact from the wrong source commit.'
    }

    Write-Host 'Release promotion contract validation passed.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
