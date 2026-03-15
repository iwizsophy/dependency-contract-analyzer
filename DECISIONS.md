# DECISIONS.md

This file records **Architecture Decision Records (ADR)** for the
project.

Its purpose is to capture **why decisions were made**, not just what the
code does. This helps maintainers, contributors, and AI coding agents
understand the reasoning behind design choices.

SPECIFICATION.md defines *expected behavior*. AGENTS.md defines *process
rules*. ARCHITECTURE.md defines *structural principles*. DECISIONS.md
records *specific design decisions*.

Together they form the project's governance model.

------------------------------------------------------------------------

# How to Use

Whenever a significant design decision is made, record it here.

Examples:

-   architectural direction
-   public API design choices
-   naming conventions
-   dependency rules
-   analyzer policy decisions
-   configuration surface decisions
-   breaking change rationale

Do not record trivial implementation details.

Each decision should be recorded as its own section. The file is a
decision log, not an issue log.

Use `ADR-###` numbering only when an ADR is accepted:

-   `ADR-001`
-   `ADR-002`
-   `ADR-003`

Only accepted ADRs are recorded in this file. Proposals belong in a
GitHub Issue, typically titled `ADR proposal: <title>`.

Do not create gaps intentionally. If an ADR is superseded, keep its
number and mark its status accordingly.

------------------------------------------------------------------------

# ADR Threshold

An ADR is required when a change affects any of the following:

-   external contracts or public API meaning
-   diagnostic identifiers, default severities, or diagnostic policy
-   configuration keys or configuration semantics
-   naming rules
-   dependency rules
-   architecture boundaries or structural constraints
-   test strategy or repository-wide policy
-   breaking-change direction

An ADR is optional for typo fixes, wording-only documentation edits,
narrow internal cleanup within established rules, and straightforward
implementation of already-specified behavior.

If the decision is likely to be referenced again, explains a meaningful
trade-off, or could reasonably have gone another way, prefer recording
an ADR.

------------------------------------------------------------------------

# Specification Effect

A decision record does not change the specification by itself.

When a decision changes specified behavior, the change becomes
effective only when `SPECIFICATION.md` is updated.

An ADR may be drafted or accepted before the specification is updated,
but implementation and merge should not rely on the ADR alone as the new
official specification.

------------------------------------------------------------------------

# Acceptance Rule

Only a maintainer can mark an ADR as accepted.

For governance purposes, a maintainer is a GitHub user with write
permission or higher, including repository owners, maintainer team
members, and write-permission holders.

Acceptance must be recorded in GitHub through one of the following:

-   an Issue comment
-   a Pull Request review or comment
-   a Discussion comment

The ADR status field must be updated to `Accepted` when the decision is
formally accepted. The `Specification:` field must be set to `Updated`
or `Pending update`. The maintainer assigns the next available
`ADR-###` number at that point.

------------------------------------------------------------------------

# Decision Template

Use the following template.

## ADR-XXX `<short title>`{=html}

Status: Accepted \| Superseded \| Deprecated  
Specification: Updated \| Pending update

### Context

Describe the problem and the situation that led to this decision.

Include constraints, alternatives considered, and relevant background.

### Decision

Describe the chosen solution.

Explain the reasoning behind the decision.

### Consequences

Describe the effects of the decision:

-   benefits
-   trade-offs
-   risks
-   future considerations

### Related

-   Issue:
-   Pull Request:
-   Specification reference:
-   Related decisions:

------------------------------------------------------------------------

# Example

## ADR-001 Diagnostic IDs are stable

Status: Accepted  
Specification: Updated

### Context

Users depend on diagnostic IDs in CI pipelines and editor integrations.
Changing IDs would break automation and documentation.

### Decision

Diagnostic IDs are treated as part of the public API and must remain
stable.

### Consequences

Pros: - predictable integrations - stable tooling behavior

Cons: - refactoring diagnostic grouping becomes harder

------------------------------------------------------------------------

# Maintenance Rules

-   Each decision should be concise.
-   Record one decision per section.
-   Decisions should be append-only whenever possible.
-   Use sequential `ADR-###` numbering for accepted ADRs only.
-   If a decision changes, mark the old one **Superseded** and reference
    the new one.
-   Major decisions should link to the relevant GitHub Issue.

------------------------------------------------------------------------

# Relationship to AGENTS.md

If AGENTS.md rules change due to a design decision, record the decision
here.

This ensures future contributors understand *why the rule exists*.

------------------------------------------------------------------------

# Relationship to ARCHITECTURE.md

ARCHITECTURE.md describes stable principles.

DECISIONS.md explains the historical reasoning behind those principles.
