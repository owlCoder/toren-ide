[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$version = '1.2.3-preview.4'
$channel = 'preview'
$sourceCommit = '0123456789abcdef0123456789abcdef01234567'
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
                requiresPlatformSigning = $true
                distributionReady = $false
            }
        )
    }
    $candidate | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $candidateManifestPath -Encoding utf8NoBOM

    $null = New-SignedMacFixture `
        -Directory $signedX64Root `
        -RuntimeIdentifier 'osx-x64' `
        -Content 'signed-macos-x64'
    $null = New-SignedMacFixture `
        -Directory $signedArm64Root `
        -RuntimeIdentifier 'osx-arm64' `
        -Content 'signed-macos-arm64'

    & (Join-Path $PSScriptRoot 'promote-release-manifest.ps1') `
        -CandidateManifestPath $candidateManifestPath `
        -PackageArtifactsRoot $packageRoot `
        -SignedMacX64Root $signedX64Root `
        -SignedMacArm64Root $signedArm64Root `
        -ExpectedVersion $version `
        -ExpectedChannel $channel `
        -ExpectedSourceCommit $sourceCommit `
        -OutputDirectory $outputRoot

    $releaseManifestPath = Join-Path $outputRoot 'Toren-IDE-release-manifest.json'
    $release = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json
    if ($release.version -ne $version -or $release.channel -ne $channel -or $release.sourceCommit -ne $sourceCommit) {
        throw 'Promoted release identity does not match the expected fixture identity.'
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

    $negativeOutput = Join-Path $root 'negative-output'
    $rejected = $false
    try {
        & (Join-Path $PSScriptRoot 'promote-release-manifest.ps1') `
            -CandidateManifestPath $candidateManifestPath `
            -PackageArtifactsRoot $packageRoot `
            -SignedMacX64Root $signedX64Root `
            -SignedMacArm64Root $signedArm64Root `
            -ExpectedVersion $version `
            -ExpectedChannel $channel `
            -ExpectedSourceCommit $sourceCommit `
            -OutputDirectory $negativeOutput
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
