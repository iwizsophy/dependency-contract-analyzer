using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DependencyContractAnalyzer.Diagnostics;
using DependencyContractAnalyzer.Utilities;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal sealed class ContractAliasResolver
{
    private const string ContractAliasAttributeMetadataName = "DependencyContractAnalyzer.ContractAliasAttribute";
    private const string ContractHierarchyAttributeMetadataName = "DependencyContractAnalyzer.ContractHierarchyAttribute";

    private static readonly ContractAliasResolver Empty =
        new(
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase),
            ImmutableArray<Diagnostic>.Empty);

    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _aliasClosure;

    private ContractAliasResolver(
        ImmutableDictionary<string, ImmutableHashSet<string>> aliasClosure,
        ImmutableArray<Diagnostic> diagnostics)
    {
        _aliasClosure = aliasClosure;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public static ContractAliasResolver Create(
        IAssemblySymbol assembly,
        INamedTypeSymbol? contractAliasAttributeSymbol,
        INamedTypeSymbol? contractHierarchyAttributeSymbol)
    {
        return CreateCore(
            assembly,
            contractAliasAttributeSymbol,
            contractHierarchyAttributeSymbol,
            reportDiagnostics: true);
    }

    public static ContractAliasResolver CreateExternal(
        IAssemblySymbol assembly,
        INamedTypeSymbol? contractAliasAttributeSymbol,
        INamedTypeSymbol? contractHierarchyAttributeSymbol)
    {
        // Referenced assemblies can contribute implication edges, but their malformed
        // declarations should not surface diagnostics in the consuming compilation.
        return CreateCore(
            assembly,
            contractAliasAttributeSymbol,
            contractHierarchyAttributeSymbol,
            reportDiagnostics: false);
    }

    public static ImmutableHashSet<string> ExpandAcross(
        ImmutableHashSet<string> contracts,
        IEnumerable<ContractAliasResolver> resolvers)
    {
        var expandedContracts = contracts;

        while (true)
        {
            var changed = false;

            foreach (var resolver in resolvers)
            {
                // Local and referenced graphs can unlock each other transitively,
                // so repeat until the combined implication set stops growing.
                var nextContracts = resolver.Expand(expandedContracts);
                if (nextContracts.SetEquals(expandedContracts))
                {
                    continue;
                }

                expandedContracts = nextContracts;
                changed = true;
            }

            if (!changed)
            {
                return expandedContracts;
            }
        }
    }

    private static ContractAliasResolver CreateCore(
        IAssemblySymbol assembly,
        INamedTypeSymbol? contractAliasAttributeSymbol,
        INamedTypeSymbol? contractHierarchyAttributeSymbol,
        bool reportDiagnostics)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var duplicateCandidates = new Dictionary<string, List<ImplicationDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in assembly.GetAttributes())
        {
            var isAlias = HasMatchingAttributeClass(
                attribute,
                contractAliasAttributeSymbol,
                ContractAliasAttributeMetadataName);
            var isHierarchy = HasMatchingAttributeClass(
                attribute,
                contractHierarchyAttributeSymbol,
                ContractHierarchyAttributeMetadataName);
            if (!isAlias && !isHierarchy)
            {
                continue;
            }

            var hasFrom = TryGetStringArgument(attribute, 0, out var fromName);
            var hasTo = TryGetStringArgument(attribute, 1, out var toName);

            var normalizedFrom = hasFrom
                ? ContractNameNormalizer.Normalize(fromName)
                : null;
            if (normalizedFrom is null)
            {
                if (reportDiagnostics)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute)));
                }
            }

            var normalizedTo = hasTo
                ? ContractNameNormalizer.Normalize(toName)
                : null;
            if (normalizedTo is null)
            {
                if (reportDiagnostics)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.EmptyContractName,
                            GetAttributeLocation(attribute)));
                }
            }

            if (normalizedFrom is null || normalizedTo is null)
            {
                continue;
            }

            if (!ContractNameFormat.IsLowerKebabCase(normalizedFrom))
            {
                if (reportDiagnostics)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ContractNamingFormatViolation,
                            GetAttributeLocation(attribute),
                            normalizedFrom));
                }
            }

            if (!ContractNameFormat.IsLowerKebabCase(normalizedTo))
            {
                if (reportDiagnostics)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ContractNamingFormatViolation,
                            GetAttributeLocation(attribute),
                            normalizedTo));
                }
            }

            var definition = new ImplicationDefinition(attribute, normalizedFrom, normalizedTo);
            var duplicateKey = normalizedFrom + "|" + normalizedTo;
            if (!duplicateCandidates.TryGetValue(duplicateKey, out var definitions))
            {
                definitions = new List<ImplicationDefinition>();
                duplicateCandidates.Add(duplicateKey, definitions);
            }

            definitions.Add(definition);
        }

        var implicationDefinitions = new List<ImplicationDefinition>();
        foreach (var entry in duplicateCandidates)
        {
            implicationDefinitions.Add(entry.Value[0]);

            if (!reportDiagnostics || entry.Value.Count < 2)
            {
                continue;
            }

            var displayName = FormatAlias(entry.Value[0].From, entry.Value[0].To);
            for (var index = 1; index < entry.Value.Count; index++)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateContractDeclaration,
                        GetAttributeLocation(entry.Value[index].Attribute),
                        displayName));
            }
        }

        if (implicationDefinitions.Count == 0)
        {
            return new ContractAliasResolver(
                Empty._aliasClosure,
                reportDiagnostics ? diagnostics.ToImmutable() : ImmutableArray<Diagnostic>.Empty);
        }

        var adjacency = BuildAdjacency(implicationDefinitions);
        if (reportDiagnostics)
        {
            ReportCycles(implicationDefinitions, adjacency, diagnostics);
        }

        return new ContractAliasResolver(
            BuildClosure(adjacency),
            reportDiagnostics ? diagnostics.ToImmutable() : ImmutableArray<Diagnostic>.Empty);
    }

    public ImmutableHashSet<string> Expand(ImmutableHashSet<string> contracts)
    {
        if (contracts.IsEmpty || _aliasClosure.Count == 0)
        {
            return contracts;
        }

        var expanded = contracts.ToBuilder();
        foreach (var contract in contracts)
        {
            if (_aliasClosure.TryGetValue(contract, out var impliedContracts))
            {
                expanded.UnionWith(impliedContracts);
            }
        }

        return expanded.ToImmutable();
    }

    private static Dictionary<string, ImmutableHashSet<string>> BuildAdjacency(IEnumerable<ImplicationDefinition> implicationDefinitions)
    {
        var adjacency = new Dictionary<string, ImmutableHashSet<string>.Builder>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in implicationDefinitions)
        {
            if (!adjacency.TryGetValue(definition.From, out var targets))
            {
                targets = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                adjacency.Add(definition.From, targets);
            }

            targets.Add(definition.To);
        }

        return adjacency.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToImmutable(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static ImmutableDictionary<string, ImmutableHashSet<string>> BuildClosure(
        Dictionary<string, ImmutableHashSet<string>> adjacency)
    {
        var closure = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var from in adjacency.Keys)
        {
            var reachable = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
            var toVisit = new Stack<string>(adjacency[from]);

            while (toVisit.Count > 0)
            {
                var current = toVisit.Pop();
                if (!reachable.Add(current))
                {
                    continue;
                }

                if (!adjacency.TryGetValue(current, out var nextContracts))
                {
                    continue;
                }

                foreach (var next in nextContracts)
                {
                    toVisit.Push(next);
                }
            }

            reachable.Remove(from);
            closure.Add(from, reachable.ToImmutable());
        }

        return closure.ToImmutable();
    }

    private static void ReportCycles(
        IReadOnlyList<ImplicationDefinition> implicationDefinitions,
        Dictionary<string, ImmutableHashSet<string>> adjacency,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var componentByNode = FindStronglyConnectedComponents(adjacency);
        if (componentByNode.Count == 0)
        {
            return;
        }

        var componentSizes = componentByNode
            .GroupBy(static pair => pair.Value)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var cyclicComponents = new HashSet<int>();

        foreach (var pair in adjacency)
        {
            var fromComponent = componentByNode[pair.Key];
            foreach (var to in pair.Value)
            {
                var toComponent = componentByNode[to];
                if (fromComponent != toComponent)
                {
                    continue;
                }

                if (componentSizes[fromComponent] > 1 ||
                    string.Equals(pair.Key, to, StringComparison.OrdinalIgnoreCase))
                {
                    cyclicComponents.Add(fromComponent);
                }
            }
        }

        foreach (var definition in implicationDefinitions)
        {
            if (!componentByNode.TryGetValue(definition.From, out var component) ||
                !componentByNode.TryGetValue(definition.To, out var toComponent) ||
                component != toComponent ||
                !cyclicComponents.Contains(component))
            {
                continue;
            }

            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.CyclicAliasDefinition,
                    GetAttributeLocation(definition.Attribute),
                    FormatAlias(definition.From, definition.To)));
        }
    }

    private static Dictionary<string, int> FindStronglyConnectedComponents(
        Dictionary<string, ImmutableHashSet<string>> adjacency)
    {
        var nodes = adjacency.Keys
            .Concat(adjacency.Values.SelectMany(static values => values))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        var indexByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowLinkByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var componentByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        var componentIndex = 0;

        foreach (var node in nodes)
        {
            if (!indexByNode.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        return componentByNode;

        void StrongConnect(string node)
        {
            indexByNode[node] = index;
            lowLinkByNode[node] = index;
            index++;
            stack.Push(node);
            onStack.Add(node);

            if (adjacency.TryGetValue(node, out var nextNodes))
            {
                foreach (var nextNode in nextNodes)
                {
                    if (!indexByNode.ContainsKey(nextNode))
                    {
                        StrongConnect(nextNode);
                        lowLinkByNode[node] = Math.Min(lowLinkByNode[node], lowLinkByNode[nextNode]);
                    }
                    else if (onStack.Contains(nextNode))
                    {
                        lowLinkByNode[node] = Math.Min(lowLinkByNode[node], indexByNode[nextNode]);
                    }
                }
            }

            if (lowLinkByNode[node] != indexByNode[node])
            {
                return;
            }

            while (true)
            {
                var member = stack.Pop();
                onStack.Remove(member);
                componentByNode[member] = componentIndex;
                if (string.Equals(member, node, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            componentIndex++;
        }
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

    private static Location GetAttributeLocation(AttributeData attribute) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
        Location.None;

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

    private static string FormatAlias(string from, string to) => from + " -> " + to;

    private readonly struct ImplicationDefinition
    {
        public ImplicationDefinition(AttributeData attribute, string from, string to)
        {
            Attribute = attribute;
            From = from;
            To = to;
        }

        public AttributeData Attribute { get; }

        public string From { get; }

        public string To { get; }
    }
}
