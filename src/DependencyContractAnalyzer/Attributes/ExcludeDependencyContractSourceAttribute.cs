using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Excludes dependency extraction from the annotated member.
/// </summary>
/// <remarks>
/// This attribute suppresses dependency discovery from the annotated constructor,
/// method, property, or field only. It does not suppress requirement declarations
/// on the owning type, and property-level exclusion also covers the generated
/// accessor members that Roslyn exposes separately.
/// </remarks>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractSourceAttribute : Attribute
{
}
