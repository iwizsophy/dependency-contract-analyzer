# DependencyContractAnalyzer Architecture

This document describes the intended end-state architecture of `DependencyContractAnalyzer`.

The core principle is:

`Type dependency is allowed only when declared contracts are satisfied.`

This is not only an analyzer for attributes. It is a static architecture verification foundation that combines:

- type dependencies
- declared contracts
- architectural scope and target metadata

## 1. Overall shape

```text
[Type / Symbol]
    |
    +-- ProvidesContract
    +-- ContractTarget
    +-- ContractScope

[Dependency Extraction]
    |
    +-- discover which type depends on which type

[Rule Declaration]
    |
    +-- RequiresDependencyContract
    +-- RequiresContractOnTarget
    +-- RequiresContractOnScope

[Rule Engine]
    |
    +-- contract matching
    +-- alias / implication resolution
    +-- standard Roslyn suppression, exact requirement suppression, and owner-type exclusions
    +-- diagnostics
```

## 2. Core concepts

### Contract

An arbitrary string such as:

- `thread-safe`
- `retry-safe`
- `no-blocking`
- `tenant-aware`

### Provider

A type that provides one or more contracts.

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

### Consumer

A type that requires contracts from its dependencies.

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

### Target

A type category such as `repository` or `controller`.

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

### Scope

An architectural layer or code region such as `application` or `repository`.

```csharp
[ContractScope("application")]
public class OrderService
{
}
```

## 3. Final attribute set

### 3.1 Provided contracts

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }
    public ProvidesContractAttribute(string name) => Name = name;
}
```

### 3.2 Type-specific requirements

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

### 3.3 Target-based requirements

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    public string Name { get; }
    public ContractTargetAttribute(string name) => Name = name;
}
```

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

### 3.4 Scope-based requirements

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    public string Name { get; }
    public ContractScopeAttribute(string name) => Name = name;
}
```

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

### 3.5 Contract implication edges

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

Example:

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
```

Meaning:

`snapshot-cache` satisfies both `immutable` and `thread-safe`.

Current semantics:

- `child -> parent` is a contract implication edge
- repeated attributes allow multiple parents
- `ContractAlias` remains supported and is treated as the same implication edge model for backward compatibility
- alias and hierarchy edges are resolved in one combined DAG
- `DCA202` continues to report cycles across the combined graph
- contract satisfaction uses the transitive closure of that combined graph

This keeps existing alias behavior intact while providing a clearer API for multi-level and multi-parent contract hierarchies.

## 4. Rule evaluation precedence

Evaluation precedence inside the rule engine should be explicit:

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. implication graph resolution

This precedence is about how a dependency is evaluated, not about release order.

## 5. Dependency evaluation model

Initially, dependencies can stay limited to strong type relationships:

```text
Consumer Type
   +-- constructor parameter
   +-- method parameter
   +-- property
   +-- field
   +-- new expression
   +-- static member usage, including `using static` imports
   +-- base type
   +-- interface
```

For each dependency target, the engine reads:

```text
Dependency Type
   +-- ProvidesContract
   +-- ContractTarget
   +-- ContractScope
```

## 6. Examples

### 6.1 Type-specific requirement

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

Result: valid.

### 6.2 Target-based requirement

```csharp
[RequiresContractOnTarget("repository", "thread-safe")]
public class OrderService
{
    public OrderService(IUserRepository repository) {}
}
```

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

If `UserRepository` does not provide `thread-safe`, the analyzer reports a violation.

### 6.3 Scope-based requirement

```csharp
[ContractScope("application")]
[RequiresContractOnScope("repository", "retry-safe")]
public class BillingService
{
    public BillingService(IBillingRepository repository) {}
}
```

```csharp
[ContractScope("repository")]
public class BillingRepository : IBillingRepository
{
}
```

If `BillingRepository` does not provide `retry-safe`, the analyzer reports a violation.

### 6.4 Implication-based requirement

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
```

```csharp
[ProvidesContract("snapshot-cache")]
public class SnapshotStore : IStore
{
}
```

```csharp
[RequiresDependencyContract(typeof(IStore), "thread-safe")]
public class StoreConsumer
{
    public StoreConsumer(IStore store) {}
}
```

Result: valid because the implication graph satisfies the requirement.

## 7. Suggested internal model

Keep the analyzer architecture centered on explicit internal descriptors.

```csharp
internal sealed record ContractDescriptor(string Name);

internal sealed record TargetDescriptor(string Name);

internal sealed record ScopeDescriptor(string Name);

internal sealed record DependencyEdge(
    INamedTypeSymbol Consumer,
    INamedTypeSymbol Dependency,
    DependencyKind Kind);

internal sealed record RequirementDescriptor(
    RequirementKind Kind,
    string SubjectName,
    string ContractName,
    INamedTypeSymbol OwnerType);
```

### 7.1 RequirementKind

```csharp
internal enum RequirementKind
{
    DependencyType,
    Target,
    Scope
}
```

### 7.2 DependencyKind

```csharp
internal enum DependencyKind
{
    ConstructorParameter,
    MethodParameter,
    Property,
    Field,
    ObjectCreation,
    StaticMemberAccess,
    BaseType,
    InterfaceImplementation
}
```

## 8. Analyzer flow

```text
CompilationStart
   |
   +-- resolve attribute symbols
   |
   +-- extract per-type metadata
   |     +-- provided contracts
   |     +-- targets
   |     +-- scopes
   |     +-- requirements
   |
   +-- extract dependency edges
   |
   +-- evaluate with rule engine
   |
   +-- emit diagnostics
```

## 9. Rule engine shape

The core mental model is:

```text
Evaluate(consumer, dependency) -> violations
```

Inside that evaluation:

1. Enumerate the consumer requirements.
2. Decide whether the dependency matches the requirement subject.
3. Check whether the dependency satisfies the required contract.
4. Apply implication-graph resolution when needed.
5. Emit diagnostics if the contract is still not satisfied.

## 10. Diagnostic family

### Base diagnostics

- `DCA001`: dependency does not provide a required contract
- `DCA002`: declared dependency type is not used

### Contract-definition diagnostics

- `DCA100`: empty contract name
- `DCA101`: contract naming format violation
- `DCA102`: duplicate contract declaration

`DCA101` applies only to contract names, requirement-suppression contract arguments, and alias or hierarchy endpoints. It does not apply to target names or scope names, and the enforced v1 format is lower-kebab-case.

### Rule-definition diagnostics

- `DCA200`: unknown target required
- `DCA201`: unknown scope required
- `DCA202`: cyclic implication definition
- `DCA203`: empty scope name
- `DCA204`: empty target name
- `DCA205`: unused target requirement
- `DCA206`: unused scope requirement

## 11. Why this is strong as OSS

With this design, the tool goes beyond DI-specific checks.

It can express:

- dependency-level contract validation
- architecture-layer rules
- category-level design rules
- team-specific conventions
- future ArchUnit-like extensions

The package name can stay `DependencyContractAnalyzer`, but the design direction is much closer to an architecture analyzer platform.
