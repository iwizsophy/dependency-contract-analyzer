using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct BehaviorPresetOptions
{
    private const string BehaviorPresetKey = "dependency_contract_analyzer.behavior_preset";

    public BehaviorPresetOptions(BehaviorPreset preset)
    {
        Preset = preset;
    }

    public BehaviorPreset Preset { get; }

    // Presets only define fallback defaults. Explicit per-option settings still win.
    public bool DefaultOptionalDependencySourceAnalysisEnabled =>
        Preset != BehaviorPreset.Relaxed;

    public int DefaultNamespaceInferenceMaxSegments =>
        Preset switch
        {
            BehaviorPreset.Strict => 2,
            BehaviorPreset.Relaxed => 0,
            _ => 1,
        };

    public ExternalDependencyPolicy DefaultExternalDependencyPolicy =>
        Preset == BehaviorPreset.Strict
            ? ExternalDependencyPolicy.Metadata
            : ExternalDependencyPolicy.Ignore;

    public static BehaviorPresetOptions Default => new(BehaviorPreset.Default);

    public static BehaviorPresetOptions Create(AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider)
    {
        if (!analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(BehaviorPresetKey, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return Default;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "default" => Default,
            "strict" => new BehaviorPresetOptions(BehaviorPreset.Strict),
            "relaxed" => new BehaviorPresetOptions(BehaviorPreset.Relaxed),
            _ => Default,
        };
    }
}

internal enum BehaviorPreset
{
    Default,
    Strict,
    Relaxed,
}
