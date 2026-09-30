[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RunId,

    [Parameter(Mandatory)]
    [string] $ExpectedRepository,

    [Parameter(Mandatory)]
    [string] $ExpectedWorkflowName,

    [string] $ExpectedWorkflowPath,

    [Parameter(Mandatory)]
    [string[]] $AllowedEvents,

    [string] $ExpectedHeadSha,

    [string] $GitHubToken,

    [string] $RunMetadataPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RunId -notmatch '^\d+$') {
    throw "Workflow run ID '$RunId' is invalid."
}
if ($ExpectedRepository -notmatch '^[^/]+/[^/]+$') {
    throw "Expected repository '$ExpectedRepository' must use owner/name form."
}
if ([string]::IsNullOrWhiteSpace($ExpectedWorkflowName)) {
    throw 'Expected workflow name is required.'
}
if ([string]::IsNullOrWhiteSpace($ExpectedWorkflowPath)) {
    $ExpectedWorkflowPath = switch ($ExpectedWorkflowName) {
        'Package' { '.github/workflows/package.yml' }
        'Signed macOS Package' { '.github/workflows/signed-macos-package.yml' }
        'Publish Release' { '.github/workflows/publish-release.yml' }
        'Release Validation' { '.github/workflows/release-validation.yml' }
        default { throw "Expected workflow path is required for unknown workflow '$ExpectedWorkflowName'." }
    }
}
if ($ExpectedWorkflowPath -notmatch '^\.github/workflows/[^/]+\.(?:yml|yaml)$') {
    throw "Expected workflow path '$ExpectedWorkflowPath' is invalid."
}
if ($AllowedEvents.Count -eq 0 -or @($AllowedEvents | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
    throw 'At least one non-empty allowed workflow event is required.'
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedHeadSha) -and $ExpectedHeadSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Expected head SHA '$ExpectedHeadSha' is invalid."
}

if (-not [string]::IsNullOrWhiteSpace($RunMetadataPath)) {
    if (-not (Test-Path -LiteralPath $RunMetadataPath -PathType Leaf)) {
        throw "Workflow run metadata file does not exist: $RunMetadataPath"
    }
    $run = Get-Content -LiteralPath $RunMetadataPath -Raw | ConvertFrom-Json
}
else {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        throw 'GitHubToken is required when RunMetadataPath is not supplied.'
    }

    $headers = @{
        Accept = 'application/vnd.github+json'
        Authorization = "Bearer $GitHubToken"
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'Toren-IDE-release-provenance'
    }
    $uri = "https://api.github.com/repos/$ExpectedRepository/actions/runs/$RunId"
    $run = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
}

if ([string]$run.id -ne $RunId) {
    throw "Workflow run metadata ID '$($run.id)' does not match requested run '$RunId'."
}
if ($run.repository.full_name -ne $ExpectedRepository) {
    throw "Workflow run repository '$($run.repository.full_name)' does not match '$ExpectedRepository'."
}
if ($run.name -ne $ExpectedWorkflowName) {
    throw "Workflow run '$RunId' belongs to '$($run.name)', expected '$ExpectedWorkflowName'."
}
if ($run.path -ne $ExpectedWorkflowPath) {
    throw "Workflow run '$RunId' path '$($run.path)' does not match '$ExpectedWorkflowPath'."
}
if ($run.status -ne 'completed') {
    throw "Workflow run '$RunId' is not completed (status '$($run.status)')."
}
if ($run.conclusion -ne 'success') {
    throw "Workflow run '$RunId' did not succeed (conclusion '$($run.conclusion)')."
}
if ($run.event -notin $AllowedEvents) {
    throw "Workflow run '$RunId' event '$($run.event)' is not allowed; expected one of: $($AllowedEvents -join ', ')."
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedHeadSha) -and $run.head_sha -ne $ExpectedHeadSha) {
    throw "Workflow run '$RunId' head SHA '$($run.head_sha)' does not match '$ExpectedHeadSha'."
}

Write-Host "Validated GitHub Actions provenance for run ${RunId}: $ExpectedWorkflowName at $ExpectedWorkflowPath ($($run.event), $($run.conclusion))."
