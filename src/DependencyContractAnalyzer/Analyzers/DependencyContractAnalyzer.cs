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
            var metadataResolver = new DependencyContractMetadataResolver(
                startContext.Compilation.Assembly,
                namespaceInferenceOptions,
                providesContractAttributeSymbol,
                contractTargetAttributeSymbol,
                contractScopeAttributeSymbol,
                contractImplicationResolver,
                ResolveReferencedAssemblyImplicationGraph);
            var assemblyScopeDiagnostics = contractScopeAttributeSymbol is null
                ? ImmutableArray<Diagnostic>.Empty
                : CollectAssemblyScopeDiagnostics(startContext.Compilation.Assembly, contractScopeAttributeSymbol);

            if (!contractImplicationResolver.Diagnostics.IsDefaultOrEmpty ||
                !assemblyScopeDiagnostics.IsDefaultOrEmpty)
            {
                startContext.RegisterCompilationEndAction(
                    compilationContext =>
                    {
                        foreach (var diagnostic in contractImplicationResolver.Diagnostics)
                        {
                            compilationContext.ReportDiagnostic(diagnostic);
                        }

                        foreach (var diagnostic in assemblyScopeDiagnostics)
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
                        metadataResolver,
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
        DependencyContractMetadataResolver metadataResolver,
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
            metadataResolver,
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

    private static ImmutableArray<Diagnostic> CollectAssemblyScopeDiagnostics(
        IAssemblySymbol assembly,
        INamedTypeSymbol contractScopeAttributeSymbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var attribute in assembly.GetAttributes())
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

            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.EmptyScopeName,
                    GetAttributeLocation(attribute, assembly)));
        }

        return diagnostics.ToImmutable();
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
        DependencyContractMetadataResolver metadataResolver,
        INamedTypeSymbol? requiresContractOnScopeAttributeSymbol,
        INamedTypeSymbol? requiresContractOnTargetAttributeSymbol,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredDependencyContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredTargetContractAttributeSymbol,
        INamedTypeSymbol? suppressRequiredScopeContractAttributeSymbol)
    {
        var collectedRequirements = RequirementCollector.Collect(
            context,
            namedType,
            requiresDependencyContractAttributeSymbol,
            contractTargetAttributeSymbol is null ? null : requiresContractOnTargetAttributeSymbol,
            contractScopeAttributeSymbol is null ? null : requiresContractOnScopeAttributeSymbol,
            suppressRequiredDependencyContractAttributeSymbol,
            suppressRequiredTargetContractAttributeSymbol,
            suppressRequiredScopeContractAttributeSymbol);

        if (!collectedRequirements.HasAnyRequirement)
        {
            return;
        }

        var dependencies = DependencyCollector.Collect(
            namedType,
            context.Compilation,
            dependencyCollectionOptions,
            excludeDependencyContractSourceAttributeSymbol,
            context.CancellationToken);
        RequirementEvaluator.Evaluate(
            context,
            namedType,
            collectedRequirements,
            dependencies,
            requirementEvaluationOptions,
            externalDependencyOptions,
            metadataResolver,
            MinimalSymbolDisplayFormat);
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
}
