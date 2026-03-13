using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a role or category for a class or interface.
/// </summary>
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
    /// Gets the declared target name.
    /// </summary>
    public string Name { get; }
}
