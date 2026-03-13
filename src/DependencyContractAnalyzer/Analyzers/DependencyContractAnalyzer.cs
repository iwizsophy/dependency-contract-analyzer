using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DependencyContractAnalyzer.Diagnostics;
using DependencyContractAnalyzer.Helpers;
using DependencyContractAnalyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DependencyContractAnalyzer.Analyzers;

/// <summary>
/// Analyzes declared dependency contracts and reports violations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependencyContractAnalyzerDiagnosticAnalyzer : DiagnosticAnalyzer
{
    private const string ContractAliasAttributeMetadataName = "DependencyContractAnalyzer.ContractAliasAttribute";
    private const string ContractHierarchyAttributeMetadataName = "DependencyContractAnalyzer.ContractHierarchyAttribute";
    private const string ContractScopeAttributeMetadataName = "DependencyContractAnalyzer.ContractScopeAttribute";
    private const string ContractTargetAttributeMetadataName = "DependencyContractAnalyzer.ContractTargetAttribute";
    private const string ExcludeDependencyContractAnalysisAttributeMetadataName = "DependencyContractAnalyzer.ExcludeDependencyContractAnalysisAttribute";
    private const string ExcludeDependencyContractSourceAttributeMetadataName = "DependencyContractAnalyzer.ExcludeDependencyContractSourceAttribute";
    private const string ProvidesContractAttributeMetadataName = "DependencyContractAnalyzer.ProvidesContractAttribute";
    private const string RequiresContractOnScopeAttributeMetadataName = "DependencyContractAnalyzer.RequiresContractOnScopeAttribute";
    private const string RequiresContractOnTargetAttributeMetadataName = "DependencyContractAnalyzer.RequiresContractOnTargetAttribute";
    private const string RequiresDependencyContractAttributeMetadataName = "DependencyContractAnalyzer.RequiresDependencyContractAttribute";
    private const string SuppressRequiredDependencyContractAttributeMetadataName = "DependencyContractAnalyzer.SuppressRequiredDependencyContractAttribute";
    private const string SuppressRequiredScopeContractAttributeMetadataName = "DependencyContractAnalyzer.SuppressRequiredScopeContractAttribute";
    private const string SuppressRequiredTargetContractAttributeMetadataName = "DependencyContractAnalyzer.SuppressRequiredTargetContractAttribute";

    private static readonly SymbolDisplayFormat MinimalSymbolDisplayFormat =
        new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingRequiredContract,
            DiagnosticDescriptors.UnusedRequiredDependencyType,
            DiagnosticDescriptors.EmptyContractName,
            DiagnosticDescriptors.ContractNamingFormatViolation,
            DiagnosticDescriptors.UndeclaredRequiredTarget,
            DiagnosticDescriptors.UndeclaredRequiredScope,
            DiagnosticDescriptors.CyclicAliasDefinition,
            DiagnosticDescriptors.EmptyScopeName,
            DiagnosticDescriptors.EmptyTargetName,
            DiagnosticDescriptors.UnusedRequiredTarget,
            DiagnosticDescriptors.UnusedRequiredScope,
            DiagnosticDescriptors.DuplicateContractDeclaration);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var contractAliasAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ContractAliasAttributeMetadataName);
            var contractHierarchyAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ContractHierarchyAttributeMetadataName);
            var providesContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ProvidesContractAttributeMetadataName);
            var excludeDependencyContractAnalysisAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ExcludeDependencyContractAnalysisAttributeMetadataName);
            var excludeDependencyContractSourceAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ExcludeDependencyContractSourceAttributeMetadataName);
            var contractScopeAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ContractScopeAttributeMetadataName);
            var contractTargetAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ContractTargetAttributeMetadataName);
            var requiresContractOnScopeAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(RequiresContractOnScopeAttributeMetadataName);
            var requiresContractOnTargetAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(RequiresContractOnTargetAttributeMetadataName);
            var requiresDependencyContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(RequiresDependencyContractAttributeMetadataName);
            var suppressRequiredDependencyContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(SuppressRequiredDependencyContractAttributeMetadataName);
            var suppressRequiredTargetContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(SuppressRequiredTargetContractAttributeMetadataName);
            var suppressRequiredScopeContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(SuppressRequiredScopeContractAttributeMetadataName);

            if (providesContractAttributeSymbol is null || requiresDependencyContractAttributeSymbol is null)
            {
                return;
            }

            var contractAliasResolver = ContractAliasResolver.Create(
                startContext.Compilation.Assembly,
                contractAliasAttributeSymbol,
                contractHierarchyAttributeSymbol);
            var knownTargets = contractTargetAttributeSymbol is null
                ? CreateEmptyNameSet()
                : CollectKnownTargets(startContext.Compilation.Assembly, contractTargetAttributeSymbol);
            var knownScopes = contractScopeAttributeSymbol is null
                ? CreateEmptyNameSet()
                : CollectKnownScopes(startContext.Compilation.Assembly, contractScopeAttributeSymbol);

            if (!contractAliasResolver.Diagnostics.IsDefaultOrEmpty)
            {
                startContext.RegisterCompilationEndAction(
                    compilationContext =>
                    {
                        foreach (var diagnostic in contractAliasResolver.Diagnostics)
                        {
                            compilationContext.ReportDiagnostic(diagnostic);
                        }
                    });
            }

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    AnalyzeNamedType(
                        symbolContext,
                        contractScopeAttributeSymbol,
                        contractTargetAttributeSymbol,
                        excludeDependencyContractAnalysisAttributeSymbol,
                        excludeDependencyContractSourceAttributeSymbol,
                        contractAliasResolver,
                        knownScopes,
                        knownTargets,
                        providesContractAttributeSymbol,
                        requiresContractOnScopeAttributeSymbol,
                        requiresContractOnTargetAttributeSymbol,
                        requiresDependencyContractAttributeSymbol,
                        suppressRequiredDependencyContractAttributeSymbol,
                        suppressRequiredTargetContractAttributeSymbol,
                        suppressRequiredScopeContractAttributeSymbol);
                },
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol? contractScopeAttributeSymbol,
        INamedTypeSymbol? contractTargetAttributeSymbol,
        INamedTypeSymbol? excludeDependencyContractAnalysisAttributeSymbol,
        INamedTypeSymbol? excludeDependencyContractSourceAttributeSymbol,
        ContractAliasResolver contractAliasResolver,
        ImmutableHashSet<string> knownScopes,
        ImmutableHashSet<string> knownTargets,
        INamedTypeSymbol providesContractAttributeSymbol,
        INamedTypeSymbol? requiresContractOnScopeAttributeSymbol,
        INamedTypeSymbol? requiresContractOnTargetAttributeSymbol,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredTargetContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredScopeContractAttributeSymbol)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        if (namedType.TypeKind is not (TypeKind.Class or TypeKind.Interface))
        {
            return;
        }

        if (excludeDependencyContractAnalysisAttributeSymbol is not null &&
            HasExclusionAttribute(namedType, excludeDependencyContractAnalysisAttributeSymbol))
        {
            return;
        }

        if (AnalysisExclusionOptions.Create(context.Options.AnalyzerConfigOptionsProvider, namedType)
            .ShouldSkipType(namedType))
        {
            return;
        }

        if (contractScopeAttributeSymbol is not null)
        {
            AnalyzeDeclaredScopes(context, namedType, contractScopeAttributeSymbol);
        }

        if (contractTargetAttributeSymbol is not null)
        {
            AnalyzeDeclaredTargets(context, namedType, contractTargetAttributeSymbol);
        }

        AnalyzeProvidedContracts(context, namedType, providesContractAttributeSymbol);

        if (namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        AnalyzeRequirements(
            context,
            namedType,
            contractScopeAttributeSymbol,
            contractTargetAttributeSymbol,
            DependencyCollectionOptions.Create(context.Options.AnalyzerConfigOptionsProvider, namedType),
            excludeDependencyContractSourceAttributeSymbol,
            contractAliasResolver,
            knownScopes,
            knownTargets,
            providesContractAttributeSymbol,
            requiresContractOnScopeAttributeSymbol,
            requiresContractOnTargetAttributeSymbol,
            requiresDependencyContractAttributeSymbol,
            suppressRequiredDependencyContractAttributeSymbol,
            suppressRequiredTargetContractAttributeSymbol,
            suppressRequiredScopeContractAttributeSymbol);
    }

    private static void AnalyzeDeclaredScopes(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol contractScopeAttributeSymbol)
    {
        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(contractScopeAttributeSymbol))
            {
                continue;
            }

            if (!TryGetStringArgument(attribute, 0, out var scopeName))
            {
                continue;
            }

            if (ContractNameNormalizer.Normalize(scopeName) is not null)
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.EmptyScopeName,
                    GetAttributeLocation(attribute, namedType)));
        }
    }

    private static void AnalyzeDeclaredTargets(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol contractTargetAttributeSymbol)
    {
        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(contractTargetAttributeSymbol))
            {
                continue;
            }

            if (!TryGetStringArgument(attribute, 0, out var targetName))
            {
                continue;
            }

            if (ContractNameNormalizer.Normalize(targetName) is not null)
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.EmptyTargetName,
                    GetAttributeLocation(attribute, namedType)));
        }
    }

    private static void AnalyzeProvidedContracts(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol providesContractAttributeSymbol)
    {
        var duplicateCandidates = new Dictionary<string, List<AttributeData>>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(providesContractAttributeSymbol))
            {
                continue;
            }

            if (!TryGetStringArgument(attribute, 0, out var contractName))
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

            if (!duplicateCandidates.TryGetValue(normalizedContractName, out var attributes))
            {
                attributes = new List<AttributeData>();
                duplicateCandidates.Add(normalizedContractName, attributes);
            }

            attributes.Add(attribute);
        }

        foreach (var entry in duplicateCandidates)
        {
            if (entry.Value.Count < 2)
            {
                continue;
            }

            for (var index = 1; index < entry.Value.Count; index++)
            {
                var attribute = entry.Value[index];
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateContractDeclaration,
                        GetAttributeLocation(attribute, namedType),
                        entry.Key));
            }
        }
    }

    private static void AnalyzeRequirements(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol? contractScopeAttributeSymbol,
        INamedTypeSymbol? contractTargetAttributeSymbol,
        DependencyCollectionOptions dependencyCollectionOptions,
        INamedTypeSymbol? excludeDependencyContractSourceAttributeSymbol,
        ContractAliasResolver contractAliasResolver,
        ImmutableHashSet<string> knownScopes,
        ImmutableHashSet<string> knownTargets,
        INamedTypeSymbol providesContractAttributeSymbol,
        INamedTypeSymbol? requiresContractOnScopeAttributeSymbol,
        INamedTypeSymbol? requiresContractOnTargetAttributeSymbol,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredTargetContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredScopeContractAttributeSymbol)
    {
        var dependencyTypeRequirements = new List<RequirementDescriptor>();
        var duplicateDependencyCandidates = new Dictionary<string, List<RequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(requiresDependencyContractAttributeSymbol))
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
            dependencyTypeRequirements.Add(requirement);

            var duplicateKey = GetDuplicateRequirementKey(dependencyType, normalizedContractName);
            if (!duplicateDependencyCandidates.TryGetValue(duplicateKey, out var duplicates))
            {
                duplicates = new List<RequirementDescriptor>();
                duplicateDependencyCandidates.Add(duplicateKey, duplicates);
            }

            duplicates.Add(requirement);
        }

        var targetRequirements = new List<TargetRequirementDescriptor>();
        var duplicateTargetCandidates = new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);

        if (contractTargetAttributeSymbol is not null && requiresContractOnTargetAttributeSymbol is not null)
        {
            foreach (var attribute in namedType.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(requiresContractOnTargetAttributeSymbol))
                {
                    continue;
                }

                var hasTargetName = TryGetStringArgument(attribute, 0, out var targetName);
                var hasContractName = TryGetStringArgument(attribute, 1, out var contractName);

                var normalizedTargetName = hasTargetName
                    ? ContractNameNormalizer.Normalize(targetName)
                    : null;
                if (normalizedTargetName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyTargetName,
                            GetAttributeLocation(attribute, namedType)));
                }

                var normalizedContractName = hasContractName
                    ? ContractNameNormalizer.Normalize(contractName)
                    : null;
                if (normalizedContractName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute, namedType)));
                }

                if (normalizedTargetName is null || normalizedContractName is null)
                {
                    continue;
                }

                ReportInvalidContractName(context, normalizedContractName, attribute, namedType);

                var requirement = new TargetRequirementDescriptor(attribute, normalizedTargetName, normalizedContractName);
                targetRequirements.Add(requirement);

                var duplicateKey = normalizedTargetName + "|" + normalizedContractName;
                if (!duplicateTargetCandidates.TryGetValue(duplicateKey, out var duplicates))
                {
                    duplicates = new List<TargetRequirementDescriptor>();
                    duplicateTargetCandidates.Add(duplicateKey, duplicates);
                }

                duplicates.Add(requirement);
            }
        }

        var scopeRequirements = new List<ScopeRequirementDescriptor>();
        var duplicateScopeCandidates = new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);

        if (contractScopeAttributeSymbol is not null && requiresContractOnScopeAttributeSymbol is not null)
        {
            foreach (var attribute in namedType.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(requiresContractOnScopeAttributeSymbol))
                {
                    continue;
                }

                var hasScopeName = TryGetStringArgument(attribute, 0, out var scopeName);
                var hasContractName = TryGetStringArgument(attribute, 1, out var contractName);

                var normalizedScopeName = hasScopeName
                    ? ContractNameNormalizer.Normalize(scopeName)
                    : null;
                if (normalizedScopeName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyScopeName,
                            GetAttributeLocation(attribute, namedType)));
                }

                var normalizedContractName = hasContractName
                    ? ContractNameNormalizer.Normalize(contractName)
                    : null;
                if (normalizedContractName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute, namedType)));
                }

                if (normalizedScopeName is null || normalizedContractName is null)
                {
                    continue;
                }

                ReportInvalidContractName(context, normalizedContractName, attribute, namedType);

                var requirement = new ScopeRequirementDescriptor(attribute, normalizedScopeName, normalizedContractName);
                scopeRequirements.Add(requirement);

                var duplicateKey = normalizedScopeName + "|" + normalizedContractName;
                if (!duplicateScopeCandidates.TryGetValue(duplicateKey, out var duplicates))
                {
                    duplicates = new List<ScopeRequirementDescriptor>();
                    duplicateScopeCandidates.Add(duplicateKey, duplicates);
                }

                duplicates.Add(requirement);
            }
        }

        var dependencyTypeSuppressions = new List<RequirementDescriptor>();
        var duplicateDependencySuppressionCandidates = new Dictionary<string, List<RequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidDependencySuppressionAttributes = new HashSet<AttributeData>();

        if (suppressRequiredDependencyContractAttributeSymbol is not null)
        {
            foreach (var attribute in namedType.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(suppressRequiredDependencyContractAttributeSymbol))
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
                    invalidDependencySuppressionAttributes.Add(attribute);
                }

                var suppression = new RequirementDescriptor(attribute, dependencyType, normalizedContractName);
                dependencyTypeSuppressions.Add(suppression);

                var duplicateKey = GetDuplicateRequirementKey(dependencyType, normalizedContractName);
                if (!duplicateDependencySuppressionCandidates.TryGetValue(duplicateKey, out var duplicates))
                {
                    duplicates = new List<RequirementDescriptor>();
                    duplicateDependencySuppressionCandidates.Add(duplicateKey, duplicates);
                }

                duplicates.Add(suppression);
            }
        }

        var targetSuppressions = new List<TargetRequirementDescriptor>();
        var duplicateTargetSuppressionCandidates = new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidTargetSuppressionAttributes = new HashSet<AttributeData>();

        if (suppressRequiredTargetContractAttributeSymbol is not null)
        {
            foreach (var attribute in namedType.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(suppressRequiredTargetContractAttributeSymbol))
                {
                    continue;
                }

                var hasTargetName = TryGetStringArgument(attribute, 0, out var targetName);
                var hasContractName = TryGetStringArgument(attribute, 1, out var contractName);

                var normalizedTargetName = hasTargetName
                    ? ContractNameNormalizer.Normalize(targetName)
                    : null;
                if (normalizedTargetName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyTargetName,
                            GetAttributeLocation(attribute, namedType)));
                }

                var normalizedContractName = hasContractName
                    ? ContractNameNormalizer.Normalize(contractName)
                    : null;
                if (normalizedContractName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute, namedType)));
                }

                if (normalizedTargetName is null || normalizedContractName is null)
                {
                    continue;
                }

                if (ReportInvalidContractName(context, normalizedContractName, attribute, namedType))
                {
                    invalidTargetSuppressionAttributes.Add(attribute);
                }

                var suppression = new TargetRequirementDescriptor(attribute, normalizedTargetName, normalizedContractName);
                targetSuppressions.Add(suppression);

                var duplicateKey = GetNamedRequirementKey(normalizedTargetName, normalizedContractName);
                if (!duplicateTargetSuppressionCandidates.TryGetValue(duplicateKey, out var duplicates))
                {
                    duplicates = new List<TargetRequirementDescriptor>();
                    duplicateTargetSuppressionCandidates.Add(duplicateKey, duplicates);
                }

                duplicates.Add(suppression);
            }
        }

        var scopeSuppressions = new List<ScopeRequirementDescriptor>();
        var duplicateScopeSuppressionCandidates = new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidScopeSuppressionAttributes = new HashSet<AttributeData>();

        if (suppressRequiredScopeContractAttributeSymbol is not null)
        {
            foreach (var attribute in namedType.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(suppressRequiredScopeContractAttributeSymbol))
                {
                    continue;
                }

                var hasScopeName = TryGetStringArgument(attribute, 0, out var scopeName);
                var hasContractName = TryGetStringArgument(attribute, 1, out var contractName);

                var normalizedScopeName = hasScopeName
                    ? ContractNameNormalizer.Normalize(scopeName)
                    : null;
                if (normalizedScopeName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyScopeName,
                            GetAttributeLocation(attribute, namedType)));
                }

                var normalizedContractName = hasContractName
                    ? ContractNameNormalizer.Normalize(contractName)
                    : null;
                if (normalizedContractName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute, namedType)));
                }

                if (normalizedScopeName is null || normalizedContractName is null)
                {
                    continue;
                }

                if (ReportInvalidContractName(context, normalizedContractName, attribute, namedType))
                {
                    invalidScopeSuppressionAttributes.Add(attribute);
                }

                var suppression = new ScopeRequirementDescriptor(attribute, normalizedScopeName, normalizedContractName);
                scopeSuppressions.Add(suppression);

                var duplicateKey = GetNamedRequirementKey(normalizedScopeName, normalizedContractName);
                if (!duplicateScopeSuppressionCandidates.TryGetValue(duplicateKey, out var duplicates))
                {
                    duplicates = new List<ScopeRequirementDescriptor>();
                    duplicateScopeSuppressionCandidates.Add(duplicateKey, duplicates);
                }

                duplicates.Add(suppression);
            }
        }

        var duplicateDependencyRequirements = GetDuplicateAttributes(
            context,
            namedType,
            duplicateDependencyCandidates);
        var duplicateTargetRequirements = GetDuplicateAttributes(
            context,
            namedType,
            duplicateTargetCandidates);
        var duplicateScopeRequirements = GetDuplicateAttributes(
            context,
            namedType,
            duplicateScopeCandidates);
        var duplicateDependencySuppressions = GetDuplicateAttributes(
            context,
            namedType,
            duplicateDependencySuppressionCandidates);
        var duplicateTargetSuppressions = GetDuplicateAttributes(
            context,
            namedType,
            duplicateTargetSuppressionCandidates);
        var duplicateScopeSuppressions = GetDuplicateAttributes(
            context,
            namedType,
            duplicateScopeSuppressionCandidates);

        var dependencySuppressionKeys = CreateValidRequirementKeys(
            dependencyTypeSuppressions,
            duplicateDependencySuppressions,
            invalidDependencySuppressionAttributes,
            static suppression => GetDuplicateRequirementKey(suppression.DependencyType, suppression.ContractName));
        var targetSuppressionKeys = CreateValidRequirementKeys(
            targetSuppressions,
            duplicateTargetSuppressions,
            invalidTargetSuppressionAttributes,
            static suppression => GetNamedRequirementKey(suppression.TargetName, suppression.ContractName));
        var scopeSuppressionKeys = CreateValidRequirementKeys(
            scopeSuppressions,
            duplicateScopeSuppressions,
            invalidScopeSuppressionAttributes,
            static suppression => GetNamedRequirementKey(suppression.ScopeName, suppression.ContractName));

        if (dependencyTypeRequirements.Count == 0 && targetRequirements.Count == 0 && scopeRequirements.Count == 0)
        {
            return;
        }

        var dependencies = DependencyCollector.Collect(
            namedType,
            context.Compilation,
            dependencyCollectionOptions,
            excludeDependencyContractSourceAttributeSymbol,
            context.CancellationToken);
        var providedContractCache = new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);
        var targetCache = new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);
        var scopeCache = new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);

        foreach (var requirement in dependencyTypeRequirements)
        {
            if (duplicateDependencyRequirements.Contains(requirement.Attribute))
            {
                continue;
            }

            if (dependencySuppressionKeys.Contains(GetDuplicateRequirementKey(requirement.DependencyType, requirement.ContractName)))
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

                if (dependency.Type.IsExternalTo(context.Compilation.Assembly))
                {
                    continue;
                }

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = GetProvidedContracts(
                        dependency.Type,
                        providesContractAttributeSymbol,
                        contractAliasResolver);
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
                        dependency.Type.ToDisplayString(MinimalSymbolDisplayFormat),
                        requirement.ContractName));
            }

            if (!hasMatchingDependency)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnusedRequiredDependencyType,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.DependencyType.ToDisplayString(MinimalSymbolDisplayFormat)));
            }
        }

        if (contractTargetAttributeSymbol is not null)
        {
            foreach (var requirement in targetRequirements)
            {
                if (duplicateTargetRequirements.Contains(requirement.Attribute))
                {
                    continue;
                }

                if (targetSuppressionKeys.Contains(GetNamedRequirementKey(requirement.TargetName, requirement.ContractName)))
                {
                    continue;
                }

                if (!knownTargets.Contains(requirement.TargetName))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UndeclaredRequiredTarget,
                            GetAttributeLocation(requirement.Attribute, namedType),
                            requirement.TargetName));
                    continue;
                }

                var hasMatchingDependency = false;
                foreach (var dependency in dependencies)
                {
                    if (dependency.Type.IsExternalTo(context.Compilation.Assembly))
                    {
                        continue;
                    }

                    if (!targetCache.TryGetValue(dependency.Type, out var targets))
                    {
                        targets = GetTargets(dependency.Type, contractTargetAttributeSymbol);
                        targetCache.Add(dependency.Type, targets);
                    }

                    if (!targets.Contains(requirement.TargetName))
                    {
                        continue;
                    }

                    hasMatchingDependency = true;

                    if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                    {
                        providedContracts = GetProvidedContracts(
                            dependency.Type,
                            providesContractAttributeSymbol,
                            contractAliasResolver);
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
                            dependency.Type.ToDisplayString(MinimalSymbolDisplayFormat),
                            requirement.ContractName));
                }

                if (!hasMatchingDependency)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnusedRequiredTarget,
                            GetAttributeLocation(requirement.Attribute, namedType),
                            requirement.TargetName));
                }
            }
        }

        if (contractScopeAttributeSymbol is null)
        {
            return;
        }

        foreach (var requirement in scopeRequirements)
        {
            if (duplicateScopeRequirements.Contains(requirement.Attribute))
            {
                continue;
            }

            if (scopeSuppressionKeys.Contains(GetNamedRequirementKey(requirement.ScopeName, requirement.ContractName)))
            {
                continue;
            }

            if (!knownScopes.Contains(requirement.ScopeName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UndeclaredRequiredScope,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.ScopeName));
                continue;
            }

            var hasMatchingDependency = false;
            foreach (var dependency in dependencies)
            {
                if (dependency.Type.IsExternalTo(context.Compilation.Assembly))
                {
                    continue;
                }

                if (!scopeCache.TryGetValue(dependency.Type, out var scopes))
                {
                    scopes = GetScopes(dependency.Type, contractScopeAttributeSymbol);
                    scopeCache.Add(dependency.Type, scopes);
                }

                if (!scopes.Contains(requirement.ScopeName))
                {
                    continue;
                }

                hasMatchingDependency = true;

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = GetProvidedContracts(
                        dependency.Type,
                        providesContractAttributeSymbol,
                        contractAliasResolver);
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
                        dependency.Type.ToDisplayString(MinimalSymbolDisplayFormat),
                        requirement.ContractName));
            }

            if (!hasMatchingDependency)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnusedRequiredScope,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.ScopeName));
            }
        }
    }

    private static ImmutableHashSet<string> GetProvidedContracts(
        INamedTypeSymbol type,
        INamedTypeSymbol providesContractAttributeSymbol,
        ContractAliasResolver contractAliasResolver)
    {
        var contracts = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var toVisit = new Stack<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        toVisit.Push(type);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            AddNormalizedNames(current.GetAttributes(), providesContractAttributeSymbol, 0, contracts);

            if (current.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                toVisit.Push(baseType);
            }

            foreach (var interfaceType in current.Interfaces)
            {
                toVisit.Push(interfaceType);
            }
        }

        return contractAliasResolver.Expand(contracts.ToImmutable());
    }

    private static ImmutableHashSet<string> GetTargets(
        INamedTypeSymbol type,
        INamedTypeSymbol contractTargetAttributeSymbol)
    {
        var targets = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var toVisit = new Stack<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        toVisit.Push(type);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            AddResolvedTargetNames(current, contractTargetAttributeSymbol, targets);

            if (current.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                toVisit.Push(baseType);
            }

            foreach (var interfaceType in current.Interfaces)
            {
                toVisit.Push(interfaceType);
            }
        }

        return targets.ToImmutable();
    }

    private static ImmutableHashSet<string> GetScopes(
        INamedTypeSymbol type,
        INamedTypeSymbol contractScopeAttributeSymbol)
    {
        var scopes = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var hasAssemblyLevelScopes = AddNormalizedNames(
            type.ContainingAssembly.GetAttributes(),
            contractScopeAttributeSymbol,
            0,
            scopes);
        var toVisit = new Stack<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        toVisit.Push(type);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            AddResolvedScopeNames(current, contractScopeAttributeSymbol, scopes, hasAssemblyLevelScopes);

            if (current.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                toVisit.Push(baseType);
            }

            foreach (var interfaceType in current.Interfaces)
            {
                toVisit.Push(interfaceType);
            }
        }

        return scopes.ToImmutable();
    }

    private static ImmutableHashSet<string> CollectKnownTargets(
        IAssemblySymbol assembly,
        INamedTypeSymbol contractTargetAttributeSymbol)
    {
        var knownNames = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        AddKnownTargets(assembly.GlobalNamespace, contractTargetAttributeSymbol, knownNames);
        return knownNames.ToImmutable();
    }

    private static ImmutableHashSet<string> CollectKnownScopes(
        IAssemblySymbol assembly,
        INamedTypeSymbol contractScopeAttributeSymbol)
    {
        var knownNames = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var hasAssemblyLevelScopes = AddNormalizedNames(
            assembly.GetAttributes(),
            contractScopeAttributeSymbol,
            0,
            knownNames);
        AddKnownScopes(assembly.GlobalNamespace, contractScopeAttributeSymbol, knownNames, hasAssemblyLevelScopes);
        return knownNames.ToImmutable();
    }

    private static void AddKnownTargets(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol contractTargetAttributeSymbol,
        ImmutableHashSet<string>.Builder knownNames)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            AddKnownTargets(type, contractTargetAttributeSymbol, knownNames);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            AddKnownTargets(nestedNamespace, contractTargetAttributeSymbol, knownNames);
        }
    }

    private static void AddKnownTargets(
        INamedTypeSymbol type,
        INamedTypeSymbol contractTargetAttributeSymbol,
        ImmutableHashSet<string>.Builder knownNames)
    {
        AddResolvedTargetNames(type, contractTargetAttributeSymbol, knownNames);

        foreach (var nestedType in type.GetTypeMembers())
        {
            AddKnownTargets(nestedType, contractTargetAttributeSymbol, knownNames);
        }
    }

    private static void AddKnownScopes(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol contractScopeAttributeSymbol,
        ImmutableHashSet<string>.Builder knownNames,
        bool hasAssemblyLevelScopes)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            AddKnownScopes(type, contractScopeAttributeSymbol, knownNames, hasAssemblyLevelScopes);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            AddKnownScopes(nestedNamespace, contractScopeAttributeSymbol, knownNames, hasAssemblyLevelScopes);
        }
    }

    private static void AddKnownScopes(
        INamedTypeSymbol type,
        INamedTypeSymbol contractScopeAttributeSymbol,
        ImmutableHashSet<string>.Builder knownNames,
        bool hasAssemblyLevelScopes)
    {
        AddResolvedScopeNames(type, contractScopeAttributeSymbol, knownNames, hasAssemblyLevelScopes);

        foreach (var nestedType in type.GetTypeMembers())
        {
            AddKnownScopes(nestedType, contractScopeAttributeSymbol, knownNames, hasAssemblyLevelScopes);
        }
    }

    private static ImmutableHashSet<string> CreateEmptyNameSet() =>
        ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

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

    private static bool AddNormalizedNames(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol expectedAttributeSymbol,
        int argumentIndex,
        ImmutableHashSet<string>.Builder values)
    {
        var added = false;

        foreach (var attribute in attributes)
        {
            if (!attribute.AttributeClass.SymbolEquals(expectedAttributeSymbol) ||
                !TryGetStringArgument(attribute, argumentIndex, out var value))
            {
                continue;
            }

            var normalizedValue = ContractNameNormalizer.Normalize(value);
            if (normalizedValue is not null)
            {
                values.Add(normalizedValue);
                added = true;
            }
        }

        return added;
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

    private static string GetDuplicateRequirementKey(INamedTypeSymbol dependencyType, string contractName) =>
        dependencyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + contractName;

    private static string GetNamedRequirementKey(string name, string contractName) =>
        name + "|" + contractName;

    private static ImmutableHashSet<string> CreateValidRequirementKeys<TRequirement>(
        IEnumerable<TRequirement> requirements,
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

    private static bool HasExclusionAttribute(
        INamedTypeSymbol namedType,
        INamedTypeSymbol exclusionAttributeSymbol)
    {
        if (namedType.ContainingAssembly.GetAttributes().Any(attribute => attribute.AttributeClass.SymbolEquals(exclusionAttributeSymbol)))
        {
            return true;
        }

        for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
        {
            if (current.GetAttributes().Any(attribute => attribute.AttributeClass.SymbolEquals(exclusionAttributeSymbol)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddResolvedTargetNames(
        INamedTypeSymbol type,
        INamedTypeSymbol contractTargetAttributeSymbol,
        ImmutableHashSet<string>.Builder targetNames)
    {
        if (AddNormalizedNames(type.GetAttributes(), contractTargetAttributeSymbol, 0, targetNames))
        {
            return;
        }

        if (NamespaceNameInference.InferName(type.ContainingNamespace) is { } inferredName)
        {
            targetNames.Add(inferredName);
        }
    }

    private static void AddResolvedScopeNames(
        INamedTypeSymbol type,
        INamedTypeSymbol contractScopeAttributeSymbol,
        ImmutableHashSet<string>.Builder scopeNames,
        bool hasAssemblyLevelScopes)
    {
        if (AddNormalizedNames(type.GetAttributes(), contractScopeAttributeSymbol, 0, scopeNames) ||
            hasAssemblyLevelScopes)
        {
            return;
        }

        if (NamespaceNameInference.InferName(type.ContainingNamespace) is { } inferredName)
        {
            scopeNames.Add(inferredName);
        }
    }

    private interface IRequirement
    {
        AttributeData Attribute { get; }

        string ContractName { get; }
    }

    private readonly struct RequirementDescriptor : IRequirement
    {
        public RequirementDescriptor(
            AttributeData attribute,
            INamedTypeSymbol dependencyType,
            string contractName)
        {
            Attribute = attribute;
            DependencyType = dependencyType;
            ContractName = contractName;
        }

        public AttributeData Attribute { get; }

        public INamedTypeSymbol DependencyType { get; }

        public string ContractName { get; }
    }

    private readonly struct TargetRequirementDescriptor : IRequirement
    {
        public TargetRequirementDescriptor(
            AttributeData attribute,
            string targetName,
            string contractName)
        {
            Attribute = attribute;
            TargetName = targetName;
            ContractName = contractName;
        }

        public AttributeData Attribute { get; }

        public string TargetName { get; }

        public string ContractName { get; }
    }

    private readonly struct ScopeRequirementDescriptor : IRequirement
    {
        public ScopeRequirementDescriptor(
            AttributeData attribute,
            string scopeName,
            string contractName)
        {
            Attribute = attribute;
            ScopeName = scopeName;
            ContractName = contractName;
        }

        public AttributeData Attribute { get; }

        public string ScopeName { get; }

        public string ContractName { get; }
    }
}
