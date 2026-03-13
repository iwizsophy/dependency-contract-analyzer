using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal static class AnalyzerConfigOptionReader
{
    public static string? GetNormalizedGlobalOption(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        string key)
    {
        if (!analyzerConfigOptionsProvider.GlobalOptions.TryGetValue(key, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return rawValue.Trim().ToLowerInvariant();
    }

    public static AnalyzerConfigOptions? GetSourceOptions(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        var sourceTree = GetSourceTree(type);
        return sourceTree is null
            ? null
            : analyzerConfigOptionsProvider.GetOptions(sourceTree);
    }

    public static bool GetBooleanOption(
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

    public static int GetGlobalIntOption(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        string key,
        int defaultValue,
        int minValue,
        int maxValue)
    {
        var normalizedValue = GetNormalizedGlobalOption(analyzerConfigOptionsProvider, key);
        if (!int.TryParse(normalizedValue, out var parsedValue) ||
            parsedValue < minValue ||
            parsedValue > maxValue)
        {
            return defaultValue;
        }

        return parsedValue;
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
}
