Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$publishWorkflowPath = Join-Path $PSScriptRoot '..\.github\workflows\publish.yml'
$publishWorkflowContent = Get-Content $publishWorkflowPath -Raw

$requiredFragments = [ordered]@{
    'contents write permission' = 'contents: write'
    'NuGet.org package routing' = 'package_source=https://www.nuget.org/api/v2/package'
    'int.nugettest.org package routing' = 'package_source=https://int.nugettest.org/api/v2/package'
    'release notes step name' = '- name: Prepare GitHub Release notes'
    'release notes step id' = 'id: github_release_notes'
    'release step name' = '- name: Create or update GitHub Release'
    'main tag release condition' = "if: github.event_name == 'push' && steps.publish_target.outputs.publish_branch == 'main'"
    'GitHub token wiring' = 'GH_TOKEN: ${{ github.token }}'
    'release notes source' = 'Failed to locate release notes for version $version in CHANGELOG.md.'
    'release notes output' = 'release_notes_file=$notesFile'
    'release notes reuse' = '${{ steps.github_release_notes.outputs.release_notes_file }}'
    'release update command' = 'gh release edit $env:GITHUB_REF_NAME --title $env:GITHUB_REF_NAME --notes-file $notesFile'
    'release create command' = 'gh release create $env:GITHUB_REF_NAME --verify-tag --title $env:GITHUB_REF_NAME --notes-file $notesFile'
}

foreach ($requiredFragment in $requiredFragments.GetEnumerator()) {
    if (-not $publishWorkflowContent.Contains($requiredFragment.Value)) {
        throw "Missing publish workflow invariant: $($requiredFragment.Key)."
    }
}

$prepareReleaseNotesIndex = $publishWorkflowContent.IndexOf('- name: Prepare GitHub Release notes', [System.StringComparison]::Ordinal)
$publishPackagesIndex = $publishWorkflowContent.IndexOf('- name: Publish packages', [System.StringComparison]::Ordinal)
$releaseStepIndex = $publishWorkflowContent.IndexOf('- name: Create or update GitHub Release', [System.StringComparison]::Ordinal)
if ($prepareReleaseNotesIndex -lt 0 -or $publishPackagesIndex -lt 0 -or $releaseStepIndex -lt 0) {
    throw 'Failed to determine publish workflow step ordering.'
}

if ($prepareReleaseNotesIndex -ge $publishPackagesIndex) {
    throw 'The GitHub Release notes step must run before the NuGet publish step.'
}

if ($releaseStepIndex -le $publishPackagesIndex) {
    throw 'The GitHub Release step must run after the NuGet publish step succeeds.'
}
