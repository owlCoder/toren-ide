Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$resolver = Join-Path $PSScriptRoot 'resolve-package-identity.ps1'

function Assert-Rejected {
    param([scriptblock] $Action)
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Expected invalid package identity to be rejected.' }
}

$preview = & $resolver -RefType tag -RefName 'v1.0.0-preview.1'
if ($preview.Version -ne '1.0.0-preview.1' -or $preview.Channel -ne 'preview' -or $preview.NumericVersion -ne '1.0.0') {
    throw 'Preview identity lost its prerelease version or numeric bundle version.'
}
$stable = & $resolver -RefType tag -RefName 'v1.2.3'
if ($stable.Version -ne '1.2.3' -or $stable.Channel -ne 'stable') { throw 'Stable tag resolved incorrectly.' }
$development = & $resolver -RefType branch -RefName main -RunNumber 17
if ($development.Version -ne '0.0.0-preview.17') { throw 'Development identity resolved incorrectly.' }
$manual = & $resolver -RefType branch -RefName main -InputVersion '1.2.3-preview.4' -InputChannel preview
if ($manual.Version -ne '1.2.3-preview.4') { throw 'Manual package version was ignored.' }

Assert-Rejected { & $resolver -RefType tag -RefName 'main' }
Assert-Rejected { & $resolver -RefType tag -RefName 'v1.0.0-preview.0' }
Assert-Rejected { & $resolver -RefType tag -RefName 'v1.0.0' -InputVersion '2.0.0' -InputChannel stable }
Assert-Rejected { & $resolver -RefType tag -RefName 'v1.0.0-preview.1' -InputVersion '1.0.0-preview.1' -InputChannel stable }
Assert-Rejected { & $resolver -RefType branch -InputVersion '1.0.0-preview.1' -InputChannel stable }
Assert-Rejected { & $resolver -RefType branch -InputVersion '1.0.0' -InputChannel preview }
Assert-Rejected { & $resolver -RefType branch -InputVersion "1.0.0`nOTHER=value" -InputChannel stable }
Assert-Rejected { & $resolver -RefType branch -RunNumber 0 }
Write-Host 'Package identity contract tests passed.'
