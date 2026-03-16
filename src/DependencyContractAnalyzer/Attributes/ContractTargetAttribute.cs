using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a role or category for a class or interface.
/// </summary>
/// <remarks>
/// Targets are matched by <see cref="RequiresContractOnTargetAttribute"/> and are
/// normalized by trimming surrounding whitespace and comparing with ordinal
/// case-insensitive semantics. Target names are intentionally separate from
/// contract-name validation rules such as <c>DCA101</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractTargetAttribute"/> class.
    /// </summary>
    /// <param name="name">The declared target name.</param>
    public ContractTargetAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the declared target name after consumer-side normalization by the analyzer.
    /// </summary>
    public string Name { get; }
}
