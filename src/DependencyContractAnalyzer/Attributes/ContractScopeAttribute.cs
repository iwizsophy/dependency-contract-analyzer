using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares an architectural scope for a class, interface, or assembly.
/// </summary>
/// <remarks>
/// Scopes are matched by <see cref="RequiresContractOnScopeAttribute"/> and are
/// normalized by trimming surrounding whitespace and comparing with ordinal
/// case-insensitive semantics. When this attribute is applied at the assembly
/// level, the declared scope applies to types in that assembly. Type-level scope
/// declarations add scopes for the type and do not remove assembly-level scopes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractScopeAttribute"/> class.
    /// </summary>
    /// <param name="name">The declared scope name.</param>
    public ContractScopeAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the declared scope name after consumer-side normalization by the analyzer.
    /// </summary>
    public string Name { get; }
}
