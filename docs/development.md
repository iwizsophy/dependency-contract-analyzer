# Development Guide

This document is for maintainers and contributors.

## Prerequisites

- .NET SDK installed
- Familiarity with Roslyn analyzer development
- A local environment that can run unit tests for `Microsoft.CodeAnalysis.Testing`

## Typical local validation

Run from the repository root:

```powershell
dotnet restore DependencyContractAnalyzer.slnx
dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore
dotnet test DependencyContractAnalyzer.slnx -c Release --no-build
```

Local package output:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-build -o artifacts
```

## Project layout

The repository currently follows this structure:

- `src/DependencyContractAnalyzer`: analyzer, diagnostics, attributes, and helper logic
- `samples/DependencyContractAnalyzer.Sample`: runnable consumer example with intentional valid and invalid analyzer cases
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
- NuGet.org publishing guidance is documented in `docs/trusted-publishing.md`.
- Release publishing is defined in `.github/workflows/publish.yml`.
