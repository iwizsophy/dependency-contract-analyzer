using System.Threading.Tasks;
using DependencyContractAnalyzer.Diagnostics;
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
                .WithArguments("thread-safe"));
    }

    [Fact]
    public async Task MatchesContractsIgnoringCaseAndWhitespace()
    {
        const string source = """
            using DependencyContractAnalyzer;

            [ProvidesContract(" THREAD-SAFE ")]
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
}
