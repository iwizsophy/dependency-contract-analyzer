Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ciWorkflowPath = Join-Path $PSScriptRoot '..\.github\workflows\ci.yml'
$ciWorkflowContent = Get-Content $ciWorkflowPath -Raw

$requiredFragments = [ordered]@{
    'build validation command' = 'run: dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore'
    'test validation command' = 'run: dotnet test DependencyContractAnalyzer.slnx -c Release --no-restore --collect "XPlat Code Coverage" --results-directory artifacts/test-results'
    'analyzer validation command' = 'run: dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore -warnaserror'
    'pack validation command' = 'run: dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-restore -o artifacts'
    'release PR changelog validation step name' = '- name: Validate release PR changelog advancement'
    'release PR changelog validation condition' = "if: github.event_name == 'pull_request' && github.base_ref == 'main'"
    'release PR changelog validation command' = 'run: ./scripts/Validate-ReleasePrChangelog.ps1'
}

foreach ($requiredFragment in $requiredFragments.GetEnumerator()) {
    if (-not $ciWorkflowContent.Contains($requiredFragment.Value)) {
        throw "Missing CI workflow invariant: $($requiredFragment.Key)."
    }
}

if ($ciWorkflowContent.Contains('-m:1')) {
    throw 'CI workflow must not force single-process MSBuild execution.'
}
