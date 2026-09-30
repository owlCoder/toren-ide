[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PromotionRoot,

    [Parameter(Mandatory)]
    [string] $ExpectedVersion,

    [Parameter(Mandatory)]
    [ValidateSet('preview', 'stable')]
    [string] $ExpectedChannel,

    [Parameter(Mandatory)]
    [string] $ExpectedSourceCommit,

    [Parameter(Mandatory)]
    [string] $PromotionRunId,

    [Parameter(Mandatory)]
    [string] $ValidationRunId,

    [Parameter(Mandatory)]
    [string] $ValidationRunAttempt,

    [Parameter(Mandatory)]
    [string] $ValidatedBy,

    [Parameter(Mandatory)]
    [bool] $WindowsSmokeConfirmed,

    [Parameter(Mandatory)]
    [bool] $LinuxSmokeConfirmed,

    [Parameter(Mandatory)]
    [bool] $MacosSmokeConfirmed,

    [Parameter(Mandatory)]
    [bool] $VisualAccessibilityConfirmed,

    [Parameter(Mandatory)]
    [bool] $PerformanceConfirmed,

    [Parameter(Mandatory)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checks = [ordered]@{
    windowsSmoke = $WindowsSmokeConfirmed
    linuxSmoke = $LinuxSmokeConfirmed
    macosSmoke = $MacosSmokeConfirmed
    visualAccessibility = $VisualAccessibilityConfirmed
    performance = $PerformanceConfirmed
}

$failedChecks = @($checks.GetEnumerator() | Where-Object Value -ne $true | ForEach-Object Key)
if ($failedChecks.Count -ne 0) {
    throw "Release validation cannot be recorded while checks are incomplete: $($failedChecks -join ', ')."
}
if ([string]::IsNullOrWhiteSpace($ValidatedBy)) {
    throw 'Release validation must record the validating operator.'
}
if ($PromotionRunId -notmatch '^\d+$') {
    throw "Promotion run ID '$PromotionRunId' is not valid."
}
if ($ValidationRunId -notmatch '^\d+$') {
    throw "Validation run ID '$ValidationRunId' is not valid."
}
if ($ValidationRunAttempt -notmatch '^[1-9]\d*$') {
    throw "Validation run attempt '$ValidationRunAttempt' is not valid."
}
if (-not (Test-Path -LiteralPath $PromotionRoot -PathType Container)) {
    throw "Promotion artifact root does not exist: $PromotionRoot"
}

$manifestPath = Join-Path $PromotionRoot 'Toren-IDE-release-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Promoted release manifest does not exist: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'Toren IDE') {
    throw 'Promoted release manifest does not use the supported Toren IDE schema.'
}
if ($manifest.version -ne $ExpectedVersion) {
    throw "Promoted release version '$($manifest.version)' does not match '$ExpectedVersion'."
}
if ($manifest.channel -ne $ExpectedChannel) {
    throw "Promoted release channel '$($manifest.channel)' does not match '$ExpectedChannel'."
}
if ($manifest.sourceCommit -ne $ExpectedSourceCommit) {
    throw "Promoted release source commit '$($manifest.sourceCommit)' does not match '$ExpectedSourceCommit'."
}
if ($manifest.releaseGatesConfirmed -ne $true) {
    throw 'Promoted release manifest does not record release-gate confirmation.'
}
if ($manifest.promotionDryRun -ne $true) {
    throw 'Release validation only accepts artifacts produced by a Publish Release dry run.'
}
if ($manifest.promotionRunId -ne $PromotionRunId) {
    throw "Promoted release manifest run ID '$($manifest.promotionRunId)' does not match '$PromotionRunId'."
}
if ($manifest.promotionRunAttempt -notmatch '^[1-9]\d*$') {
    throw "Promoted release manifest run attempt '$($manifest.promotionRunAttempt)' is invalid."
}

$expectedRids = @('linux-x64', 'win-x64', 'osx-x64', 'osx-arm64')
$artifacts = @($manifest.artifacts)
if ($artifacts.Count -ne $expectedRids.Count) {
    throw "Promoted release manifest must contain exactly $($expectedRids.Count) artifacts."
}

$validatedArtifacts = foreach ($runtimeIdentifier in $expectedRids) {
    $entries = @($artifacts | Where-Object runtimeIdentifier -eq $runtimeIdentifier)
    if ($entries.Count -ne 1) {
        throw "Promoted release manifest must contain exactly one '$runtimeIdentifier' artifact."
    }

    $entry = $entries[0]
    if ($entry.distributionReady -ne $true) {
        throw "Promoted '$runtimeIdentifier' artifact is not marked distribution-ready."
    }
    if ($runtimeIdentifier -like 'osx-*' -and $entry.platformSigningValidated -ne $true) {
        throw "Promoted '$runtimeIdentifier' artifact does not record validated platform signing."
    }
    if ($entry.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "Promoted '$runtimeIdentifier' artifact does not contain a valid SHA-256 hash."
    }

    $packagePath = Join-Path $PromotionRoot $entry.fileName
    $checksumPath = "$packagePath.sha256"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Promoted package does not exist: $packagePath"
    }
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Promoted checksum does not exist: $checksumPath"
    }

    $actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $entry.sha256) {
        throw "Promoted '$runtimeIdentifier' package bytes do not match the release manifest."
    }

    $checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($checksumText -notmatch '^(?<hash>[0-9a-fA-F]{64})\s+') {
        throw "Promoted '$runtimeIdentifier' checksum file is invalid."
    }
    if ($Matches['hash'].ToLowerInvariant() -ne $actualHash) {
        throw "Promoted '$runtimeIdentifier' checksum does not match package bytes."
    }

    [ordered]@{
        runtimeIdentifier = $runtimeIdentifier
        fileName = $entry.fileName
        sha256 = $actualHash
    }
}

$expectedAssetNames = @('Toren-IDE-release-manifest.json')
foreach ($artifact in $validatedArtifacts) {
    $expectedAssetNames += $artifact.fileName
    $expectedAssetNames += "$($artifact.fileName).sha256"
}
$promotionFiles = @(Get-ChildItem -LiteralPath $PromotionRoot -File)
if ($promotionFiles.Count -ne $expectedAssetNames.Count) {
    throw "Promoted release asset set must contain exactly $($expectedAssetNames.Count) files, found $($promotionFiles.Count)."
}
foreach ($expectedAssetName in $expectedAssetNames) {
    if (@($promotionFiles | Where-Object Name -eq $expectedAssetName).Count -ne 1) {
        throw "Promoted release asset set is missing or duplicates '$expectedAssetName'."
    }
}
foreach ($promotionFile in $promotionFiles) {
    if ($promotionFile.Name -notin $expectedAssetNames) {
        throw "Promoted release asset set contains unexpected file '$($promotionFile.Name)'."
    }
}

$validatedAssets = @(
    $promotionFiles |
        Sort-Object Name |
        ForEach-Object {
            [ordered]@{
                fileName = $_.Name
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)

$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$record = [ordered]@{
    schemaVersion = 1
    product = 'Toren IDE'
    version = $ExpectedVersion
    channel = $ExpectedChannel
    sourceCommit = $ExpectedSourceCommit
    promotionRunId = $PromotionRunId
    promotionRunAttempt = $manifest.promotionRunAttempt
    promotionManifestSha256 = $manifestHash
    validationRunId = $ValidationRunId
    validationRunAttempt = $ValidationRunAttempt
    validatedBy = $ValidatedBy
    validatedAtUtc = [DateTime]::UtcNow.ToString('O')
    checks = $checks
    artifacts = @($validatedArtifacts)
    assets = @($validatedAssets)
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Release validation record written to $OutputPath"
