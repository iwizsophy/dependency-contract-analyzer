# Trusted Publishing Guide

This document describes the recommended release model for publishing `DependencyContractAnalyzer` to NuGet feeds.

## Recommended approach

Use NuGet Trusted Publishing with GitHub Actions and OpenID Connect instead of long-lived API keys.

## Setup checklist

1. Reserve or create the `DependencyContractAnalyzer` package on nuget.org.
2. Reserve or create the `DependencyContractAnalyzer` package on `int.nugettest.org` if `develop` publishes should push there.
3. Add this GitHub repository as a Trusted Publisher for the nuget.org package.
4. Add this GitHub repository as a Trusted Publisher for the `int.nugettest.org` package if `develop` publishes are enabled.
5. Register the `publish.yml` workflow file in the nuget.org Trusted Publishing policy. Use the workflow file name only: `publish.yml`.
6. Register the same `publish.yml` workflow file in the `int.nugettest.org` Trusted Publishing policy if `develop` publishes are enabled.
7. Configure the repository variable `NUGET_PUBLISH_USER` with the nuget.org account name that is allowed to publish the package.
8. Configure the repository variable `NUGETTEST_PUBLISH_USER` with the `int.nugettest.org` account name that is allowed to publish the package.
9. Ensure the publish workflow keeps `permissions.id-token: write`.
10. Create and push an annotated release tag using the format
   `v<major>.<minor>.<patch>`, such as `v0.1.0`.
11. Package and assembly versions are resolved from git tags by `RelaxVersioner`.

## Workflow expectations

- The repository includes `.github/workflows/ci.yml` for `restore`, `build`, `test`, and `pack` validation on push and pull request.
- The repository includes `.github/workflows/publish.yml` for tag-based release publishing and branch-based manual publish/validation runs.
- The publish workflow authenticates using `NuGet/login@v1`.
- Avoid storing long-lived API keys in repository secrets when OIDC can be used.
- Build, test, and pack should run before pushing packages.
- The publish workflow expects `NUGET_PUBLISH_USER` and `NUGETTEST_PUBLISH_USER` to be configured as repository variables for their respective targets.
- Manual `workflow_dispatch` runs are allowed only on `develop` and `main`.
- Manual runs from `develop` publish packages to `https://int.nugettest.org/api/v2/package`.
- Manual runs from `main` are validation-only, may build/test/pack/upload artifacts, and do not publish packages.
- Published release tags must point to commits already merged into `main`.
- The publish workflow validates that release tags are annotated tags.
- Pushes of annotated release tags publish packages to `https://www.nuget.org/api/v2/package`.
- GitHub Releases should be created from release tags on `main`.
- Release notes should reference `CHANGELOG.md`.

## Branching model

- `main`: default branch and stable release branch for `nuget.org` publishing
- `develop`: integration branch and `int.nugettest.org` manual publish branch

## Release checklist

- `CHANGELOG.md` updated
- translation follow-up checked
- docs-sync checked
- `THIRD-PARTY-NOTICES.md` updated when dependency changes are included
- transitive dependency audit completed and recorded in the monthly
  dependency audit issue
- breaking-change issues reviewed
- release Pull Request merged into `main`
- stable version confirmed
- annotated tag created from `main`
