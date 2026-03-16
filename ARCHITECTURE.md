# ARCHITECTURE.md

## Purpose

This document describes the architectural principles and structural
expectations of this repository. It exists to support the governance
model by giving a stable reference for design decisions.

When implementation questions arise, this document explains the intended
structure so contributors and AI agents can make consistent decisions.

If a conflict occurs between implementation and architecture
documentation, the **Specification** remains the source of truth, and
discrepancies must be recorded and resolved.

`ARCHITECTURE.md` is the stable summary of architectural principles and
constraints. `docs/architecture.md` is the detailed companion document
for current structure, target structure, diagrams, dependency maps, and
component placement guidance.

If `ARCHITECTURE.md` and `docs/architecture.md` disagree, the principles
in this file win until the detailed document is updated.

Architecture principle changes must update `docs/architecture.md` when
the detailed guidance is affected.

------------------------------------------------------------------------

# Architectural Principles

## Architecture First

Design integrity takes precedence over convenience.

Code changes MUST preserve:

-   separation of concerns
-   clear module boundaries
-   predictable dependency direction
-   understandable structure

Short‑term workarounds that break architectural consistency are not
acceptable.

------------------------------------------------------------------------

## Single Responsibility

Classes and modules should have a single clear responsibility.

Signs of violation:

-   classes growing excessively large
-   unrelated behaviors inside one component
-   utilities that accumulate unrelated helpers

When responsibility boundaries become unclear, refactoring is required.

------------------------------------------------------------------------

## Explicit Design Over Implicit Behavior

Behavior should be expressed clearly in code structure and naming.

Avoid:

-   hidden side effects
-   implicit contracts
-   magic behavior
-   behavior dependent on naming conventions alone

Prefer explicit types, explicit configuration, and explicit control
flow.

------------------------------------------------------------------------

# Dependency Rules

## Direction of Dependencies

Dependencies should move **from high‑level policy to lower‑level
implementation details**.

Typical direction:

Core / Domain ↓ Application Logic ↓ Infrastructure / Integration

Higher‑level modules must not depend on implementation details from
lower layers.

------------------------------------------------------------------------

## Avoid Cyclic Dependencies

Modules must not form circular dependencies.

If cycles appear, responsibilities must be separated or moved.

------------------------------------------------------------------------

# Public Surface

The **public surface** of the project includes:

-   public classes and interfaces
-   diagnostic identifiers
-   configuration keys
-   documented behavior
-   external configuration contracts

Changes to these elements must follow the **Public API rules defined in
AGENTS.md**.

------------------------------------------------------------------------

# Refactoring Guidelines

Refactoring is encouraged when it improves:

-   clarity
-   structure
-   naming
-   dependency direction
-   maintainability

However, refactoring must not exceed the scope of the requested change
unless justified according to AGENTS.md rules.

------------------------------------------------------------------------

# Testing Expectations

Architecture must remain testable.

Design should allow:

-   isolated unit tests
-   deterministic behavior
-   clear test boundaries

If a design prevents testing, it should be reconsidered.

------------------------------------------------------------------------

# Documentation Alignment

Architecture documentation and implementation must remain consistent.

When architectural intent changes:

1.  Update this document
2.  Update `docs/architecture.md` if detailed structure guidance is
    affected
3.  Update `AGENTS.md` if process rules are affected
4.  Record the decision in a GitHub Issue and `DECISIONS.md` when an ADR
    is required

------------------------------------------------------------------------

# When in Doubt

Follow the decision rule defined in AGENTS.md:

Specification ↓ Architecture consistency ↓ Maintainability ↓ Readability
↓ Testability ↓ Simplicity
