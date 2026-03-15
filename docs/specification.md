# DependencyContractAnalyzer Specification

This document captures the currently implemented scope of `DependencyContractAnalyzer`.
It is the detailed companion to the repository-root `SPECIFICATION.md`,
which remains the authoritative summary specification.

Use this document for detailed rules, examples, edge cases, long tables,
and walkthroughs. If this file and `SPECIFICATION.md` disagree,
`SPECIFICATION.md` wins.

When specification behavior changes, update this file together with
`SPECIFICATION.md` unless the change is editorial only.

For the intended end-state design, see `docs/architecture.md`.

## 1. Purpose

Declare dependency contracts on classes and interfaces, then verify through static analysis whether dependent types satisfy those contracts.

The analyzer inspects type dependencies only and does not depend on DI registration analysis.

## 2. Current scope

The analyzer inspects the following dependency kinds:

| Dependency kind | Included |
| --- | --- |
| Constructor parameters | Yes |
| Non-constructor method parameters | Yes |
| Property types | Yes |
| Field types | Yes |
| `new` expressions | Yes |
| Static member usage | Yes |
| Base type | Yes |
| Implemented interfaces | Yes |

The following dependency kinds can be disabled through `.editorconfig` and default to `true`:

- `dependency_contract_analyzer.analyze_fields`
- `dependency_contract_analyzer.analyze_base_types`
- `dependency_contract_analyzer.analyze_interface_implementations`
- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`

Constructor parameters are always analyzed.

The following rule families are currently implemented:

| Rule family | Included |
| --- | --- |
| `ProvidesContract` | Yes |
| `RequiresDependencyContract` | Yes |
| `ContractTarget` | Yes |
| `RequiresContractOnTarget` | Yes |
| `ContractScope` | Yes |
| `RequiresContractOnScope` | Yes |
| `ContractHierarchy` | Yes |

Still out of scope:

| Item | Reason |
| --- | --- |
| Namespace-based inference beyond trailing 2-segment normalization | The current implementation supports leaf fallback by default and trailing 2-segment fallback through configuration |
| Attribute references | Dependency discovery remains limited to strong type relationships |
| Generic constraints | Dependency discovery remains limited to strong type relationships |
| `typeof` references | Dependency discovery remains limited to strong type relationships |
| Return types | Dependency discovery remains limited to strong type relationships |

## 3. Attribute model

### 3.1 Provided contract

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }

    public ProvidesContractAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.2 Required dependency contract

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresDependencyContractAttribute : Attribute
{
    public Type DependencyType { get; }
    public string ContractName { get; }

    public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }
}
```

### 3.3 Target declaration

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    public string Name { get; }

    public ContractTargetAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.4 Target-based requirement

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnTargetAttribute : Attribute
{
    public string TargetName { get; }
    public string ContractName { get; }

    public RequiresContractOnTargetAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }
}
```

### 3.5 Scope declaration

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    public string Name { get; }

    public ContractScopeAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.6 Scope-based requirement

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnScopeAttribute : Attribute
{
    public string ScopeName { get; }
    public string ContractName { get; }

    public RequiresContractOnScopeAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }
}
```

### 3.7 Contract hierarchy

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractHierarchyAttribute : Attribute
{
    public string Child { get; }
    public string Parent { get; }

    public ContractHierarchyAttribute(string child, string parent)
    {
        Child = child;
        Parent = parent;
    }
}
```

### 3.8 Implication semantics

- `ContractHierarchy` declares directed implication edges using `child -> parent`.
- Contract satisfaction is transitive across the implication graph.
- A contract satisfies itself and every contract reachable through hierarchy edges.
- Repeated hierarchy attributes allow multiple parents for the same contract.
- Cycles in the implication graph are invalid and reported as `DCA202`.

### 3.9 Custom exclusion attribute

```csharp
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractAnalysisAttribute : Attribute
{
}
```

When applied to an assembly or owner type, analyzer execution for that owner type is skipped.

### 3.10 Requirement suppression attributes

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredDependencyContractAttribute : Attribute
{
    public Type DependencyType { get; }
    public string ContractName { get; }

    public SuppressRequiredDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredTargetContractAttribute : Attribute
{
    public string TargetName { get; }
    public string ContractName { get; }

    public SuppressRequiredTargetContractAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredScopeContractAttribute : Attribute
{
    public string ScopeName { get; }
    public string ContractName { get; }

    public SuppressRequiredScopeContractAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }
}
```

These attributes suppress diagnostics for exact requirement matches on the owning class. They do not exclude dependency discovery itself.

### 3.12 Member-level dependency source exclusion

```csharp
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractSourceAttribute : Attribute
{
}
```

When applied to a constructor, method, property, or field, dependency extraction skips that member's declared dependency source and syntax-based dependency discovery.

For DI-agnostic analysis, declare contracts on the consumed abstraction when a dependency is typed as an interface or base class.

## 4. Rule evaluation model

The analyzer currently evaluates requirements in this order:

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. contract implication expansion when matching provided contracts

Current behavior:

- `RequiresDependencyContract` reports a diagnostic only when a matching dependency exists and the required contract is still missing.
- `RequiresDependencyContract` reports `DCA002` when the declared dependency type is not used.
- `RequiresContractOnTarget` evaluates only dependencies whose declared targets match the normalized target name.
- `RequiresContractOnScope` evaluates only dependencies whose declared scopes match the normalized scope name.
- Dependencies outside the current compilation assembly are ignored by default.
- If `dependency_contract_analyzer.external_dependency_policy = metadata`, the analyzer reads explicit provided-contract metadata from matching external dependency types before evaluating `DCA001`.
- If `dependency_contract_analyzer.external_dependency_policy = metadata`, `RequiresContractOnTarget` and `RequiresContractOnScope` also read explicit target and scope metadata from matching external dependency types and assemblies.
- If `dependency_contract_analyzer.external_dependency_policy = metadata`, the analyzer also reads `ContractHierarchy` implication edges from referenced assemblies when expanding provided contracts for external dependencies.
- Namespace fallback inference remains limited to types declared in the current compilation, even in `metadata` mode.
- `DCA200` and `DCA201` still validate declared targets and scopes against the current compilation only.
- Diagnostics for referenced implication definitions are not reported in the consuming compilation.
- If `dependency_contract_analyzer.report_undeclared_requirement_diagnostics = false`, target/scope requirement evaluation continues after the undeclared check instead of stopping at `DCA200` / `DCA201`.
- Type-level targets and scopes use explicit attributes first, and v1 does not combine type-level explicit names with namespace-inferred names for the same kind.
- If a type has no explicit target, the analyzer infers one from the final namespace segment in the current compilation.
- If `dependency_contract_analyzer.namespace_inference_max_segments = 2`, the analyzer also infers a trailing two-segment fallback target name in the current compilation.
- Assembly-level scope declarations always apply to types in the current compilation.
- If a type has an explicit scope, namespace-based scope inference is skipped for that type.
- If a type has no explicit scope, the analyzer infers one from the final namespace segment in the current compilation even when assembly-level scopes are present.
- If `dependency_contract_analyzer.namespace_inference_max_segments = 2`, the analyzer also infers a trailing two-segment fallback scope name when the type has no explicit scope.
- Assembly/type-level `ExcludeDependencyContractAnalysisAttribute` skips analyzer execution for owner types.
- `ExcludeDependencyContractSourceAttribute` removes dependency sources from matching constructors, methods, properties, and fields after owner-type exclusions are applied.
- Exact-match requirement suppression attributes skip `DCA001`, `DCA002`, `DCA200`, `DCA201`, `DCA205`, and `DCA206` for the matching requirement only.

## 5. Name normalization rules

The analyzer normalizes all declared names using the same basic rules:

- Trim leading and trailing whitespace.
- Ignore case.
- Use ordinal comparison semantics.

This applies to:

- contract names
- target names
- scope names
- hierarchy endpoint names

## 6. Dependency discovery

Collect dependencies from the target type using:

### 6.1 Constructor dependencies

```csharp
public A(B b)
```

Source:

- `INamedTypeSymbol`
- `Constructors`
- `Parameters`

### 6.2 Method dependencies

```csharp
public void Execute(B b)
```

Source:

- `IMethodSymbol`
- `Parameters`

Included methods:

- ordinary methods
- explicit interface implementation methods

Excluded methods:

- constructors
- property and event accessors
- operators and conversions
- implicitly declared methods

### 6.3 Property dependencies

```csharp
public B Dependency { get; set; }
```

Source:

- `IPropertySymbol.Type`

### 6.4 Field dependencies

```csharp
private B _b;
```

Source:

- `IFieldSymbol.Type`

### 6.5 Object creation dependencies

```csharp
var dependency = new B();
```

```csharp
B dependency = new();
```

Source:

- `ObjectCreationExpressionSyntax`
- `ImplicitObjectCreationExpressionSyntax`
- semantic model type resolution

### 6.6 Static member dependencies

Representative sources:

- static method invocation
- static property access
- static field access
- `using static` imported member usage

Excluded:

- extension methods in reduced form
- `const` fields
- enum members

### 6.7 Inheritance

```csharp
class A : B
```

Source:

- `BaseType`

### 6.8 Interface implementation

```csharp
class A : IFoo
```

Source:

- `Interfaces`

The analyzer does not treat attribute references, generic constraints,
`typeof` references, or return types as dependency sources.

## 7. Metadata discovery

The analyzer currently reads metadata as follows:

- Provided contracts: from the dependency type itself, implemented interfaces, and base types
- Targets: from the dependency type itself, implemented interfaces, and base types
- Scopes: from the dependency type itself, implemented interfaces, base types, and assembly-level scope declarations
- Implication edges: from assembly-level `ContractHierarchyAttribute` declarations

Provided contracts are expanded through the transitive implication closure before matching requirements.

Assembly-level scopes always apply to types in the declaring assembly. Type-level scope declarations add to those assembly-level scopes. Namespace-based scope inference contributes additional scope names only when the type has no explicit scope declarations.

When `dependency_contract_analyzer.external_dependency_policy = metadata`, the analyzer also reads explicit provided contracts, targets, scopes, and implication edges from referenced assemblies for dependency matching. Local and referenced implication graphs are applied together until contract expansion reaches a fixed point. Referenced assemblies do not contribute namespace-inferred names, referenced implication diagnostics are not reported in the consuming compilation, and undeclared target/scope validation remains current-compilation-only.

## 8. Diagnostics

| ID | Default severity | Meaning |
| --- | --- | --- |
| `DCA001` | `Warning` | Dependency does not provide the required contract |
| `DCA002` | `Warning` | Declared dependency type is not used |
| `DCA100` | `Warning` | Contract name is empty |
| `DCA101` | `Warning` | Contract name violates the naming format |
| `DCA102` | `Warning` | Contract or requirement declaration is duplicated |
| `DCA200` | `Warning` | Required target is undeclared in the compilation |
| `DCA201` | `Warning` | Required scope is undeclared in the compilation |
| `DCA202` | `Warning` | Contract implication definition is cyclic |
| `DCA203` | `Warning` | Scope name is empty |
| `DCA204` | `Warning` | Target name is empty |
| `DCA205` | `Info` | Required target is not used by any analyzable dependency |
| `DCA206` | `Info` | Required scope is not used by any analyzable dependency |

Severity remains configurable through `.editorconfig`.

Default diagnostic severities are product defaults; recommended CI severities are documented separately and are not part of the analyzer's semantic specification.

### 8.1 EditorConfig options

`DependencyContractAnalyzer` supports the following boolean `.editorconfig` options. These defaults apply under `behavior_preset = default`:

- `dependency_contract_analyzer.analyze_fields` (default: `true`)
- `dependency_contract_analyzer.analyze_base_types` (default: `true`)
- `dependency_contract_analyzer.analyze_interface_implementations` (default: `true`)
- `dependency_contract_analyzer.analyze_method_parameters` (default: `true`)
- `dependency_contract_analyzer.analyze_properties` (default: `true`)
- `dependency_contract_analyzer.analyze_object_creation` (default: `true`)
- `dependency_contract_analyzer.analyze_static_members` (default: `true`)
- `dependency_contract_analyzer.report_unused_requirement_diagnostics` (default: `true`)
- `dependency_contract_analyzer.report_undeclared_requirement_diagnostics` (default: `true`)

If an option value is missing or invalid, the analyzer falls back to the default.

The analyzer also supports this global integer `.editorconfig` option:

- `dependency_contract_analyzer.namespace_inference_max_segments` (default: `1`, supported values: `1`, `2`)

The analyzer also supports this global string `.editorconfig` option:

- `dependency_contract_analyzer.external_dependency_policy` (default: `ignore`, supported values: `ignore`, `metadata`)

The analyzer also supports this global preset `.editorconfig` option:

- `dependency_contract_analyzer.behavior_preset` (default: `default`, supported values: `default`, `strict`, `relaxed`)

The analyzer also supports these list-valued `.editorconfig` options:

- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`

`behavior_preset` shifts the default behavior of other options without replacing them.

- `default`: current product defaults
- `strict`: all optional dependency-source toggles enabled, namespace inference defaults to trailing two-segment fallback, and external dependency policy defaults to `metadata`
- `relaxed`: optional dependency-source toggles disabled, namespace inference disabled, and external dependency policy defaults to `ignore`

Explicit per-option settings always override the preset. `report_unused_requirement_diagnostics` controls `DCA002`, `DCA205`, and `DCA206`. `report_undeclared_requirement_diagnostics` controls `DCA200` and `DCA201`; when it is `false`, target/scope requirement evaluation continues against matching dependencies instead of stopping at the undeclared check. `excluded_namespaces` skips analyzer execution for owner types in the listed namespaces and their subnamespaces. `excluded_types` skips analyzer execution for listed fully qualified owner type names. List values accept comma, semicolon, or newline separators. `namespace_inference_max_segments` controls whether fallback inference uses only the final namespace segment (`1`) or both the final segment and trailing two-segment combinations (`2`). Invalid or missing values fall back to the preset-derived default. `external_dependency_policy` controls whether dependencies outside the current compilation are ignored (`ignore`) or matched against explicit metadata and implication edges from referenced assemblies (`metadata`). Invalid or missing values fall back to the preset-derived default. Exclusion lists and diagnostic severity remain independent of `behavior_preset`.

`analyze_*`, `report_*`, `excluded_namespaces`, and `excluded_types` are source-scoped options. When the owner type is partial, the analyzer evaluates all declaring source files for that type. Boolean source-scoped options merge conservatively, so any explicit `false` wins for the whole type. List-valued source-scoped options merge by distinct union across declarations. `behavior_preset`, `namespace_inference_max_segments`, and `external_dependency_policy` remain compilation-wide options.

### 8.2 Contract naming rule

`DCA101` validates contract naming format.

- Required format: lower-kebab-case
- Regex: `^[a-z0-9]+(-[a-z0-9]+)*$`
- Applies only to contract names and hierarchy endpoints
- Also applies to the contract name arguments of requirement suppression attributes
- Does not apply to target names or scope names

Covered names:

- `ProvidesContract`
- the contract name argument of `RequiresDependencyContract`
- the contract name argument of `RequiresContractOnTarget`
- the contract name argument of `RequiresContractOnScope`
- the contract name argument of `SuppressRequiredDependencyContract`
- the contract name argument of `SuppressRequiredTargetContract`
- the contract name argument of `SuppressRequiredScopeContract`
- both `child` and `parent` arguments of `ContractHierarchy`

### 8.3 Suppression model

The current implementation supports:

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` severity settings
- `.editorconfig` owner-type exclusion via `excluded_namespaces` and `excluded_types`
- `ExcludeDependencyContractAnalysisAttribute` on assemblies and owner types
- `ExcludeDependencyContractSourceAttribute` on constructors, methods, properties, and fields
- `SuppressRequiredDependencyContractAttribute`, `SuppressRequiredTargetContractAttribute`, and `SuppressRequiredScopeContractAttribute` on owner types for exact requirement matches

Member-level exclusion removes dependency sources only. It does not suppress requirements by itself.

## 9. Current project layout

```text
src/
 └ DependencyContractAnalyzer
   ├ Analyzers
   │  └ DependencyContractAnalyzer.cs
   ├ Attributes
   │  ├ ContractHierarchyAttribute.cs
   │  ├ ContractScopeAttribute.cs
   │  ├ ContractTargetAttribute.cs
   │  ├ ExcludeDependencyContractAnalysisAttribute.cs
   │  ├ ExcludeDependencyContractSourceAttribute.cs
   │  ├ ProvidesContractAttribute.cs
   │  ├ RequiresContractOnScopeAttribute.cs
   │  ├ RequiresContractOnTargetAttribute.cs
   │  ├ RequiresDependencyContractAttribute.cs
   │  ├ SuppressRequiredDependencyContractAttribute.cs
   │  ├ SuppressRequiredScopeContractAttribute.cs
   │  └ SuppressRequiredTargetContractAttribute.cs
   ├ Diagnostics
   │  └ DiagnosticDescriptors.cs
   ├ Helpers
   │  ├ AnalysisExclusionOptions.cs
   │  ├ BehaviorPresetOptions.cs
   │  ├ ContractImplicationResolver.cs
   │  ├ ContractNameNormalizer.cs
   │  ├ DependencyCollectionOptions.cs
   │  ├ DependencyCollector.cs
   │  ├ ExternalDependencyOptions.cs
   │  ├ NamespaceInferenceOptions.cs
   │  └ RequirementEvaluationOptions.cs
   └ Utilities
      └ SymbolExtensions.cs
samples/
 └ DependencyContractAnalyzer.Sample
   ├ DependencyContractAnalyzer.Sample.csproj
   ├ Program.cs
   └ README.md
```

## 10. Analyzer flow

```text
CompilationStart
        |
        +-- Resolve attribute symbols
        |
        +-- Read assembly-level implication declarations
        |
        +-- Report implication diagnostics at compilation end
        |
        +-- SymbolAction(TypeSymbol)
              |
              +-- Validate declared contracts / targets / scopes
              |
              +-- Read dependency, target, and scope requirements
              |
              +-- Collect dependency types
              |
              +-- Read provided contracts, targets, and scopes
              |
              +-- Expand provided contracts through implication edges
              |
              +-- Report diagnostics when requirements are not satisfied
```

## 11. Test strategy

Use `Microsoft.CodeAnalysis.Testing`.

Representative scenarios include:

- No diagnostic when a dependency directly provides the required contract
- No diagnostic when a required dependency appears only on a non-constructor method parameter
- No diagnostic when a required dependency appears only on a property type
- No diagnostic when a required dependency appears only through a `new` expression
- No diagnostic when a required dependency appears only through static member usage
- `DCA002` when field, base-type, interface-implementation, method-parameter, property, object-creation, or static-member dependency analysis is disabled through `.editorconfig`
- No diagnostic when the owner type is excluded through `.editorconfig` namespace or type exclusion settings
- `DCA002`, `DCA205`, or `DCA206` when the only matching dependency source is excluded at member level
- No diagnostic when a matching dependency, target, or scope requirement is suppressed on the owner type
- `DCA001` when a matching dependency does not provide the required contract
- `DCA002` when `RequiresDependencyContract` references an unused dependency type
- Scope-based matching through type-level and assembly-level scopes
- Target-based matching through direct and inherited target declarations
- Implication-based matching through hierarchy and multi-step chains
- Diagnostics for empty names, duplicate declarations, and cyclic implication graphs
- No diagnostic when the owner type is excluded through the custom exclusion attribute

## 12. Future extensions

- Additional EditorConfig policy categories beyond the current option set
- Richer namespace metadata inference beyond trailing 2-segment normalization

## 13. Non-goals

- DI registration analysis
- Runtime dependency resolution
- Scrutor behavior
- Factory registration behavior
- DI container behavior

## 14. Coding guidelines

- Minimize allocations in analyzer code
- Prefer `ImmutableArray`
- Use `SymbolEqualityComparer.Default`
- Use ordinal case-insensitive string comparison

## 15. Done criteria

- Implement the attributes listed in this document
- Implement the analyzer rule evaluation described above
- Implement the diagnostics listed in this document
- Add unit tests for the supported rule families
- Keep README and specification documents aligned with the implementation
