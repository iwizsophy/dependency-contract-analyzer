Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ciWorkflowPath = Join-Path $PSScriptRoot '..\.github\workflows\ci.yml'
$ciWorkflowContent = Get-Content $ciWorkflowPath -Raw

$requiredFragments = [ordered]@{
    'build validation command' = 'run: dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore'
    'test validation command' = 'run: dotnet test DependencyContractAnalyzer.slnx -c Release --no-restore --collect "XPlat Code Coverage" --results-directory artifacts/test-results'
    'analyzer validation command' = 'run: dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore -warnaserror'
    'Syft version environment' = 'SYFT_VERSION: v1.42.3'
    'pack validation command' = 'run: ./scripts/Invoke-PackWithSbom.ps1 -ProjectPath src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -Configuration Release -OutputDirectory artifacts -NoRestore -SyftVersion ${{ env.SYFT_VERSION }} -DownloadSyftIfMissing'
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
