Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path ([IO.Path]::GetTempPath()) "toren-workflow-provenance-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $root | Out-Null

function Write-RunFixture {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string] $Name = 'Package',
        [string] $Status = 'completed',
        [string] $Conclusion = 'success',
        [string] $Event = 'push',
        [string] $HeadSha = '1111111111111111111111111111111111111111',
        [string] $Repository = 'owlCoder/toren-ide',
        [string] $Id = '12345'
    )

    [ordered]@{
        id = [long]$Id
        name = $Name
        status = $Status
        conclusion = $Conclusion
        event = $Event
        head_sha = $HeadSha
        repository = [ordered]@{ full_name = $Repository }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
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
    throw "Expected provenance validation to reject: $ExpectedMessage"
}

try {
    $fixture = Join-Path $root 'run.json'
    Write-RunFixture -Path $fixture

    & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') `
        -RunId '12345' `
        -ExpectedRepository 'owlCoder/toren-ide' `
        -ExpectedWorkflowName 'Package' `
        -AllowedEvents @('push', 'workflow_dispatch') `
        -ExpectedHeadSha '1111111111111111111111111111111111111111' `
        -RunMetadataPath $fixture

    Write-RunFixture -Path $fixture -Name 'CI'
    Assert-Rejected -ExpectedMessage "belongs to 'CI'" -Action {
        & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') -RunId '12345' -ExpectedRepository 'owlCoder/toren-ide' -ExpectedWorkflowName 'Package' -AllowedEvents @('push') -RunMetadataPath $fixture
    }

    Write-RunFixture -Path $fixture -Conclusion 'failure'
    Assert-Rejected -ExpectedMessage 'did not succeed' -Action {
        & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') -RunId '12345' -ExpectedRepository 'owlCoder/toren-ide' -ExpectedWorkflowName 'Package' -AllowedEvents @('push') -RunMetadataPath $fixture
    }

    Write-RunFixture -Path $fixture -Event 'pull_request'
    Assert-Rejected -ExpectedMessage 'is not allowed' -Action {
        & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') -RunId '12345' -ExpectedRepository 'owlCoder/toren-ide' -ExpectedWorkflowName 'Package' -AllowedEvents @('push', 'workflow_dispatch') -RunMetadataPath $fixture
    }

    Write-RunFixture -Path $fixture -HeadSha '2222222222222222222222222222222222222222'
    Assert-Rejected -ExpectedMessage 'does not match' -Action {
        & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') -RunId '12345' -ExpectedRepository 'owlCoder/toren-ide' -ExpectedWorkflowName 'Package' -AllowedEvents @('push') -ExpectedHeadSha '1111111111111111111111111111111111111111' -RunMetadataPath $fixture
    }

    Write-RunFixture -Path $fixture -Repository 'other/repo'
    Assert-Rejected -ExpectedMessage "does not match 'owlCoder/toren-ide'" -Action {
        & (Join-Path $PSScriptRoot 'assert-github-workflow-run.ps1') -RunId '12345' -ExpectedRepository 'owlCoder/toren-ide' -ExpectedWorkflowName 'Package' -AllowedEvents @('push') -RunMetadataPath $fixture
    }

    Write-Host 'GitHub workflow run provenance contract tests passed.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
