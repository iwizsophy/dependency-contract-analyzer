using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresContractOnScopeAttribute"/> declaration.
/// </summary>
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
    /// Gets the scope name to suppress.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the suppressed contract name.
    /// </summary>
    public string ContractName { get; }
}
