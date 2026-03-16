using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that a specific dependency type must satisfy.
/// </summary>
/// <remarks>
/// A requirement matches when the discovered dependency type, one of its base
/// types, or one of its implemented interfaces matches <see cref="DependencyType"/>.
/// Contract names are normalized by trimming surrounding whitespace and are
/// compared with ordinal case-insensitive semantics.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresDependencyContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresDependencyContractAttribute"/> class.
    /// </summary>
    /// <param name="dependencyType">The dependency type, base type, or interface to match.</param>
    /// <param name="contractName">The required contract name.</param>
    public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the dependency type, base type, or interface to match.
    /// </summary>
    public Type DependencyType { get; }

    /// <summary>
    /// Gets the required contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
