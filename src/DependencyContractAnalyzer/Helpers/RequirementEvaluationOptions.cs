using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct RequirementEvaluationOptions
{
    private const string ReportUnusedRequirementDiagnosticsKey =
        "dependency_contract_analyzer.report_unused_requirement_diagnostics";
    private const string ReportUndeclaredRequirementDiagnosticsKey =
        "dependency_contract_analyzer.report_undeclared_requirement_diagnostics";

    private RequirementEvaluationOptions(
        bool reportUnusedRequirementDiagnostics,
        bool reportUndeclaredRequirementDiagnostics)
    {
        ReportUnusedRequirementDiagnostics = reportUnusedRequirementDiagnostics;
        ReportUndeclaredRequirementDiagnostics = reportUndeclaredRequirementDiagnostics;
    }

    public bool ReportUnusedRequirementDiagnostics { get; }

    public bool ReportUndeclaredRequirementDiagnostics { get; }

    public static RequirementEvaluationOptions Default => new(
        reportUnusedRequirementDiagnostics: true,
        reportUndeclaredRequirementDiagnostics: true);

    public static RequirementEvaluationOptions Create(
        AnalyzerConfigOptionsProvider analyzerConfigOptionsProvider,
        INamedTypeSymbol type)
    {
        // Keep these switches source-scoped like the dependency-source toggles so
        // teams can relax migration-heavy areas without changing the whole solution.
        var sourceTree = GetSourceTree(type);
        if (sourceTree is null)
        {
            return Default;
        }

        var options = analyzerConfigOptionsProvider.GetOptions(sourceTree);
        return new RequirementEvaluationOptions(
            GetBooleanOption(options, ReportUnusedRequirementDiagnosticsKey, defaultValue: true),
            GetBooleanOption(options, ReportUndeclaredRequirementDiagnosticsKey, defaultValue: true));
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
