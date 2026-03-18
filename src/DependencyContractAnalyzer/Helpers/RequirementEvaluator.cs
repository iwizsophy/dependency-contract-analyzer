using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DependencyContractAnalyzer.Diagnostics;
using DependencyContractAnalyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Helpers;

internal static class RequirementEvaluator
{
    public static void Evaluate(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        RequirementCollectionResult requirements,
        ImmutableArray<DependencyDescriptor> dependencies,
        RequirementEvaluationOptions requirementEvaluationOptions,
        ExternalDependencyOptions externalDependencyOptions,
        DependencyContractMetadataResolver metadataResolver,
        SymbolDisplayFormat minimalSymbolDisplayFormat)
    {
        var providedContractCache =
            new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);
        var targetCache =
            new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);
        var scopeCache =
            new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);

        EvaluateDependencyTypeRequirements(
            context,
            namedType,
            requirements.DependencyTypeRequirements,
            requirements.DependencySuppressionKeys,
            dependencies,
            requirementEvaluationOptions,
            externalDependencyOptions,
            metadataResolver,
            providedContractCache,
            minimalSymbolDisplayFormat);

        if (!requirements.TargetRequirements.IsDefaultOrEmpty)
        {
            ImmutableHashSet<string> ResolveTargets(INamedTypeSymbol dependencyType, bool isExternalDependency)
            {
                if (!targetCache.TryGetValue(dependencyType, out var targets))
                {
                    targets = metadataResolver.GetTargets(
                        dependencyType,
                        allowNamespaceInference: !isExternalDependency);
                    targetCache.Add(dependencyType, targets);
                }

                return targets;
            }

            EvaluateNamedRequirements(
                context,
                namedType,
                requirements.TargetRequirements,
                requirements.TargetSuppressionKeys,
                metadataResolver.KnownTargets,
                dependencies,
                requirementEvaluationOptions,
                externalDependencyOptions,
                metadataResolver,
                providedContractCache,
                ResolveTargets,
                minimalSymbolDisplayFormat,
                DiagnosticDescriptors.UndeclaredRequiredTarget,
                DiagnosticDescriptors.UnusedRequiredTarget);
        }

        if (!requirements.ScopeRequirements.IsDefaultOrEmpty)
        {
            ImmutableHashSet<string> ResolveScopes(INamedTypeSymbol dependencyType, bool isExternalDependency)
            {
                if (!scopeCache.TryGetValue(dependencyType, out var scopes))
                {
                    scopes = metadataResolver.GetScopes(
                        dependencyType,
                        allowNamespaceInference: !isExternalDependency);
                    scopeCache.Add(dependencyType, scopes);
                }

                return scopes;
            }

            EvaluateNamedRequirements(
                context,
                namedType,
                requirements.ScopeRequirements,
                requirements.ScopeSuppressionKeys,
                metadataResolver.KnownScopes,
                dependencies,
                requirementEvaluationOptions,
                externalDependencyOptions,
                metadataResolver,
                providedContractCache,
                ResolveScopes,
                minimalSymbolDisplayFormat,
                DiagnosticDescriptors.UndeclaredRequiredScope,
                DiagnosticDescriptors.UnusedRequiredScope);
        }
    }

    private static void EvaluateDependencyTypeRequirements(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        ImmutableArray<RequirementDescriptor> requirements,
        ImmutableHashSet<string> suppressionKeys,
        ImmutableArray<DependencyDescriptor> dependencies,
        RequirementEvaluationOptions requirementEvaluationOptions,
        ExternalDependencyOptions externalDependencyOptions,
        DependencyContractMetadataResolver metadataResolver,
        Dictionary<INamedTypeSymbol, ImmutableHashSet<string>> providedContractCache,
        SymbolDisplayFormat minimalSymbolDisplayFormat)
    {
        foreach (var requirement in requirements)
        {
            if (suppressionKeys.Contains(
                GetDependencyRequirementKey(requirement.DependencyType, requirement.ContractName)))
            {
                continue;
            }

            var hasMatchingDependency = false;

            foreach (var dependency in dependencies)
            {
                if (!dependency.Type.MatchesRequiredDependencyType(requirement.DependencyType))
                {
                    continue;
                }

                hasMatchingDependency = true;

                if (dependency.Type.IsExternalTo(context.Compilation.Assembly) &&
                    externalDependencyOptions.Policy == ExternalDependencyPolicy.Ignore)
                {
                    continue;
                }

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = metadataResolver.GetProvidedContracts(dependency.Type);
                    providedContractCache.Add(dependency.Type, providedContracts);
                }

                if (providedContracts.Contains(requirement.ContractName))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MissingRequiredContract,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        dependency.Type.ToDisplayString(minimalSymbolDisplayFormat),
                        requirement.ContractName));
            }

            if (!hasMatchingDependency &&
                requirementEvaluationOptions.ReportUnusedRequirementDiagnostics)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnusedRequiredDependencyType,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.DependencyType.ToDisplayString(minimalSymbolDisplayFormat)));
            }
        }
    }

    private static void EvaluateNamedRequirements<TRequirement>(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        ImmutableArray<TRequirement> requirements,
        ImmutableHashSet<string> suppressionKeys,
        ImmutableHashSet<string> knownNames,
        ImmutableArray<DependencyDescriptor> dependencies,
        RequirementEvaluationOptions requirementEvaluationOptions,
        ExternalDependencyOptions externalDependencyOptions,
        DependencyContractMetadataResolver metadataResolver,
        Dictionary<INamedTypeSymbol, ImmutableHashSet<string>> providedContractCache,
        Func<INamedTypeSymbol, bool, ImmutableHashSet<string>> resolveDependencyNames,
        SymbolDisplayFormat minimalSymbolDisplayFormat,
        DiagnosticDescriptor undeclaredDescriptor,
        DiagnosticDescriptor unusedDescriptor)
        where TRequirement : struct, INamedRequirement
    {
        foreach (var requirement in requirements)
        {
            if (suppressionKeys.Contains(GetNamedRequirementKey(requirement.Name, requirement.ContractName)))
            {
                continue;
            }

            if (!knownNames.Contains(requirement.Name) &&
                requirementEvaluationOptions.ReportUndeclaredRequirementDiagnostics)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        undeclaredDescriptor,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.Name));
                continue;
            }

            var hasMatchingDependency = false;
            foreach (var dependency in dependencies)
            {
                var isExternalDependency = dependency.Type.IsExternalTo(context.Compilation.Assembly);
                if (isExternalDependency &&
                    externalDependencyOptions.Policy == ExternalDependencyPolicy.Ignore)
                {
                    continue;
                }

                var dependencyNames = resolveDependencyNames(dependency.Type, isExternalDependency);
                if (!dependencyNames.Contains(requirement.Name))
                {
                    continue;
                }

                hasMatchingDependency = true;

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = metadataResolver.GetProvidedContracts(dependency.Type);
                    providedContractCache.Add(dependency.Type, providedContracts);
                }

                if (providedContracts.Contains(requirement.ContractName))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MissingRequiredContract,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        dependency.Type.ToDisplayString(minimalSymbolDisplayFormat),
                        requirement.ContractName));
            }

            if (!hasMatchingDependency &&
                requirementEvaluationOptions.ReportUnusedRequirementDiagnostics)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        unusedDescriptor,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.Name));
            }
        }
    }

    private static Location GetAttributeLocation(AttributeData attribute, ISymbol fallbackSymbol) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
        fallbackSymbol.Locations.FirstOrDefault() ??
        Location.None;

    private static string GetDependencyRequirementKey(INamedTypeSymbol dependencyType, string contractName) =>
        dependencyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + contractName;

    private static string GetNamedRequirementKey(string name, string contractName) =>
        name + "|" + contractName;
}
