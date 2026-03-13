using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct DependencyCollectionOptions
{
    private const string AnalyzeMethodParametersKey = "dependency_contract_analyzer.analyze_method_parameters";
    private const string AnalyzePropertiesKey = "dependency_contract_analyzer.analyze_properties";
    private const string AnalyzeObjectCreationKey = "dependency_contract_analyzer.analyze_object_creation";
    private const string AnalyzeStaticMembersKey = "dependency_contract_analyzer.analyze_static_members";

    public DependencyCollectionOptions(
        bool analyzeMethodParameters,
        bool analyzeProperties,
        bool analyzeObjectCreation,
        bool analyzeStaticMembers)
    {
        AnalyzeMethodParameters = analyzeMethodParameters;
        AnalyzeProperties = analyzeProperties;
        AnalyzeObjectCreation = analyzeObjectCreation;
        AnalyzeStaticMembers = analyzeStaticMembers;
    }

    public bool AnalyzeMethodParameters { get; }

    public bool AnalyzeProperties { get; }

    public bool AnalyzeObjectCreation { get; }

    public bool AnalyzeStaticMembers { get; }

    public static DependencyCollectionOptions Default =>
        new(
            analyzeMethodParameters: true,
            analyzeProperties: true,
            analyzeObjectCreation: true,
            analyzeStaticMembers: true);

    public static DependencyCollectionOptions Create(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        var sourceTree = GetSourceTree(type);
        if (sourceTree is null)
        {
            return Default;
        }

        var options = analyzerConfigOptionsProvider.GetOptions(sourceTree);
        return new DependencyCollectionOptions(
            GetBooleanOption(options, AnalyzeMethodParametersKey, defaultValue: true),
            GetBooleanOption(options, AnalyzePropertiesKey, defaultValue: true),
            GetBooleanOption(options, AnalyzeObjectCreationKey, defaultValue: true),
            GetBooleanOption(options, AnalyzeStaticMembersKey, defaultValue: true));
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
