[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RecordPath,

    [Parameter(Mandatory)]
    [string] $PromotionRoot,

    [Parameter(Mandatory)]
    [string] $ExpectedVersion,

    [Parameter(Mandatory)]
    [ValidateSet('preview', 'stable')]
    [string] $ExpectedChannel,

    [Parameter(Mandatory)]
    [string] $ExpectedSourceCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $RecordPath -PathType Leaf)) {
    throw "Release validation record does not exist: $RecordPath"
}
if (-not (Test-Path -LiteralPath $PromotionRoot -PathType Container)) {
    throw "Promotion artifact root does not exist: $PromotionRoot"
}

$record = Get-Content -LiteralPath $RecordPath -Raw | ConvertFrom-Json
if ($record.schemaVersion -ne 1 -or $record.product -ne 'Toren IDE') {
    throw 'Release validation record does not use the supported Toren IDE schema.'
}
if ($record.version -ne $ExpectedVersion) {
    throw "Validation record version '$($record.version)' does not match '$ExpectedVersion'."
}
if ($record.channel -ne $ExpectedChannel) {
    throw "Validation record channel '$($record.channel)' does not match '$ExpectedChannel'."
}
if ($record.sourceCommit -ne $ExpectedSourceCommit) {
    throw "Validation record source commit '$($record.sourceCommit)' does not match '$ExpectedSourceCommit'."
}
if ($record.promotionRunId -notmatch '^\d+$') {
    throw "Validation record promotion run ID '$($record.promotionRunId)' is invalid."
}
if ([string]::IsNullOrWhiteSpace($record.validatedBy)) {
    throw 'Validation record does not identify the validating operator.'
}

$validatedAt = [DateTime]::MinValue
if (-not [DateTime]::TryParse($record.validatedAtUtc, [ref]$validatedAt)) {
    throw "Validation record timestamp '$($record.validatedAtUtc)' is invalid."
}

$requiredChecks = @('windowsSmoke', 'linuxSmoke', 'macosSmoke', 'visualAccessibility', 'performance')
foreach ($check in $requiredChecks) {
    $property = $record.checks.PSObject.Properties[$check]
    if ($null -eq $property -or $property.Value -ne $true) {
        throw "Validation record does not confirm required check '$check'."
    }
}

$manifestPath = Join-Path $PromotionRoot 'Toren-IDE-release-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Promoted release manifest does not exist: $manifestPath"
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'Toren IDE') {
    throw 'Promoted release manifest does not use the supported Toren IDE schema.'
}
if ($manifest.version -ne $ExpectedVersion -or $manifest.channel -ne $ExpectedChannel -or $manifest.sourceCommit -ne $ExpectedSourceCommit) {
    throw 'Promoted release identity does not match the validation record identity.'
}
if ($manifest.releaseGatesConfirmed -ne $true) {
    throw 'Promoted release manifest does not record release-gate confirmation.'
}

$expectedRids = @('linux-x64', 'win-x64', 'osx-x64', 'osx-arm64')
$manifestArtifacts = @($manifest.artifacts)
$recordArtifacts = @($record.artifacts)
if ($manifestArtifacts.Count -ne $expectedRids.Count -or $recordArtifacts.Count -ne $expectedRids.Count) {
    throw 'Release validation requires exactly four promoted artifacts.'
}

foreach ($runtimeIdentifier in $expectedRids) {
    $manifestEntries = @($manifestArtifacts | Where-Object runtimeIdentifier -eq $runtimeIdentifier)
    $recordEntries = @($recordArtifacts | Where-Object runtimeIdentifier -eq $runtimeIdentifier)
    if ($manifestEntries.Count -ne 1 -or $recordEntries.Count -ne 1) {
        throw "Release validation requires exactly one '$runtimeIdentifier' artifact in both records."
    }

    $manifestEntry = $manifestEntries[0]
    $recordEntry = $recordEntries[0]
    if ($manifestEntry.distributionReady -ne $true) {
        throw "Promoted '$runtimeIdentifier' artifact is not marked distribution-ready."
    }
    if ($runtimeIdentifier -like 'osx-*' -and $manifestEntry.platformSigningValidated -ne $true) {
        throw "Promoted '$runtimeIdentifier' artifact does not record validated platform signing."
    }
    if ($manifestEntry.fileName -ne $recordEntry.fileName -or $manifestEntry.sha256 -ne $recordEntry.sha256) {
        throw "Promoted '$runtimeIdentifier' artifact does not match the validated release candidate bytes."
    }

    $packagePath = Join-Path $PromotionRoot $manifestEntry.fileName
    $checksumPath = "$packagePath.sha256"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf) -or -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Promoted '$runtimeIdentifier' package or checksum is missing."
    }

    $actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $manifestEntry.sha256) {
        throw "Promoted '$runtimeIdentifier' package bytes changed after validation."
    }

    $checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($checksumText -notmatch '^(?<hash>[0-9a-fA-F]{64})\s+' -or $Matches['hash'].ToLowerInvariant() -ne $actualHash) {
        throw "Promoted '$runtimeIdentifier' checksum does not match package bytes."
    }
}

Write-Host "Release validation record is valid for $ExpectedVersion ($ExpectedChannel) at $ExpectedSourceCommit."
