using System.Collections.Immutable;

namespace DependencyContractAnalyzer.Helpers;

internal readonly struct RequirementCollectionResult
{
    public RequirementCollectionResult(
        ImmutableArray<RequirementDescriptor> dependencyTypeRequirements,
        ImmutableArray<TargetRequirementDescriptor> targetRequirements,
        ImmutableArray<ScopeRequirementDescriptor> scopeRequirements,
        ImmutableHashSet<string> dependencySuppressionKeys,
        ImmutableHashSet<string> targetSuppressionKeys,
        ImmutableHashSet<string> scopeSuppressionKeys)
    {
        DependencyTypeRequirements = dependencyTypeRequirements;
        TargetRequirements = targetRequirements;
        ScopeRequirements = scopeRequirements;
        DependencySuppressionKeys = dependencySuppressionKeys;
        TargetSuppressionKeys = targetSuppressionKeys;
        ScopeSuppressionKeys = scopeSuppressionKeys;
    }

    public ImmutableArray<RequirementDescriptor> DependencyTypeRequirements { get; }

    public ImmutableArray<TargetRequirementDescriptor> TargetRequirements { get; }

    public ImmutableArray<ScopeRequirementDescriptor> ScopeRequirements { get; }

    public ImmutableHashSet<string> DependencySuppressionKeys { get; }

    public ImmutableHashSet<string> TargetSuppressionKeys { get; }

    public ImmutableHashSet<string> ScopeSuppressionKeys { get; }

    public bool HasAnyRequirement =>
        !DependencyTypeRequirements.IsDefaultOrEmpty ||
        !TargetRequirements.IsDefaultOrEmpty ||
        !ScopeRequirements.IsDefaultOrEmpty;
}
