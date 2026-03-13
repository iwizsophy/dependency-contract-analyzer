# DependencyContractAnalyzer Specification

This document captures the currently implemented scope of `DependencyContractAnalyzer`. For the intended end-state design, see `docs/architecture.md`.

## 1. Purpose

Declare dependency contracts on classes and interfaces, then verify through static analysis whether dependent types satisfy those contracts.

The analyzer inspects type dependencies only and does not depend on DI registration analysis.

## 2. Current scope

The analyzer currently inspects the following dependency kinds:

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

- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`

Constructor parameters, field types, base types, and implemented interfaces are always analyzed.

The following rule families are currently implemented:

| Rule family | Included |
| --- | --- |
| `ProvidesContract` | Yes |
| `RequiresDependencyContract` | Yes |
| `ContractTarget` | Yes |
| `RequiresContractOnTarget` | Yes |
| `ContractScope` | Yes |
| `RequiresContractOnScope` | Yes |
| `ContractAlias` | Yes |

Still out of scope:

| Item | Reason |
| --- | --- |
| Namespace-based target or scope inference | Explicit metadata only |

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

### 3.7 Contract alias

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractAliasAttribute : Attribute
{
    public string From { get; }
    public string To { get; }

    public ContractAliasAttribute(string from, string to)
    {
        From = from;
        To = to;
    }
}
```

### 3.8 Alias semantics

In v1, contract hierarchy is modeled only by transitive aliases.

- Aliases are directed edges from `from` to `to`.
- Alias satisfaction is transitive.
- A contract satisfies itself and every contract reachable through alias edges.
- Alias cycles are invalid and reported as `DCA202`.
- Contract hierarchies beyond alias implication are out of scope for v1.

For DI-agnostic analysis, declare contracts on the consumed abstraction when a dependency is typed as an interface or base class.

## 4. Rule evaluation model

The analyzer currently evaluates requirements in this order:

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. `ContractAlias` expansion when matching provided contracts

Current behavior:

- `RequiresDependencyContract` reports a diagnostic only when a matching dependency exists and the required contract is still missing.
- `RequiresDependencyContract` reports `DCA002` when the declared dependency type is not used.
- `RequiresContractOnTarget` evaluates only dependencies whose declared targets match the normalized target name.
- `RequiresContractOnScope` evaluates only dependencies whose declared scopes match the normalized scope name.
- Dependencies outside the current compilation assembly are ignored for missing-contract checks.
- Targets and scopes are recognized only from explicit attributes declared in the current compilation.
- Namespace-based target or scope inference is out of scope for v1.

## 5. Name normalization rules

The analyzer normalizes all declared names using the same basic rules:

- Trim leading and trailing whitespace.
- Ignore case.
- Use ordinal comparison semantics.

This applies to:

- contract names
- target names
- scope names
- alias `from` / `to` names

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

## 7. Metadata discovery

The analyzer currently reads metadata as follows:

- Provided contracts: from the dependency type itself, implemented interfaces, and base types
- Targets: from the dependency type itself, implemented interfaces, and base types
- Scopes: from the dependency type itself, implemented interfaces, base types, and assembly-level scope declarations
- Aliases: from assembly-level `ContractAliasAttribute` declarations

Provided contracts are expanded through the transitive alias closure before matching requirements.

Assembly-level scopes act as default scopes in addition to type-level scope declarations.

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
| `DCA202` | `Warning` | Contract alias definition is cyclic |
| `DCA203` | `Warning` | Scope name is empty |
| `DCA204` | `Warning` | Target name is empty |
| `DCA205` | `Info` | Required target is not used by any analyzable dependency |
| `DCA206` | `Info` | Required scope is not used by any analyzable dependency |

Severity remains configurable through `.editorconfig`.

Default diagnostic severities are product defaults; recommended CI severities are documented separately and are not part of the analyzer's semantic specification.

### 8.1 EditorConfig options

`DependencyContractAnalyzer` supports the following boolean `.editorconfig` options:

- `dependency_contract_analyzer.analyze_method_parameters` (default: `true`)
- `dependency_contract_analyzer.analyze_properties` (default: `true`)
- `dependency_contract_analyzer.analyze_object_creation` (default: `true`)
- `dependency_contract_analyzer.analyze_static_members` (default: `true`)

If an option value is missing or invalid, the analyzer falls back to the default.

### 8.2 Contract naming rule

`DCA101` validates contract naming format.

- Required format: lower-kebab-case
- Regex: `^[a-z0-9]+(-[a-z0-9]+)*$`
- Applies only to contract names and alias endpoints
- Does not apply to target names or scope names

Covered names:

- `ProvidesContract`
- the contract name argument of `RequiresDependencyContract`
- the contract name argument of `RequiresContractOnTarget`
- the contract name argument of `RequiresContractOnScope`
- both `from` and `to` arguments of `ContractAlias`

### 8.3 Suppression model

v1 supports only standard Roslyn suppression mechanisms:

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` severity settings

Custom exclusion attributes, namespace-level exclusions, and requirement-level exclusions are out of scope for v1.

## 9. Current project layout

```text
src/
 └ DependencyContractAnalyzer
   ├ Analyzers
   │  └ DependencyContractAnalyzer.cs
   ├ Attributes
   │  ├ ContractAliasAttribute.cs
   │  ├ ContractScopeAttribute.cs
   │  ├ ContractTargetAttribute.cs
   │  ├ ProvidesContractAttribute.cs
   │  ├ RequiresContractOnScopeAttribute.cs
   │  ├ RequiresContractOnTargetAttribute.cs
   │  └ RequiresDependencyContractAttribute.cs
   ├ Diagnostics
   │  └ DiagnosticDescriptors.cs
   ├ Helpers
   │  ├ ContractAliasResolver.cs
   │  ├ ContractNameNormalizer.cs
   │  └ DependencyCollector.cs
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
        +-- Read assembly-level ContractAlias declarations
        |
        +-- Report alias diagnostics at compilation end
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
              +-- Expand provided contracts through aliases
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
- `DCA002` when method parameter, property, object creation, or static-member dependency analysis is disabled through `.editorconfig`
- `DCA001` when a matching dependency does not provide the required contract
- `DCA002` when `RequiresDependencyContract` references an unused dependency type
- Scope-based matching through type-level and assembly-level scopes
- Target-based matching through direct and inherited target declarations
- Alias-based matching through direct and multi-step alias chains
- Diagnostics for empty names, duplicate declarations, and cyclic aliases

## 12. Future extensions

- Contract hierarchies beyond alias implication
- EditorConfig-based policy control beyond dependency collection toggles
- Namespace-based metadata inference

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
