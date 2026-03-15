# Agent Contract

All AI coding agents working in this repository MUST follow these rules:

1.  Do NOT implement behavior based on assumptions.
2.  Large changes MUST be tracked in a GitHub Issue before
    implementation.
3.  Code MUST NOT be merged without tests.
4.  All analyzer diagnostics MUST be resolved (Error / Warning / Info).
5.  Public API changes MUST clearly describe their impact.
6.  Do NOT introduce local fixes that break overall architecture.
7.  Names MUST be clear and understandable when read later.
8.  When code changes, update XML documentation and related documents.
9.  Before modifying more than 3 files, the agent MUST explain the
    change plan and impacted components.
10. The agent MUST NOT perform opportunistic refactoring outside the
    requested scope unless it is required for correctness, testability,
    or architectural consistency.
11. Any refactoring outside the requested scope MUST be justified
    explicitly before implementation.

If governance documents conflict, follow this order:

SPECIFICATION.md ↓ ARCHITECTURE.md ↓ DECISIONS.md ↓ AGENTS.md

Within that framework, use this implementation decision order:

Specification correctness ↓ Architecture consistency ↓ Maintainability
↓ Readability ↓ Testability ↓ Simplicity

AGENTS.md defines workflow and quality rules. It does not override the
specification or architecture.

------------------------------------------------------------------------

# AGENTS.md

## Principles

### Quality First

Do not settle for quick fixes. Investigate problems thoroughly and aim
for the best long-term solution.

Avoid temporary workarounds when a proper design solution exists.

### Architecture First

All implementations must maintain consistency with the overall
architecture.

Avoid:

-   "Just make it work" implementations
-   Breaking separation of concerns
-   Special-case logic proliferation
-   Temporary hacks

Refactor existing code when necessary to maintain architectural
integrity.

### Compatibility

This project is **pre-release**.

Backward compatibility is **not required**.

Correct design takes priority over compatibility.

------------------------------------------------------------------------

# Workflow

Development should generally follow this sequence:

1.  Understand the problem
2.  Identify the impact scope
3.  Create a GitHub Issue if needed
4.  Design the solution
5.  Document the design in the Issue
6.  Write tests
7.  Implement the solution
8.  Refactor if needed
9.  Run tests
10. Update documentation
11. Record results in the Issue

High-impact specification changes should update `SPECIFICATION.md` and
`DECISIONS.md` in the same change whenever practical.

When fixing behavior that is not yet specified, decide the desired
behavior first, update the specification, and then implement the fix in
the same change whenever practical. Urgent temporary fixes require
maintainer or reviewer confirmation, and a follow-up Issue for the
missing specification update MUST be created in the same Pull Request.

Use GitHub in this order for design work:

1.  Discussion for exploration when needed
2.  Issue for decisions and tracked work
3.  Pull Request for implementation

Issue is always a valid entry point when the decision surface is already
clear.

------------------------------------------------------------------------

# Branch Strategy

The repository branch roles are:

-   `main`: release branch
-   `develop`: integration branch
-   `feature/*`: feature work
-   `bugfix/*`: bug-fix work
-   `chore/*`: maintenance work

Default branch flow:

-   `feature/*` -> `develop`
-   `bugfix/*` -> `develop`
-   `chore/*` -> `develop`
-   `develop` -> `main` for releases

The GitHub default branch should be `main`.

Direct pushes to `main` or `develop` are not allowed. All changes must
be merged via Pull Requests with at least one approval and passing
checks.

------------------------------------------------------------------------

# Release Flow

Releases must use a Pull Request from `develop` into `main`.

-   `develop` -> `main` release Pull Requests use merge commits
-   feature and bug-fix Pull Requests into `develop` use squash merges
-   release tags must be annotated tags using the format
    `v<major>.<minor>.<patch>`
-   release tags must be created only from commits already merged into
    `main`
-   publish operations must originate from `main` tags
-   GitHub Releases are created from release tags on `main`
-   manual publish workflow dispatches are validation-only and may run
    only for `develop` and `main`
-   publish workflow validation enforces annotated release tags

The current required status check is `build-test-pack`. If CI is split
into multiple jobs later, update the required-check policy at the same
time.

If a direct push reaches `main` or `develop`, revert it and reintroduce
the change through the normal Pull Request flow unless a maintainer has
explicitly approved the direct push as an emergency. Emergency
exceptions must be documented in an Issue or Pull Request comment.

------------------------------------------------------------------------

# Large Change Rule

A change is considered **large** if any of the following applies:

-   it changes a public API or externally visible contract
-   it changes architecture or responsibility boundaries
-   it spans multiple layers
-   it affects naming or structure across a wide scope
-   it is expected to modify **7 or more files**

Documentation-only or translation-only changes are exempt from the
7-file threshold unless another large-change condition applies.

Changes to `AGENTS.md`, `SPECIFICATION.md`, `ARCHITECTURE.md`, or
`DECISIONS.md` are not treated as documentation-only exceptions.

Changes affecting **4 to 6 files require a written change plan before
implementation**, even when they are not classified as large.

Large changes MUST NOT be implemented directly.

Instead:

1.  Create a GitHub Issue
2.  Document the impact scope
3.  Propose the implementation plan
4.  Record the decision in the Issue

Large changes require maintainer confirmation recorded in GitHub before
implementation begins.

------------------------------------------------------------------------

# Issue Management

## Progress Tracking

Milestones track overall progress.

Issues track individual work items.

## Issues MUST be created for

-   large changes
-   public API changes
-   design decisions
-   specification clarifications
-   deferred work
-   behavior changes visible to users
-   governance document changes

Issues are optional for narrowly scoped bug fixes, test-only changes,
documentation-only changes, and minor internal renames unless
non-trivial judgment is required.

Editorial-only changes to governance documents do not require a GitHub
Issue, but they still require maintainer review.

An Issue is required whenever any of the following applies:

-   multiple reasonable solutions exist
-   behavior must be chosen or clarified
-   architecture direction must be chosen
-   naming rules must be decided
-   public API is affected
-   diagnostics or configuration semantics are affected

When a change also requires an ADR, update `DECISIONS.md` as part of the
same work unless the ADR is intentionally left in draft form.

Normal changes require maintainer approval recorded in GitHub before
merge.

## Issue Completion Notes

When work is completed, record:

-   What was implemented
-   Design decisions
-   Tests added
-   Impact scope
-   Remaining tasks

Issues serve as **design history**.

Completion notes may be recorded in:

-   the Issue thread
-   the Pull Request description
-   the closing Pull Request comment

------------------------------------------------------------------------

# Decision Records

Significant design decisions MUST be recorded in `DECISIONS.md`.

## ADR Required

An ADR is required when the change affects any of the following:

-   external contracts or public API meaning
-   diagnostic IDs, default severities, or configuration semantics
-   `.editorconfig` keys or policy defaults
-   naming rules or dependency rules
-   architecture boundaries or structural constraints
-   test strategy or repository-wide policy
-   breaking-change direction

## ADR Optional

An ADR is optional for:

-   typo fixes
-   documentation wording improvements without policy impact
-   narrow internal cleanup within existing rules
-   obvious implementation of already-specified behavior

## ADR Rules

-   assign `ADR-###` numbers only when the ADR is accepted
-   numbering is assigned by a maintainer in allocation order
-   one decision = one section
-   keep `DECISIONS.md` append-only whenever practical
-   mark outdated decisions as **Superseded** instead of deleting them
-   accepted ADRs may precede a specification update, but they do not
    change the specification by themselves
-   only accepted ADRs are recorded in `DECISIONS.md`

ADR proposals must be tracked in a GitHub Issue, typically titled
`ADR proposal: <title>`.

A specification change becomes effective only when `SPECIFICATION.md` is
updated.

ADR acceptance is maintainer-only. The ADR status field must be updated
to `Accepted`, the `Specification:` field must show `Updated` or
`Pending update`, and the acceptance must be recorded in GitHub through
a maintainer Issue comment, Pull Request review or comment, or
Discussion comment.

------------------------------------------------------------------------

# Coding Standards

## Language

Primary language: **C#**

Follow common C# practices for design, naming, exception handling, and
testing.

## Naming

Names must clearly communicate meaning when read later.

Avoid names such as:

-   tmp
-   data
-   obj
-   val
-   util
-   manager

Names should reveal:

-   purpose
-   responsibility
-   domain meaning

### Naming Rule

Contract names MUST use **lower-kebab-case**.

Target and scope names may use domain-appropriate naming but MUST remain
consistent within their domain.

## Responsibility

Classes and methods should follow the **single responsibility
principle**.

Avoid:

-   overly large classes
-   large methods
-   excessive static helpers

## Implementation Quality

Avoid:

-   magic numbers
-   temporary hacks
-   unused code
-   swallowed exceptions
-   unclear branching logic

------------------------------------------------------------------------

# Documentation

## XML Documentation

Use **C# XML documentation comments**.

Required for:

-   public classes
-   public methods
-   public properties
-   important internal APIs

Example:

``` csharp
/// <summary>
/// Resolves service lifetime dependencies.
/// </summary>
```

## Documentation Consistency

When code changes, verify and update related documentation:

-   README
-   specification documents
-   architecture documents
-   `DECISIONS.md` when rationale or policy changes
-   XML documentation
-   comments

Documentation and code must remain consistent.

Specification changes MUST update both `SPECIFICATION.md` and
`docs/specification.md` unless the change is editorial only.

Architecture principle changes MUST update `docs/architecture.md` when
relevant.

Editorial changes MUST NOT alter semantics, behavior, contracts, or
interpretation.

Editorial-only examples:

-   typo fixes
-   grammar corrections
-   wording clarifications that preserve meaning
-   heading reorganization
-   table reordering
-   formatting-only changes
-   examples that only illustrate already-specified behavior

Non-editorial examples:

-   examples that define new behavior
-   ambiguity resolution that establishes new meaning
-   behavior description changes
-   default-value description changes
-   diagnostic description changes

### Documentation Source Rule

English documentation is the **source of truth**.

Source documentation MUST be updated in the same change. Translations
may follow later but important translation updates MUST be tracked in
GitHub.

Translation follow-up Issues are required for public API changes,
diagnostic changes, configuration changes, and other visible behavior
changes when the translated documents are not updated in the same
change.

Use the `translation` and `docs-sync` labels for translation follow-up
Issues. Translation follow-up Issues must be completed before the next
release.

Important contract rules may appear in both root governance documents
and detailed `docs/` documents. Keep the root documents concise and use
`docs/` for expanded explanation, examples, and walkthroughs.

------------------------------------------------------------------------

# Third-Party Dependencies

New third-party dependencies require a GitHub Issue before they are
added.

Major third-party dependency updates or dependency replacements also
require a GitHub Issue. Patch and minor dependency updates may proceed
without an Issue unless they also trigger other Issue-required rules.

When a third-party dependency is added or updated,
`THIRD-PARTY-NOTICES.md` must be updated in the same Pull Request.

`THIRD-PARTY-NOTICES.md` tracks all direct third-party dependencies used
by the repository, including runtime, build-time, and development-time
dependencies. Direct references with `PrivateAssets="all"` are still
included. Transitive dependencies are not listed there by default.

Transitive dependencies must be audited with
`dotnet list package --include-transitive`, Dependabot, and GitHub
security advisories at least monthly and before each release.
Significant license or security concerns in transitive dependencies must
be tracked in GitHub.

The monthly transitive dependency audit is a maintainer responsibility.
Record monthly and release-preparation audit results in a dedicated
GitHub Issue, typically titled `Monthly Dependency Audit`, using
follow-up comments instead of creating a new Issue each month.

Dependabot should be configured for NuGet and GitHub Actions with a
weekly schedule against `develop`.

Dependabot Pull Requests follow the normal Pull Request rules:

-   passing CI is required
-   maintainer review is required
-   automatic merge is not allowed

Permissive licenses such as MIT, Apache-2.0, BSD-2-Clause, and
BSD-3-Clause are generally acceptable. Copyleft or restricted licenses
require explicit maintainer review and approval.

------------------------------------------------------------------------

# Testing

## Tests Are Mandatory

Code without tests is considered incomplete.

## Test Rule

For bug fixes, add a failing test first whenever practical.

For new behavior, create tests from the specification before or
alongside implementation.

All changes MUST include appropriate regression coverage.

## Test Quality

Tests should verify:

-   behavior
-   specifications
-   contracts

Tests must not rely heavily on internal implementation details.

------------------------------------------------------------------------

# Build Quality Gate

All diagnostics must be addressed during builds.

This includes:

-   Error
-   Warning
-   Info

Goal:

0 Errors\
0 Warnings\
0 Info

Analyzer diagnostics are considered part of the project's quality rules.

### Scope

The zero-diagnostic rule applies to **all repository-owned code**,
including:

-   src
-   tests
-   samples

Generated code and third-party code are excluded.

------------------------------------------------------------------------

# Change Rules

## Small Change Principle

One pull request should focus on **one purpose**.

Avoid mixing unrelated changes.

## Scope Clarity

Each change should clearly identify:

-   what is included
-   what is intentionally excluded
-   future improvements

Important deferred work should be tracked with Issues.

------------------------------------------------------------------------

# Decision Rule

When in doubt, prioritize:

1.  Specification correctness
2.  Architectural consistency
3.  Maintainability
4.  Readability
5.  Testability
6.  Simplicity

Compatibility is not prioritized before release.

------------------------------------------------------------------------

# Public API Definition

Public API includes any **externally relied upon contract**, including:

-   public code symbols
-   diagnostic IDs
-   default severities
-   editorconfig keys
-   documented behavior
-   configuration surfaces

------------------------------------------------------------------------

# Visible Behavior Change

Visible behavior change means a change that users, tools, or CI can
observe.

This includes:

-   diagnostic additions or removals
-   diagnostic IDs
-   diagnostic message text
-   default severities
-   `.editorconfig` keys
-   `.editorconfig` semantics
-   invalid-value fallback behavior
-   configuration key names
-   public API behavior
-   documented behavior

This does not include:

-   internal refactoring
-   private helper renames
-   performance-only optimizations without observable semantic change
-   internal logging

------------------------------------------------------------------------

# Pre-release Breaking Changes

Pre-release status does not remove the need to document breaking
changes.

Pre-release breaking changes require:

-   a GitHub Issue
-   an ADR in `DECISIONS.md`
-   a `CHANGELOG.md` update

Breaking changes must be recorded under a `## Breaking Changes` section
in `CHANGELOG.md`. Omit that section when there are no breaking changes.

------------------------------------------------------------------------

# Maintainer Definition

For repository governance, a maintainer is a GitHub user with write
permission or higher, including:

-   repository owners
-   maintainer team members
-   write-permission holders

Read-only and triage-only access do not qualify as maintainer access.

------------------------------------------------------------------------

# Reviewer Definition

A reviewer is a contributor with Pull Request review authority, either:

-   a maintainer
-   a contributor explicitly requested through a GitHub review request
-   a contributor explicitly assigned by a maintainer comment

Example maintainer delegation comment:

`@user please review as delegated reviewer`

------------------------------------------------------------------------

# Approval Rule

A normal change is approved when a maintainer either:

-   approves the Pull Request
-   explicitly confirms approval in a comment

Examples include comments such as `LGTM`, `Approved`, or `Looks good to
merge` from a maintainer.

------------------------------------------------------------------------

# Governance Conflict Rule

If governance documents conflict:

1.  Follow `SPECIFICATION.md`
2.  Then follow `ARCHITECTURE.md`
3.  Then follow `DECISIONS.md`
4.  Then follow `AGENTS.md`

A decision record does not change the specification by itself.

Record mismatches and update the documents before implementation
whenever practical.

------------------------------------------------------------------------

# Forbidden

The following are prohibited:

-   implementing code without tests
-   large changes without an Issue
-   ignoring analyzer diagnostics
-   architectural violations
-   leaving documentation outdated
-   unclear or meaningless naming
-   implementing behavior based on assumptions
