# Development Guide

This document is for maintainers and contributors.

## Prerequisites

- .NET 10 SDK installed
- .NET 8, .NET 9, and .NET 10 runtimes installed for the full test suite
- Familiarity with Roslyn analyzer development
- A local environment that can run unit tests for `Microsoft.CodeAnalysis.Testing`

## Typical local validation

Run from the repository root:

```powershell
dotnet restore DependencyContractAnalyzer.slnx
dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore -m:1
dotnet test DependencyContractAnalyzer.slnx -c Release --no-build -m:1
```

This test command executes the unit suite for `net8.0`, `net9.0`, and `net10.0`.

To collect local coverage in Cobertura format:

```powershell
dotnet test tests/DependencyContractAnalyzer.Tests/DependencyContractAnalyzer.Tests.csproj -c Release --collect "XPlat Code Coverage" -m:1
```

The coverage file is written under `tests/DependencyContractAnalyzer.Tests/TestResults/**/coverage.cobertura.xml`.

Local package output:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-build -o artifacts
```

Packed-package smoke validation expects a clean package directory with a
single `.nupkg` and currently verifies package consumption on `net8.0`,
`net9.0`, and `net10.0`:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release -o artifacts/package-smoke-current
pwsh -NoProfile -File ./scripts/Test-PackedPackageConsumption.ps1 -PackageDirectory artifacts/package-smoke-current
```

## Analyzer test host policy

`tests/DependencyContractAnalyzer.Tests` targets `net8.0`, `net9.0`,
and `net10.0`.

The verifier pins explicit `Microsoft.NETCore.App.Ref` reference
assemblies that match the active test host target framework. The test
harness does not rely on implicit `Microsoft.CodeAnalysis.Testing`
defaults or on runtime assembly discovery for platform references.

Packed-package compatibility validation separately pins SDK host lines
through per-project `global.json` files so package consumption is
checked under `.NET 8`, `.NET 9`, and `.NET 10` compiler hosts instead
of only changing the target framework.

This is an internal validation policy for the current host lines used in
development and CI. The packaged `netstandard2.0` analyzer's current
guaranteed public support scope is `.NET 8`, `.NET 9`, and `.NET 10`.
The repository does not publish a full version-by-version host support
matrix beyond that statement.

Technically, the current implementation is expected to work on `.NET 5+`
build environments and Visual Studio `2019 16.8+` when Roslyn analyzer
loading is available and the host compiler remains compatible with the
packaged analyzer. However, those environments are outside the
guaranteed support scope, are not covered by the repository's automated
validation policy, and are not part of the project's support commitment.

## Project layout

The repository currently follows this structure:

- `src/DependencyContractAnalyzer`: analyzer, diagnostics, attributes, and helper logic
- `samples/DependencyContractAnalyzer.Sample`: runnable consumer example that should build cleanly; representative invalid cases are documented in the sample README
- `tests/DependencyContractAnalyzer.Tests`: unit tests built on `Microsoft.CodeAnalysis.Testing`
- `docs/`: contributor, release, and specification documentation

## Implementation notes

- Keep analyzer allocations low.
- Use `ImmutableArray` where it improves analyzer-path performance or API clarity.
- Use `SymbolEqualityComparer.Default` for symbol comparisons.
- Normalize contract names with trimming and ordinal case-insensitive comparison.
- Keep the first release scope limited to constructor parameters, non-constructor method parameters, property types, fields, `new` expressions, static member usage, base types, and implemented interfaces.

## Release

- CI validation is defined in `.github/workflows/ci.yml`.
- CI uploads both package artifacts and `dotnet test` coverage/test-result artifacts.
- NuGet.org publishing guidance is documented in `docs/trusted-publishing.md`.
- Release publishing is defined in `.github/workflows/publish.yml`.
