using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct AnalysisExclusionOptions
{
    private const string ExcludedNamespacesKey = "dependency_contract_analyzer.excluded_namespaces";
    private const string ExcludedTypesKey = "dependency_contract_analyzer.excluded_types";

    private AnalysisExclusionOptions(
        IReadOnlyCollection<string> excludedNamespaces,
        IReadOnlyCollection<string> excludedTypes)
    {
        ExcludedNamespaces = excludedNamespaces;
        ExcludedTypes = excludedTypes;
    }

    public IReadOnlyCollection<string> ExcludedNamespaces { get; }

    public IReadOnlyCollection<string> ExcludedTypes { get; }

    public static AnalysisExclusionOptions Create(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        var options = AnalyzerConfigOptionReader.GetSourceOptions(analyzerConfigOptionsProvider, type);
        if (options is null)
        {
            return new AnalysisExclusionOptions(Array.Empty<string>(), Array.Empty<string>());
        }

        // Exclusion lists are source-scoped so teams can carve out migration-heavy
        // files or namespaces gradually without suppressing the entire solution.
        return new AnalysisExclusionOptions(
            GetListOption(options, ExcludedNamespacesKey),
            GetListOption(options, ExcludedTypesKey));
    }

    public bool ShouldSkipType(INamedTypeSymbol type)
    {
        var fullTypeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (ExcludedTypes.Contains(fullTypeName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (type.ContainingNamespace is not { IsGlobalNamespace: false } containingNamespace)
        {
            return false;
        }

        var namespaceName = containingNamespace.ToDisplayString();
        foreach (var excludedNamespace in ExcludedNamespaces)
        {
            // Namespace exclusions cascade to subnamespaces to avoid repeating entries
            // for each nested area of the same architectural slice.
            if (namespaceName.Equals(excludedNamespace, StringComparison.OrdinalIgnoreCase) ||
                namespaceName.StartsWith(excludedNamespace + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    private static string[] GetListOption(AnalyzerConfigOptions options, string key)
    {
        if (!options.TryGetValue(key, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<string>();
        }

        return rawValue
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
