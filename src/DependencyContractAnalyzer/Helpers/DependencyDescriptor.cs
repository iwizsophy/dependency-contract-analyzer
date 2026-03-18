using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

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
    MethodParameter,
    Property,
    Field,
    ObjectCreation,
    StaticMemberAccess,
    BaseType,
    InterfaceImplementation,
}
