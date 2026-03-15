# SPECIFICATION.md

## Purpose

This document defines **the behavioral specification of the project**.

It answers the question:

> What should the system do?

While other governance documents serve different purposes:

-   **AGENTS.md** -- development process rules
-   **ARCHITECTURE.md** -- architectural principles
-   **DECISIONS.md** -- historical design decisions
-   **SPECIFICATION.md** -- expected behavior and contracts

When conflicts occur between implementation and documentation, **the
specification defined here takes precedence**.

`SPECIFICATION.md` is the authoritative summary specification.
`docs/specification.md` is the detailed companion document for examples,
tables, edge cases, migration notes, and walkthroughs.

If `SPECIFICATION.md` and `docs/specification.md` disagree, this file
wins.

Specification changes must update both `SPECIFICATION.md` and
`docs/specification.md` unless the change is editorial only.

------------------------------------------------------------------------

# Specification Scope

The specification describes externally observable behavior, including:

-   public API behavior
-   configuration semantics
-   analyzer diagnostics and meanings
-   command behavior (if applicable)
-   configuration keys
-   expected system reactions

Internal implementation details are **not part of the specification**.

------------------------------------------------------------------------

# Specification Principles

## Behavior First

Specifications describe **observable behavior**, not implementation
details.

Avoid statements such as:

-   internal algorithm choices
-   specific class names
-   internal data structures

Prefer describing:

-   inputs
-   outputs
-   constraints
-   guarantees

------------------------------------------------------------------------

## Stability

Behavior described here is considered **contractual behavior**.

Changes to specified behavior require:

1.  documentation update
2.  associated test updates
3.  justification recorded in `DECISIONS.md`

For high-impact changes, update `SPECIFICATION.md` and `DECISIONS.md` in
the same change whenever practical.

------------------------------------------------------------------------

## Explicit Semantics

Specification must clarify:

-   expected input
-   expected output
-   allowed states
-   invalid states
-   error conditions

Ambiguous wording should be avoided.

------------------------------------------------------------------------

# Diagnostic Specification (Example Section)

For projects that expose diagnostics (e.g. analyzers):

Each diagnostic should define:

-   **ID**
-   **Severity**
-   **Trigger condition**
-   **Expected fix or guidance**
-   **Configurability via editorconfig**

Example:

## Diagnostic: DCA101

### Description

Detects unsafe lifetime dependency chains.

### Trigger

Occurs when a singleton depends on a scoped or transient service without
appropriate safety guarantees.

### Default Severity

Warning

### Configuration

.editorconfig key:

    dotnet_diagnostic.DCA101.severity

### Expected Fix

Refactor dependency chain or adjust service lifetime.

------------------------------------------------------------------------

# Change Policy

Specification changes are considered **high-impact changes**.

Required steps:

1.  Update this file
2.  Update related tests
3.  Record reasoning in `DECISIONS.md`
4.  Mention change in PR description

An ADR may be drafted or even accepted earlier, but the specification
does not change until `SPECIFICATION.md` is updated.

------------------------------------------------------------------------

# Relationship to Tests

Tests should validate the behavior described here.

If tests and specification disagree:

1.  check specification intent
2.  update tests if they are incorrect
3.  update specification if behavior legitimately changed

If a bug fix exposes previously unspecified behavior, decide and record
the intended behavior in the specification before treating the fix as
complete. Urgent temporary fixes must be followed by a tracked
specification update.

------------------------------------------------------------------------

# Relationship to AGENTS.md

AGENTS.md describes **how changes are implemented**.

SPECIFICATION.md defines **what the system must do**.

If uncertainty arises during implementation, consult this document
first.

------------------------------------------------------------------------

# Analyzer Metadata Resolution

The analyzer resolves target and scope metadata using these rules:

-   Type-level `ContractTarget` and `ContractScope` declarations take
    precedence over namespace-based inference for their respective
    metadata kinds.
-   Namespace-based inference is fallback-only and applies only when the
    type has no type-level explicit metadata of that kind.
-   Assembly-level `ContractScope` declarations always apply to types in
    the declaring assembly.
-   Type-level `ContractScope` declarations add scopes for the type and
    do not remove assembly-level scopes.
-   Namespace-based scope inference may add fallback scope names
    alongside assembly-level scopes when the type has no type-level
    explicit scope declarations.
-   In `external_dependency_policy = metadata` mode, namespace-based
    inference remains limited to current-compilation types; referenced
    assemblies contribute explicit metadata and implication edges only.
-   `DCA200` and `DCA201` validate declared targets and scopes against
    the current compilation only, even when external metadata matching
    is enabled.
-   Malformed implication definitions from referenced assemblies do not
    report diagnostics in the consuming compilation; those diagnostics
    remain owned by the compilation that declares the implication
    metadata.

------------------------------------------------------------------------

# Dependency Extraction Boundary

Dependency extraction remains limited to strong type relationships.

The analyzer extracts dependencies from:

-   constructor parameters
-   non-constructor method parameters
-   property types
-   field types
-   object creation
-   static member usage
-   base types
-   implemented interfaces

The analyzer does not extract dependencies from weaker symbol
relationships such as attribute references, generic constraints,
`typeof` references, or return types.

Constructor parameters are always analyzed and are not controlled by
`behavior_preset` or any `analyze_*` option.

------------------------------------------------------------------------

# Requirement Suppression Boundary

Requirement suppression remains exact-match only.

Each suppression matches one normalized dependency type or one
normalized target/scope name together with one normalized contract name.
Suppression does not broaden across other requirement kinds, wildcards,
prefixes, or contract-only forms.

------------------------------------------------------------------------

# Product Boundary

DependencyContractAnalyzer remains DI-agnostic.

The product does not analyze:

-   DI registration analysis
-   runtime dependency resolution
-   Scrutor-style registration expansion
-   factory registration behavior
-   container-specific wiring rules

Contracts must be discoverable from the analyzed type surface and the
documented metadata model rather than from container configuration.

------------------------------------------------------------------------

# Maintenance

Specification should evolve slowly.

Prefer:

-   extending sections
-   adding explicit clarifications

Avoid:

-   rewriting existing meaning without explanation
-   removing behavior without recording rationale in `DECISIONS.md`
