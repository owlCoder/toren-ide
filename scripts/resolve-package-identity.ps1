[CmdletBinding()]
param(
    [string] $RefType = $env:GITHUB_REF_TYPE,
    [string] $RefName = $env:GITHUB_REF_NAME,
    [string] $InputVersion,
    [string] $InputChannel = 'preview',
    [string] $RunNumber = $env:GITHUB_RUN_NUMBER
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RefType -eq 'tag') {
    if ($RefName -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-preview\.\d+)?)$') {
        throw "Unsupported release tag '$RefName'."
    }
    $version = $Matches['version']
    $channel = if ($version.Contains('-preview.')) { 'preview' } else { 'stable' }
    if (-not [string]::IsNullOrWhiteSpace($InputVersion) -and
        ($InputVersion -ne $version -or $InputChannel -ne $channel)) {
        throw 'Input version/channel must match the immutable release tag.'
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($InputVersion)) {
    $version = $InputVersion
    $channel = $InputChannel
}
else {
    if ($RunNumber -notmatch '^[1-9]\d*$') {
        throw 'A positive run number is required for development packages.'
    }
    $version = "0.0.0-preview.$RunNumber"
    $channel = 'preview'
}

$pattern = switch ($channel) {
    'stable' { '^\d+\.\d+\.\d+$' }
    'preview' { '^\d+\.\d+\.\d+-preview\.[1-9]\d*$' }
    default { throw "Unsupported release channel '$channel'." }
}
if ($version -notmatch $pattern) {
    throw "Version '$version' is not valid for the '$channel' channel."
}

# Apple bundle versions and .NET assembly versions use the numeric portion;
# the complete SemVer remains in assembly metadata and TorenReleaseVersion.
[pscustomobject]@{
    Version = $version
    Channel = $channel
    NumericVersion = $version.Split('-')[0]
}
