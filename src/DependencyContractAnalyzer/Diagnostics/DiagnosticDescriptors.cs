using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Diagnostics;

/// <summary>
/// Defines the diagnostic identifiers emitted by <c>DependencyContractAnalyzer</c>.
/// </summary>
public static class DiagnosticIds
{
    /// <summary>
    /// Dependency does not provide the required contract.
    /// </summary>
    public const string MissingRequiredContract = "DCA001";

    /// <summary>
    /// Declared dependency type is not used by the target type.
    /// </summary>
    public const string UnusedRequiredDependencyType = "DCA002";

    /// <summary>
    /// Contract name is empty or whitespace.
    /// </summary>
    public const string EmptyContractName = "DCA100";

    /// <summary>
    /// Contract declaration is duplicated.
    /// </summary>
    public const string DuplicateContractDeclaration = "DCA102";
}

internal static class DiagnosticDescriptors
{
    private const string Category = "DependencyContracts";

    public static readonly DiagnosticDescriptor MissingRequiredContract =
        new(
            DiagnosticIds.MissingRequiredContract,
            "Dependency does not provide the required contract",
            "Dependency '{0}' does not provide required contract '{1}'.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedRequiredDependencyType =
        new(
            DiagnosticIds.UnusedRequiredDependencyType,
            "Declared dependency type is not used",
            "Dependency '{0}' specified in RequiresDependencyContract is not used by the target type.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyContractName =
        new(
            DiagnosticIds.EmptyContractName,
            "Contract name is empty",
            "Contract name must not be empty.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateContractDeclaration =
        new(
            DiagnosticIds.DuplicateContractDeclaration,
            "Contract is declared multiple times",
            "Contract '{0}' is declared multiple times.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
}
