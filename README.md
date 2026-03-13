# DependencyContractAnalyzer

Japanese README: [README.ja.md](README.ja.md)

`DependencyContractAnalyzer` is a Roslyn analyzer package for declaring dependency contracts on types and verifying them through static analysis, without relying on DI registration analysis.

Its core concept is:

`Type dependency is allowed only when declared contracts are satisfied.`

## Status

- Implemented analyzer rules: `ProvidesContract`, `RequiresDependencyContract`, `ContractTarget`, `RequiresContractOnTarget`, `ContractScope`, `RequiresContractOnScope`, `ContractAlias`, `ContractHierarchy`
- Dependency extraction currently covers constructor parameters, non-constructor method parameters, property types, fields, `new` expressions, static member usage, base types, and implemented interfaces
- Package ID: `DependencyContractAnalyzer`
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
- Non-constructor method parameters
- Property types
- Field types
- `new` expressions
- Static member usage
- Base types
- Implemented interfaces

## Package reference

Once the package is published, consume it like this:

```xml
<ItemGroup>
  <PackageReference Include="DependencyContractAnalyzer" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```

## Usage

Declare contracts on provided types:

```csharp
[ProvidesContract("thread-safe")]
public interface ICacheStore
{
}

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

Because the analyzer is DI-agnostic, the required contract must be discoverable from the consumed type itself, its base types, or its implemented interfaces. If a class depends on an interface, put the contract on that interface when you want the requirement to pass without DI analysis.

Scope-based rules are also supported:

```csharp
[ContractScope("repository")]
[ProvidesContract("thread-safe")]
public sealed class UserRepository
{
}

[RequiresContractOnScope("repository", "thread-safe")]
public sealed class OrderService
{
    public OrderService(UserRepository repository)
    {
    }
}
```

Scope names use the same normalization rules as contract names: `Trim()` with `StringComparison.OrdinalIgnoreCase`.

Target-based rules are also supported:

```csharp
[ContractTarget("repository")]
[ProvidesContract("thread-safe")]
public sealed class UserRepository
{
}

[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class OrderService
{
    public OrderService(UserRepository repository)
    {
    }
}
```

Target names also use `Trim()` with `StringComparison.OrdinalIgnoreCase`.

Assembly-level implication edges can be declared with either `ContractAlias` or `ContractHierarchy`:

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]

[ProvidesContract("snapshot-cache")]
public sealed class SnapshotCache
{
}
```

With these declarations, `snapshot-cache` satisfies both `immutable` and `thread-safe`. Mixed alias and hierarchy chains are supported, and cyclic implication definitions are reported as `DCA202`.

`ContractAlias` remains a backward-compatible implication edge. `ContractHierarchy` is the explicit hierarchy API and supports multi-parent graphs by repeating attributes. For targets and scopes, explicit attributes remain the primary metadata source, and the analyzer infers a fallback name from the final namespace segment when type-level metadata is absent. `ReadModel` becomes `read-model`. For scopes, assembly-level `ContractScope` remains explicit metadata and suppresses namespace inference.

## Default severities

- `DCA001`, `DCA002`, `DCA100`, `DCA101`, `DCA102`, `DCA200`, `DCA201`, `DCA202`, `DCA203`, `DCA204`: `Warning`
- `DCA205`, `DCA206`: `Info`

These are product defaults. All diagnostics remain configurable through `.editorconfig`.

`.editorconfig` also supports dependency collection policy toggles for these optional dependency sources:

- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`
- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`

The four `analyze_*` options default to `true`. Constructor parameters, field types, base types, and implemented interfaces remain enabled.

`excluded_namespaces` skips analyzer execution for owner types in the listed namespaces and their subnamespaces. `excluded_types` skips analyzer execution for listed fully qualified owner type names.

## Recommended CI policy

- Promote `DCA202`, `DCA203`, and `DCA204` to `Error` in CI.
- Keep `DCA205` and `DCA206` at `Info` unless the codebase is already stable enough to treat stale requirements as build-blocking.
- `DCA101` validates lower-kebab-case contract names, requirement-suppression contract arguments, and alias or hierarchy endpoints only. It does not apply to target or scope names.

## Suppression model

The current implementation supports:

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` severity settings
- `.editorconfig` owner-type exclusion through `excluded_namespaces` and `excluded_types`
- `[ExcludeDependencyContractAnalysis]` on assemblies and owner types
- `[ExcludeDependencyContractSource]` on constructors, methods, properties, and fields to ignore dependency sources from that member
- `[SuppressRequiredDependencyContract]`, `[SuppressRequiredTargetContract]`, and `[SuppressRequiredScopeContract]` on owner types for exact requirement matches

Member-level exclusion removes dependency sources only. It does not suppress matching requirements by itself.

## Non-goals

- DI container registration analysis
- Runtime dependency resolution
- Scrutor or factory registration behavior
- Container-specific wiring rules

## Documentation

- Japanese user guide: [README.ja.md](README.ja.md)
- Sample consumer project: [samples/DependencyContractAnalyzer.Sample](samples/DependencyContractAnalyzer.Sample)
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
