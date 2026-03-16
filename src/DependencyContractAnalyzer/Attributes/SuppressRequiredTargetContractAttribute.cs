using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Suppresses diagnostics for a matching <see cref="RequiresContractOnTargetAttribute"/> declaration.
/// </summary>
/// <remarks>
/// Suppression is an exact-match rule on the owning type. The analyzer matches the
/// normalized target name together with the normalized contract name and does not
/// broaden the suppression to dependency-type or scope requirements.
/// </remarks>
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
    /// Gets the target name to suppress after consumer-side normalization by the analyzer.
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Gets the suppressed contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string ContractName { get; }
}
