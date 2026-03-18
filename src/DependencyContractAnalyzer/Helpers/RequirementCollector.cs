using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DependencyContractAnalyzer.Diagnostics;
using DependencyContractAnalyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal static class RequirementCollector
{
    public static RequirementCollectionResult Collect(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol,
        INamedTypeSymbol? requiresContractOnTargetAttributeSymbol,
        INamedTypeSymbol? requiresContractOnScopeAttributeSymbol,
        INamedTypeSymbol? suppressRequiredDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredTargetContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredScopeContractAttributeSymbol)
    {
        var dependencyDuplicateCandidates =
            new Dictionary<string, List<RequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var dependencyTypeRequirements = CollectDependencyRequirements(
            context,
            namedType,
            requiresDependencyContractAttributeSymbol,
            dependencyDuplicateCandidates);

        var targetDuplicateCandidates =
            new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var targetRequirements = requiresContractOnTargetAttributeSymbol is null
            ? ImmutableArray<TargetRequirementDescriptor>.Empty
            : CollectNamedRequirements(
                context,
                namedType,
                requiresContractOnTargetAttributeSymbol,
                DiagnosticDescriptors.EmptyTargetName,
                targetDuplicateCandidates,
                static (attribute, targetName, contractName) =>
                    new TargetRequirementDescriptor(attribute, targetName, contractName));

        var scopeDuplicateCandidates =
            new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var scopeRequirements = requiresContractOnScopeAttributeSymbol is null
            ? ImmutableArray<ScopeRequirementDescriptor>.Empty
            : CollectNamedRequirements(
                context,
                namedType,
                requiresContractOnScopeAttributeSymbol,
                DiagnosticDescriptors.EmptyScopeName,
                scopeDuplicateCandidates,
                static (attribute, scopeName, contractName) =>
                    new ScopeRequirementDescriptor(attribute, scopeName, contractName));

        var dependencySuppressionDuplicateCandidates =
            new Dictionary<string, List<RequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidDependencySuppressionAttributes = new HashSet<AttributeData>();
        var dependencySuppressions = suppressRequiredDependencyContractAttributeSymbol is null
            ? ImmutableArray<RequirementDescriptor>.Empty
            : CollectDependencySuppressions(
                context,
                namedType,
                suppressRequiredDependencyContractAttributeSymbol,
                dependencySuppressionDuplicateCandidates,
                invalidDependencySuppressionAttributes);

        var targetSuppressionDuplicateCandidates =
            new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidTargetSuppressionAttributes = new HashSet<AttributeData>();
        var targetSuppressions = suppressRequiredTargetContractAttributeSymbol is null
            ? ImmutableArray<TargetRequirementDescriptor>.Empty
            : CollectNamedRequirements(
                context,
                namedType,
                suppressRequiredTargetContractAttributeSymbol,
                DiagnosticDescriptors.EmptyTargetName,
                targetSuppressionDuplicateCandidates,
                static (attribute, targetName, contractName) =>
                    new TargetRequirementDescriptor(attribute, targetName, contractName),
                invalidTargetSuppressionAttributes);

        var scopeSuppressionDuplicateCandidates =
            new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidScopeSuppressionAttributes = new HashSet<AttributeData>();
        var scopeSuppressions = suppressRequiredScopeContractAttributeSymbol is null
            ? ImmutableArray<ScopeRequirementDescriptor>.Empty
            : CollectNamedRequirements(
                context,
                namedType,
                suppressRequiredScopeContractAttributeSymbol,
                DiagnosticDescriptors.EmptyScopeName,
                scopeSuppressionDuplicateCandidates,
                static (attribute, scopeName, contractName) =>
                    new ScopeRequirementDescriptor(attribute, scopeName, contractName),
                invalidScopeSuppressionAttributes);

        var duplicateDependencyRequirements = GetDuplicateAttributes(
            context,
            namedType,
            dependencyDuplicateCandidates);
        var duplicateTargetRequirements = GetDuplicateAttributes(
            context,
            namedType,
            targetDuplicateCandidates);
        var duplicateScopeRequirements = GetDuplicateAttributes(
            context,
            namedType,
            scopeDuplicateCandidates);
        var duplicateDependencySuppressions = GetDuplicateAttributes(
            context,
            namedType,
            dependencySuppressionDuplicateCandidates);
        var duplicateTargetSuppressions = GetDuplicateAttributes(
            context,
            namedType,
            targetSuppressionDuplicateCandidates);
        var duplicateScopeSuppressions = GetDuplicateAttributes(
            context,
            namedType,
            scopeSuppressionDuplicateCandidates);

        return new RequirementCollectionResult(
            FilterValidRequirements(dependencyTypeRequirements, duplicateDependencyRequirements),
            FilterValidRequirements(targetRequirements, duplicateTargetRequirements),
            FilterValidRequirements(scopeRequirements, duplicateScopeRequirements),
            CreateValidRequirementKeys(
                dependencySuppressions,
                duplicateDependencySuppressions,
                invalidDependencySuppressionAttributes,
                static suppression =>
                    GetDependencyRequirementKey(suppression.DependencyType, suppression.ContractName)),
            CreateValidRequirementKeys(
                targetSuppressions,
                duplicateTargetSuppressions,
                invalidTargetSuppressionAttributes,
                static suppression => GetNamedRequirementKey(suppression.Name, suppression.ContractName)),
            CreateValidRequirementKeys(
                scopeSuppressions,
                duplicateScopeSuppressions,
                invalidScopeSuppressionAttributes,
                static suppression => GetNamedRequirementKey(suppression.Name, suppression.ContractName)));
    }

    private static ImmutableArray<RequirementDescriptor> CollectDependencyRequirements(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol attributeSymbol,
        Dictionary<string, List<RequirementDescriptor>> duplicateCandidates)
    {
        var requirements = ImmutableArray.CreateBuilder<RequirementDescriptor>();

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(attributeSymbol))
            {
                continue;
            }

            if (!TryGetNamedTypeArgument(attribute, 0, out var dependencyType) ||
                !TryGetStringArgument(attribute, 1, out var contractName))
            {
                continue;
            }

            var normalizedContractName = ContractNameNormalizer.Normalize(contractName);
            if (normalizedContractName is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.EmptyContractName,
                        GetAttributeLocation(attribute, namedType)));
                continue;
            }

            ReportInvalidContractName(context, normalizedContractName, attribute, namedType);

            var requirement = new RequirementDescriptor(attribute, dependencyType, normalizedContractName);
            requirements.Add(requirement);
            AddDuplicateCandidate(
                duplicateCandidates,
                GetDependencyRequirementKey(dependencyType, normalizedContractName),
                requirement);
        }

        return requirements.ToImmutable();
    }

    private static ImmutableArray<RequirementDescriptor> CollectDependencySuppressions(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol attributeSymbol,
        Dictionary<string, List<RequirementDescriptor>> duplicateCandidates,
        HashSet<AttributeData> invalidAttributes)
    {
        var suppressions = ImmutableArray.CreateBuilder<RequirementDescriptor>();

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(attributeSymbol))
            {
                continue;
            }

            if (!TryGetNamedTypeArgument(attribute, 0, out var dependencyType) ||
                !TryGetStringArgument(attribute, 1, out var contractName))
            {
                continue;
            }

            var normalizedContractName = ContractNameNormalizer.Normalize(contractName);
            if (normalizedContractName is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.EmptyContractName,
                        GetAttributeLocation(attribute, namedType)));
                continue;
            }

            if (ReportInvalidContractName(context, normalizedContractName, attribute, namedType))
            {
                invalidAttributes.Add(attribute);
            }

            var suppression = new RequirementDescriptor(attribute, dependencyType, normalizedContractName);
            suppressions.Add(suppression);
            AddDuplicateCandidate(
                duplicateCandidates,
                GetDependencyRequirementKey(dependencyType, normalizedContractName),
                suppression);
        }

        return suppressions.ToImmutable();
    }

    private static ImmutableArray<TRequirement> CollectNamedRequirements<TRequirement>(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol attributeSymbol,
        DiagnosticDescriptor emptyNameDescriptor,
        Dictionary<string, List<TRequirement>> duplicateCandidates,
        Func<AttributeData, string, string, TRequirement> requirementFactory,
        HashSet<AttributeData>? invalidAttributes = null)
        where TRequirement : struct, INamedRequirement
    {
        var requirements = ImmutableArray.CreateBuilder<TRequirement>();

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(attributeSymbol))
            {
                continue;
            }

            if (!TryGetNamedRequirementParts(
                    context,
                    namedType,
                    attribute,
                    emptyNameDescriptor,
                    out var normalizedName,
                    out var normalizedContractName))
            {
                continue;
            }

            if (ReportInvalidContractName(context, normalizedContractName, attribute, namedType) &&
                invalidAttributes is not null)
            {
                invalidAttributes.Add(attribute);
            }

            var requirement = requirementFactory(attribute, normalizedName, normalizedContractName);
            requirements.Add(requirement);
            AddDuplicateCandidate(
                duplicateCandidates,
                GetNamedRequirementKey(requirement.Name, requirement.ContractName),
                requirement);
        }

        return requirements.ToImmutable();
    }

    private static HashSet<AttributeData> GetDuplicateAttributes<TRequirement>(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        Dictionary<string, List<TRequirement>> duplicateCandidates)
        where TRequirement : struct, IRequirement
    {
        var duplicateAttributes = new HashSet<AttributeData>();

        foreach (var entry in duplicateCandidates)
        {
            if (entry.Value.Count < 2)
            {
                continue;
            }

            var diagnosticContractName = entry.Value[0].ContractName;
            for (var index = 1; index < entry.Value.Count; index++)
            {
                var requirement = entry.Value[index];
                duplicateAttributes.Add(requirement.Attribute);
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateContractDeclaration,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        diagnosticContractName));
            }
        }

        return duplicateAttributes;
    }

    private static ImmutableArray<TRequirement> FilterValidRequirements<TRequirement>(
        ImmutableArray<TRequirement> requirements,
        HashSet<AttributeData> duplicateAttributes)
        where TRequirement : struct, IRequirement
    {
        if (requirements.IsDefaultOrEmpty || duplicateAttributes.Count == 0)
        {
            return requirements;
        }

        var filteredRequirements = ImmutableArray.CreateBuilder<TRequirement>();
        foreach (var requirement in requirements)
        {
            if (!duplicateAttributes.Contains(requirement.Attribute))
            {
                filteredRequirements.Add(requirement);
            }
        }

        return filteredRequirements.ToImmutable();
    }

    private static ImmutableHashSet<string> CreateValidRequirementKeys<TRequirement>(
        ImmutableArray<TRequirement> requirements,
        HashSet<AttributeData> duplicateAttributes,
        HashSet<AttributeData> invalidAttributes,
        Func<TRequirement, string> keySelector)
        where TRequirement : struct, IRequirement
    {
        var keys = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in requirements)
        {
            if (duplicateAttributes.Contains(requirement.Attribute) ||
                invalidAttributes.Contains(requirement.Attribute))
            {
                continue;
            }

            keys.Add(keySelector(requirement));
        }

        return keys.ToImmutable();
    }

    private static bool ReportInvalidContractName(
        SymbolAnalysisContext context,
        string contractName,
        AttributeData attribute,
        ISymbol fallbackSymbol)
    {
        if (ContractNameFormat.IsLowerKebabCase(contractName))
        {
            return false;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.ContractNamingFormatViolation,
                GetAttributeLocation(attribute, fallbackSymbol),
                contractName));
        return true;
    }

    private static bool TryGetNamedRequirementParts(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        AttributeData attribute,
        DiagnosticDescriptor emptyNameDescriptor,
        out string normalizedName,
        out string normalizedContractName)
    {
        normalizedName = string.Empty;
        normalizedContractName = string.Empty;

        var hasName = TryGetStringArgument(attribute, 0, out var name);
        var hasContractName = TryGetStringArgument(attribute, 1, out var contractName);

        var candidateName = hasName
            ? ContractNameNormalizer.Normalize(name)
            : null;
        if (candidateName is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    emptyNameDescriptor,
                    GetAttributeLocation(attribute, namedType)));
        }

        var candidateContractName = hasContractName
            ? ContractNameNormalizer.Normalize(contractName)
            : null;
        if (candidateContractName is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.EmptyContractName,
                    GetAttributeLocation(attribute, namedType)));
        }

        if (candidateName is null || candidateContractName is null)
        {
            return false;
        }

        normalizedName = candidateName;
        normalizedContractName = candidateContractName;
        return true;
    }

    private static bool TryGetNamedTypeArgument(AttributeData attribute, int index, out INamedTypeSymbol dependencyType)
    {
        dependencyType = null!;

        if (attribute.ConstructorArguments.Length <= index)
        {
            return false;
        }

        if (attribute.ConstructorArguments[index].Value is INamedTypeSymbol symbol)
        {
            dependencyType = symbol;
            return true;
        }

        return false;
    }

    private static bool TryGetStringArgument(AttributeData attribute, int index, out string value)
    {
        value = string.Empty;

        if (attribute.ConstructorArguments.Length <= index)
        {
            return false;
        }

        value = attribute.ConstructorArguments[index].Value as string ?? string.Empty;
        return true;
    }

    private static Location GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
        fallbackSymbol.Locations.FirstOrDefault() ??
        Location.None;

    private static string GetDependencyRequirementKey(INamedTypeSymbol dependencyType, string contractName) =>
        dependencyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + contractName;

    private static string GetNamedRequirementKey(string name, string contractName) =>
        name + "|" + contractName;

    private static void AddDuplicateCandidate<TRequirement>(
        Dictionary<string, List<TRequirement>> duplicateCandidates,
        string key,
        TRequirement requirement)
    {
        if (!duplicateCandidates.TryGetValue(key, out var duplicates))
        {
            duplicates = new List<TRequirement>();
            duplicateCandidates.Add(key, duplicates);
        }

        duplicates.Add(requirement);
    }
}
