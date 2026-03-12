# Trusted Publishing Guide

This document describes the recommended release model for publishing `DependencyContractAnalyzer` to NuGet feeds.

## Recommended approach

Use NuGet Trusted Publishing with GitHub Actions and OpenID Connect instead of long-lived API keys.

## Setup checklist

1. Reserve or create the `DependencyContractAnalyzer` package on nuget.org.
2. Add this GitHub repository as a Trusted Publisher for the package.
3. If you also validate prereleases on nugettest.org, register the same workflow there.
4. Ensure the publish workflow keeps `permissions.id-token: write`.
5. Create and push a version tag such as `v0.1.0`.
6. Publish packages from a dedicated release workflow, typically `.github/workflows/publish.yml`.
7. Package and assembly versions are resolved from git tags by `RelaxVersioner`.

## Workflow expectations

- The workflow should authenticate using NuGet trusted publishing or `NuGet/login@v1`.
- Avoid storing long-lived API keys in repository secrets when OIDC can be used.
- Build, test, and pack should run before pushing packages.
- Release notes should reference `CHANGELOG.md`.

## Branching suggestion

- `main`: stable releases to nuget.org
- `develop` or equivalent: optional prerelease validation to nugettest.org

Adapt the branching model if the repository uses a different release strategy.
