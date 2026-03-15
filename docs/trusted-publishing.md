# Trusted Publishing Guide

This document describes the recommended release model for publishing `DependencyContractAnalyzer` to NuGet feeds.

## Recommended approach

Use NuGet Trusted Publishing with GitHub Actions and OpenID Connect instead of long-lived API keys.

## Setup checklist

1. Reserve or create the `DependencyContractAnalyzer` package on nuget.org.
2. Add this GitHub repository as a Trusted Publisher for the package.
3. Register the `publish.yml` workflow file in the nuget.org Trusted Publishing policy. Use the workflow file name only: `publish.yml`.
4. Configure the repository variable `NUGET_PUBLISH_USER` with the nuget.org account name that is allowed to publish the package.
5. If you validate publishing workflow behavior on nugettest.org, register the same workflow there. This is optional validation and not part of the normal release flow.
6. Ensure the publish workflow keeps `permissions.id-token: write`.
7. Create and push an annotated release tag using the format
   `v<major>.<minor>.<patch>`, such as `v0.1.0`.
8. Package and assembly versions are resolved from git tags by `RelaxVersioner`.

## Workflow expectations

- The repository includes `.github/workflows/ci.yml` for `restore`, `build`, `test`, and `pack` validation on push and pull request.
- The repository includes `.github/workflows/publish.yml` for tag-based release publishing and manual validation runs.
- The publish workflow authenticates using `NuGet/login@v1`.
- Avoid storing long-lived API keys in repository secrets when OIDC can be used.
- Build, test, and pack should run before pushing packages.
- The publish workflow expects `NUGET_PUBLISH_USER` to be configured as a repository variable.
- Manual `workflow_dispatch` runs are validation-only, may build/test/pack/upload artifacts, and do not publish packages.
- Manual validation runs are allowed only on `develop` and `main`.
- Published release tags must point to commits already merged into `main`.
- Annotated tags are required immediately. Workflow-level validation may
  be added later as separately tracked automation.
- `nugettest.org` may be used for publishing workflow validation, but it is not part of the normal release flow. Official releases are published only to `nuget.org` from stable tags on `main`.
- GitHub Releases should be created from release tags on `main`.
- Release notes should reference `CHANGELOG.md`.

## Branching model

- `develop`: default branch and integration branch
- `main`: stable release branch for nuget.org publishing

`master` should be retired after branch migration is completed and all
references are removed.

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
