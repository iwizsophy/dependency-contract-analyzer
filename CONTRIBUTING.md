# Contributing

Thanks for your interest in contributing to DependencyContractAnalyzer.

## Before you start

- For contributor-facing changes, consult governance documents in this order:
  `SPECIFICATION.md`, `ARCHITECTURE.md`, `DECISIONS.md`, `AGENTS.md`.
- Use GitHub in this order when needed: Discussion for exploration, Issue for
  decisions, Pull Request for implementation. Issue is always a valid starting
  point when the decision surface is already clear.
- Open an issue for large changes, public API changes, visible behavior changes,
  design decisions, governance document changes, bug reports, or analyzer-rule
  proposals.
- Editorial-only governance document changes do not require an Issue, but they
  still require maintainer review.
- Keep changes focused and small when possible.
- The GitHub default branch is `main`.
- The standard Pull Request target for `feature/*`, `bugfix/*`, and
  `chore/*` work is `develop`.
- Use this branch flow:
  `feature/* -> develop`, `bugfix/* -> develop`, `chore/* -> develop`,
  and `develop -> main` for releases.
- Direct pushes to `main` and `develop` are not allowed.
- For analyzer behavior changes, include or update tests and the relevant
  specification documents.
- High-impact specification or public API changes should update
  `SPECIFICATION.md` and `DECISIONS.md` in the same change whenever practical.
- Pre-release breaking changes must also update `CHANGELOG.md` under a
  `## Breaking Changes` section.
- Release Pull Requests from `develop` into `main` must use merge commits.
- Feature and bug-fix Pull Requests into `develop` should use squash merges.
- Release tags must be annotated tags using the format
  `v<major>.<minor>.<patch>`.
- Tag publishes are routed by branch intent: tagged commits reachable
  only from `main` publish to `nuget.org`, and tagged commits reachable
  only from `develop` publish to `https://int.nugettest.org/`.
- Tag publishes fail when the tagged commit is reachable from both
  `main` and `develop`, or from neither branch.
- GitHub Releases are created from release tags on `main`.
- Manual publish workflow dispatches may run only for `develop` and
  `main`.
- Manual dispatches from `develop` publish to `https://int.nugettest.org/`.
- Manual dispatches from `main` publish to `https://www.nuget.org/`.
- The required CI checks are `build`, `test`, `analyzer`, and `pack`.
- New third-party dependency additions require an Issue.
- Major dependency updates or dependency replacements require an Issue.
- `THIRD-PARTY-NOTICES.md` must be updated in the same Pull Request when a
  dependency is added or updated.
- `THIRD-PARTY-NOTICES.md` tracks all direct third-party dependencies,
  including runtime, build-time, and development-time dependencies.
- Transitive dependencies are audited with
  `dotnet list package --include-transitive`, Dependabot, and GitHub
  security advisories instead of being listed in
  `THIRD-PARTY-NOTICES.md` by default.
- Run transitive dependency audits at least monthly and before each
  release.
- Maintainers own the monthly transitive dependency audit and should
  record results in a dedicated GitHub Issue, typically `Monthly
  Dependency Audit`, by adding monthly comments.
- Dependabot should target `develop` for weekly NuGet and GitHub Actions
  update proposals.
- Dependabot Pull Requests require normal CI, maintainer review, and
  manual merge. Auto-merge is not used.
- Permissive licenses such as MIT, Apache-2.0, and BSD are generally
  acceptable. Copyleft or restricted licenses require explicit
  maintainer review.
- Final technical decisions remain with the maintainers and should be recorded
  in GitHub for high-impact changes.

## Development workflow

1. Fork the repository and create a topic branch.
2. Open a Discussion when the work needs design exploration.
3. Open or update an Issue when the work changes behavior, architecture, or
   other tracked governance decisions.
4. Use an Issue titled `ADR proposal: <title>` when proposing an ADR.
5. Update the relevant specification, architecture, or decision records when
   the change affects them.
6. If you introduce a temporary fix for an urgent issue, obtain maintainer or
   reviewer confirmation and create the follow-up Issue in the same Pull
   Request.
7. Implement the change with matching analyzer tests or documentation updates.
8. Run local validation from the repository root once the source projects are present:
   - `dotnet restore`
   - `dotnet build -c Release --no-restore`
   - `dotnet test -c Release --no-build`
9. Submit a pull request with:
   - What changed
   - Why it changed
   - Validation results

If a direct push reaches `main` or `develop`, revert it and reapply the
change through the normal Pull Request flow unless a maintainer has
explicitly approved the direct push as an emergency and documented the
reason in an Issue or Pull Request comment.

## Project expectations

- Prefer explicit symbol handling and `SymbolEqualityComparer.Default`.
- Minimize unnecessary allocations in analyzer code.
- Use `ImmutableArray` where appropriate.
- Compare contract names with `StringComparison.OrdinalIgnoreCase` after trimming.

## Additional docs

- Governance summary specification: `SPECIFICATION.md`
- Governance architecture principles: `ARCHITECTURE.md`
- Architecture decision records: `DECISIONS.md`
- Repository workflow and quality rules: `AGENTS.md`
- Detailed product specification: `docs/specification.md`
- Detailed architecture: `docs/architecture.md`
- Development details: `docs/development.md`
- Trusted Publishing: `docs/trusted-publishing.md`
- Code of Conduct: `CODE_OF_CONDUCT.md`
- Support policy: `.github/SUPPORT.md`
