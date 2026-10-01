Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) "toren-draft-$([Guid]::NewGuid().ToString('N'))"
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $root 'output'
$manifest = Join-Path $artifacts 'Toren-IDE-release-manifest/Toren-IDE-release-manifest.json'
$commit = '1111111111111111111111111111111111111111'

function Write-Fixture {
    if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    $entries = foreach ($rid in @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')) {
        $directory = Join-Path $artifacts "Toren-IDE-$rid"
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $extension = if ($rid -eq 'linux-x64') { '.tar.gz' } else { '.zip' }
        $fileName = "Toren-IDE-$rid$extension"
        $path = Join-Path $directory $fileName
        Set-Content -LiteralPath $path -Value "fixture-$rid" -Encoding utf8NoBOM
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $fileName" | Set-Content -LiteralPath "$path.sha256" -Encoding utf8NoBOM -NoNewline
        [ordered]@{
            runtimeIdentifier = $rid
            fileName = $fileName
            sha256 = $hash
            packageValidated = $true
            requiresPlatformSigning = $rid.StartsWith('osx-')
            distributionReady = $false
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $manifest -Parent) | Out-Null
    [ordered]@{
        schemaVersion = 1
        product = 'Toren IDE'
        version = '1.0.0-preview.1'
        channel = 'preview'
        sourceCommit = $commit
        artifacts = @($entries)
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8NoBOM
}

function Invoke-Draft {
    & (Join-Path $PSScriptRoot 'prepare-draft-release.ps1') -ArtifactsRoot $artifacts -ExpectedVersion '1.0.0-preview.1' -ExpectedSourceCommit $commit -OutputDirectory $output
}

function Change-Manifest {
    param([scriptblock] $Change)
    $candidate = Get-Content $manifest -Raw | ConvertFrom-Json
    & $Change $candidate
    $candidate | ConvertTo-Json -Depth 5 | Set-Content $manifest -Encoding utf8NoBOM
}

function Assert-Rejected {
    param([scriptblock] $Change, [string] $Message)
    Write-Fixture
    & $Change
    try { Invoke-Draft }
    catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw "Expected '$Message', got '$($_.Exception.Message)'." }
        return
    }
    throw "Draft preparation did not reject '$Message'."
}

try {
    Write-Fixture
    Invoke-Draft
    if (@(Get-ChildItem $output -File).Count -ne 9) { throw 'Wrong draft asset count.' }
    $copied = Get-Content (Join-Path $output 'Toren-IDE-release-manifest.json') -Raw | ConvertFrom-Json
    if (@($copied.artifacts | Where-Object distributionReady -ne $false).Count -ne 0) {
        throw 'Draft incorrectly promoted candidates to public distribution readiness.'
    }
    Assert-Rejected { Add-Content (Join-Path $artifacts 'Toren-IDE-win-x64/Toren-IDE-win-x64.zip') 'tampered' } 'Checksum mismatch'
    Assert-Rejected { Change-Manifest { param($c) $c.sourceCommit = '2222222222222222222222222222222222222222' } } 'draft release identity'
    Assert-Rejected { Change-Manifest { param($c) $c.artifacts[0].runtimeIdentifier = 'linux-x64' } } "exactly one 'win-x64'"
    Assert-Rejected { Change-Manifest { param($c) $c.artifacts[0].fileName = '../outside.zip' } } 'policy or filename'
    Assert-Rejected { Change-Manifest { param($c) $c.artifacts[2].requiresPlatformSigning = $false } } 'policy or filename'
    Assert-Rejected { Change-Manifest { param($c) $c.artifacts[0].distributionReady = $true } } 'policy or filename'
    Assert-Rejected { Set-Content (Join-Path $artifacts 'unexpected.txt') 'extra' } 'unexpected files'
    Assert-Rejected {
        New-Item -ItemType Directory -Path $output | Out-Null
        Set-Content (Join-Path $output 'stale.txt') 'stale'
    } 'must be empty'
    Write-Host 'Draft release contract tests passed.'
}
finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
