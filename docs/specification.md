# DependencyContractAnalyzer Specification

This document captures the initial implementation scope for the first public release. For the intended end-state design, see `docs/architecture.md`.

## 1. Purpose

Declare dependency contracts on classes and interfaces, then verify through static analysis whether dependent types satisfy those contracts.

The analyzer inspects type dependencies only and does not depend on DI registration analysis.

## 2. Initial scope

The first release analyzes only the following dependency kinds:

| Dependency kind | Included |
| --- | --- |
| Constructor parameters | Yes |
| Field types | Yes |
| Base type | Yes |
| Implemented interfaces | Yes |

Out of scope for the first release:

| Dependency kind | Reason |
| --- | --- |
| Properties | Avoid early false positives |
| Method parameters | Deferred scope expansion |
| `new` expressions | Weaker dependency signal |
| Static usage | Not treated as a dependency |

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

Example:

```csharp
[ProvidesContract("thread-safe")]
public interface ICacheStore
{
}

public class RedisCacheStore : ICacheStore
{
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

Example:

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store)
    {
    }
}
```

For DI-agnostic analysis, declare the contract on the consumed abstraction when a dependency is typed as an interface or base class.

## 4. Analysis rule

Report a diagnostic when all of the following are true:

1. `RequiresDependencyContractAttribute` is declared on a type.
2. A matching dependency for the declared `DependencyType` exists.
3. The dependency target does not provide the required contract.

## 5. Contract matching

Contract names are compared with the following rules:

- Trim leading and trailing whitespace.
- Ignore case.
- Use ordinal comparison semantics.

Values such as `thread-safe`, `THREAD-SAFE`, and `Thread-Safe` are treated as equivalent.

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

### 6.2 Field dependencies

```csharp
private B _b;
```

Source:

- `IFieldSymbol.Type`

### 6.3 Inheritance

```csharp
class A : B
```

Source:

- `BaseType`

### 6.4 Interface implementation

```csharp
class A : IFoo
```

Source:

- `Interfaces`

## 7. Contract discovery

Read `ProvidesContractAttribute` from the dependency target, including:

- The class itself
- Implemented interfaces
- Base types

`Inherited = true` allows inherited contract behavior.

## 8. Diagnostic

- Diagnostic ID: `DCA001`
- Default severity: `Warning`
- Message: `Dependency '{DependencyType}' does not provide required contract '{ContractName}'.`

Severity should remain configurable through `.editorconfig`.

## 9. Planned project layout

```text
src/
 └ DependencyContractAnalyzer
   ├ Analyzers
   │  └ DependencyContractAnalyzer.cs
   ├ Attributes
   │  ├ ProvidesContractAttribute.cs
   │  └ RequiresDependencyContractAttribute.cs
   ├ Diagnostics
   │  └ DiagnosticDescriptors.cs
   ├ Helpers
   │  ├ ContractNameNormalizer.cs
   │  └ DependencyCollector.cs
   └ Utilities
      └ SymbolExtensions.cs
```

## 10. Analyzer flow

```text
CompilationStart
        |
        +-- SymbolAction(TypeSymbol)
        |
        +-- Read RequiresDependencyContractAttribute
        |
        +-- Collect dependency types
        |
        +-- Filter dependencies matching DependencyType
        |
        +-- Read ProvidesContractAttribute
        |
        +-- Report diagnostic when the contract is missing
```

## 11. Test strategy

Use `Microsoft.CodeAnalysis.Testing`.

Expected scenarios:

- No diagnostic when the dependency provides the required contract
- `DCA001` when the dependency does not provide the required contract

Example:

```csharp
[RequiresDependencyContract(typeof(IFoo), "thread-safe")]
class A
{
    public A(IFoo foo) {}
}

class Foo : IFoo {}
```

Expected result: diagnostic reported.

## 12. Future extensions

- `ContractScope` for architecture-layer or code-region declarations
- `RequiresContractOnScope` for scope-level contract requirements
- `ContractTarget` for type-category declarations
- `RequiresContractOnTarget` for category-level contract requirements
- `ContractAlias` for contract implication / alias relationships
- Method parameter dependencies
- Property dependencies
- Object creation (`new`) analysis
- Static usage analysis
- Contract hierarchies
- EditorConfig-based policy control

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

- Attributes implemented
- Analyzer implemented
- Diagnostic reporting implemented
- Unit tests added
- README completed
