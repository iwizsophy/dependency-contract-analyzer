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
7. Configure the repository secret `NUGET_USER` with the account name that is allowed to publish the package on both nuget.org and `int.nugettest.org`.
8. Ensure the publish workflow keeps `permissions.id-token: write` and `permissions.contents: write`.
9. Create and push an annotated release tag using the format
   `v<major>.<minor>.<patch>`, such as `v0.1.0`.
10. Package and assembly versions are resolved from git tags by `RelaxVersioner`.

## Workflow expectations

- The repository includes `.github/workflows/ci.yml` for `restore`, `build`, `test`, and `pack` validation on push and pull request.
- The repository includes `.github/workflows/publish.yml` for tag-based release publishing and branch-based manual publish/validation runs.
- The publish workflow authenticates using `NuGet/login@v1`.
- Avoid storing long-lived API keys in repository secrets when OIDC can be used.
- Build, test, and pack should run before pushing packages.
- The publish workflow expects a single repository secret `NUGET_USER`, and it uses the same publishing account name for both nuget.org and `int.nugettest.org`.
- Manual `workflow_dispatch` runs are allowed only on `develop` and `main`.
- Manual runs from `develop` publish packages to `https://int.nugettest.org/api/v2/package`.
- Manual runs from `main` publish packages to `https://www.nuget.org/api/v2/package`.
- The publish workflow validates that release tags are annotated tags.
- Tag pushes publish by branch instead of by trigger type: `main` tags publish to `https://www.nuget.org/api/v2/package`, and `develop` tags publish to `https://int.nugettest.org/api/v2/package`.
- Tag pushes fail when the tagged commit is reachable from both `main` and `develop`, or from neither branch.
- The publish-time pack step disables RelaxVersioner's working-directory dirty check so generated build outputs do not silently bump the package version.
- Tag pushes verify that the generated `.nupkg` filename matches the release tag version before upload.
- After a successful `main` tag publish, the workflow creates or updates the matching GitHub Release from that tag.
- `develop` tag publishes and manual `workflow_dispatch` runs do not create GitHub Releases.
- GitHub Release notes are generated from the matching `## [<version>]` section in `CHANGELOG.md`.
- After a release Pull Request is merged into `main`, re-synchronize `develop` with `main` before starting the next development cycle.
- Prefer a non-destructive synchronization Pull Request from `main` into `develop` instead of rewriting protected branch history.
- If GitHub branch protection requires Pull Request heads to be up to date with the base branch, skipping this re-sync can block the next release Pull Request from `develop` into `main`.

## Branching model

- `main`: default branch and stable release branch for `nuget.org` publishing
- `develop`: integration branch and `int.nugettest.org` publish branch

## Release checklist

- `CHANGELOG.md` updated
- translation follow-up checked
- docs-sync checked
- `THIRD-PARTY-NOTICES.md` updated when dependency changes are included
- transitive dependency audit completed and recorded in the monthly
  dependency audit issue
- breaking-change issues reviewed
- release Pull Request merged into `main`
- `develop` re-synchronized with `main` after the release merge
- stable version confirmed
- publish target branch confirmed (`main` for nuget.org or `develop` for `int.nugettest.org`)
- annotated tag created from the intended publish branch
- `CHANGELOG.md` contains a `## [<version>]` section that matches the release tag
