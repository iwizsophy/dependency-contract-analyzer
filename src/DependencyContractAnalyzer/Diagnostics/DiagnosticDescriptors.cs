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
    /// Contract name violates the naming format.
    /// </summary>
    public const string ContractNamingFormatViolation = "DCA101";

    /// <summary>
    /// Contract declaration is duplicated.
    /// </summary>
    public const string DuplicateContractDeclaration = "DCA102";

    /// <summary>
    /// Contract implication definition is cyclic.
    /// </summary>
    public const string CyclicAliasDefinition = "DCA202";

    /// <summary>
    /// Required target is undeclared in the compilation.
    /// </summary>
    public const string UndeclaredRequiredTarget = "DCA200";

    /// <summary>
    /// Required scope is undeclared in the compilation.
    /// </summary>
    public const string UndeclaredRequiredScope = "DCA201";

    /// <summary>
    /// Scope name is empty or whitespace.
    /// </summary>
    public const string EmptyScopeName = "DCA203";

    /// <summary>
    /// Target name is empty or whitespace.
    /// </summary>
    public const string EmptyTargetName = "DCA204";

    /// <summary>
    /// Required target is not used by any analyzable dependency.
    /// </summary>
    public const string UnusedRequiredTarget = "DCA205";

    /// <summary>
    /// Required scope is not used by any analyzable dependency.
    /// </summary>
    public const string UnusedRequiredScope = "DCA206";
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

    public static readonly DiagnosticDescriptor ContractNamingFormatViolation =
        new(
            DiagnosticIds.ContractNamingFormatViolation,
            "Contract name violates the naming format",
            "Contract name '{0}' must use lower-kebab-case.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UndeclaredRequiredTarget =
        new(
            DiagnosticIds.UndeclaredRequiredTarget,
            "Required target is undeclared",
            "Target '{0}' required by this type is not declared anywhere in the compilation.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UndeclaredRequiredScope =
        new(
            DiagnosticIds.UndeclaredRequiredScope,
            "Required scope is undeclared",
            "Scope '{0}' required by this type is not declared anywhere in the compilation.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyScopeName =
        new(
            DiagnosticIds.EmptyScopeName,
            "Scope name is empty",
            "Scope name must not be empty.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyTargetName =
        new(
            DiagnosticIds.EmptyTargetName,
            "Target name is empty",
            "Target name must not be empty.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedRequiredTarget =
        new(
            DiagnosticIds.UnusedRequiredTarget,
            "Required target is not used",
            "Target '{0}' specified in RequiresContractOnTarget is not used by any dependency of this type.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedRequiredScope =
        new(
            DiagnosticIds.UnusedRequiredScope,
            "Required scope is not used",
            "Scope '{0}' specified in RequiresContractOnScope is not used by any dependency of this type.",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateContractDeclaration =
        new(
            DiagnosticIds.DuplicateContractDeclaration,
            "Contract is declared multiple times",
            "Contract '{0}' is declared multiple times.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CyclicAliasDefinition =
        new(
            DiagnosticIds.CyclicAliasDefinition,
            "Contract implication definition is cyclic",
            "Contract implication '{0}' participates in a cycle.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
}
