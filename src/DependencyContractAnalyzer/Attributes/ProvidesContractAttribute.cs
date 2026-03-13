using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract provided by a class or interface.
/// </summary>
/// <remarks>
/// Contract names are normalized by trimming surrounding whitespace and are
/// compared with ordinal case-insensitive semantics. The declared contract also
/// participates in transitive implication through <see cref="ContractAliasAttribute"/>
/// and <see cref="ContractHierarchyAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProvidesContractAttribute"/> class.
    /// </summary>
    /// <param name="name">The provided contract name.</param>
    public ProvidesContractAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the provided contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string Name { get; }
}
