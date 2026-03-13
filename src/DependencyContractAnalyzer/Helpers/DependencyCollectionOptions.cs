using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct DependencyCollectionOptions
{
    private const string AnalyzeFieldsKey = "dependency_contract_analyzer.analyze_fields";
    private const string AnalyzeBaseTypesKey = "dependency_contract_analyzer.analyze_base_types";
    private const string AnalyzeInterfaceImplementationsKey = "dependency_contract_analyzer.analyze_interface_implementations";
    private const string AnalyzeMethodParametersKey = "dependency_contract_analyzer.analyze_method_parameters";
    private const string AnalyzePropertiesKey = "dependency_contract_analyzer.analyze_properties";
    private const string AnalyzeObjectCreationKey = "dependency_contract_analyzer.analyze_object_creation";
    private const string AnalyzeStaticMembersKey = "dependency_contract_analyzer.analyze_static_members";

    public DependencyCollectionOptions(
        bool analyzeFields,
        bool analyzeBaseTypes,
        bool analyzeInterfaceImplementations,
        bool analyzeMethodParameters,
        bool analyzeProperties,
        bool analyzeObjectCreation,
        bool analyzeStaticMembers)
    {
        AnalyzeFields = analyzeFields;
        AnalyzeBaseTypes = analyzeBaseTypes;
        AnalyzeInterfaceImplementations = analyzeInterfaceImplementations;
        AnalyzeMethodParameters = analyzeMethodParameters;
        AnalyzeProperties = analyzeProperties;
        AnalyzeObjectCreation = analyzeObjectCreation;
        AnalyzeStaticMembers = analyzeStaticMembers;
    }

    public bool AnalyzeFields { get; }

    public bool AnalyzeBaseTypes { get; }

    public bool AnalyzeInterfaceImplementations { get; }

    public bool AnalyzeMethodParameters { get; }

    public bool AnalyzeProperties { get; }

    public bool AnalyzeObjectCreation { get; }

    public bool AnalyzeStaticMembers { get; }

    public static DependencyCollectionOptions Default =>
        new(
            analyzeFields: true,
            analyzeBaseTypes: true,
            analyzeInterfaceImplementations: true,
            analyzeMethodParameters: true,
            analyzeProperties: true,
            analyzeObjectCreation: true,
            analyzeStaticMembers: true);

    public static DependencyCollectionOptions Create(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        var behaviorPreset = BehaviorPresetOptions.Create(analyzerConfigOptionsProvider);
        var options = AnalyzerConfigOptionReader.GetSourceOptions(analyzerConfigOptionsProvider, type);
        if (options is null)
        {
            // Metadata-only types cannot map back to a source-specific editorconfig
            // section, so fall back to preset-derived defaults for all optional sources.
            return new DependencyCollectionOptions(
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled);
        }

        // Source-scoped toggles allow teams to ratchet dependency extraction up or down
        // for specific files without changing the whole compilation at once.
        return new DependencyCollectionOptions(
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeFieldsKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeBaseTypesKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeInterfaceImplementationsKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeMethodParametersKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzePropertiesKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeObjectCreationKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            AnalyzerConfigOptionReader.GetBooleanOption(options, AnalyzeStaticMembersKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled));
    }
}
