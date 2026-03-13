using System;

namespace DependencyContractAnalyzer;

/// <summary>
/// Skips dependency contract analysis for the annotated owner type or assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractAnalysisAttribute : Attribute
{
}
