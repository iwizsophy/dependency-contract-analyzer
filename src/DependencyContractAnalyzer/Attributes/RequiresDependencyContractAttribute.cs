using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that a specific dependency type must satisfy.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresDependencyContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresDependencyContractAttribute"/> class.
    /// </summary>
    /// <param name="dependencyType">The dependency type to match.</param>
    /// <param name="contractName">The required contract name.</param>
    public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the dependency type to match.
    /// </summary>
    public Type DependencyType { get; }

    /// <summary>
    /// Gets the required contract name.
    /// </summary>
    public string ContractName { get; }
}
