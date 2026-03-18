![DependencyContractAnalyzer icon](https://raw.githubusercontent.com/iwizsophy/dependency-contract-analyzer/develop/assets/package-icon.png)

# DependencyContractAnalyzer

Japanese README: [README.ja.md](README.ja.md)

`DependencyContractAnalyzer` is a Roslyn analyzer package for declaring dependency contracts on types and verifying them through static analysis, without relying on DI registration analysis.

Its core concept is:

`Type dependency is allowed only when declared contracts are satisfied.`

## Status

- Implemented analyzer rules: `ProvidesContract`, `RequiresDependencyContract`, `ContractTarget`, `RequiresContractOnTarget`, `ContractScope`, `RequiresContractOnScope`, `ContractHierarchy`
- Dependency extraction covers constructor parameters, non-constructor method parameters, property types, fields, `new` expressions, static member usage (including static events and `using static` imports, but excluding enum members), base types, and implemented interfaces
- Package ID: `DependencyContractAnalyzer`
- Implementation scope for the first release is tracked in [docs/specification.md](docs/specification.md)
- The intended end-state architecture is tracked in [docs/architecture.md](docs/architecture.md)

## Supported environments

`DependencyContractAnalyzer` is distributed as a Roslyn analyzer targeting `netstandard2.0`.

The current guaranteed support scope is the Microsoft-supported .NET release lines at the time of release. For the current release, that means:

- `.NET 8`
- `.NET 9`
- `.NET 10`

Within those release lines, supported build environments must:

- support Roslyn analyzers
- consume analyzer packages targeting `netstandard2.0`

This repository does not maintain a full version-by-version IDE or SDK support matrix beyond that support statement.

Technically, the current implementation is expected to work on `.NET 5` and later build environments, and on Visual Studio `2019 16.8` and later, when Roslyn analyzer loading is available and the host compiler is compatible with the packaged analyzer. However, those environments are outside the guaranteed support scope. This project does not test them as part of its validation policy and does not provide support commitments for them.

## Disclaimer

- Unofficial tool for architecture and dependency contract verification
- Not affiliated with Microsoft, the .NET Foundation, or the Roslyn team

## Why

Modern .NET codebases often depend on design assumptions that are not visible in the type system alone, such as thread safety, side-effect constraints, or infrastructure boundaries. This analyzer makes those assumptions explicit and verifies them against actual type dependencies.

## Dependency extraction scope

Dependency extraction is intentionally limited to strong type relationships:

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

Assembly-level implication edges are declared with `ContractHierarchy`:

```csharp
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
[assembly: ContractHierarchy("immutable", "thread-safe")]

[ProvidesContract("snapshot-cache")]
public sealed class SnapshotCache
{
}
```

With these declarations, `snapshot-cache` satisfies both `immutable` and `thread-safe`. Multi-step and multi-parent hierarchy chains are supported, and cyclic implication definitions are reported as `DCA202`.

`ContractHierarchy` is the implication API and supports multi-parent graphs by repeating attributes. For targets and scopes, type-level explicit attributes remain the primary metadata source, and the analyzer does not add namespace-inferred names for the same kind when explicit type metadata is present. By default, the analyzer infers a fallback name from the final namespace segment when type-level metadata is absent, so `ReadModel` becomes `read-model`. With `dependency_contract_analyzer.namespace_inference_max_segments = 2`, trailing two-segment fallbacks such as `ReadModels.Query` -> `read-models-query` are also inferred. For scopes, assembly-level `ContractScope` declarations always apply to types in the assembly, type-level scopes add to those assembly scopes, and namespace inference still contributes fallback scope names for types without a type-level scope declaration. Dependencies outside the current compilation are ignored by default; with `dependency_contract_analyzer.external_dependency_policy = metadata`, the analyzer also reads explicit provided-contract, target, and scope metadata plus referenced `ContractHierarchy` edges. Referenced implication diagnostics are not reported in the consuming compilation. Undeclared target and scope validation still uses declarations from the current compilation only.

## Default severities

- `DCA001`, `DCA002`, `DCA100`, `DCA101`, `DCA102`, `DCA200`, `DCA201`, `DCA202`, `DCA203`, `DCA204`: `Warning`
- `DCA205`, `DCA206`: `Info`

These are product defaults. All diagnostics remain configurable through `.editorconfig`.

`.editorconfig` also supports dependency collection policy toggles for these optional dependency sources:

- `dependency_contract_analyzer.behavior_preset`
- `dependency_contract_analyzer.analyze_fields`
- `dependency_contract_analyzer.analyze_base_types`
- `dependency_contract_analyzer.analyze_interface_implementations`
- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`
- `dependency_contract_analyzer.report_unused_requirement_diagnostics`
- `dependency_contract_analyzer.report_undeclared_requirement_diagnostics`
- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`
- `dependency_contract_analyzer.namespace_inference_max_segments`
- `dependency_contract_analyzer.external_dependency_policy`

Under `behavior_preset = default`, all `analyze_*` options default to `true`. Constructor parameters remain enabled regardless of the preset.

`behavior_preset` is a global option. Supported values are `default`, `strict`, and `relaxed`; invalid values fall back to `default`.

- `default`: the current product defaults
- `strict`: enables all optional dependency-source toggles, uses `namespace_inference_max_segments = 2`, and defaults `external_dependency_policy` to `metadata`
- `relaxed`: disables optional dependency-source toggles, disables namespace inference, and defaults `external_dependency_policy` to `ignore`

Explicit per-option settings always override the preset. For example, `analyze_method_parameters = true`, `namespace_inference_max_segments = 2`, or `external_dependency_policy = metadata` each take precedence over `behavior_preset`. Exclusion lists and diagnostic severity remain separate controls.

Source-scoped options apply across all declaring files of a partial owner type. Boolean source-scoped options (`analyze_*`, `report_*`) merge conservatively, so any explicit `false` disables that option for the type. List-valued source-scoped options (`excluded_namespaces`, `excluded_types`) merge by distinct union across declarations. Global options such as `behavior_preset`, `namespace_inference_max_segments`, and `external_dependency_policy` remain compilation-wide.

`report_unused_requirement_diagnostics` controls `DCA002`, `DCA205`, and `DCA206`. `report_undeclared_requirement_diagnostics` controls `DCA200` and `DCA201`. Both default to `true`, and invalid values fall back to the default. When undeclared requirement diagnostics are disabled, target and scope requirements continue to evaluate matching dependencies instead of stopping at the undeclared check.

`excluded_namespaces` skips analyzer execution for owner types in the listed namespaces and their subnamespaces. `excluded_types` skips analyzer execution for listed fully qualified owner type names. `namespace_inference_max_segments` is a global option. Supported values are `1` and `2`, the default is `1`, and invalid values fall back to the preset-derived default. `external_dependency_policy` is also global. Supported values are `ignore` and `metadata`, the default is `ignore`, and invalid values fall back to the preset-derived default. In `metadata` mode, namespace inference still remains limited to current-compilation types; referenced assemblies contribute explicit metadata and implication edges only.

## Recommended CI policy

- Promote `DCA202`, `DCA203`, and `DCA204` to `Error` in CI.
- Keep `DCA205` and `DCA206` at `Info` unless the codebase is already stable enough to treat stale requirements as build-blocking.
- `DCA101` validates lower-kebab-case contract names, requirement-suppression contract arguments, and hierarchy endpoints only. It does not apply to target or scope names.

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
- Layer dependency enforcement
- Namespace or package boundary rules
- Generic forbidden dependency graph rules
- Cycle detection for architectural layers
- Naming analyzers unrelated to contracts
- File or directory layout rules
- Project or solution structure validation
- A general architecture DSL similar to ArchUnit

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
