namespace DependencyContractAnalyzer.Tests;

internal static class TestAttributeSources
{
    public static string CreateExternalAssemblySource(string body) =>
        body + "\r\n" + ReferencedMetadataAttributes;

    // Local source-defined tests need the public consumer-facing surface that the analyzer
    // expects to resolve inside the current compilation without referencing the package assembly.
    public const string SourceDefinedPublicAttributes = """
        using System;

        namespace DependencyContractAnalyzer
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
            public sealed class ProvidesContractAttribute : Attribute
            {
                public ProvidesContractAttribute(string name)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class RequiresDependencyContractAttribute : Attribute
            {
                public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
            public sealed class ContractTargetAttribute : Attribute
            {
                public ContractTargetAttribute(string name)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class RequiresContractOnTargetAttribute : Attribute
            {
                public RequiresContractOnTargetAttribute(string targetName, string contractName)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            public sealed class ContractScopeAttribute : Attribute
            {
                public ContractScopeAttribute(string name)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class RequiresContractOnScopeAttribute : Attribute
            {
                public RequiresContractOnScopeAttribute(string scopeName, string contractName)
                {
                }
            }

            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            public sealed class ContractHierarchyAttribute : Attribute
            {
                public ContractHierarchyAttribute(string child, string parent)
                {
                }
            }
        }
        """;

    // Referenced assemblies only need the metadata-bearing attribute types that the analyzer
    // reads from symbols; requirement/exclusion attributes are irrelevant on the external side.
    public const string ReferencedMetadataAttributes = """
        namespace DependencyContractAnalyzer
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
            internal sealed class ProvidesContractAttribute : System.Attribute
            {
                public ProvidesContractAttribute(string name)
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
            internal sealed class ContractTargetAttribute : System.Attribute
            {
                public ContractTargetAttribute(string name)
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface | System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            internal sealed class ContractScopeAttribute : System.Attribute
            {
                public ContractScopeAttribute(string name)
                {
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            internal sealed class ContractHierarchyAttribute : System.Attribute
            {
                public ContractHierarchyAttribute(string child, string parent)
                {
                }
            }
        }
        """;
}
