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
        var sourceTree = GetSourceTree(type);
        if (sourceTree is null)
        {
            return new DependencyCollectionOptions(
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled,
                behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled);
        }

        var options = analyzerConfigOptionsProvider.GetOptions(sourceTree);
        return new DependencyCollectionOptions(
            GetBooleanOption(options, AnalyzeFieldsKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzeBaseTypesKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzeInterfaceImplementationsKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzeMethodParametersKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzePropertiesKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzeObjectCreationKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled),
            GetBooleanOption(options, AnalyzeStaticMembersKey, behaviorPreset.DefaultOptionalDependencySourceAnalysisEnabled));
    }

    private static SyntaxTree? GetSourceTree(INamedTypeSymbol type)
    {
        foreach (var location in type.Locations)
        {
            if (location.IsInSource && location.SourceTree is not null)
            {
                return location.SourceTree;
            }
        }

        return null;
    }

    private static bool GetBooleanOption(
        AnalyzerConfigOptions options,
        string key,
        bool defaultValue)
    {
        if (!options.TryGetValue(key, out var rawValue) ||
            !bool.TryParse(rawValue, out var parsedValue))
        {
            return defaultValue;
        }

        return parsedValue;
    }
}
