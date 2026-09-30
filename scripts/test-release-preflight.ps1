Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path ([IO.Path]::GetTempPath()) "toren-release-preflight-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $root | Out-Null

function Invoke-Git {
    param([Parameter(Mandatory)] [string[]] $Arguments)
    & git -C $root @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-Rejected {
    param(
        [Parameter(Mandatory)] [scriptblock] $Action,
        [Parameter(Mandatory)] [string] $ExpectedMessage
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessage*") {
            throw "Expected failure containing '$ExpectedMessage', got '$($_.Exception.Message)'."
        }
        return
    }

    throw "Expected release preflight to reject: $ExpectedMessage"
}

try {
    $requiredFiles = @(
        '.github/workflows/package.yml',
        '.github/workflows/signed-macos-package.yml',
        '.github/workflows/publish-release.yml',
        '.github/workflows/release-validation.yml',
        'scripts/assert-github-workflow-run.ps1',
        'scripts/promote-release-manifest.ps1',
        'scripts/create-release-validation-record.ps1',
        'scripts/validate-release-validation-record.ps1',
        'scripts/sign-notarize-macos.sh',
        'docs/release/checklist.md',
        'docs/release/publishing.md',
        'docs/release/macos-signing.md'
    )

    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $root $relativePath
        New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
        Set-Content -LiteralPath $path -Value 'fixture' -Encoding utf8NoBOM
    }

    Invoke-Git @('init')
    Invoke-Git @('config', 'user.email', 'release-preflight@example.invalid')
    Invoke-Git @('config', 'user.name', 'Release Preflight Tests')
    Invoke-Git @('add', '.')
    Invoke-Git @('commit', '-m', 'fixture')
    $commit = (& git -C $root rev-parse HEAD).Trim()

    & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
        -ReleaseVersion '1.0.0' `
        -ExpectedCommit $commit `
        -RepositoryRoot $root `
        -AllowMissingTag

    Assert-Rejected -ExpectedMessage "Release tag 'v1.0.0' does not exist" -Action {
        & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
            -ReleaseVersion '1.0.0' `
            -ExpectedCommit $commit `
            -RepositoryRoot $root
    }

    Invoke-Git @('tag', 'v1.0.0')
    & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
        -ReleaseVersion '1.0.0' `
        -ExpectedCommit $commit `
        -RepositoryRoot $root

    Assert-Rejected -ExpectedMessage 'Unsupported release version' -Action {
        & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
            -ReleaseVersion '1.0' `
            -ExpectedCommit $commit `
            -RepositoryRoot $root
    }

    Assert-Rejected -ExpectedMessage 'does not match expected release commit' -Action {
        & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
            -ReleaseVersion '1.0.0' `
            -ExpectedCommit '1111111111111111111111111111111111111111' `
            -RepositoryRoot $root
    }

    Remove-Item -LiteralPath (Join-Path $root 'docs/release/macos-signing.md') -Force
    Assert-Rejected -ExpectedMessage 'Required release file is missing' -Action {
        & (Join-Path $PSScriptRoot 'assert-release-preflight.ps1') `
            -ReleaseVersion '1.0.0' `
            -ExpectedCommit $commit `
            -RepositoryRoot $root
    }

    Write-Host 'Release preflight contract tests passed.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
