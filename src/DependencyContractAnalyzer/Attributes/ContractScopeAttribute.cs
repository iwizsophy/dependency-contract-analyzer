using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares an architectural scope for a class, interface, or assembly.
/// </summary>
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
    /// Gets the declared scope name.
    /// </summary>
    public string Name { get; }
}
