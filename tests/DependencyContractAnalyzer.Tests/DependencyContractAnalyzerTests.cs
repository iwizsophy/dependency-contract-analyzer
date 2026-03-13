using System.Threading.Tasks;
using DependencyContractAnalyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DependencyContractAnalyzer.Tests;

public sealed class DependencyContractAnalyzerTests
{
    [Fact]
    public async Task ReportsNoDiagnosticWhenDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            public sealed class Foo : IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenDependencyDoesNotProvideRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            public sealed class Foo : IFoo
            {
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("IFoo", "thread-safe"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenDeclaredDependencyTypeIsNotUsed()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            public interface IBar
            {
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(IBar bar)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(0)
                .WithArguments("IFoo"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenContractNameIsEmpty()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [{|#0:ProvidesContract("   ")|}]
            public sealed class Foo : IFoo
            {
            }

            [{|#1:RequiresDependencyContract(typeof(IFoo), "")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(1));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenContractNameViolatesLowerKebabCase()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractAlias("immutable", "ThreadSafe")|}]
            [assembly: {|#1:ContractAlias("thread_safe", "thread-safe")|}]

            [{|#2:ProvidesContract("ThreadSafe")|}]
            public interface IFoo
            {
            }

            [ContractTarget("ThreadSafe")]
            [ContractScope("ThreadSafe")]
            [{|#3:RequiresDependencyContract(typeof(IFoo), "thread_safe")|}]
            [{|#4:RequiresContractOnTarget("ThreadSafe", "Thread.Safe")|}]
            [{|#5:RequiresContractOnScope("ThreadSafe", "THREAD-SAFE")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(0)
                .WithArguments("ThreadSafe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("thread_safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(2)
                .WithArguments("ThreadSafe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(3)
                .WithArguments("thread_safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(4)
                .WithArguments("Thread.Safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(5)
                .WithArguments("THREAD-SAFE"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(3)
                .WithArguments("IFoo", "thread_safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredTarget, DiagnosticSeverity.Info)
                .WithLocation(4)
                .WithArguments("ThreadSafe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredScope, DiagnosticSeverity.Info)
                .WithLocation(5)
                .WithArguments("ThreadSafe"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenContractIsDeclaredMultipleTimes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:ProvidesContract("thread-safe")|}]
            [{|#1:ProvidesContract(" THREAD-SAFE ")|}]
            public sealed class Foo
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(1)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task MatchesContractsIgnoringCaseAndWhitespace()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:ProvidesContract(" THREAD-SAFE ")|}]
            public interface IFoo
            {
            }

            public sealed class Foo : IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(0)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenDependencyRequirementIsDeclaredMultipleTimes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            [{|#1:RequiresDependencyContract(typeof(IFoo), " THREAD-SAFE ")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(1)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task UsesContractsDeclaredOnImplementedInterfaces()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            public sealed class Foo : IFoo
            {
            }

            [RequiresDependencyContract(typeof(Foo), "thread-safe")]
            public sealed class Consumer
            {
                private readonly Foo _foo;

                public Consumer(Foo foo)
                {
                    _foo = foo;
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task IgnoresExternalDependenciesForMissingContractChecks()
    {
        const string source = """
            using System;
            using DependencyContractAnalyzer;

            [RequiresDependencyContract(typeof(IDisposable), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IDisposable dependency)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticForExternalDependencyWhenMetadataPolicyIsEnabled()
    {
        const string source = """
            using System;
            using DependencyContractAnalyzer;

            [RequiresDependencyContract(typeof(IDisposable), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IDisposable dependency)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.MissingRequiredContract, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("IDisposable", diagnostic.GetMessage());
        Assert.Contains("thread-safe", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReadsProvidedContractsFromExternalDependencyMetadata()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [RequiresDependencyContract(typeof(IExternalWorker), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IExternalWorker worker)
                {
                }
            }
            """;
        const string externalBody = """
            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("thread-safe")]
                public interface IExternalWorker
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) },
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReadsExternalAliasImplicationMetadataWhenMetadataPolicyIsEnabled()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [RequiresDependencyContract(typeof(IExternalWorker), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IExternalWorker worker)
                {
                }
            }
            """;
        const string externalBody = """
            [assembly: DependencyContractAnalyzer.ContractAlias("immutable", "thread-safe")]

            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("immutable")]
                public interface IExternalWorker
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) },
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReadsExternalHierarchyImplicationMetadataWhenMetadataPolicyIsEnabled()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [RequiresDependencyContract(typeof(IExternalWorker), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IExternalWorker worker)
                {
                }
            }
            """;
        const string externalBody = """
            [assembly: DependencyContractAnalyzer.ContractHierarchy("snapshot-cache", "thread-safe")]

            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("snapshot-cache")]
                public interface IExternalWorker
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) },
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task CombinesLocalAndExternalImplicationGraphsForExternalDependencies()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [assembly: ContractAlias("immutable", "thread-safe")]

            [RequiresDependencyContract(typeof(IExternalWorker), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IExternalWorker worker)
                {
                }
            }
            """;
        const string externalBody = """
            [assembly: DependencyContractAnalyzer.ContractHierarchy("snapshot-cache", "immutable")]

            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("snapshot-cache")]
                public interface IExternalWorker
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) },
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task DoesNotReportImplicationDiagnosticsFromReferencedAssemblies()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [RequiresDependencyContract(typeof(IExternalWorker), "b")]
            public sealed class Consumer
            {
                public Consumer(IExternalWorker worker)
                {
                }
            }
            """;
        const string externalBody = """
            [assembly: DependencyContractAnalyzer.ContractAlias("a", "b")]
            [assembly: DependencyContractAnalyzer.ContractHierarchy("b", "a")]

            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("a")]
                public interface IExternalWorker
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) },
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReadsTargetMetadataFromExternalDependencyWhenMetadataPolicyIsEnabled()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [ContractTarget("repository")]
            public sealed class TargetMarker
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;
        const string externalBody = """
            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ContractTarget("repository")]
                [DependencyContractAnalyzer.ProvidesContract("thread-safe")]
                public sealed class UserRepository
                {
                }
            }
            """;
        var additionalReferenceSources = new[] { CreateExternalAssemblySource(externalBody) };

        var ignoreDiagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            additionalReferenceSources);
        var metadataDiagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            additionalReferenceSources,
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        var ignoreDiagnostic = Assert.Single(ignoreDiagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredTarget, ignoreDiagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, ignoreDiagnostic.Severity);
        Assert.Contains("repository", ignoreDiagnostic.GetMessage());
        Assert.Empty(metadataDiagnostics);
    }

    [Fact]
    public async Task ReadsScopeMetadataFromExternalDependencyWhenMetadataPolicyIsEnabled()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [assembly: ContractScope("repository")]

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;
        const string externalBody = """
            [assembly: DependencyContractAnalyzer.ContractScope("repository")]

            namespace ExternalContracts
            {
                [DependencyContractAnalyzer.ProvidesContract("thread-safe")]
                public sealed class UserRepository
                {
                }
            }
            """;
        var additionalReferenceSources = new[] { CreateExternalAssemblySource(externalBody) };

        var ignoreDiagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            additionalReferenceSources);
        var metadataDiagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            additionalReferenceSources,
            ("dependency_contract_analyzer.external_dependency_policy", "metadata"));

        var ignoreDiagnostic = Assert.Single(ignoreDiagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredScope, ignoreDiagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, ignoreDiagnostic.Severity);
        Assert.Contains("repository", ignoreDiagnostic.GetMessage());
        Assert.Empty(metadataDiagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenMethodParameterProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public void Execute(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodParameterAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public void Execute(IFoo foo)
                {
                }
            }
            """;
        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_method_parameters", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("IFoo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenTargetedMethodParameterProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public void Execute(IRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenScopedMethodParameterProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public void Execute(IRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenPropertyDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public IFoo Foo { get; set; }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenPropertyAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public IFoo Foo { get; set; }
            }
            """;
        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_properties", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("IFoo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenTargetedPropertyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public IRepository Repository { get; set; }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenScopedPropertyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public IRepository Repository { get; set; }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenFieldDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                private readonly IFoo _foo;
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenFieldAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                private readonly IFoo _foo;
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_fields", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("IFoo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenBaseTypeDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public class FooBase
            {
            }

            [RequiresDependencyContract(typeof(FooBase), "thread-safe")]
            public sealed class Consumer : FooBase
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenBaseTypeAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public class FooBase
            {
            }

            [RequiresDependencyContract(typeof(FooBase), "thread-safe")]
            public sealed class Consumer : FooBase
            {
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_base_types", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("FooBase", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenImplementedInterfaceDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer : IFoo
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenImplementedInterfaceAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer : IFoo
            {
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_interface_implementations", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("IFoo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenCreatedTypeProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class Foo
            {
            }

            [RequiresDependencyContract(typeof(Foo), "thread-safe")]
            public sealed class Consumer
            {
                public void Execute()
                {
                    Foo foo = new();
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenObjectCreationAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class Foo
            {
            }

            [RequiresDependencyContract(typeof(Foo), "thread-safe")]
            public sealed class Consumer
            {
                public void Execute()
                {
                    Foo foo = new();
                }
            }
            """;
        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_object_creation", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Foo", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenTargetedCreatedTypeProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public void Execute()
                {
                    var repository = new UserRepository();
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenScopedCreatedTypeProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public void Execute()
                {
                    var repository = new UserRepository();
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenStaticUsageProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using static Clock;

            [ProvidesContract("thread-safe")]
            public static class Clock
            {
                public static int UtcHour => 12;
            }

            [RequiresDependencyContract(typeof(Clock), "thread-safe")]
            public sealed class Consumer
            {
                public int Read() => UtcHour;
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenStaticUsageAnalysisIsDisabledByEditorConfig()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using static Clock;

            [ProvidesContract("thread-safe")]
            public static class Clock
            {
                public static int UtcHour => 12;
            }

            [RequiresDependencyContract(typeof(Clock), "thread-safe")]
            public sealed class Consumer
            {
                public int Read() => UtcHour;
            }
            """;
        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.analyze_static_members", "false"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredDependencyType, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Clock", diagnostic.GetMessage());
    }

    [Fact]
    public async Task SkipsAnalyzerWhenOwnerNamespaceIsExcludedByEditorConfig()
    {
        const string source = """
            namespace MyCompany.Legacy;

            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.excluded_namespaces", "MyCompany.Legacy"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SkipsAnalyzerWhenOwnerSubnamespaceIsExcludedByEditorConfig()
    {
        const string source = """
            namespace MyCompany.Legacy.Migrations;

            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.excluded_namespaces", "MyCompany.Legacy"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SkipsAnalyzerWhenOwnerTypeIsExcludedByEditorConfig()
    {
        const string source = """
            namespace MyCompany.Application;

            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.excluded_types", "MyCompany.Application.Consumer"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task DoesNotSkipAnalyzerForSiblingNamespaceExclusion()
    {
        const string source = """
            namespace MyCompany.Legacyish;

            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.excluded_namespaces", "MyCompany.Legacy"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.MissingRequiredContract, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task SkipsAnalyzerWhenOwnerTypeHasCustomExclusionAttribute()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [ExcludeDependencyContractAnalysis]
            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SkipsAnalyzerWhenAssemblyHasCustomExclusionAttribute()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ExcludeDependencyContractAnalysis]

            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotSkipAnalyzerWhenDependencyTypeHasCustomExclusionAttribute()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ExcludeDependencyContractAnalysis]
            public interface IFoo
            {
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("IFoo", "thread-safe"));
    }

    [Fact]
    public async Task SuppressesMatchingDependencyRequirementDiagnostics()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            public interface IBar
            {
            }

            [SuppressRequiredDependencyContract(typeof(IFoo), "thread-safe")]
            [SuppressRequiredDependencyContract(typeof(IBar), "retry-safe")]
            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            [RequiresDependencyContract(typeof(IBar), "retry-safe")]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SuppressesMatchingTargetRequirementDiagnostics()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            public interface IRepository
            {
            }

            [ContractTarget("service")]
            public interface IService
            {
            }

            [SuppressRequiredTargetContract("repository", "thread-safe")]
            [SuppressRequiredTargetContract("service", "thread-safe")]
            [SuppressRequiredTargetContract("gateway", "thread-safe")]
            [RequiresContractOnTarget("repository", "thread-safe")]
            [RequiresContractOnTarget("service", "thread-safe")]
            [RequiresContractOnTarget("gateway", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SuppressesMatchingScopeRequirementDiagnostics()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("platform")]

            [ContractScope("repository")]
            public interface IRepository
            {
            }

            [ContractScope("service")]
            public interface IService
            {
            }

            [SuppressRequiredScopeContract("repository", "thread-safe")]
            [SuppressRequiredScopeContract("service", "thread-safe")]
            [SuppressRequiredScopeContract("gateway", "thread-safe")]
            [SuppressRequiredScopeContract("platform", "thread-safe")]
            [RequiresContractOnScope("repository", "thread-safe")]
            [RequiresContractOnScope("service", "thread-safe")]
            [RequiresContractOnScope("gateway", "thread-safe")]
            [RequiresContractOnScope("platform", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticsForInvalidSuppressionAttributes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [{|#0:SuppressRequiredDependencyContract(typeof(IFoo), "")|}]
            [{|#1:SuppressRequiredDependencyContract(typeof(IFoo), "THREAD-SAFE")|}]
            [{|#2:SuppressRequiredTargetContract("", "thread-safe")|}]
            [{|#3:SuppressRequiredScopeContract(" ", "thread-safe")|}]
            [{|#4:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("THREAD-SAFE"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyTargetName).WithLocation(2),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyScopeName).WithLocation(3),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(4)
                .WithArguments("IFoo", "thread-safe"));
    }

    [Fact]
    public async Task ReportsDiagnosticForDuplicateSuppressionAttributes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ContractScope("repository")]
            public interface IRepository
            {
            }

            [SuppressRequiredDependencyContract(typeof(IRepository), "thread-safe")]
            [{|#0:SuppressRequiredDependencyContract(typeof(IRepository), " thread-safe ")|}]
            [SuppressRequiredTargetContract("repository", "thread-safe")]
            [{|#1:SuppressRequiredTargetContract(" repository ", "thread-safe")|}]
            [SuppressRequiredScopeContract("repository", "thread-safe")]
            [{|#2:SuppressRequiredScopeContract(" repository ", "thread-safe")|}]
            [RequiresDependencyContract(typeof(IRepository), "thread-safe")]
            [RequiresContractOnTarget("repository", "thread-safe")]
            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(IRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(0)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(1)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(2)
                .WithArguments("thread-safe"));
    }

    [Fact]
    public async Task DoesNotSuppressRequirementWhenSuppressionDoesNotExactlyMatch()
    {
        const string source = """
            using DependencyContractAnalyzer;

            public interface IFoo
            {
            }

            [SuppressRequiredDependencyContract(typeof(IFoo), "retry-safe")]
            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("IFoo", "thread-safe"));
    }

    [Fact]
    public async Task ReportsUnusedDependenciesWhenConstructorSourceIsExcluded()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [ProvidesContract("thread-safe")]
            public sealed class Foo
            {
            }

            [ProvidesContract("thread-safe")]
            public static class Clock
            {
                public static int CurrentHour => 12;
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            [{|#1:RequiresDependencyContract(typeof(Foo), "thread-safe")|}]
            [{|#2:RequiresDependencyContract(typeof(Clock), "thread-safe")|}]
            public sealed class Consumer
            {
                [ExcludeDependencyContractSource]
                public Consumer(IFoo foo)
                {
                    var created = new Foo();
                    _ = Clock.CurrentHour;
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(0)
                .WithArguments("IFoo"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(1)
                .WithArguments("Foo"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(2)
                .WithArguments("Clock"));
    }

    [Fact]
    public async Task ReportsUnusedDependenciesWhenMethodSourceIsExcluded()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [ProvidesContract("thread-safe")]
            public sealed class Foo
            {
            }

            [ProvidesContract("thread-safe")]
            public static class Clock
            {
                public static int CurrentHour() => 12;
            }

            [{|#0:RequiresDependencyContract(typeof(IFoo), "thread-safe")|}]
            [{|#1:RequiresDependencyContract(typeof(Foo), "thread-safe")|}]
            [{|#2:RequiresDependencyContract(typeof(Clock), "thread-safe")|}]
            public sealed class Consumer
            {
                [ExcludeDependencyContractSource]
                public void Execute(IFoo foo)
                {
                    var created = new Foo();
                    _ = Clock.CurrentHour();
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(0)
                .WithArguments("IFoo"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(1)
                .WithArguments("Foo"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(2)
                .WithArguments("Clock"));
    }

    [Fact]
    public async Task KeepsOtherDependencySourcesWhenOnlyOneMethodIsExcluded()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public interface IFoo
            {
            }

            [RequiresDependencyContract(typeof(IFoo), "thread-safe")]
            public sealed class Consumer
            {
                [ExcludeDependencyContractSource]
                public void Ignored(IFoo foo)
                {
                }

                public void Used(IFoo foo)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsUnusedDependencyWhenPropertySourceIsExcluded()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class Foo
            {
            }

            [{|#0:RequiresDependencyContract(typeof(Foo), "thread-safe")|}]
            public sealed class Consumer
            {
                [ExcludeDependencyContractSource]
                public Foo Service => new();
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredDependencyType)
                .WithLocation(0)
                .WithArguments("Foo"));
    }

    [Fact]
    public async Task ReportsUnusedTargetAndScopeWhenFieldSourceIsExcluded()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class Repository
            {
            }

            [{|#0:RequiresContractOnTarget("repository", "thread-safe")|}]
            [{|#1:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                [ExcludeDependencyContractSource]
                private readonly Repository _repository = new();
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredTarget, DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("repository"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredScope, DiagnosticSeverity.Info)
                .WithLocation(1)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenTargetedStaticUsageProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public static class RepositoryClock
            {
                public static int CurrentHour() => 12;
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public int Read() => RepositoryClock.CurrentHour();
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenScopedStaticUsageProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public static class RepositoryClock
            {
                public static int CurrentHour => 12;
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public int Read() => RepositoryClock.CurrentHour;
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenScopedDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenScopedDependencyDoesNotProvideRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            public sealed class UserRepository
            {
            }

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("UserRepository", "thread-safe"));
    }

    [Fact]
    public async Task IgnoresDependenciesOutsideRequiredScope()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [ContractScope("cache")]
            public sealed class UserCache
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository, UserCache cache)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenScopeNameIsEmpty()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnScope("   ", "thread-safe")|}]
            [{|#1:RequiresContractOnScope("repository", "")|}]
            public sealed class Consumer
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyScopeName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(1));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenScopeRequirementIsDeclaredMultipleTimes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            [{|#1:RequiresContractOnScope(" REPOSITORY ", " THREAD-SAFE ")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(1)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task MatchesScopeNamesIgnoringCaseAndWhitespace()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope(" REPOSITORY ")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task InfersScopeFromNamespaceLeafSegment()
    {
        const string source = """
            namespace MyCompany.ApplicationService;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class ServiceClock
            {
            }

            [RequiresContractOnScope("application-service", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(ServiceClock clock)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task InfersScopeFromTrailingTwoNamespaceSegmentsWhenConfigured()
    {
        const string source = """
            namespace MyCompany.ReadModels.Query;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class ServiceClock
            {
            }

            [RequiresContractOnScope("read-models-query", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(ServiceClock clock)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.namespace_inference_max_segments", "2"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task PrefersAssemblyScopeOverNamespaceInference()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("application")]

            namespace MyCompany.Repository;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredScope)
                .WithLocation(0)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task PrefersAssemblyScopeOverConfiguredTrailingNamespaceInference()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("application")]

            namespace MyCompany.ReadModels.Query;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("read-models-query", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.namespace_inference_max_segments", "2"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UndeclaredRequiredScope, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("read-models-query", diagnostic.GetMessage());
    }

    [Fact]
    public async Task UsesScopeDeclaredOnImplementedInterfaces()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            public sealed class UserRepository : IRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                private readonly UserRepository _repository;

                public Consumer(UserRepository repository)
                {
                    _repository = repository;
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UsesAssemblyScopeAsDefaultScope()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("repository")]

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task CombinesTypeAndAssemblyScopesWhenMatchingDependencies()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("repository")]

            [ContractScope("cache")]
            public sealed class UserRepository
            {
            }

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("UserRepository", "thread-safe"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenRequiredScopeIsUndeclaredInCompilation()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnScope("reposotiry", "thread-safe")|}]
            public sealed class Consumer
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredScope)
                .WithLocation(0)
                .WithArguments("reposotiry"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenKnownScopeIsUnusedByDependencies()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractScope("repository")]
            public sealed class UserRepository
            {
            }

            public interface ILogger
            {
            }

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(ILogger logger)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredScope, DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task UsesAssemblyLevelScopeForUndeclaredValidation()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractScope("repository")]

            [{|#0:RequiresContractOnScope("repository", "thread-safe")|}]
            public sealed class Consumer
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredScope, DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task ReportsNoDiagnosticWhenTargetedDependencyProvidesRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenTargetedDependencyDoesNotProvideRequiredContract()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            public sealed class UserRepository
            {
            }

            [{|#0:RequiresContractOnTarget("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.MissingRequiredContract)
                .WithLocation(0)
                .WithArguments("UserRepository", "thread-safe"));
    }

    [Fact]
    public async Task IgnoresDependenciesOutsideRequiredTarget()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [ContractTarget("cache")]
            public sealed class UserCache
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository, UserCache cache)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenTargetNameIsEmpty()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:ContractTarget("   ")|}]
            public sealed class UserRepository
            {
            }

            [{|#1:RequiresContractOnTarget("   ", "thread-safe")|}]
            [{|#2:RequiresContractOnTarget("repository", "")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyTargetName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyTargetName).WithLocation(1),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(2));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenTargetRequirementIsDeclaredMultipleTimes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnTarget("repository", "thread-safe")|}]
            [{|#1:RequiresContractOnTarget(" REPOSITORY ", " THREAD-SAFE ")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(1)
                .WithArguments("thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task MatchesTargetNamesIgnoringCaseAndWhitespace()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget(" REPOSITORY ")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task InfersTargetFromNamespaceLeafSegment()
    {
        const string source = """
            namespace MyCompany.ReadModel;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("read-model", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotInferTrailingTwoNamespaceSegmentsByDefault()
    {
        const string source = """
            namespace MyCompany.ReadModels.Query;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [{|#0:RequiresContractOnTarget("read-models-query", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredTarget)
                .WithLocation(0)
                .WithArguments("read-models-query"));
    }

    [Fact]
    public async Task InfersTargetFromTrailingTwoNamespaceSegmentsWhenConfigured()
    {
        const string source = """
            namespace MyCompany.ReadModels.Query;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("read-models-query", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.namespace_inference_max_segments", "2"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FallsBackToLeafNamespaceInferenceWhenConfiguredValueIsInvalid()
    {
        const string source = """
            namespace MyCompany.ReadModels.Query;

            using DependencyContractAnalyzer;

            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("read-models-query", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.namespace_inference_max_segments", "invalid"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UndeclaredRequiredTarget, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("read-models-query", diagnostic.GetMessage());
    }

    [Fact]
    public async Task PrefersExplicitTargetOverNamespaceInference()
    {
        const string source = """
            using DependencyContractAnalyzer;

            namespace MyCompany.Repository
            {
                [ContractTarget("storage")]
                [ProvidesContract("thread-safe")]
                public sealed class UserRepository
                {
                }
            }

            namespace MyCompany.Application
            {
                [{|#0:RequiresContractOnTarget("repository", "thread-safe")|}]
                public sealed class Consumer
                {
                    public Consumer(MyCompany.Repository.UserRepository repository)
                    {
                    }
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredTarget)
                .WithLocation(0)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task PrefersExplicitTargetOverConfiguredTrailingNamespaceInference()
    {
        const string source = """
            using DependencyContractAnalyzer;

            namespace MyCompany.ReadModels.Query
            {
                [ContractTarget("storage")]
                [ProvidesContract("thread-safe")]
                public sealed class UserRepository
                {
                }
            }

            namespace MyCompany.Application
            {
                [RequiresContractOnTarget("read-models-query", "thread-safe")]
                public sealed class Consumer
                {
                    public Consumer(MyCompany.ReadModels.Query.UserRepository repository)
                    {
                    }
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithOptionsAsync(
            source,
            ("dependency_contract_analyzer.namespace_inference_max_segments", "2"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UndeclaredRequiredTarget, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("read-models-query", diagnostic.GetMessage());
    }

    [Fact]
    public async Task DoesNotInferTargetFromExternalDependencyNamespace()
    {
        const string source = """
            using System.IO;
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnTarget("io", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(StringReader reader)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredTarget)
                .WithLocation(0)
                .WithArguments("io"));
    }

    [Fact]
    public async Task DoesNotInferTargetFromExternalImplementedInterfaceNamespace()
    {
        const string source = """
            using DependencyContractAnalyzer;
            using ExternalContracts;

            [ContractTarget("external-contracts")]
            public sealed class TargetMarker
            {
            }

            public sealed class UserRepository : IRepository
            {
            }

            [RequiresContractOnTarget("external-contracts", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;
        const string externalBody = """
            namespace ExternalContracts
            {
                public interface IRepository
                {
                }
            }
            """;

        var diagnostics = await DependencyContractAnalyzerVerifier.GetAnalyzerDiagnosticsWithSourceDefinedAttributesAndAdditionalReferenceSourcesAsync(
            source,
            new[] { CreateExternalAssemblySource(externalBody) });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.UnusedRequiredTarget, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains("external-contracts", diagnostic.GetMessage());
    }

    [Fact]
    public async Task UsesTargetDeclaredOnImplementedInterfaces()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ProvidesContract("thread-safe")]
            public interface IRepository
            {
            }

            public sealed class UserRepository : IRepository
            {
            }

            [RequiresContractOnTarget("repository", "thread-safe")]
            public sealed class Consumer
            {
                private readonly UserRepository _repository;

                public Consumer(UserRepository repository)
                {
                    _repository = repository;
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MatchesAnyDeclaredTargetOnDependency()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            [ContractTarget("read-model")]
            [ProvidesContract("thread-safe")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("read-model", "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenRequiredTargetIsUndeclaredInCompilation()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [{|#0:RequiresContractOnTarget("reposotiry", "thread-safe")|}]
            public sealed class Consumer
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UndeclaredRequiredTarget)
                .WithLocation(0)
                .WithArguments("reposotiry"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenKnownTargetIsUnusedByDependencies()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ContractTarget("repository")]
            public sealed class UserRepository
            {
            }

            public interface ILogger
            {
            }

            [{|#0:RequiresContractOnTarget("repository", "thread-safe")|}]
            public sealed class Consumer
            {
                public Consumer(ILogger logger)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.UnusedRequiredTarget, DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("repository"));
    }

    [Fact]
    public async Task ResolvesAliasForDependencyRequirement()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractAlias("immutable", "thread-safe")]

            [ProvidesContract("immutable")]
            public interface ICache
            {
            }

            public sealed class ImmutableCache : ICache
            {
            }

            [RequiresDependencyContract(typeof(ICache), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(ICache cache)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ResolvesMultiStepAliasForTargetRequirement()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractAlias("immutable", "thread-safe")]
            [assembly: ContractAlias("thread-safe", "retry-safe")]

            [ContractTarget("repository")]
            [ProvidesContract("immutable")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("repository", "retry-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ResolvesHierarchyForDependencyRequirement()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractHierarchy("immutable", "thread-safe")]

            [ProvidesContract("immutable")]
            public interface ICache
            {
            }

            public sealed class ImmutableCache : ICache
            {
            }

            [RequiresDependencyContract(typeof(ICache), "thread-safe")]
            public sealed class Consumer
            {
                public Consumer(ICache cache)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ResolvesMultiParentHierarchyForTargetRequirement()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractHierarchy("immutable", "thread-safe")]
            [assembly: ContractHierarchy("immutable", "snapshot-safe")]

            [ContractTarget("repository")]
            [ProvidesContract("immutable")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnTarget("repository", "snapshot-safe")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ResolvesMixedAliasAndHierarchyForScopeRequirement()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractAlias("immutable", "thread-safe")]
            [assembly: ContractHierarchy("thread-safe", "resilient")]

            [ContractScope("repository")]
            [ProvidesContract("immutable")]
            public sealed class UserRepository
            {
            }

            [RequiresContractOnScope("repository", "resilient")]
            public sealed class Consumer
            {
                public Consumer(UserRepository repository)
                {
                }
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenAliasNameIsEmpty()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractAlias("   ", "thread-safe")|}]
            [assembly: {|#1:ContractAlias("immutable", "   ")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(1));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenHierarchyNameIsEmpty()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractHierarchy("   ", "thread-safe")|}]
            [assembly: {|#1:ContractHierarchy("immutable", "   ")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(0),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.EmptyContractName).WithLocation(1));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenAliasIsDeclaredMultipleTimes()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractAlias("immutable", "thread-safe")]
            [assembly: {|#0:ContractAlias(" IMMUTABLE ", " THREAD-SAFE ")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(0)
                .WithArguments("immutable -> thread-safe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(0)
                .WithArguments("IMMUTABLE"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(0)
                .WithArguments("THREAD-SAFE"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenHierarchyNameViolatesLowerKebabCase()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractHierarchy("ThreadSafe", "thread-safe")|}]
            [assembly: {|#1:ContractHierarchy("immutable", "SnapshotSafe")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(0)
                .WithArguments("ThreadSafe"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.ContractNamingFormatViolation)
                .WithLocation(1)
                .WithArguments("SnapshotSafe"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenHierarchyDuplicatesAliasDefinition()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: ContractAlias("immutable", "thread-safe")]
            [assembly: {|#0:ContractHierarchy("immutable", "thread-safe")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.DuplicateContractDeclaration)
                .WithLocation(0)
                .WithArguments("immutable -> thread-safe"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenAliasDefinitionIsCyclic()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractAlias("a", "b")|}]
            [assembly: {|#1:ContractAlias("b", "a")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.CyclicAliasDefinition)
                .WithLocation(0)
                .WithArguments("a -> b"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.CyclicAliasDefinition)
                .WithLocation(1)
                .WithArguments("b -> a"));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenAliasAndHierarchyDefinitionAreCyclicTogether()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [assembly: {|#0:ContractAlias("a", "b")|}]
            [assembly: {|#1:ContractHierarchy("b", "a")|}]

            public sealed class Marker
            {
            }
            """;

        await DependencyContractAnalyzerVerifier.VerifyAnalyzerAsync(
            source,
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.CyclicAliasDefinition)
                .WithLocation(0)
                .WithArguments("a -> b"),
            DependencyContractAnalyzerVerifier.Diagnostic(DiagnosticIds.CyclicAliasDefinition)
                .WithLocation(1)
                .WithArguments("b -> a"));
    }

    private static string CreateExternalAssemblySource(string body) =>
        body + "\r\n" + ExternalAttributeSource;

    private const string ExternalAttributeSource = """
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
            internal sealed class ContractAliasAttribute : System.Attribute
            {
                public ContractAliasAttribute(string from, string to)
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
