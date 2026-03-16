# Third-Party Notices

This file tracks all direct third-party dependencies used by this
repository, including runtime, build-time, and development-time
dependencies. Transitive dependencies are not listed by default.

Transitive dependencies are audited at least monthly and before each
release using `dotnet list package --include-transitive`, Dependabot,
and GitHub security advisories. They are not listed here by default
unless explicit notice is required.

## RelaxVersioner

- Project: https://github.com/kekyo/RelaxVersioner
- Package: `RelaxVersioner` (NuGet)
- License: Apache License 2.0 (`Apache-2.0`)
- License text: https://www.apache.org/licenses/LICENSE-2.0
- Copyright:
  Copyright (c) Kouji Matsui

Usage note:

- `RelaxVersioner` is used as a build-time/development dependency to resolve package and assembly versions from git tags.
- It is referenced with `PrivateAssets="all"` and is not redistributed as part of this package.
