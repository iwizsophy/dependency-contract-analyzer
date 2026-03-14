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

    public static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithSourcesAndPathOptionsAsync(
        IReadOnlyList<(string Path, string Source)> sources,
        IReadOnlyList<(string Path, (string Key, string Value)[] Options)> pathOptions,
        params (string Key, string Value)[] globalOptions)
    {
        var syntaxTrees = sources
            .Select(static sourceFile => ParseSource(sourceFile.Source, sourceFile.Path))
            .ToArray();
        var compilation = CreateCompilation(
            "DependencyContractAnalyzer.Tests.MultiFile.EditorConfig",
            syntaxTrees,
            DefaultMetadataReferences);
        return RunAnalyzerAsync(compilation, globalOptions, pathOptions);
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
        // Some external-reference tests need the current compilation to own its local
        // attribute declarations so referenced metadata stays isolated from the analyzer assembly.
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
        var syntaxTree = ParseSource(source, "/0/Test0.cs");
        var compilation = CreateCompilation(
            "DependencyContractAnalyzer.Tests.EditorConfig",
            new[] { syntaxTree },
            DefaultMetadataReferences.AddRange(additionalReferences));
        return await RunAnalyzerAsync(compilation, options);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithoutAnalyzerReferenceAsync(
        string source,
        IEnumerable<(string Key, string Value)> options,
        ImmutableArray<MetadataReference> additionalReferences)
    {
        var syntaxTrees = new[]
        {
            ParseSource(TestAttributeSources.SourceDefinedPublicAttributes, "/0/DependencyContractAnalyzer.Attributes.cs"),
            ParseSource(source, "/0/Test0.cs"),
        };
        var compilation = CreateCompilation(
            "DependencyContractAnalyzer.Tests.SourceDefinedAttributes",
            syntaxTrees,
            PlatformMetadataReferences.AddRange(additionalReferences));
        return await RunAnalyzerAsync(compilation, options);
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
        private readonly AnalyzerConfigOptions _globalOptions;
        private readonly ImmutableDictionary<string, AnalyzerConfigOptions> _pathOptions;

        public TestAnalyzerConfigOptionsProvider(
            IEnumerable<(string Key, string Value)> globalOptions,
            IEnumerable<(string Path, (string Key, string Value)[] Options)> pathOptions)
        {
            _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
            _pathOptions = pathOptions.ToImmutableDictionary(
                static pathOption => pathOption.Path,
                pathOption => (AnalyzerConfigOptions)new TestAnalyzerConfigOptions(globalOptions.Concat(pathOption.Options)),
                StringComparer.OrdinalIgnoreCase);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            tree.FilePath is not null &&
            _pathOptions.TryGetValue(tree.FilePath, out var options)
                ? options
                : _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(IEnumerable<(string Key, string Value)> options)
        {
            _options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in options)
            {
                _options[key] = value;
            }
        }

        public override bool TryGetValue(string key, out string value) =>
            _options.TryGetValue(key, out value!);
    }

    private static MetadataReference CreateMetadataReferenceFromSource(string source)
    {
        // Build throwaway referenced assemblies in-memory so tests can model metadata-only
        // dependencies without checked-in fixture projects.
        var syntaxTree = ParseSource(source, "/0/External.cs");
        var compilation = CreateCompilation(
            "DependencyContractAnalyzer.Tests.External." + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            PlatformMetadataReferences);

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

    private static SyntaxTree ParseSource(string source, string path) =>
        CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path: path);

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        IEnumerable<SyntaxTree> syntaxTrees,
        ImmutableArray<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ThrowIfCompilationHasErrors(compilation);
        return compilation;
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        CSharpCompilation compilation,
        IEnumerable<(string Key, string Value)> options)
    {
        return await RunAnalyzerAsync(
            compilation,
            options,
            Array.Empty<(string Path, (string Key, string Value)[] Options)>());
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        CSharpCompilation compilation,
        IEnumerable<(string Key, string Value)> globalOptions,
        IEnumerable<(string Path, (string Key, string Value)[] Options)> pathOptions)
    {
        var analyzer = new DependencyContractAnalyzerDiagnosticAnalyzer();
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(globalOptions, pathOptions));
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
            .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.End)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void ThrowIfCompilationHasErrors(Compilation compilation)
    {
        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToImmutableArray();
        if (!compilationErrors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, compilationErrors));
        }
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
}
