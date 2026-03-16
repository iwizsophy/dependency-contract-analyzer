# Third-Party Notices

This file tracks all direct third-party dependencies used by this
repository, including runtime, build-time, development-time, and
workflow dependencies. Transitive dependencies are not listed by
default.

Transitive dependencies are audited at least monthly and before each
release using `dotnet list package --include-transitive`, Dependabot,
and GitHub security advisories. They are not listed here by default
unless explicit notice is required.

## NuGet Packages

### RelaxVersioner

- Dependency: `RelaxVersioner` (NuGet)
- Version: `3.21.0`
- Project: https://github.com/kekyo/RelaxVersioner
- License: Apache License 2.0 (`Apache-2.0`)
- License text: https://licenses.nuget.org/Apache-2.0
- Copyright:
  Copyright (c) Kouji Matsui
- Usage note: Used as a build-time dependency to resolve package and
  assembly versions from git tags.
- Usage note: Referenced with `PrivateAssets="all"` and not published as
  a package dependency.

### Microsoft.CodeAnalysis.Analyzers

- Dependency: `Microsoft.CodeAnalysis.Analyzers` (NuGet)
- Version: `5.3.0`
- Project: https://github.com/dotnet/roslyn
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) Microsoft Corporation. All rights reserved.
- Usage note: Used as a development-time analyzer dependency for the
  analyzer project build.
- Usage note: Referenced with `PrivateAssets="all"` and not published as
  a package dependency.

### Microsoft.CodeAnalysis.CSharp

- Dependency: `Microsoft.CodeAnalysis.CSharp` (NuGet)
- Version: `5.3.0`
- Project: https://github.com/dotnet/roslyn
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) Microsoft Corporation. All rights reserved.
- Usage note: Used to compile the Roslyn analyzer implementation.
- Usage note: Package dependency metadata is suppressed when packing the
  published analyzer package.

### coverlet.collector

- Dependency: `coverlet.collector` (NuGet)
- Version: `8.0.0`
- Project: https://github.com/coverlet-coverage/coverlet
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) 2018 Toni Solarin-Sodara
- Usage note: Used only by the test project for local and CI coverage
  collection.
- Usage note: Referenced with `PrivateAssets="all"` and not published as
  a package dependency.

### Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit

- Dependency:
  `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit` (NuGet)
- Version: `1.1.2`
- Project: https://github.com/dotnet/roslyn-sdk
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) Microsoft Corporation. All rights reserved.
- Usage note: Provides the Roslyn analyzer test harness used by the unit
  test project.

### Microsoft.NET.Test.Sdk

- Dependency: `Microsoft.NET.Test.Sdk` (NuGet)
- Version: `17.14.1`
- Project: https://github.com/microsoft/vstest
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) Microsoft Corporation. All rights reserved.
- Usage note: Provides the .NET test host and test project build
  integration for the unit test project.

### xunit

- Dependency: `xunit` (NuGet)
- Version: `2.4.2`
- Project: https://github.com/xunit/xunit
- License: Apache License 2.0 (`Apache-2.0`)
- License text: https://licenses.nuget.org/Apache-2.0
- Copyright:
  Copyright (c) .NET Foundation
- Usage note: Primary test framework used by the unit test project.

### xunit.runner.visualstudio

- Dependency: `xunit.runner.visualstudio` (NuGet)
- Version: `2.4.5`
- Project: https://github.com/xunit/visualstudio.xunit
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Copyright:
  Copyright (c) .NET Foundation and Contributors.
- Usage note: Visual Studio and `dotnet test` runner integration for the
  unit test project.
- Usage note: Referenced with `PrivateAssets="all"` and not published as
  a package dependency.

## GitHub Actions

### actions/checkout

- Dependency: `actions/checkout` (GitHub Action)
- Version: `v6`
- Project: https://github.com/actions/checkout
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Usage note: Used in CI and publish workflows to fetch repository
  contents.

### actions/setup-dotnet

- Dependency: `actions/setup-dotnet` (GitHub Action)
- Version: `v5`
- Project: https://github.com/actions/setup-dotnet
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Usage note: Used in CI and publish workflows to install required .NET
  SDK and runtime versions.

### actions/upload-artifact

- Dependency: `actions/upload-artifact` (GitHub Action)
- Version: `v7`
- Project: https://github.com/actions/upload-artifact
- License: MIT License (`MIT`)
- License text: https://licenses.nuget.org/MIT
- Usage note: Used in CI and publish workflows to persist test results
  and package artifacts.

### NuGet/login

- Dependency: `NuGet/login` (GitHub Action)
- Version: `v1`
- Project: https://github.com/NuGet/login
- License: Apache License 2.0 (`Apache-2.0`)
- License text: https://licenses.nuget.org/Apache-2.0
- Usage note: Used in the publish workflow for NuGet Trusted Publishing
  authentication.
