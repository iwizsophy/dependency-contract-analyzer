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
        var behaviorPreset = BehaviorPresetOptions.Create(analyzerConfigOptionsProvider);
        var defaultPolicy = behaviorPreset.DefaultExternalDependencyPolicy;
        var normalizedValue = AnalyzerConfigOptionReader.GetNormalizedGlobalOption(
            analyzerConfigOptionsProvider,
            ExternalDependencyPolicyKey);

        // External dependency handling is a compilation-wide concern because known
        // metadata and implication graphs are shared across analyzed types.
        if (normalizedValue is null)
        {
            return new ExternalDependencyOptions(defaultPolicy);
        }

        return normalizedValue switch
        {
            "ignore" => Default,
            "metadata" => new ExternalDependencyOptions(ExternalDependencyPolicy.Metadata),
            _ => new ExternalDependencyOptions(defaultPolicy),
        };
    }
}

internal enum ExternalDependencyPolicy
{
    Ignore,
    Metadata,
}
