using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares a contract that dependencies in a specific target category must satisfy.
/// </summary>
/// <remarks>
/// The analyzer resolves dependency targets from explicit <see cref="ContractTargetAttribute"/>
/// declarations first and then falls back to configured namespace inference when no
/// explicit target is present. Target names are normalized by trimming surrounding
/// whitespace and are compared with ordinal case-insensitive semantics.
/// </remarks>
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
    /// Gets the dependency target category to match after consumer-side normalization by the analyzer.
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Gets the required contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
