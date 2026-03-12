using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal static class DependencyCollector
{
    public static ImmutableArray<DependencyDescriptor> Collect(INamedTypeSymbol type)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyDescriptor>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.IsImplicitlyDeclared)
            {
                continue;
            }

            foreach (var parameter in constructor.Parameters)
            {
                AddDependency(parameter.Type, DependencyKind.ConstructorParameter, dependencies, seen);
            }
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field ||
                field.IsImplicitlyDeclared ||
                field.AssociatedSymbol is not null ||
                field.IsConst)
            {
                continue;
            }

            AddDependency(field.Type, DependencyKind.Field, dependencies, seen);
        }

        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            AddDependency(baseType, DependencyKind.BaseType, dependencies, seen);
        }

        foreach (var interfaceType in type.Interfaces)
        {
            AddDependency(interfaceType, DependencyKind.InterfaceImplementation, dependencies, seen);
        }

        return dependencies.ToImmutable();
    }

    private static void AddDependency(
        ITypeSymbol type,
        DependencyKind dependencyKind,
        ImmutableArray<DependencyDescriptor>.Builder dependencies,
        HashSet<INamedTypeSymbol> seen)
    {
        if (type is not INamedTypeSymbol namedType || !seen.Add(namedType))
        {
            return;
        }

        dependencies.Add(new DependencyDescriptor(namedType, dependencyKind));
    }
}

internal readonly struct DependencyDescriptor
{
    public DependencyDescriptor(INamedTypeSymbol type, DependencyKind kind)
    {
        Type = type;
        Kind = kind;
    }

    public INamedTypeSymbol Type { get; }

    public DependencyKind Kind { get; }
}

internal enum DependencyKind
{
    ConstructorParameter,
    Field,
    BaseType,
    InterfaceImplementation,
}
