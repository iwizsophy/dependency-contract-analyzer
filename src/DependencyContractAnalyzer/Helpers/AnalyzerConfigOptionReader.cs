using System;
using System.Collections.Generic;
using System.Linq;
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

    public static bool GetBooleanOption(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type,
        string key,
        bool defaultValue)
    {
        var resolvedValue = defaultValue;
        var hasExplicitValue = false;

        foreach (var options in EnumerateSourceOptions(analyzerConfigOptionsProvider, type))
        {
            if (!options.TryGetValue(key, out var rawValue) ||
                rawValue is null ||
                !bool.TryParse(rawValue.Trim(), out var parsedValue))
            {
                continue;
            }

            // Partial declarations can span editorconfig sections. Merge explicit values
            // conservatively so a single "false" cannot be bypassed by declaration order.
            resolvedValue = hasExplicitValue
                ? resolvedValue && parsedValue
                : parsedValue;
            hasExplicitValue = true;
        }

        return hasExplicitValue
            ? resolvedValue
            : defaultValue;
    }

    public static string[] GetListOption(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type,
        string key)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var options in EnumerateSourceOptions(analyzerConfigOptionsProvider, type))
        {
            if (!options.TryGetValue(key, out var rawValue) ||
                string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var value in rawValue.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmedValue = value.Trim();
                if (trimmedValue.Length > 0)
                {
                    values.Add(trimmedValue);
                }
            }
        }

        return values.Count == 0
            ? Array.Empty<string>()
            : values.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray();
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

    private static IEnumerable<AnalyzerConfigOptions> EnumerateSourceOptions(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        var seenSourceTrees = new HashSet<SyntaxTree>();

        foreach (var location in type.Locations)
        {
            if (location is { IsInSource: true, SourceTree: { } sourceTree } &&
                seenSourceTrees.Add(sourceTree))
            {
                yield return analyzerConfigOptionsProvider.GetOptions(sourceTree);
            }
        }
    }
}
