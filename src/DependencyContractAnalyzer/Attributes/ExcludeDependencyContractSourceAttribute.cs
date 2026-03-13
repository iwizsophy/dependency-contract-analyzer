using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Excludes dependency extraction from the annotated member.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractSourceAttribute : Attribute
{
}
