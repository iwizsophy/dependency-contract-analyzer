# Contributing

Thanks for your interest in contributing to DependencyContractAnalyzer.

## Before you start

- Open an issue for bug reports, analyzer-rule proposals, or API changes.
- Keep changes focused and small when possible.
- For analyzer behavior changes, include or update tests.
- Final technical decisions remain with the maintainers.

## Development workflow

1. Fork the repository and create a topic branch.
2. Implement the change with matching analyzer tests or documentation updates.
3. Run local validation from the repository root once the source projects are present:
   - `dotnet restore`
   - `dotnet build -c Release --no-restore`
   - `dotnet test -c Release --no-build`
4. Submit a pull request with:
   - What changed
   - Why it changed
   - Validation results

## Project expectations

- Prefer explicit symbol handling and `SymbolEqualityComparer.Default`.
- Minimize unnecessary allocations in analyzer code.
- Use `ImmutableArray` where appropriate.
- Compare contract names with `StringComparison.OrdinalIgnoreCase` after trimming.

## Additional docs

- Specification: `docs/specification.md`
- Development details: `docs/development.md`
- Trusted Publishing: `docs/trusted-publishing.md`
- Code of Conduct: `CODE_OF_CONDUCT.md`
- Support policy: `.github/SUPPORT.md`
