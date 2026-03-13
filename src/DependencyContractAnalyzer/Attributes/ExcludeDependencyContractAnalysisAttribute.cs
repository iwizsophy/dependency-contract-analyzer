using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Skips dependency contract analysis for the annotated owner type or assembly.
/// </summary>
/// <remarks>
/// Apply this attribute when a type or assembly should not participate in
/// dependency contract diagnostics. Assembly-level exclusion applies to every
/// analyzed type in the compilation, and type-level exclusion also covers nested
/// types discovered under the excluded owner.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractAnalysisAttribute : Attribute
{
}
