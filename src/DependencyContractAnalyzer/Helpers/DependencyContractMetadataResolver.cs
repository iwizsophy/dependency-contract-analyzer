using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DependencyContractAnalyzer.Utilities;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal sealed class DependencyContractMetadataResolver
{
    private static readonly ImmutableHashSet<string> EmptyNames =
        ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private readonly IAssemblySymbol _compilationAssembly;
    private readonly NamespaceInferenceOptions _namespaceInferenceOptions;
    private readonly INamedTypeSymbol _providesContractAttributeSymbol;
    private readonly string _providesContractAttributeMetadataName;
    private readonly INamedTypeSymbol? _contractTargetAttributeSymbol;
    private readonly string? _contractTargetAttributeMetadataName;
    private readonly INamedTypeSymbol? _contractScopeAttributeSymbol;
    private readonly string? _contractScopeAttributeMetadataName;
    private readonly ContractImplicationResolver _contractImplicationResolver;
    private readonly Func<IAssemblySymbol, ContractImplicationResolver> _resolveReferencedAssemblyImplicationGraph;

    public DependencyContractMetadataResolver(
        IAssemblySymbol compilationAssembly,
        NamespaceInferenceOptions namespaceInferenceOptions,
        INamedTypeSymbol providesContractAttributeSymbol,
        INamedTypeSymbol? contractTargetAttributeSymbol,
        INamedTypeSymbol? contractScopeAttributeSymbol,
        ContractImplicationResolver contractImplicationResolver,
        Func<IAssemblySymbol, ContractImplicationResolver> resolveReferencedAssemblyImplicationGraph)
    {
        _compilationAssembly = compilationAssembly;
        _namespaceInferenceOptions = namespaceInferenceOptions;
        _providesContractAttributeSymbol = providesContractAttributeSymbol;
        _providesContractAttributeMetadataName = GetFullyQualifiedMetadataName(providesContractAttributeSymbol);
        _contractTargetAttributeSymbol = contractTargetAttributeSymbol;
        _contractTargetAttributeMetadataName = contractTargetAttributeSymbol is null
            ? null
            : GetFullyQualifiedMetadataName(contractTargetAttributeSymbol);
        _contractScopeAttributeSymbol = contractScopeAttributeSymbol;
        _contractScopeAttributeMetadataName = contractScopeAttributeSymbol is null
            ? null
            : GetFullyQualifiedMetadataName(contractScopeAttributeSymbol);
        _contractImplicationResolver = contractImplicationResolver;
        _resolveReferencedAssemblyImplicationGraph = resolveReferencedAssemblyImplicationGraph;

        KnownTargets = contractTargetAttributeSymbol is null
            ? EmptyNames
            : CollectKnownTargets(compilationAssembly);
        KnownScopes = contractScopeAttributeSymbol is null
            ? EmptyNames
            : CollectKnownScopes(compilationAssembly);
    }

    public ImmutableHashSet<string> KnownTargets { get; }

    public ImmutableHashSet<string> KnownScopes { get; }

    public ImmutableHashSet<string> GetProvidedContracts(INamedTypeSymbol type)
    {
        var contracts = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var implicationResolvers = new List<ContractImplicationResolver> { _contractImplicationResolver };
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
                _providesContractAttributeSymbol,
                _providesContractAttributeMetadataName,
                0,
                contracts);

            if (!current.ContainingAssembly.SymbolEquals(_compilationAssembly) &&
                referencedAssemblies.Add(current.ContainingAssembly))
            {
                implicationResolvers.Add(
                    _resolveReferencedAssemblyImplicationGraph(current.ContainingAssembly));
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

    public ImmutableHashSet<string> GetTargets(
        INamedTypeSymbol type,
        bool allowNamespaceInference = true)
    {
        if (_contractTargetAttributeSymbol is null ||
            _contractTargetAttributeMetadataName is null)
        {
            return EmptyNames;
        }

        return CollectResolvedHierarchyNames(
            type,
            static _ => { },
            (current, targetNames, canInferFromNamespace) =>
                AddResolvedNames(
                    current,
                    _contractTargetAttributeSymbol,
                    _contractTargetAttributeMetadataName,
                    targetNames,
                    allowNamespaceInference: allowNamespaceInference && canInferFromNamespace));
    }

    public ImmutableHashSet<string> GetScopes(
        INamedTypeSymbol type,
        bool allowNamespaceInference = true)
    {
        if (_contractScopeAttributeSymbol is null ||
            _contractScopeAttributeMetadataName is null)
        {
            return EmptyNames;
        }

        return CollectResolvedHierarchyNames(
            type,
            scopeNames =>
                AddNormalizedNames(
                    type.ContainingAssembly.GetAttributes(),
                    _contractScopeAttributeSymbol,
                    _contractScopeAttributeMetadataName,
                    0,
                    scopeNames),
            (current, scopeNames, canInferFromNamespace) =>
                AddResolvedNames(
                    current,
                    _contractScopeAttributeSymbol,
                    _contractScopeAttributeMetadataName,
                    scopeNames,
                    allowNamespaceInference: allowNamespaceInference && canInferFromNamespace));
    }

    private ImmutableHashSet<string> CollectKnownTargets(IAssemblySymbol assembly)
    {
        if (_contractTargetAttributeSymbol is null ||
            _contractTargetAttributeMetadataName is null)
        {
            return EmptyNames;
        }

        return CollectKnownNames(
            assembly,
            static _ => { },
            (type, knownNames) =>
                AddResolvedNames(
                    type,
                    _contractTargetAttributeSymbol,
                    _contractTargetAttributeMetadataName,
                    knownNames));
    }

    private ImmutableHashSet<string> CollectKnownScopes(IAssemblySymbol assembly)
    {
        if (_contractScopeAttributeSymbol is null ||
            _contractScopeAttributeMetadataName is null)
        {
            return EmptyNames;
        }

        return CollectKnownNames(
            assembly,
            knownNames =>
                AddNormalizedNames(
                    assembly.GetAttributes(),
                    _contractScopeAttributeSymbol,
                    _contractScopeAttributeMetadataName,
                    0,
                    knownNames),
            (type, knownNames) =>
                AddResolvedNames(
                    type,
                    _contractScopeAttributeSymbol,
                    _contractScopeAttributeMetadataName,
                    knownNames));
    }

    private ImmutableHashSet<string> CollectResolvedHierarchyNames(
        INamedTypeSymbol type,
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
                !current.IsExternalTo(_compilationAssembly));

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

    private void AddResolvedNames(
        INamedTypeSymbol type,
        INamedTypeSymbol? expectedAttributeSymbol,
        string? expectedAttributeMetadataName,
        ImmutableHashSet<string>.Builder names,
        bool blockNamespaceInference = false,
        bool allowNamespaceInference = true)
    {
        if (expectedAttributeMetadataName is null ||
            AddNormalizedNames(
                type.GetAttributes(),
                expectedAttributeSymbol,
                expectedAttributeMetadataName,
                0,
                names) ||
            blockNamespaceInference ||
            !allowNamespaceInference)
        {
            return;
        }

        foreach (var inferredName in NamespaceNameInference.InferNames(type.ContainingNamespace, _namespaceInferenceOptions.MaxSegments))
        {
            names.Add(inferredName);
        }
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
}
