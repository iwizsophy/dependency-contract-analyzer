using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct NamespaceInferenceOptions
{
    private const string NamespaceInferenceMaxSegmentsKey = "dependency_contract_analyzer.namespace_inference_max_segments";

    public NamespaceInferenceOptions(int maxSegments)
    {
        MaxSegments = maxSegments;
    }

    public int MaxSegments { get; }

    public static NamespaceInferenceOptions Default => new(maxSegments: 1);

    public static NamespaceInferenceOptions Create(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        var behaviorPreset = BehaviorPresetOptions.Create(analyzerConfigOptionsProvider);
        var defaultMaxSegments = behaviorPreset.DefaultNamespaceInferenceMaxSegments;

        if (!analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(NamespaceInferenceMaxSegmentsKey, out var rawValue) ||
            !int.TryParse(rawValue, out var parsedValue) ||
            parsedValue is < 1 or > 2)
        {
            return new NamespaceInferenceOptions(defaultMaxSegments);
        }

        return new NamespaceInferenceOptions(parsedValue);
    }
}
