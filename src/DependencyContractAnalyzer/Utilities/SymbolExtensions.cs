using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Utilities;

internal static class SymbolExtensions
{
    public static bool SymbolEquals(this ISymbol? left, ISymbol? right) =>
        SymbolEqualityComparer.Default.Equals(left, right);

    public static bool MatchesRequiredDependencyType(
        this INamedTypeSymbol dependencyType,
        INamedTypeSymbol requiredType)
    {
        // Dependency-type requirements match against the discovered type itself plus
        // its base-type and interface closure, mirroring how provided contracts flow.
        foreach (var candidate in dependencyType.EnumerateTypeClosure())
        {
            if (MatchesSymbol(candidate, requiredType))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExternalTo(this INamedTypeSymbol type, IAssemblySymbol compilationAssembly) =>
        !type.ContainingAssembly.SymbolEquals(compilationAssembly);

    private static IEnumerable<INamedTypeSymbol> EnumerateTypeClosure(this INamedTypeSymbol type)
    {
        yield return type;

        foreach (var baseType in type.GetBaseTypes())
        {
            yield return baseType;
        }

        foreach (var interfaceType in type.AllInterfaces)
        {
            yield return interfaceType;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetBaseTypes(this INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    private static bool MatchesSymbol(INamedTypeSymbol left, INamedTypeSymbol right) =>
        // Compare both constructed and original definitions so generic dependencies
        // can satisfy requirements expressed on either open or closed type forms.
        left.SymbolEquals(right) ||
        left.OriginalDefinition.SymbolEquals(right) ||
        left.SymbolEquals(right.OriginalDefinition) ||
        left.OriginalDefinition.SymbolEquals(right.OriginalDefinition);
}
