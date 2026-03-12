using System.Threading.Tasks;
using DependencyContractAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace DependencyContractAnalyzer.Tests;

internal static class DependencyContractAnalyzerVerifier
{
    public static DiagnosticResult Diagnostic(string diagnosticId) =>
        new(diagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);

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

    private sealed class Test : CSharpAnalyzerTest<DependencyContractAnalyzerDiagnosticAnalyzer, XUnitVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Default;
        }
    }
}
