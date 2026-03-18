using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal interface IRequirement
{
    AttributeData Attribute { get; }

    string ContractName { get; }
}

internal interface INamedRequirement : IRequirement
{
    string Name { get; }
}

internal readonly struct RequirementDescriptor : IRequirement
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

internal readonly struct TargetRequirementDescriptor : INamedRequirement
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

internal readonly struct ScopeRequirementDescriptor : INamedRequirement
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
