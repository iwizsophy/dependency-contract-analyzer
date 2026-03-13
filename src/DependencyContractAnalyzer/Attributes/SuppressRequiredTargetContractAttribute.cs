using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresContractOnTargetAttribute"/> declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredTargetContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuppressRequiredTargetContractAttribute"/> class.
    /// </summary>
    /// <param name="targetName">The target name to suppress.</param>
    /// <param name="contractName">The contract name to suppress.</param>
    public SuppressRequiredTargetContractAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }

    /// <summary>
    /// Gets the target name to suppress.
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Gets the suppressed contract name.
    /// </summary>
    public string ContractName { get; }
}
