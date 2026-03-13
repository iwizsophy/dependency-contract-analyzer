# Trusted Publishing Guide

This document describes the recommended release model for publishing `DependencyContractAnalyzer` to NuGet feeds.

## Recommended approach

Use NuGet Trusted Publishing with GitHub Actions and OpenID Connect instead of long-lived API keys.

## Setup checklist

1. Reserve or create the `DependencyContractAnalyzer` package on nuget.org.
2. Add this GitHub repository as a Trusted Publisher for the package.
3. Register the `publish.yml` workflow file in the nuget.org Trusted Publishing policy. Use the workflow file name only: `publish.yml`.
4. Configure the repository variable `NUGET_PUBLISH_USER` with the nuget.org account name that is allowed to publish the package.
5. If you also validate prereleases on nugettest.org, register the same workflow there.
6. Ensure the publish workflow keeps `permissions.id-token: write`.
7. Create and push a version tag such as `v0.1.0`.
8. Package and assembly versions are resolved from git tags by `RelaxVersioner`.

## Workflow expectations

- The repository includes `.github/workflows/ci.yml` for `restore`, `build`, `test`, and `pack` validation on push and pull request.
- The repository includes `.github/workflows/publish.yml` for tag-based or manual release publishing.
- The publish workflow authenticates using `NuGet/login@v1`.
- Avoid storing long-lived API keys in repository secrets when OIDC can be used.
- Build, test, and pack should run before pushing packages.
- The publish workflow expects `NUGET_PUBLISH_USER` to be configured as a repository variable.
- Release notes should reference `CHANGELOG.md`.

## Branching suggestion

- `main`: stable releases to nuget.org
- `develop` or equivalent: optional prerelease validation to nugettest.org

Adapt the branching model if the repository uses a different release strategy.
