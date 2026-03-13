using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DependencyContractAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace DependencyContractAnalyzer.Tests;

internal static class DependencyContractAnalyzerVerifier
{
    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(ProvidesContractAttribute).Assembly.Location);

    private static readonly ImmutableArray<MetadataReference> PlatformMetadataReferences = CreatePlatformMetadataReferences();

    private static readonly ImmutableArray<MetadataReference> DefaultMetadataReferences =
        PlatformMetadataReferences.Add(AnalyzerAssemblyReference);

    public static DiagnosticResult Diagnostic(
        string diagnosticId,
        Microsoft.CodeAnalysis.DiagnosticSeverity severity = Microsoft.CodeAnalysis.DiagnosticSeverity.Warning) =>
        new(diagnosticId, severity);

    public static async Task VerifyAnalyzerAsync(
        string source,
        params DiagnosticResult[] expectedDiagnostics)
    {
        var test = new Test
        {
            TestCode = source,
        };

        test.TestState.AdditionalReferences.Add(AnalyzerAssemblyReference);

        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        await test.RunAsync();
    }

    public static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithOptionsAsync(
        string source,
        params (string Key, string Value)[] options)
    {
        return GetAnalyzerDiagnosticsAsync(
            source,
            options,
            ImmutableArray<MetadataReference>.Empty);
    }

    public static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithAdditionalReferenceSourcesAsync(
        string source,
        string[] additionalReferenceSources,
        params (string Key, string Value)[] options)
    {
        var additionalReferences = additionalReferenceSources
            .Select(CreateMetadataReferenceFromSource)
            .ToImmutableArray();

        return GetAnalyzerDiagnosticsAsync(source, options, additionalReferences);
    }

    public static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
        string source,
        string[] additionalReferenceSources,
        params (string Key, string Value)[] options)
    {
        var additionalReferences = additionalReferenceSources
            .Select(CreateMetadataReferenceFromSource)
            .ToImmutableArray();

        return GetAnalyzerDiagnosticsWithoutAnalyzerReferenceAsync(source, options, additionalReferences);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        IEnumerable<(string Key, string Value)> options,
        ImmutableArray<MetadataReference> additionalReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: "/0/Test0.cs");
        var compilation = CSharpCompilation.Create(
            assemblyName: "DependencyContractAnalyzer.Tests.EditorConfig",
            syntaxTrees: new[] { syntaxTree },
            references: DefaultMetadataReferences.AddRange(additionalReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToImmutableArray();
        if (!compilationErrors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, compilationErrors));
        }

        var analyzer = new DependencyContractAnalyzerDiagnosticAnalyzer();
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(options));
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new CompilationWithAnalyzersOptions(
                analyzerOptions,
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));

        return (await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync())
            .OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithoutAnalyzerReferenceAsync(
        string source,
        IEnumerable<(string Key, string Value)> options,
        ImmutableArray<MetadataReference> additionalReferences)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(
                SourceDefinedAttributeSource,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: "/0/DependencyContractAnalyzer.Attributes.cs"),
            CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: "/0/Test0.cs"),
        };
        var compilation = CSharpCompilation.Create(
            assemblyName: "DependencyContractAnalyzer.Tests.SourceDefinedAttributes",
            syntaxTrees: syntaxTrees,
            references: PlatformMetadataReferences.AddRange(additionalReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToImmutableArray();
        if (!compilationErrors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, compilationErrors));
        }

        var analyzer = new DependencyContractAnalyzerDiagnosticAnalyzer();
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(options));
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new CompilationWithAnalyzersOptions(
                analyzerOptions,
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false));

        return (await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync())
            .OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    private sealed class Test : CSharpAnalyzerTest<DependencyContractAnalyzerDiagnosticAnalyzer, XUnitVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Default;
        }
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options;

        public TestAnalyzerConfigOptionsProvider(IEnumerable<(string Key, string Value)> options)
        {
            _options = new TestAnalyzerConfigOptions(options);
        }

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(IEnumerable<(string Key, string Value)> options)
        {
            _options = options.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        public override bool TryGetValue(string key, out string value) =>
            _options.TryGetValue(key, out value!);
    }

    private static MetadataReference CreateMetadataReferenceFromSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: "/0/External.cs");
        var compilation = CSharpCompilation.Create(
            assemblyName: "DependencyContractAnalyzer.Tests.External." + Guid.NewGuid().ToString("N"),
            syntaxTrees: new[] { syntaxTree },
            references: PlatformMetadataReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToImmutableArray();
        if (!errors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            var emitErrors = emitResult.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString());
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitErrors));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static ImmutableArray<MetadataReference> CreatePlatformMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ??
            Array.Empty<string>();
        return trustedPlatformAssemblies
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private const string SourceDefinedAttributeSource = """
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
            public sealed class ContractAliasAttribute : Attribute
            {
                public ContractAliasAttribute(string from, string to)
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
}
