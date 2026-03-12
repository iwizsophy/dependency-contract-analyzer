# Third-Party Notices

This repository uses the following third-party component during build/versioning:

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
