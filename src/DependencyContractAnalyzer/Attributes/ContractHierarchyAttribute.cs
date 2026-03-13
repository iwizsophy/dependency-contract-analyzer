using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares that one contract satisfies a parent contract within the contract hierarchy.
/// </summary>
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
    /// Gets the more specific provided contract name.
    /// </summary>
    public string Child { get; }

    /// <summary>
    /// Gets the satisfied parent contract name.
    /// </summary>
    public string Parent { get; }
}
