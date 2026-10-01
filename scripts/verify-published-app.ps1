[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string] $RuntimeIdentifier,
    [Parameter(Mandatory)] [string] $ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$nativeFiles = switch -Wildcard ($RuntimeIdentifier) {
    'win-*' { @('Toren.App.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll') }
    'linux-*' { @('Toren.App', 'libcoreclr.so', 'libhostfxr.so', 'libhostpolicy.so') }
    'osx-*' { @('Toren.App', 'libcoreclr.dylib', 'libhostfxr.dylib', 'libhostpolicy.dylib') }
}
$requiredFiles = $nativeFiles + @(
    'Toren.App.dll', 'Toren.App.deps.json', 'Toren.App.runtimeconfig.json',
    'System.Private.CoreLib.dll', 'README.md', 'LICENSE', 'release-info.json'
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory $file) -PathType Leaf)) {
        throw "Self-contained publish is missing '$file'."
    }
}

$runtime = Get-Content -LiteralPath (Join-Path $PublishDirectory 'Toren.App.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtime.runtimeOptions.tfm -ne 'net10.0' -or
    @($runtime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').Count -ne 1) {
    throw 'Publish does not contain the expected self-contained .NET 10 runtime configuration.'
}
$assembly = Join-Path $PublishDirectory 'Toren.App.dll'
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).ProductVersion.Split('+')[0]
if ($productVersion -ne $ExpectedVersion) {
    throw "Published assembly version '$productVersion' does not match '$ExpectedVersion'."
}
$info = Get-Content -LiteralPath (Join-Path $PublishDirectory 'release-info.json') -Raw | ConvertFrom-Json
if ($info.version -ne $ExpectedVersion -or $info.runtimeIdentifier -ne $RuntimeIdentifier -or $info.selfContained -ne $true) {
    throw 'Package identity does not match the published app.'
}
Write-Host "Verified self-contained $RuntimeIdentifier publish at version $ExpectedVersion."
