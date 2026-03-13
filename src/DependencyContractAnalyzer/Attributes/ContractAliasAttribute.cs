using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Declares that one contract satisfies another contract.
/// </summary>
/// <remarks>
/// This attribute contributes an assembly-level implication edge from <see cref="From"/>
/// to <see cref="To"/>. The analyzer evaluates alias and hierarchy edges together as
/// one transitive implication graph, and cycles are reported as <c>DCA202</c>.
/// </remarks>
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
    /// Gets the provided contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Gets the satisfied contract name after consumer-side normalization by the analyzer.
    /// </summary>
    public string To { get; }
}
