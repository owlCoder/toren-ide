[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ArtifactsRoot,
    [Parameter(Mandatory)] [string] $ExpectedVersion,
    [Parameter(Mandatory)] [string] $ExpectedSourceCommit,
    [Parameter(Mandatory)] [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$identity = & (Join-Path $PSScriptRoot 'resolve-package-identity.ps1') -RefType tag -RefName "v$ExpectedVersion"
if ($ExpectedSourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Expected source commit must be a full Git SHA.'
}
$manifestPath = Join-Path $ArtifactsRoot 'Toren-IDE-release-manifest/Toren-IDE-release-manifest.json'
$candidate = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($candidate.schemaVersion -ne 1 -or $candidate.product -ne 'Toren IDE' -or
    $candidate.version -ne $ExpectedVersion -or $candidate.channel -ne $identity.Channel -or
    $candidate.sourceCommit -ne $ExpectedSourceCommit) {
    throw 'Candidate manifest does not match the draft release identity.'
}
$packages = [ordered]@{
    'win-x64' = 'Toren-IDE-win-x64.zip'
    'linux-x64' = 'Toren-IDE-linux-x64.tar.gz'
    'osx-x64' = 'Toren-IDE-osx-x64.zip'
    'osx-arm64' = 'Toren-IDE-osx-arm64.zip'
}
if (@($candidate.artifacts).Count -ne $packages.Count) {
    throw 'Draft release requires exactly four runtime artifacts.'
}

$verifiedFiles = @($manifestPath)
foreach ($rid in $packages.Keys) {
    $entries = @($candidate.artifacts | Where-Object runtimeIdentifier -eq $rid)
    if ($entries.Count -ne 1) { throw "Expected exactly one '$rid' candidate." }
    $entry = $entries[0]
    if ($entry.fileName -ne $packages[$rid] -or $entry.sha256 -notmatch '^[0-9a-f]{64}$' -or
        $entry.packageValidated -ne $true -or $entry.distributionReady -ne $false -or
        $entry.requiresPlatformSigning -ne $rid.StartsWith('osx-')) {
        throw "Invalid '$rid' candidate policy or filename."
    }
    $packagePath = Join-Path $ArtifactsRoot "Toren-IDE-$rid/$($entry.fileName)"
    $checksumPath = "$packagePath.sha256"
    $checksum = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
    if ($checksum -ne "$($entry.sha256)  $($entry.fileName)" -or
        (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Checksum mismatch for '$rid'."
    }
    $verifiedFiles += @($packagePath, $checksumPath)
}

$actualFiles = @(Get-ChildItem -LiteralPath $ArtifactsRoot -Recurse -File)
if ($actualFiles.Count -ne $verifiedFiles.Count) {
    throw 'Candidate artifact set contains unexpected files.'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    if (@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0) {
        throw 'Draft output directory must be empty.'
    }
}
else { New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null }
foreach ($file in $verifiedFiles) {
    Copy-Item -LiteralPath $file -Destination $OutputDirectory
}
Write-Host "Verified nine candidate assets for draft $ExpectedVersion. Signing and public-release gates remain pending."
