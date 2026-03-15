using System;
using System.Collections.Concurrent;
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

            var contractImplicationResolver = ContractImplicationResolver.Create(
                startContext.Compilation.Assembly,
                contractHierarchyAttributeSymbol);
            // Referenced implication graphs are resolved lazily and cached per assembly.
            // The local compilation still owns diagnostic reporting for implication problems.
            var referencedAssemblyImplicationResolverCache =
                new ConcurrentDictionary<IAssemblySymbol, ContractImplicationResolver>(SymbolEqualityComparer.Default);
            ContractImplicationResolver ResolveReferencedAssemblyImplicationGraph(IAssemblySymbol assembly) =>
                referencedAssemblyImplicationResolverCache.GetOrAdd(
                    assembly,
                    referencedAssembly =>
                        ContractImplicationResolver.CreateExternal(
                            referencedAssembly,
                            contractHierarchyAttributeSymbol));
            var externalDependencyOptions = ExternalDependencyOptions.Create(startContext.Options.AnalyzerConfigOptionsProvider);
            var namespaceInferenceOptions = NamespaceInferenceOptions.Create(startContext.Options.AnalyzerConfigOptionsProvider);
            var knownTargets = contractTargetAttributeSymbol is null
                ? CreateEmptyNameSet()
                : CollectKnownTargets(startContext.Compilation.Assembly, contractTargetAttributeSymbol, namespaceInferenceOptions);
            var knownScopes = contractScopeAttributeSymbol is null
                ? CreateEmptyNameSet()
                : CollectKnownScopes(startContext.Compilation.Assembly, contractScopeAttributeSymbol, namespaceInferenceOptions);

            if (!contractImplicationResolver.Diagnostics.IsDefaultOrEmpty)
            {
                startContext.RegisterCompilationEndAction(
                    compilationContext =>
                    {
                        foreach (var diagnostic in contractImplicationResolver.Diagnostics)
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
                        externalDependencyOptions,
                        namespaceInferenceOptions,
                        contractImplicationResolver,
                        ResolveReferencedAssemblyImplicationGraph,
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
        ExternalDependencyOptions externalDependencyOptions,
        NamespaceInferenceOptions namespaceInferenceOptions,
        ContractImplicationResolver contractImplicationResolver,
        Func<IAssemblySymbol, ContractImplicationResolver> resolveReferencedAssemblyImplicationGraph,
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
            RequirementEvaluationOptions.Create(context.Options.AnalyzerConfigOptionsProvider, namedType),
            excludeDependencyContractSourceAttributeSymbol,
            externalDependencyOptions,
            namespaceInferenceOptions,
            contractImplicationResolver,
            resolveReferencedAssemblyImplicationGraph,
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
        RequirementEvaluationOptions requirementEvaluationOptions,
        INamedTypeSymbol? excludeDependencyContractSourceAttributeSymbol,
        ExternalDependencyOptions externalDependencyOptions,
        NamespaceInferenceOptions namespaceInferenceOptions,
        ContractImplicationResolver contractImplicationResolver,
        Func<IAssemblySymbol, ContractImplicationResolver> resolveReferencedAssemblyImplicationGraph,
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

        var duplicateTargetCandidates = new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var targetRequirements = new List<TargetRequirementDescriptor>();

        if (contractTargetAttributeSymbol is not null && requiresContractOnTargetAttributeSymbol is not null)
        {
            targetRequirements = CollectNamedRequirements(
                context,
                namedType,
                requiresContractOnTargetAttributeSymbol,
                DiagnosticDescriptors.EmptyTargetName,
                duplicateTargetCandidates,
                static (attribute, targetName, contractName) => new TargetRequirementDescriptor(attribute, targetName, contractName));
        }

        var duplicateScopeCandidates = new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var scopeRequirements = new List<ScopeRequirementDescriptor>();

        if (contractScopeAttributeSymbol is not null && requiresContractOnScopeAttributeSymbol is not null)
        {
            scopeRequirements = CollectNamedRequirements(
                context,
                namedType,
                requiresContractOnScopeAttributeSymbol,
                DiagnosticDescriptors.EmptyScopeName,
                duplicateScopeCandidates,
                static (attribute, scopeName, contractName) => new ScopeRequirementDescriptor(attribute, scopeName, contractName));
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

        var duplicateTargetSuppressionCandidates = new Dictionary<string, List<TargetRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidTargetSuppressionAttributes = new HashSet<AttributeData>();
        var targetSuppressions = new List<TargetRequirementDescriptor>();

        if (suppressRequiredTargetContractAttributeSymbol is not null)
        {
            targetSuppressions = CollectNamedRequirements(
                context,
                namedType,
                suppressRequiredTargetContractAttributeSymbol,
                DiagnosticDescriptors.EmptyTargetName,
                duplicateTargetSuppressionCandidates,
                static (attribute, targetName, contractName) => new TargetRequirementDescriptor(attribute, targetName, contractName),
                invalidTargetSuppressionAttributes);
        }

        var duplicateScopeSuppressionCandidates = new Dictionary<string, List<ScopeRequirementDescriptor>>(StringComparer.OrdinalIgnoreCase);
        var invalidScopeSuppressionAttributes = new HashSet<AttributeData>();
        var scopeSuppressions = new List<ScopeRequirementDescriptor>();

        if (suppressRequiredScopeContractAttributeSymbol is not null)
        {
            scopeSuppressions = CollectNamedRequirements(
                context,
                namedType,
                suppressRequiredScopeContractAttributeSymbol,
                DiagnosticDescriptors.EmptyScopeName,
                duplicateScopeSuppressionCandidates,
                static (attribute, scopeName, contractName) => new ScopeRequirementDescriptor(attribute, scopeName, contractName),
                invalidScopeSuppressionAttributes);
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

                if (dependency.Type.IsExternalTo(context.Compilation.Assembly) &&
                    externalDependencyOptions.Policy == ExternalDependencyPolicy.Ignore)
                {
                    continue;
                }

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = GetProvidedContracts(
                        dependency.Type,
                        providesContractAttributeSymbol,
                        contractImplicationResolver,
                        context.Compilation.Assembly,
                        resolveReferencedAssemblyImplicationGraph);
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

            if (!hasMatchingDependency &&
                requirementEvaluationOptions.ReportUnusedRequirementDiagnostics)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnusedRequiredDependencyType,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.DependencyType.ToDisplayString(MinimalSymbolDisplayFormat)));
            }
        }

        ImmutableHashSet<string> ResolveTargets(INamedTypeSymbol dependencyType, bool isExternalDependency)
        {
            if (!targetCache.TryGetValue(dependencyType, out var targets))
            {
                targets = GetTargets(
                    dependencyType,
                    contractTargetAttributeSymbol!,
                    namespaceInferenceOptions,
                    context.Compilation.Assembly,
                    allowNamespaceInference: !isExternalDependency);
                targetCache.Add(dependencyType, targets);
            }

            return targets;
        }

        if (contractTargetAttributeSymbol is not null)
        {
            AnalyzeNamedRequirements(
                context,
                namedType,
                targetRequirements,
                duplicateTargetRequirements,
                targetSuppressionKeys,
                knownTargets,
                dependencies,
                requirementEvaluationOptions,
                externalDependencyOptions,
                providedContractCache,
                ResolveTargets,
                providesContractAttributeSymbol,
                contractImplicationResolver,
                resolveReferencedAssemblyImplicationGraph,
                DiagnosticDescriptors.UndeclaredRequiredTarget,
                DiagnosticDescriptors.UnusedRequiredTarget);
        }

        if (contractScopeAttributeSymbol is null)
        {
            return;
        }

        ImmutableHashSet<string> ResolveScopes(INamedTypeSymbol dependencyType, bool isExternalDependency)
        {
            if (!scopeCache.TryGetValue(dependencyType, out var scopes))
            {
                scopes = GetScopes(
                    dependencyType,
                    contractScopeAttributeSymbol,
                    namespaceInferenceOptions,
                    context.Compilation.Assembly,
                    allowNamespaceInference: !isExternalDependency);
                scopeCache.Add(dependencyType, scopes);
            }

            return scopes;
        }

        AnalyzeNamedRequirements(
            context,
            namedType,
            scopeRequirements,
            duplicateScopeRequirements,
            scopeSuppressionKeys,
            knownScopes,
            dependencies,
            requirementEvaluationOptions,
            externalDependencyOptions,
            providedContractCache,
            ResolveScopes,
            providesContractAttributeSymbol,
            contractImplicationResolver,
            resolveReferencedAssemblyImplicationGraph,
            DiagnosticDescriptors.UndeclaredRequiredScope,
            DiagnosticDescriptors.UnusedRequiredScope);
    }

    private static ImmutableHashSet<string> GetProvidedContracts(
        INamedTypeSymbol type,
        INamedTypeSymbol providesContractAttributeSymbol,
        ContractImplicationResolver contractImplicationResolver,
        IAssemblySymbol compilationAssembly,
        Func<IAssemblySymbol, ContractImplicationResolver> resolveReferencedAssemblyImplicationGraph)
    {
        var contracts = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var implicationResolvers = new List<ContractImplicationResolver> { contractImplicationResolver };
        var referencedAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
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

            AddNormalizedNames(
                current.GetAttributes(),
                providesContractAttributeSymbol,
                ProvidesContractAttributeMetadataName,
                0,
                contracts);

            if (!current.ContainingAssembly.SymbolEquals(compilationAssembly) &&
                referencedAssemblies.Add(current.ContainingAssembly))
            {
                implicationResolvers.Add(
                    resolveReferencedAssemblyImplicationGraph(current.ContainingAssembly));
            }

            if (current.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                toVisit.Push(baseType);
            }

            foreach (var interfaceType in current.Interfaces)
            {
                toVisit.Push(interfaceType);
            }
        }

        return ContractImplicationResolver.ExpandAcross(contracts.ToImmutable(), implicationResolvers);
    }

    private static ImmutableHashSet<string> GetTargets(
        INamedTypeSymbol type,
        INamedTypeSymbol contractTargetAttributeSymbol,
        NamespaceInferenceOptions namespaceInferenceOptions,
        IAssemblySymbol compilationAssembly,
        bool allowNamespaceInference = true)
        => CollectResolvedHierarchyNames(
            type,
            compilationAssembly,
            static _ => { },
            (current, targetNames, canInferFromNamespace) =>
                AddResolvedNames(
                    current,
                    contractTargetAttributeSymbol,
                    ContractTargetAttributeMetadataName,
                    namespaceInferenceOptions,
                    targetNames,
                    allowNamespaceInference: allowNamespaceInference && canInferFromNamespace));

    private static ImmutableHashSet<string> GetScopes(
        INamedTypeSymbol type,
        INamedTypeSymbol contractScopeAttributeSymbol,
        NamespaceInferenceOptions namespaceInferenceOptions,
        IAssemblySymbol compilationAssembly,
        bool allowNamespaceInference = true)
    {
        var hasAssemblyLevelScopes = false;
        return CollectResolvedHierarchyNames(
            type,
            compilationAssembly,
            scopeNames =>
                hasAssemblyLevelScopes = AddNormalizedNames(
                    type.ContainingAssembly.GetAttributes(),
                    contractScopeAttributeSymbol,
                    ContractScopeAttributeMetadataName,
                    0,
                    scopeNames),
            (current, scopeNames, canInferFromNamespace) =>
                AddResolvedNames(
                    current,
                    contractScopeAttributeSymbol,
                    ContractScopeAttributeMetadataName,
                    namespaceInferenceOptions,
                    scopeNames,
                    blockNamespaceInference: hasAssemblyLevelScopes,
                    allowNamespaceInference: allowNamespaceInference && canInferFromNamespace));
    }

    private static ImmutableHashSet<string> CollectKnownTargets(
        IAssemblySymbol assembly,
        INamedTypeSymbol contractTargetAttributeSymbol,
        NamespaceInferenceOptions namespaceInferenceOptions)
        => CollectKnownNames(
            assembly,
            static _ => { },
            (type, knownNames) =>
                AddResolvedNames(
                    type,
                    contractTargetAttributeSymbol,
                    ContractTargetAttributeMetadataName,
                    namespaceInferenceOptions,
                    knownNames));

    private static ImmutableHashSet<string> CollectKnownScopes(
        IAssemblySymbol assembly,
        INamedTypeSymbol contractScopeAttributeSymbol,
        NamespaceInferenceOptions namespaceInferenceOptions)
    {
        var hasAssemblyLevelScopes = false;
        return CollectKnownNames(
            assembly,
            knownNames =>
                hasAssemblyLevelScopes = AddNormalizedNames(
                    assembly.GetAttributes(),
                    contractScopeAttributeSymbol,
                    ContractScopeAttributeMetadataName,
                    0,
                    knownNames),
            (type, knownNames) =>
                AddResolvedNames(
                    type,
                    contractScopeAttributeSymbol,
                    ContractScopeAttributeMetadataName,
                    namespaceInferenceOptions,
                    knownNames,
                    blockNamespaceInference: hasAssemblyLevelScopes));
    }

    // Target and scope names follow the same inheritance walk; keep that traversal
    // shared so inference or metadata tweaks land in both modes together.
    private static ImmutableHashSet<string> CollectResolvedHierarchyNames(
        INamedTypeSymbol type,
        IAssemblySymbol compilationAssembly,
        Action<ImmutableHashSet<string>.Builder> initializeNames,
        Action<INamedTypeSymbol, ImmutableHashSet<string>.Builder, bool> addTypeNames)
    {
        var resolvedNames = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        initializeNames(resolvedNames);
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

            addTypeNames(
                current,
                resolvedNames,
                !current.IsExternalTo(compilationAssembly));

            if (current.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                toVisit.Push(baseType);
            }

            foreach (var interfaceType in current.Interfaces)
            {
                toVisit.Push(interfaceType);
            }
        }

        return resolvedNames.ToImmutable();
    }

    private static ImmutableHashSet<string> CollectKnownNames(
        IAssemblySymbol assembly,
        Action<ImmutableHashSet<string>.Builder> initializeNames,
        Action<INamedTypeSymbol, ImmutableHashSet<string>.Builder> addTypeNames)
    {
        var knownNames = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        initializeNames(knownNames);
        AddKnownNames(assembly.GlobalNamespace, knownNames, addTypeNames);
        return knownNames.ToImmutable();
    }

    private static void AddKnownNames(
        INamespaceSymbol namespaceSymbol,
        ImmutableHashSet<string>.Builder knownNames,
        Action<INamedTypeSymbol, ImmutableHashSet<string>.Builder> addTypeNames)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            AddKnownNames(type, knownNames, addTypeNames);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            AddKnownNames(nestedNamespace, knownNames, addTypeNames);
        }
    }

    private static void AddKnownNames(
        INamedTypeSymbol type,
        ImmutableHashSet<string>.Builder knownNames,
        Action<INamedTypeSymbol, ImmutableHashSet<string>.Builder> addTypeNames)
    {
        addTypeNames(type, knownNames);

        foreach (var nestedType in type.GetTypeMembers())
        {
            AddKnownNames(nestedType, knownNames, addTypeNames);
        }
    }

    private static ImmutableHashSet<string> CreateEmptyNameSet() =>
        ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private static List<TRequirement> CollectNamedRequirements<TRequirement>(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        INamedTypeSymbol attributeSymbol,
        DiagnosticDescriptor emptyNameDescriptor,
        Dictionary<string, List<TRequirement>> duplicateCandidates,
        Func<AttributeData, string, string, TRequirement> requirementFactory,
        HashSet<AttributeData>? invalidAttributes = null)
        where TRequirement : struct, INamedRequirement
    {
        var requirements = new List<TRequirement>();

        foreach (var attribute in namedType.GetAttributes())
        {
            if (!attribute.AttributeClass.SymbolEquals(attributeSymbol) ||
                !TryGetNamedRequirementParts(
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

        return requirements;
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

    // Target- and scope-based rules intentionally share the same evaluation path so
    // future policy changes cannot silently drift between the two named variants.
    private static void AnalyzeNamedRequirements<TRequirement>(
        SymbolAnalysisContext context,
        INamedTypeSymbol namedType,
        IEnumerable<TRequirement> requirements,
        HashSet<AttributeData> duplicateRequirements,
        ImmutableHashSet<string> suppressionKeys,
        ImmutableHashSet<string> knownNames,
        ImmutableArray<DependencyDescriptor> dependencies,
        RequirementEvaluationOptions requirementEvaluationOptions,
        ExternalDependencyOptions externalDependencyOptions,
        Dictionary<INamedTypeSymbol, ImmutableHashSet<string>> providedContractCache,
        Func<INamedTypeSymbol, bool, ImmutableHashSet<string>> resolveDependencyNames,
        INamedTypeSymbol providesContractAttributeSymbol,
        ContractImplicationResolver contractImplicationResolver,
        Func<IAssemblySymbol, ContractImplicationResolver> resolveReferencedAssemblyImplicationGraph,
        DiagnosticDescriptor undeclaredDescriptor,
        DiagnosticDescriptor unusedDescriptor)
        where TRequirement : struct, INamedRequirement
    {
        foreach (var requirement in requirements)
        {
            if (duplicateRequirements.Contains(requirement.Attribute))
            {
                continue;
            }

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
                    providedContracts = GetProvidedContracts(
                        dependency.Type,
                        providesContractAttributeSymbol,
                        contractImplicationResolver,
                        context.Compilation.Assembly,
                        resolveReferencedAssemblyImplicationGraph);
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

    private static bool AddNormalizedNames(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? expectedAttributeSymbol,
        string expectedAttributeMetadataName,
        int argumentIndex,
        ImmutableHashSet<string>.Builder values)
    {
        var added = false;

        foreach (var attribute in attributes)
        {
            if (!HasMatchingAttributeClass(attribute, expectedAttributeSymbol, expectedAttributeMetadataName) ||
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

    private static void AddResolvedNames(
        INamedTypeSymbol type,
        INamedTypeSymbol attributeSymbol,
        string attributeMetadataName,
        NamespaceInferenceOptions namespaceInferenceOptions,
        ImmutableHashSet<string>.Builder names,
        bool blockNamespaceInference = false,
        bool allowNamespaceInference = true)
    {
        if (AddNormalizedNames(
                type.GetAttributes(),
                attributeSymbol,
                attributeMetadataName,
                0,
                names) ||
            blockNamespaceInference ||
            !allowNamespaceInference)
        {
            return;
        }

        foreach (var inferredName in NamespaceNameInference.InferNames(type.ContainingNamespace, namespaceInferenceOptions.MaxSegments))
        {
            names.Add(inferredName);
        }
    }

    private static bool HasMatchingAttributeClass(
        AttributeData attribute,
        INamedTypeSymbol? expectedAttributeSymbol,
        string expectedAttributeMetadataName)
    {
        var attributeClass = attribute.AttributeClass;
        return attributeClass is not null &&
            (attributeClass.SymbolEquals(expectedAttributeSymbol) ||
             string.Equals(
                 GetFullyQualifiedMetadataName(attributeClass),
                 expectedAttributeMetadataName,
                 StringComparison.Ordinal));
    }

    private static string GetFullyQualifiedMetadataName(INamedTypeSymbol symbol)
    {
        var parts = new Stack<string>();
        ISymbol? current = symbol;

        while (current is not null)
        {
            switch (current)
            {
                case INamedTypeSymbol namedType:
                    parts.Push(namedType.MetadataName);
                    current = namedType.ContainingSymbol;
                    break;
                case INamespaceSymbol namespaceSymbol when !namespaceSymbol.IsGlobalNamespace:
                    parts.Push(namespaceSymbol.MetadataName);
                    current = namespaceSymbol.ContainingSymbol;
                    break;
                default:
                    current = null;
                    break;
            }
        }

        return string.Join(".", parts);
    }

    private interface IRequirement
    {
        AttributeData Attribute { get; }

        string ContractName { get; }
    }

    private interface INamedRequirement : IRequirement
    {
        string Name { get; }
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

    private readonly struct TargetRequirementDescriptor : INamedRequirement
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

        public string Name => TargetName;

        public string ContractName { get; }
    }

    private readonly struct ScopeRequirementDescriptor : INamedRequirement
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

        public string Name => ScopeName;

        public string ContractName { get; }
    }
}
