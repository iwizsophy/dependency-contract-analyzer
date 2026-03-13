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
    private static readonly ImmutableArray<MetadataReference> DefaultMetadataReferences = CreateDefaultMetadataReferences();

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

        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(ProvidesContractAttribute).Assembly.Location));

        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        await test.RunAsync();
    }

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithOptionsAsync(
        string source,
        params (string Key, string Value)[] options)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: "/0/Test0.cs");
        var compilation = CSharpCompilation.Create(
            assemblyName: "DependencyContractAnalyzer.Tests.EditorConfig",
            syntaxTrees: new[] { syntaxTree },
            references: DefaultMetadataReferences,
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

    private static ImmutableArray<MetadataReference> CreateDefaultMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ??
            Array.Empty<string>();
        var references = trustedPlatformAssemblies
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        return references.Add(MetadataReference.CreateFromFile(typeof(ProvidesContractAttribute).Assembly.Location));
    }
}
