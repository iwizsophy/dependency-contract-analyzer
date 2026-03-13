using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares that one contract satisfies a parent contract within the contract hierarchy.
/// </summary>
/// <remarks>
/// This attribute contributes an assembly-level implication edge from <see cref="Child"/>
/// to <see cref="Parent"/>. Hierarchy edges are resolved together with
/// <see cref="ContractAliasAttribute"/> edges, so a child contract satisfies every
/// ancestor reachable through the combined transitive graph.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractHierarchyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractHierarchyAttribute"/> class.
    /// </summary>
    /// <param name="child">The more specific provided contract name.</param>
    /// <param name="parent">The satisfied parent contract name.</param>
    public ContractHierarchyAttribute(string child, string parent)
    {
        Child = child;
        Parent = parent;
    }

    /// <summary>
    /// Gets the more specific provided contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string Child { get; }

    /// <summary>
    /// Gets the satisfied parent contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string Parent { get; }
}
