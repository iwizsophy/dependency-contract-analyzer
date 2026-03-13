using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that dependencies in a specific target category must satisfy.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnTargetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresContractOnTargetAttribute"/> class.
    /// </summary>
    /// <param name="targetName">The dependency target category to match.</param>
    /// <param name="contractName">The required contract name.</param>
    public RequiresContractOnTargetAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the dependency target category to match.
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Gets the required contract name.
    /// </summary>
    public string ContractName { get; }
}
