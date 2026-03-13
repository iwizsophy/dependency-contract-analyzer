using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares that one contract satisfies another contract.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractAliasAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractAliasAttribute"/> class.
    /// </summary>
    /// <param name="from">The provided contract name.</param>
    /// <param name="to">The satisfied contract name.</param>
    public ContractAliasAttribute(string from, string to)
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Gets the provided contract name.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the satisfied contract name.
    /// </summary>
    public string To { get; }
}
