using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that dependencies in a specific scope must satisfy.
/// </summary>
/// <remarks>
/// The analyzer resolves dependency scopes from assembly-level and type-level
/// explicit <see cref="ContractScopeAttribute"/> declarations first. Type-level
/// scope declarations add scopes for the type without removing assembly-level
/// scopes, and namespace inference is used only when the type has no type-level
/// explicit scope. Scope names are normalized by trimming surrounding whitespace
/// and are compared with ordinal case-insensitive semantics.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnScopeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresContractOnScopeAttribute"/> class.
    /// </summary>
    /// <param name="scopeName">The dependency scope to match.</param>
    /// <param name="contractName">The required contract name.</param>
    public RequiresContractOnScopeAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the dependency scope to match after consumer-side normalization by the analyzer.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the required contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
