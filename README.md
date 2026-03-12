# DependencyContractAnalyzer

Japanese README: [README.ja.md](README.ja.md)

`DependencyContractAnalyzer` is a Roslyn analyzer package for declaring dependency contracts on types and verifying them through static analysis, without relying on DI registration analysis.

Its core concept is:

`Type dependency is allowed only when declared contracts are satisfied.`

## Status

- Documentation-first OSS repository scaffold for the initial release
- Planned package ID: `DependencyContractAnalyzer`
- Implementation scope for the first release is tracked in [docs/specification.md](docs/specification.md)
- The intended end-state architecture is tracked in [docs/architecture.md](docs/architecture.md)

## Disclaimer

- Unofficial tool for architecture and dependency contract verification
- Not affiliated with Microsoft, the .NET Foundation, or the Roslyn team

## Why

Modern .NET codebases often depend on design assumptions that are not visible in the type system alone, such as thread safety, side-effect constraints, or infrastructure boundaries. This analyzer makes those assumptions explicit and verifies them against actual type dependencies.

## First release scope

The initial analyzer scope is intentionally narrow:

- Constructor parameters
- Field types
- Base types
- Implemented interfaces

The first release does not analyze properties, method parameters, `new` expressions, or static member usage.

## Planned package reference

After the first public release, the package is expected to be consumed like this:

```xml
<ItemGroup>
  <PackageReference Include="DependencyContractAnalyzer" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```

## Planned usage

Declare contracts on provided types:

```csharp
[ProvidesContract("thread-safe")]
public sealed class RedisCacheStore : ICacheStore
{
}
```

Require contracts from a consuming type:

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public sealed class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store)
    {
    }
}
```

If the matching dependency does not provide the required contract, the analyzer reports:

- Diagnostic ID: `DCA001`
- Default severity: `Warning`
- Message: `Dependency '{DependencyType}' does not provide required contract '{ContractName}'.`

Contract names are normalized with `Trim()` and compared using `StringComparison.OrdinalIgnoreCase`.

## Non-goals

- DI container registration analysis
- Runtime dependency resolution
- Scrutor or factory registration behavior
- Container-specific wiring rules

## Documentation

- Japanese user guide: [README.ja.md](README.ja.md)
- Implementation scope: [docs/specification.md](docs/specification.md)
- Japanese implementation scope: [docs/specification.ja.md](docs/specification.ja.md)
- Architecture overview: [docs/architecture.md](docs/architecture.md)
- Japanese architecture overview: [docs/architecture.ja.md](docs/architecture.ja.md)
- Contributing guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Code of Conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Development guide: [docs/development.md](docs/development.md)
- Trusted Publishing guide: [docs/trusted-publishing.md](docs/trusted-publishing.md)
- Security policy: [SECURITY.md](SECURITY.md)
- Support policy: [.github/SUPPORT.md](.github/SUPPORT.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)

## License

- This project is licensed under the MIT License. See [LICENSE](LICENSE).
- A Japanese translation is available in [LICENSE.ja.md](LICENSE.ja.md).
- Third-party notices are tracked in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
