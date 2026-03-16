# DependencyContractAnalyzer Sample

This sample is a minimal consumer project that references the local analyzer project directly.
The project itself is intended to build without `DependencyContractAnalyzer` warnings.

## What it demonstrates

- `ProvidesContract` + `RequiresDependencyContract`
- `ContractTarget` + `RequiresContractOnTarget`
- `ContractScope` + `RequiresContractOnScope`
- `ContractHierarchy`
- dependency extraction through constructor parameters, method parameters, property types, `new` expressions, and static member usage

## Build

```powershell
dotnet build samples/DependencyContractAnalyzer.Sample/DependencyContractAnalyzer.Sample.csproj
```

## Representative invalid snippets

The sample README keeps a few minimal invalid snippets here instead of shipping them in the runnable project.

`DCA001` through a method parameter:

```csharp
public interface IUnreliableClock
{
    int ReadHour();
}

[RequiresDependencyContract(typeof(IUnreliableClock), "thread-safe")]
public sealed class InvalidMethodParameterExample
{
    public void Execute(IUnreliableClock clock)
    {
    }
}
```

`DCA001` through a target requirement:

```csharp
[ContractTarget("repository")]
public sealed class SlowRepository
{
}

[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class InvalidTargetExample
{
    public SlowRepository Create() => new();
}
```
