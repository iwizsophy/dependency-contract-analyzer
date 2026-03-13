using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresDependencyContractAttribute"/> declaration.
/// </summary>
/// <remarks>
/// Suppression is an exact-match rule on the owning type. The analyzer matches the
/// normalized contract name together with the fully qualified dependency type and
/// does not broaden the suppression to other requirement kinds.
/// </remarks>
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
    /// Gets the suppressed contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
