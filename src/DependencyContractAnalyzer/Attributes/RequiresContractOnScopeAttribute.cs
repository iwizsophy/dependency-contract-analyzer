using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that dependencies in a specific scope must satisfy.
/// </summary>
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
    /// Gets the dependency scope to match.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the required contract name.
    /// </summary>
    public string ContractName { get; }
}
