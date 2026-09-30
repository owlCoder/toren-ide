[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$version = '1.2.3-preview.4'
$channel = 'preview'
$sourceCommit = '0123456789abcdef0123456789abcdef01234567'
$promotionRunId = '123456789'
$root = Join-Path ([IO.Path]::GetTempPath()) "toren-release-validation-$([Guid]::NewGuid().ToString('N'))"
$promotionRoot = Join-Path $root 'promotion'
$recordPath = Join-Path $root 'Toren-IDE-release-validation.json'

function New-PromotedPackage {
    param(
        [Parameter(Mandatory)][string] $RuntimeIdentifier,
        [Parameter(Mandatory)][string] $FileName,
        [Parameter(Mandatory)][string] $Content,
        [Parameter(Mandatory)][bool] $PlatformSigningValidated
    )

    $packagePath = Join-Path $promotionRoot $FileName
    [IO.File]::WriteAllText($packagePath, $Content, [Text.UTF8Encoding]::new($false))
    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $FileName" | Set-Content -LiteralPath "$packagePath.sha256" -Encoding utf8NoBOM -NoNewline

    return [ordered]@{
        runtimeIdentifier = $RuntimeIdentifier
        fileName = $FileName
        sha256 = $hash
        platformSigningValidated = $PlatformSigningValidated
        distributionReady = $true
    }
}

try {
    New-Item -ItemType Directory -Force -Path $promotionRoot | Out-Null
    $artifacts = @(
        New-PromotedPackage -RuntimeIdentifier 'linux-x64' -FileName 'Toren-IDE-linux-x64.tar.gz' -Content 'linux-final' -PlatformSigningValidated $false
        New-PromotedPackage -RuntimeIdentifier 'win-x64' -FileName 'Toren-IDE-win-x64.zip' -Content 'windows-final' -PlatformSigningValidated $false
        New-PromotedPackage -RuntimeIdentifier 'osx-x64' -FileName 'Toren-IDE-osx-x64.zip' -Content 'macos-x64-final' -PlatformSigningValidated $true
        New-PromotedPackage -RuntimeIdentifier 'osx-arm64' -FileName 'Toren-IDE-osx-arm64.zip' -Content 'macos-arm64-final' -PlatformSigningValidated $true
    )

    $manifest = [ordered]@{
        schemaVersion = 1
        product = 'Toren IDE'
        version = $version
        channel = $channel
        sourceCommit = $sourceCommit
        releaseGatesConfirmed = $true
        promotedAtUtc = [DateTime]::UtcNow.ToString('O')
        artifacts = $artifacts
    }
    $manifest | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $promotionRoot 'Toren-IDE-release-manifest.json') -Encoding utf8NoBOM

    & (Join-Path $PSScriptRoot 'create-release-validation-record.ps1') `
        -PromotionRoot $promotionRoot `
        -ExpectedVersion $version `
        -ExpectedChannel $channel `
        -ExpectedSourceCommit $sourceCommit `
        -PromotionRunId $promotionRunId `
        -ValidatedBy 'release-operator' `
        -WindowsSmokeConfirmed $true `
        -LinuxSmokeConfirmed $true `
        -MacosSmokeConfirmed $true `
        -VisualAccessibilityConfirmed $true `
        -PerformanceConfirmed $true `
        -OutputPath $recordPath

    & (Join-Path $PSScriptRoot 'validate-release-validation-record.ps1') `
        -RecordPath $recordPath `
        -PromotionRoot $promotionRoot `
        -ExpectedVersion $version `
        -ExpectedChannel $channel `
        -ExpectedSourceCommit $sourceCommit

    $incompleteRejected = $false
    try {
        & (Join-Path $PSScriptRoot 'create-release-validation-record.ps1') `
            -PromotionRoot $promotionRoot `
            -ExpectedVersion $version `
            -ExpectedChannel $channel `
            -ExpectedSourceCommit $sourceCommit `
            -PromotionRunId $promotionRunId `
            -ValidatedBy 'release-operator' `
            -WindowsSmokeConfirmed $true `
            -LinuxSmokeConfirmed $true `
            -MacosSmokeConfirmed $true `
            -VisualAccessibilityConfirmed $true `
            -PerformanceConfirmed $false `
            -OutputPath (Join-Path $root 'incomplete.json')
    }
    catch {
        if ($_.Exception.Message -like '*checks are incomplete*performance*') {
            $incompleteRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $incompleteRejected) {
        throw 'Release validation record accepted an incomplete manual gate set.'
    }

    $linuxPath = Join-Path $promotionRoot 'Toren-IDE-linux-x64.tar.gz'
    [IO.File]::AppendAllText($linuxPath, '-tampered', [Text.UTF8Encoding]::new($false))
    $tamperRejected = $false
    try {
        & (Join-Path $PSScriptRoot 'validate-release-validation-record.ps1') `
            -RecordPath $recordPath `
            -PromotionRoot $promotionRoot `
            -ExpectedVersion $version `
            -ExpectedChannel $channel `
            -ExpectedSourceCommit $sourceCommit
    }
    catch {
        if ($_.Exception.Message -like "*package bytes changed after validation*") {
            $tamperRejected = $true
        }
        else {
            throw
        }
    }
    if (-not $tamperRejected) {
        throw 'Release validation record accepted package bytes that changed after validation.'
    }

    Write-Host 'Release validation record contract validation passed.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
