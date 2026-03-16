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

## Example ADR: Diagnostic IDs are stable

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

## ADR-001 Type-level metadata overrides inference and assembly scopes compose additively

Status: Accepted
Specification: Updated

### Context

The analyzer already documented type-level `ContractTarget` and
`ContractScope` declarations as the primary metadata source, while
namespace inference acted as fallback behavior. However, that precedence
was not recorded as an accepted product decision. The scope model also
needed an explicit decision for how assembly-level `ContractScope`
declarations compose with type-level scopes and namespace-based fallback
resolution.

### Decision

Type-level `ContractTarget` and `ContractScope` declarations remain
authoritative over namespace-based inference for their respective
metadata kinds. The analyzer does not add namespace-inferred target or
scope names for a type when that type already declares explicit metadata
of the same kind.

Assembly-level `ContractScope` declarations always apply to types in the
declaring assembly. Type-level `ContractScope` declarations add scopes
for the type and do not remove assembly-level scopes. Namespace-based
scope inference remains fallback-only and is applied when the type has
no type-level explicit scopes, even if assembly-level scopes are
present.

This decision adds no new diagnostics.

### Consequences

-   target and scope resolution stays predictable because explicit
    type-level declarations do not mix with inferred names for the same
    metadata kind
-   assembly-wide scope classification remains stable because
    assembly-level scopes cannot be removed by individual type
    declarations
-   scope matching can include both assembly-level scopes and
    namespace-inferred fallback scopes for types without explicit
    type-level scopes
-   richer mixed explicit-plus-inferred models remain future work and
    require separate design discussion

### Related

-   Issue: #69, #71
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-002 External metadata mode stays explicit-metadata-only for namespace inference

Status: Accepted
Specification: Updated

### Context

`external_dependency_policy = metadata` already allowed the analyzer to
consume explicit provided contracts, targets, scopes, and implication
edges from referenced assemblies. The product also already limited
namespace-based target and scope inference to the current compilation,
but that boundary was not recorded as an accepted design decision.

### Decision

In `external_dependency_policy = metadata` mode, referenced assemblies
contribute explicit metadata and implication edges only. Referenced
assemblies do not participate in namespace-based target or scope
inference.

Namespace-based inference remains limited to types declared in the
current compilation. The analyzer adds no new diagnostics for
hypothetical disagreements between explicit external metadata and
would-be inferred external names because inferred external names are not
part of the product model.

### Consequences

-   external dependency matching remains explicit and predictable
-   the analyzer avoids heuristic cross-assembly naming guesses that are
    hard to explain or stabilize
-   future external inference work remains possible, but it requires a
    separate design decision and additional diagnostics discussion

### Related

-   Issue: #58
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions: ADR-001

------------------------------------------------------------------------

## ADR-003 Undeclared target and scope validation remains current-compilation-only

Status: Accepted
Specification: Updated

### Context

The analyzer already reported `DCA200` and `DCA201` by validating
required target and scope names against declarations in the current
compilation only. Even in `external_dependency_policy = metadata` mode,
referenced assemblies were used for dependency matching rather than for
declared-name validation. That boundary was documented, but it had not
been recorded as an accepted design decision.

### Decision

`DCA200` and `DCA201` remain limited to declarations in the current
compilation. Referenced assemblies do not contribute declared target or
scope names for undeclared-requirement validation, even when
`external_dependency_policy = metadata`.

Disabling `report_undeclared_requirement_diagnostics` continues
dependency evaluation after the undeclared check, but it does not change
the declared-name boundary itself.

This decision adds no new diagnostics.

### Consequences

-   undeclared-requirement diagnostics stay deterministic and tied to
    source that the current compilation owns
-   external metadata remains a matching aid rather than an authority
    for declared-name validation
-   future expansion to cross-assembly declaration validation remains
    possible, but it requires a separate design decision and additional
    diagnostic-model work

### Related

-   Issue: #57
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions: ADR-002

------------------------------------------------------------------------

## ADR-004 Referenced implication diagnostics remain silent in consuming compilations

Status: Accepted
Specification: Updated

### Context

In `external_dependency_policy = metadata` mode, referenced assemblies
already contributed implication edges for dependency matching. The
analyzer also already avoided reporting malformed referenced implication
definitions, including cyclic graphs, in the consuming compilation.
That boundary was documented and covered by regression tests, but it had
not been recorded as an accepted design decision.

### Decision

Referenced assemblies may contribute implication edges for dependency
matching in `external_dependency_policy = metadata` mode, but
diagnostics for malformed referenced implication definitions do not
surface in the consuming compilation.

Diagnostic reporting for implication-definition problems remains owned
by the compilation that declares those implication edges. This includes
`DCA202` and any future implication-definition diagnostics in the same
family.

This decision adds no new diagnostics.

### Consequences

-   package consumers do not receive diagnostics anchored to metadata
    they do not compile directly
-   implication matching can still use referenced metadata without
    turning package consumption into cross-assembly diagnostic replay
-   future cross-assembly diagnostic surfacing remains possible, but it
    requires a separate decision for allowed diagnostic kinds and
    location semantics

### Related

-   Issue: #66
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions: ADR-002, ADR-003

------------------------------------------------------------------------

## ADR-005 DependencyContractAnalyzer remains DI-agnostic

Status: Accepted
Specification: Updated

### Context

The project documentation already described the analyzer as DI-agnostic
and listed DI registration analysis, runtime dependency resolution,
Scrutor behavior, factory registration behavior, and container-specific
wiring rules as non-goals. That product boundary was visible to users,
but it had not been recorded as an accepted design decision.

### Decision

DependencyContractAnalyzer remains DI-agnostic. The product does not
analyze DI container registrations, runtime resolution behavior,
Scrutor-style registration expansion, factory registration behavior, or
other container-specific wiring rules.

Contracts must remain discoverable from the analyzed type surface, its
base types, implemented interfaces, explicit metadata, and the existing
metadata-only external reference model. Any future container-aware
features require a separate design decision.

This decision adds no new diagnostics.

### Consequences

-   analysis remains static and source-shape-driven instead of depending
    on container conventions or runtime composition
-   users must declare contracts on the types they consume rather than
    relying on registration-side behavior to satisfy requirements
-   future DI-aware features remain possible, but they require a
    separate roadmap decision and a different diagnostic model

### Related

-   Issue: #61
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-006 Dependency extraction remains limited to strong type relationships

Status: Accepted
Specification: Updated

### Context

The documentation already described dependency extraction as limited to
constructor parameters, non-constructor method parameters, property
types, field types, object creation, static member usage, base types,
and implemented interfaces. It also framed that set as an intentionally
narrow initial boundary, but the project had not yet recorded whether
that boundary was temporary or a stable product decision.

### Decision

Dependency extraction remains limited to strong type relationships. The
in-scope dependency kinds are constructor parameters, non-constructor
method parameters, property types, field types, object creation, static
member usage, base types, and implemented interfaces.

Attribute references, generic constraints, `typeof` references, return
types, and other weaker symbol relationships remain out of scope. Any
future expansion beyond this boundary requires a separate design
decision.

This decision adds no new diagnostics.

### Consequences

-   dependency discovery remains explainable from stable type-shape
    relationships instead of broader symbol presence
-   the analyzer stays aligned with its DI-agnostic and predictable
    static-analysis model
-   future extraction expansion remains possible, but it requires a
    separate product decision and additional diagnostic/test work

### Related

-   Issue: #62
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions: ADR-005

------------------------------------------------------------------------

## ADR-007 Constructor-parameter analysis remains always enabled

Status: Accepted
Specification: Updated

### Context

The implementation and detailed specification already treated constructor
parameters as the baseline dependency source even when optional
dependency-source families were disabled by presets or configuration.
However, that behavior had not been recorded as an accepted product
decision.

### Decision

Constructor parameters remain an always-on dependency source.
Constructor-parameter analysis is not controlled by `behavior_preset` or
by any `analyze_*` option.

Optional dependency-source families may be disabled, but constructor
parameters remain part of dependency extraction. Any future
configurability for constructor-parameter analysis requires a separate
design decision.

This decision adds no new diagnostics.

### Consequences

-   dependency analysis retains a stable baseline source even in relaxed
    configurations
-   presets and optional `analyze_*` switches stay limited to genuinely
    optional dependency families
-   future constructor-analysis configurability remains possible, but it
    requires a separate product decision and test updates

### Related

-   Issue: #67
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions: ADR-006

------------------------------------------------------------------------

## ADR-008 Requirement suppression remains exact-match only

Status: Accepted
Specification: Updated

### Context

The current suppression attributes already matched one normalized
dependency type or one normalized target/scope name together with one
normalized contract name. They also already avoided broadening
suppression across other requirement kinds. That behavior was documented
in the detailed specification and attribute remarks, but it had not been
recorded as an accepted design decision.

### Decision

Requirement suppression remains exact-match only. A suppression matches
one normalized dependency type or one normalized target/scope name
together with one normalized contract name.

Suppression does not broaden to wildcards, prefixes, contract-only
forms, rule-family suppression, or other requirement kinds. Any future
broader suppression model requires a separate design decision.

This decision adds no new diagnostics.

### Consequences

-   suppression behavior stays predictable and directly attributable to
    one declared requirement
-   diagnostics remain transparent because suppression cannot silently
    swallow adjacent requirement kinds or broader rule families
-   future broader suppression features remain possible, but they
    require a separate product decision and additional stale-suppression
    design work

### Related

-   Issue: #68
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-009 DCA101 remains fixed to lower-kebab-case for contract naming

Status: Accepted
Specification: Updated

### Context

The detailed documentation and diagnostics already enforced
lower-kebab-case for contract names, hierarchy endpoints, and
requirement-suppression contract-name arguments. They also already left
target names and scope names outside `DCA101`. That naming boundary was
user-visible and covered by regression tests, but it had not been
recorded as an accepted design decision.

### Decision

`DCA101` remains fixed to lower-kebab-case for contract names. The same
lower-kebab-case rule also applies to hierarchy endpoints and the
contract-name arguments of requirement suppression attributes.

`DCA101` does not apply to target names or scope names. Any future
configurability or broader naming-policy change requires a separate
design decision.

This decision adds no new diagnostics.

### Consequences

-   contract naming stays consistent across provided contracts,
    implications, and suppression metadata
-   target and scope naming remain independent of `DCA101`
-   future naming-policy expansion remains possible, but it requires a
    separate product decision and compatibility review

### Related

-   Issue: #65
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-010 EditorConfig policy surface remains limited to the current option set

Status: Accepted
Specification: Updated

### Context

The product already supported a defined `.editorconfig` surface covering
dependency-source toggles, reporting switches, exclusions,
`behavior_preset`, `namespace_inference_max_segments`, and
`external_dependency_policy`. The detailed specification still hinted at
future EditorConfig expansion beyond those categories, but no accepted
roadmap decision existed for additional policy controls.

### Decision

The EditorConfig policy surface remains limited to the current option
set. Supported policy categories remain dependency-source toggles,
reporting switches, exclusions, `behavior_preset`,
`namespace_inference_max_segments`, and
`external_dependency_policy`.

There is no accepted EditorConfig roadmap for naming-policy
configuration, broader suppression models, or wider rule-family policy
controls. Any future expansion of the EditorConfig surface requires a
separate design decision.

This decision adds no new diagnostics or configuration keys.

### Consequences

-   the supported EditorConfig surface stays explicit and finite
-   configuration semantics remain coherent with the current
    source-scoped and compilation-scoped option split
-   future policy-surface expansion remains possible, but it requires a
    separate product decision and documentation update

### Related

-   Issue: #60
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-011 Namespace inference remains limited to the current fallback heuristics

Status: Accepted
Specification: Updated

### Context

The product already supported namespace-based target and scope fallback
inference using the final namespace segment by default and an optional
trailing two-segment fallback through
`namespace_inference_max_segments = 2`. Broader namespace-derived naming
heuristics remained out of scope, but that boundary had not been
recorded as an accepted design decision.

### Decision

Namespace-based target and scope inference remains limited to the
current fallback heuristics. Supported inference remains the final
namespace segment by default and the trailing two-segment fallback when
`namespace_inference_max_segments = 2`.

Richer namespace-derived naming heuristics remain out of scope. Any
future expansion beyond this boundary requires a separate design
decision.

This decision adds no new diagnostics or configuration keys.

### Consequences

-   namespace inference stays predictable and easy to explain
-   the current `namespace_inference_max_segments` option remains the
    complete namespace-inference configuration surface
-   future richer inference remains possible, but it requires a separate
    product decision and interaction review with undeclared-name
    diagnostics

### Related

-   Issue: #53
-   Pull Request:
-   Specification reference: `SPECIFICATION.md`, `docs/specification.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-012 Publish feed routing is branch-based rather than trigger-based

Status: Accepted
Specification: Updated

### Context

The repository already supported publishing through both annotated tag
pushes and manual `workflow_dispatch` runs. However, the publish target
had been selected partly by trigger type, which allowed tag pushes to
default to `nuget.org` even when the tagged commit belonged to
`develop`.

The required release policy is stricter: every publish from `develop`
must go only to `int.nugettest.org`, and every publish from `main` must
go only to `nuget.org`, regardless of how publishing is triggered.

### Decision

Publish destination is determined by branch, not by trigger type.
Manual dispatches from `develop` publish only to
`int.nugettest.org`. Manual dispatches from `main` publish only to
`nuget.org`.

Tag-push publishes resolve their destination by tagged-commit branch
containment. A tagged commit reachable only from `main` publishes only
to `nuget.org`. A tagged commit reachable only from `develop` publishes
only to `int.nugettest.org`.

If a tagged commit is reachable from both `main` and `develop`, or from
neither branch, the workflow fails validation instead of guessing.

GitHub Releases remain `main`-only release artifacts.

### Consequences

-   feed routing now enforces the repository's branch roles across both
    manual and tag-based publishing
-   accidental cross-publishing from `develop` to `nuget.org`, or from
    `main` to `int.nugettest.org`, is blocked by validation
-   tag publishes now require branch topology to be unambiguous; shared
    commits must not be tagged for publishing unless the ambiguity is
    resolved first

### Related

-   Issue: #100
-   Pull Request:
-   Specification reference: `AGENTS.md`, `docs/trusted-publishing.md`
-   Related decisions:

------------------------------------------------------------------------

## ADR-013 Analyzer tests use explicit current-host reference assemblies

Status: Accepted
Specification: Pending update

### Context

The repository's analyzer tests had been running under a `net10.0`
test host while still relying on implicit reference-assembly selection
in `Microsoft.CodeAnalysis.Testing` and on runtime-provided platform
assemblies for hand-built compilations. That made the test harness
sensitive to host-environment drift and allowed dependency updates such
as `coverlet.collector 8.0.0` to break the suite through reference
assembly mismatches instead of analyzer regressions.

The repository still does not want to publish a version-by-version host
support matrix. However, it should validate the analyzer against the
current .NET host versions used in development and CI.

### Decision

Analyzer tests use explicit `Microsoft.NETCore.App.Ref` reference
assemblies matching the current test host target framework instead of
implicit defaults or runtime assembly discovery.

The test project runs on `net8.0`, `net9.0`, and `net10.0`, and CI test
validation installs current .NET SDK/runtime lines so the full suite
executes on those host TFMs.

This decision does not change the public support statement for the
packaged analyzer, which remains a `netstandard2.0` analyzer package
without a published host-version matrix.

### Consequences

-   test failures are more likely to point at analyzer behavior or test
    infrastructure changes instead of ambient host-runtime drift
-   CI now checks the current host-runtime set explicitly, which catches
    test-host-specific issues earlier
-   the repository's internal validation grows stricter without
    broadening the package's external compatibility promise

### Related

-   Issue: #117
-   Pull Request:
-   Specification reference:
-   Related decisions:

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
