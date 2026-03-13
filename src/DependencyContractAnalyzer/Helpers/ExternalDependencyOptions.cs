using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct ExternalDependencyOptions
{
    private const string ExternalDependencyPolicyKey = "dependency_contract_analyzer.external_dependency_policy";

    public ExternalDependencyOptions(ExternalDependencyPolicy policy)
    {
        Policy = policy;
    }

    public ExternalDependencyPolicy Policy { get; }

    public static ExternalDependencyOptions Default => new(ExternalDependencyPolicy.Ignore);

    public static ExternalDependencyOptions Create(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        if (!analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(ExternalDependencyPolicyKey, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return Default;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "ignore" => Default,
            "metadata" => new ExternalDependencyOptions(ExternalDependencyPolicy.Metadata),
            _ => Default,
        };
    }
}

internal enum ExternalDependencyPolicy
{
    Ignore,
    Metadata,
}
