[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ReleaseVersion,

    [Parameter(Mandatory)]
    [string] $ExpectedCommit,

    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [switch] $AllowMissingTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+(?:-preview\.\d+)?$') {
    throw "Unsupported release version '$ReleaseVersion'."
}
if ($ExpectedCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Expected commit '$ExpectedCommit' must be a full 40-character Git SHA."
}
if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $RepositoryRoot"
}

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
    $path = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release file is missing: $relativePath"
    }
}

Push-Location $RepositoryRoot
try {
    $insideWorkTree = (& git rev-parse --is-inside-work-tree 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $insideWorkTree -ne 'true') {
        throw "Repository root is not a Git work tree: $RepositoryRoot"
    }

    $headCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $headCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Could not resolve repository HEAD.'
    }
    if ($headCommit -ne $ExpectedCommit) {
        throw "Repository HEAD '$headCommit' does not match expected release commit '$ExpectedCommit'."
    }

    $tag = "v$ReleaseVersion"
    & git show-ref --verify --quiet "refs/tags/$tag"
    $tagExists = $LASTEXITCODE -eq 0
    if (-not $tagExists) {
        if (-not $AllowMissingTag) {
            throw "Release tag '$tag' does not exist. Create the immutable tag at '$ExpectedCommit' before signing."
        }

        Write-Host "Release preflight passed for commit $ExpectedCommit; immutable tag '$tag' is the only allowed missing release identity input."
        exit 0
    }

    $tagCommit = (& git rev-list -n 1 $tag).Trim()
    if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $ExpectedCommit) {
        throw "Release tag '$tag' resolves to '$tagCommit', expected '$ExpectedCommit'."
    }

    Write-Host "Release preflight passed for $tag at $ExpectedCommit."
}
finally {
    Pop-Location
}
