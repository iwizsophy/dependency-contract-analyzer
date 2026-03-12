# Development Guide

This document is for maintainers and contributors.

## Prerequisites

- .NET SDK installed
- Familiarity with Roslyn analyzer development
- A local environment that can run unit tests for `Microsoft.CodeAnalysis.Testing`

## Typical local validation

Run from the repository root once source projects are present:

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

## Planned project layout

The initial implementation is expected to follow this structure:

- `src/DependencyContractAnalyzer`: analyzer, diagnostics, attributes, and helper logic
- `tests/DependencyContractAnalyzer.Tests`: unit tests built on `Microsoft.CodeAnalysis.Testing`
- `docs/`: contributor, release, and specification documentation

## Implementation notes

- Keep analyzer allocations low.
- Use `ImmutableArray` where it improves analyzer-path performance or API clarity.
- Use `SymbolEqualityComparer.Default` for symbol comparisons.
- Normalize contract names with trimming and ordinal case-insensitive comparison.
- Keep the first release scope limited to constructor parameters, fields, base types, and implemented interfaces.

## Release

For NuGet.org publishing guidance, see `docs/trusted-publishing.md`.
