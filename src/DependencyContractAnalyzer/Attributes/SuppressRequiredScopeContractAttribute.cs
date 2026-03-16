using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresContractOnScopeAttribute"/> declaration.
/// </summary>
/// <remarks>
/// Suppression is an exact-match rule on the owning type. The analyzer matches the
/// normalized scope name together with the normalized contract name and does not
/// broaden the suppression to dependency-type or target requirements.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredScopeContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuppressRequiredScopeContractAttribute"/> class.
    /// </summary>
    /// <param name="scopeName">The scope name to suppress.</param>
    /// <param name="contractName">The contract name to suppress.</param>
    public SuppressRequiredScopeContractAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the scope name to suppress after consumer-side normalization by the analyzer.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the suppressed contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
