using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract provided by a class or interface.
/// </summary>
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
    /// Gets the provided contract name.
    /// </summary>
    public string Name { get; }
}
