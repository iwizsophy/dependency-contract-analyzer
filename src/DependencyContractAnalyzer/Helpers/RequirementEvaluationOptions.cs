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
        var options = AnalyzerConfigOptionReader.GetSourceOptions(analyzerConfigOptionsProvider, type);
        if (options is null)
        {
            return Default;
        }

        return new RequirementEvaluationOptions(
            AnalyzerConfigOptionReader.GetBooleanOption(options, ReportUnusedRequirementDiagnosticsKey, defaultValue: true),
            AnalyzerConfigOptionReader.GetBooleanOption(options, ReportUndeclaredRequirementDiagnosticsKey, defaultValue: true));
    }
}
