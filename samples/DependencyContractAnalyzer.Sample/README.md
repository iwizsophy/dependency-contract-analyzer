# DependencyContractAnalyzer Sample

This sample is a minimal consumer project that references the local analyzer project directly.

## What it demonstrates

- `ProvidesContract` + `RequiresDependencyContract`
- `ContractTarget` + `RequiresContractOnTarget`
- `ContractScope` + `RequiresContractOnScope`
- `ContractAlias`
- dependency extraction through constructor parameters, method parameters, property types, `new` expressions, and static member usage

## Representative diagnostics

Building the sample intentionally reports these warnings:

- `InvalidMethodParameterExample`: `DCA001`
- `InvalidTargetExample`: `DCA001`

The remaining example types are expected to build without `DependencyContractAnalyzer` diagnostics.

## Build

```powershell
dotnet build samples/DependencyContractAnalyzer.Sample/DependencyContractAnalyzer.Sample.csproj
```
