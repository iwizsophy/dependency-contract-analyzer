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
        // Known target/scope sets are collected across the whole compilation, so the
        // namespace inference shape must come from a single global option.
        return new NamespaceInferenceOptions(
            AnalyzerConfigOptionReader.GetGlobalIntOption(
                analyzerConfigOptionsProvider,
                NamespaceInferenceMaxSegmentsKey,
                behaviorPreset.DefaultNamespaceInferenceMaxSegments,
                minValue: 1,
                maxValue: 2));
    }
}
