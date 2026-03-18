using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependencyContractAnalyzer.Helpers;

internal static class DependencyCollector
{
    public static ImmutableArray<DependencyDescriptor> Collect(
        INamedTypeSymbol type,
        Compilation compilation,
        DependencyCollectionOptions options,
        INamedTypeSymbol? excludeDependencyContractSourceAttributeSymbol,
        CancellationToken cancellationToken)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyDescriptor>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Constructor parameters remain the baseline dependency source even when
        // optional source families are disabled by configuration or presets.
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.IsImplicitlyDeclared ||
                HasExcludedDependencySource(constructor, excludeDependencyContractSourceAttributeSymbol))
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
            if (!SupportsSyntaxDependencyCollection(member))
            {
                continue;
            }

            if (HasExcludedDependencySource(member, excludeDependencyContractSourceAttributeSymbol))
            {
                continue;
            }

            if (options.AnalyzeStaticMembers)
            {
                CollectStaticMemberDependencies(
                    type,
                    member,
                    compilation,
                    cancellationToken,
                    dependencies,
                    seen);
            }

            if (options.AnalyzeObjectCreation)
            {
                CollectObjectCreationDependencies(
                    member,
                    compilation,
                    cancellationToken,
                    dependencies,
                    seen);
            }

            if (member is IMethodSymbol method)
            {
                if (options.AnalyzeMethodParameters && IsMethodDependencyCandidate(method))
                {
                    foreach (var parameter in method.Parameters)
                    {
                        AddDependency(parameter.Type, DependencyKind.MethodParameter, dependencies, seen);
                    }
                }

                continue;
            }

            if (member is IPropertySymbol property)
            {
                if (options.AnalyzeProperties && !property.IsImplicitlyDeclared)
                {
                    AddDependency(property.Type, DependencyKind.Property, dependencies, seen);
                }

                continue;
            }

            if (member is not IFieldSymbol field ||
                field.IsImplicitlyDeclared ||
                field.AssociatedSymbol is not null ||
                field.IsConst)
            {
                continue;
            }

            if (options.AnalyzeFields)
            {
                AddDependency(field.Type, DependencyKind.Field, dependencies, seen);
            }
        }

        if (options.AnalyzeBaseTypes &&
            type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            AddDependency(baseType, DependencyKind.BaseType, dependencies, seen);
        }

        if (!options.AnalyzeInterfaceImplementations)
        {
            return dependencies.ToImmutable();
        }

        foreach (var interfaceType in type.Interfaces)
        {
            AddDependency(interfaceType, DependencyKind.InterfaceImplementation, dependencies, seen);
        }

        return dependencies.ToImmutable();
    }

    private static bool IsMethodDependencyCandidate(IMethodSymbol method) =>
        !method.IsImplicitlyDeclared &&
        method.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation;

    private static bool SupportsSyntaxDependencyCollection(ISymbol member) =>
        member is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol;

    private static bool HasExcludedDependencySource(
        ISymbol member,
        INamedTypeSymbol? excludeDependencyContractSourceAttributeSymbol)
    {
        if (excludeDependencyContractSourceAttributeSymbol is null)
        {
            return false;
        }

        if (HasAttribute(member, excludeDependencyContractSourceAttributeSymbol))
        {
            return true;
        }

        return member switch
        {
            // Property and event accessors surface as methods/fields in Roslyn, so mirror
            // exclusion declared on the associated property or event member as well.
            IMethodSymbol { AssociatedSymbol: { } associatedSymbol } => HasAttribute(associatedSymbol, excludeDependencyContractSourceAttributeSymbol),
            IFieldSymbol { AssociatedSymbol: { } associatedSymbol } => HasAttribute(associatedSymbol, excludeDependencyContractSourceAttributeSymbol),
            _ => false,
        };
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol) =>
        symbol.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));

    private static void CollectStaticMemberDependencies(
        INamedTypeSymbol ownerType,
        ISymbol member,
        Compilation compilation,
        CancellationToken cancellationToken,
        ImmutableArray<DependencyDescriptor>.Builder dependencies,
        HashSet<INamedTypeSymbol> seen)
    {
        foreach (var syntaxReference in member.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

            foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AddStaticMemberDependency(
                    ownerType,
                    semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol,
                    dependencies,
                    seen);
            }

            foreach (var memberAccess in syntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                AddStaticMemberDependency(
                    ownerType,
                    semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol,
                    dependencies,
                    seen);
            }

            foreach (var identifier in syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                AddStaticMemberDependency(
                    ownerType,
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    dependencies,
                    seen);
            }
        }
    }

    private static void CollectObjectCreationDependencies(
        ISymbol member,
        Compilation compilation,
        CancellationToken cancellationToken,
        ImmutableArray<DependencyDescriptor>.Builder dependencies,
        HashSet<INamedTypeSymbol> seen)
    {
        foreach (var syntaxReference in member.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

            foreach (var objectCreation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var createdType = semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type;
                AddDependency(createdType, DependencyKind.ObjectCreation, dependencies, seen);
            }

            foreach (var objectCreation in syntax.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                var createdType = semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type;
                AddDependency(createdType, DependencyKind.ObjectCreation, dependencies, seen);
            }
        }
    }

    private static void AddStaticMemberDependency(
        INamedTypeSymbol ownerType,
        ISymbol? symbol,
        ImmutableArray<DependencyDescriptor>.Builder dependencies,
        HashSet<INamedTypeSymbol> seen)
    {
        INamedTypeSymbol? containingType = symbol switch
        {
            IMethodSymbol { IsStatic: true, ReducedFrom: null, MethodKind: not MethodKind.Constructor and not MethodKind.StaticConstructor } method
                when !method.IsExtensionMethod && !method.IsImplicitlyDeclared => method.ContainingType,
            IPropertySymbol { IsStatic: true } property when !property.IsImplicitlyDeclared => property.ContainingType,
            IFieldSymbol { IsStatic: true, IsConst: false } field
                when !field.IsImplicitlyDeclared && field.ContainingType.TypeKind != TypeKind.Enum => field.ContainingType,
            IEventSymbol { IsStatic: true } eventSymbol when !eventSymbol.IsImplicitlyDeclared => eventSymbol.ContainingType,
            _ => null,
        };

        if (containingType is null || SymbolEqualityComparer.Default.Equals(containingType, ownerType))
        {
            return;
        }

        AddDependency(containingType, DependencyKind.StaticMemberAccess, dependencies, seen);
    }

    private static void AddDependency(
        ITypeSymbol? type,
        DependencyKind dependencyKind,
        ImmutableArray<DependencyDescriptor>.Builder dependencies,
        HashSet<INamedTypeSymbol> seen)
    {
        // Different discovery paths can hit the same dependency type; keep the first
        // observed kind only so later analysis reasons about unique dependency types.
        if (type is not INamedTypeSymbol namedType || !seen.Add(namedType))
        {
            return;
        }

        dependencies.Add(new DependencyDescriptor(namedType, dependencyKind));
    }
}
