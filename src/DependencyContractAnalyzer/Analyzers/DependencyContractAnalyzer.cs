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
    private const string ProvidesContractAttributeMetadataName = "DependencyContractAnalyzer.ProvidesContractAttribute";
    private const string RequiresDependencyContractAttributeMetadataName = "DependencyContractAnalyzer.RequiresDependencyContractAttribute";

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
            DiagnosticDescriptors.DuplicateContractDeclaration);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var providesContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(ProvidesContractAttributeMetadataName);
            var requiresDependencyContractAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(RequiresDependencyContractAttributeMetadataName);

            if (providesContractAttributeSymbol is null || requiresDependencyContractAttributeSymbol is null)
            {
                return;
            }

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    AnalyzeNamedType(
                        symbolContext,
                        providesContractAttributeSymbol,
                        requiresDependencyContractAttributeSymbol);
                },
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol providesContractAttributeSymbol,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        if (namedType.TypeKind is not (TypeKind.Class or TypeKind.Interface))
        {
            return;
        }

        AnalyzeProvidedContracts(context, namedType, providesContractAttributeSymbol);

        if (namedType.TypeKind != TypeKind.Class)
        {
            return;
        }

        AnalyzeRequirements(
            context,
            namedType,
            providesContractAttributeSymbol,
            requiresDependencyContractAttributeSymbol);
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
        INamedTypeSymbol providesContractAttributeSymbol,
        INamedTypeSymbol requiresDependencyContractAttributeSymbol)
    {
        var requirements = new List<RequirementDescriptor>();
        var duplicateCandidates = new Dictionary<string, List<RequirementDescriptor>>(StringComparer.Ordinal);

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

            var requirement = new RequirementDescriptor(attribute, dependencyType, normalizedContractName);
            requirements.Add(requirement);

            var duplicateKey = GetDuplicateRequirementKey(dependencyType, normalizedContractName);
            if (!duplicateCandidates.TryGetValue(duplicateKey, out var duplicates))
            {
                duplicates = new List<RequirementDescriptor>();
                duplicateCandidates.Add(duplicateKey, duplicates);
            }

            duplicates.Add(requirement);
        }

        if (requirements.Count == 0)
        {
            return;
        }

        var duplicateRequirements = new HashSet<AttributeData>();
        foreach (var entry in duplicateCandidates)
        {
            if (entry.Value.Count < 2)
            {
                continue;
            }

            for (var index = 1; index < entry.Value.Count; index++)
            {
                var requirement = entry.Value[index];
                duplicateRequirements.Add(requirement.Attribute);
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateContractDeclaration,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.ContractName));
            }
        }

        var dependencies = DependencyCollector.Collect(namedType);
        var providedContractCache = new Dictionary<INamedTypeSymbol, ImmutableHashSet<string>>(SymbolEqualityComparer.Default);

        foreach (var requirement in requirements)
        {
            if (duplicateRequirements.Contains(requirement.Attribute))
            {
                continue;
            }

            var matchingDependencies = dependencies
                .Where(dependency => dependency.Type.MatchesRequiredDependencyType(requirement.DependencyType))
                .ToImmutableArray();

            if (matchingDependencies.IsDefaultOrEmpty)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnusedRequiredDependencyType,
                        GetAttributeLocation(requirement.Attribute, namedType),
                        requirement.DependencyType.ToDisplayString(MinimalSymbolDisplayFormat)));
                continue;
            }

            foreach (var dependency in matchingDependencies)
            {
                if (dependency.Type.IsExternalTo(context.Compilation.Assembly))
                {
                    continue;
                }

                if (!providedContractCache.TryGetValue(dependency.Type, out var providedContracts))
                {
                    providedContracts = GetProvidedContracts(dependency.Type, providesContractAttributeSymbol);
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
        }
    }

    private static ImmutableHashSet<string> GetProvidedContracts(
        INamedTypeSymbol type,
        INamedTypeSymbol providesContractAttributeSymbol)
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

            foreach (var attribute in current.GetAttributes())
            {
                if (!attribute.AttributeClass.SymbolEquals(providesContractAttributeSymbol) ||
                    !TryGetStringArgument(attribute, 0, out var contractName))
                {
                    continue;
                }

                var normalizedContractName = ContractNameNormalizer.Normalize(contractName);
                if (normalizedContractName is not null)
                {
                    contracts.Add(normalizedContractName);
                }
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

        return contracts.ToImmutable();
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

    private readonly struct RequirementDescriptor
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
}
