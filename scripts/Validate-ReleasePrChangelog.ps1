param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ReleaseBranchName = 'main',
    [string]$RemoteName = 'origin',
    [switch]$SkipFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-GitCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $commandText = ($Arguments -join ' ')
        $message = if ($output) { ($output | Out-String).Trim() } else { 'No output.' }
        throw "Git command failed: git $commandText`n$message"
    }

    return @($output)
}

function Get-ChangelogVersions {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChangelogPath
    )

    $versionPattern = '^\s*## \[(?<version>\d+\.\d+\.\d+)\]\s*$'
    $versions = [System.Collections.Generic.List[Version]]::new()

    foreach ($line in Get-Content $ChangelogPath) {
        $match = [regex]::Match($line, $versionPattern)
        if (-not $match.Success) {
            continue
        }

        $versions.Add([Version]::Parse($match.Groups['version'].Value))
    }

    return $versions
}

$resolvedRepositoryRoot = (Resolve-Path $RepositoryRoot).Path
Push-Location $resolvedRepositoryRoot

try {
    if (-not $SkipFetch) {
        $remoteRefSpec = "refs/heads/$ReleaseBranchName`:refs/remotes/$RemoteName/$ReleaseBranchName"
        Invoke-GitCommand -Arguments @('fetch', $RemoteName, $remoteRefSpec, '--tags') | Out-Null
    }

    $releaseBranchRef = if ($SkipFetch) { $ReleaseBranchName } else { "$RemoteName/$ReleaseBranchName" }
    $mainReleaseTags = @(Invoke-GitCommand -Arguments @('tag', '--merged', $releaseBranchRef, '--list', 'v[0-9]*.[0-9]*.[0-9]*', '--sort=-version:refname') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $latestReleaseVersion = $null
    if ($mainReleaseTags.Count -gt 0) {
        $latestReleaseVersion = [Version]::Parse($mainReleaseTags[0].Substring(1))
    }

    $changelogPath = Join-Path $resolvedRepositoryRoot 'CHANGELOG.md'
    $changelogVersions = @(Get-ChangelogVersions -ChangelogPath $changelogPath)
    if ($changelogVersions.Count -eq 0) {
        throw 'CHANGELOG.md does not contain any version sections.'
    }

    $hasAdvancedVersion = if ($null -eq $latestReleaseVersion) {
        $true
    }
    else {
        $changelogVersions | Where-Object { $_ -gt $latestReleaseVersion } | Select-Object -First 1
    }

    if (-not $hasAdvancedVersion) {
        throw "CHANGELOG.md must contain at least one version section newer than the latest $ReleaseBranchName release tag v$latestReleaseVersion."
    }
}
finally {
    Pop-Location
}
