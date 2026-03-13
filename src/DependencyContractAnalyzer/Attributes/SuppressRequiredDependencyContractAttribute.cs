using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresDependencyContractAttribute"/> declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredDependencyContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuppressRequiredDependencyContractAttribute"/> class.
    /// </summary>
    /// <param name="dependencyType">The dependency type to suppress.</param>
    /// <param name="contractName">The contract name to suppress.</param>
    public SuppressRequiredDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the dependency type to suppress.
    /// </summary>
    public Type DependencyType { get; }

    /// <summary>
    /// Gets the suppressed contract name.
    /// </summary>
    public string ContractName { get; }
}
