# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project follows Semantic Versioning.

## [Unreleased]

### Added

- OSS documentation foundation for the `DependencyContractAnalyzer` repository
- English and Japanese READMEs, contribution guides, and development notes
- Initial implementation scope specification for the first analyzer release
- Security, support, conduct, licensing, and trusted publishing documentation
- GitHub Actions CI workflow for restore, build, test, and pack validation
- GitHub Actions publish workflow for NuGet Trusted Publishing releases
- GitHub issue templates for bug reports and feature requests
- Sample consumer project with representative valid and invalid analyzer usage
- Package icon asset for NuGet presentation
- `.editorconfig` policy toggles for method parameters, properties, object creation, and static member dependency analysis
- `.editorconfig` policy toggles for field, base-type, and interface-implementation dependency analysis
- `.editorconfig` owner-type exclusion by namespace and fully qualified type name
- `ExcludeDependencyContractAnalysisAttribute` for assembly/type-level owner exclusion
- `ExcludeDependencyContractSourceAttribute` for member-level dependency source exclusion
- Exact-match requirement suppression attributes for dependency, target, and scope requirements
- Namespace-based fallback inference for target and scope names from final namespace segments, with optional trailing two-segment inference
- Global `.editorconfig` external dependency policy with `ignore` and `metadata` modes
- External implication-graph expansion for referenced assemblies in `metadata` mode
- Global `.editorconfig` behavior preset with `default`, `strict`, and `relaxed` modes
- Requirement-diagnostic switches for unused and undeclared requirement reporting
- Dependency analysis for non-constructor method parameters
- Dependency analysis for property types
- Dependency analysis for `new` expressions, including target-typed object creation
- Dependency analysis for static member usage
- `DCA101` contract naming validation for lower-kebab-case contract names and implication endpoints
- `ContractScopeAttribute` and `RequiresContractOnScopeAttribute` for scope-based contract rules
- `ContractTargetAttribute` and `RequiresContractOnTargetAttribute` for target-based contract rules
- `ContractHierarchyAttribute` for explicit assembly-level contract hierarchy edges
- Scope-based analyzer evaluation, including assembly-level default scopes and empty-scope validation
- Target-based analyzer evaluation, including inherited target matching and empty-target validation
- Contract implication resolution, including multi-step hierarchy graphs and cycle detection
- Rule-definition diagnostics for undeclared and unused target/scope requirements
- Regression tests for scope matching, duplicate requirements, and assembly/type scope interaction
- Regression tests for target matching, duplicate requirements, and multi-target declarations
- Regression tests for hierarchy resolution, duplicate hierarchy edges, empty hierarchy names, and cyclic hierarchy definitions
- Regression tests for undeclared and unused target/scope requirements
- Regression tests for non-constructor method parameter dependencies
- Regression tests for property-only dependencies across dependency, target, and scope rules
- Regression tests for `new` expression dependencies across dependency, target, and scope rules
- Regression tests for static member usage across dependency, target, and scope rules
- Regression tests for static event usage and enum-member exclusion in static dependency analysis
- Regression tests for `DCA101` contract naming validation
- Regression tests for external dependency metadata policy across dependency, target, and scope requirements
- Regression tests for external hierarchy implication graphs and silent referenced diagnostics
- Regression tests for behavior-preset defaults and explicit override precedence
- Regression tests for unused/undeclared requirement-diagnostic switches
- Regression tests for editorconfig fallback semantics across behavior presets, namespace inference, and requirement-diagnostic switches
- Regression tests for exclusion-list parsing and nested type exclusion behavior
- Local and CI coverage collection via `coverlet.collector` and `XPlat Code Coverage`
- Regression tests for open-generic dependency matching and invalid namespace-segment inference behavior
- Multi-file verifier coverage for path-specific analyzer config options on partial types

### Changed

- Formalized repository governance with root specification and architecture summaries, ADR numbering and acceptance rules, synchronized detailed-doc updates, explicit GitHub workflow expectations, a develop-to-main release flow, third-party dependency governance, and publish validation from stable main-based tags
- Clarified the long-term product direction: the analyzer stays focused on contract fulfillment and implementation consistency rather than becoming a general architecture rule engine
- Removed the architecture-document roadmap section and moved roadmap ownership to GitHub milestones
- Product documentation now describes partial-type source-scoped option merge semantics and the exact boundaries of static-member dependency analysis
- Lowered `DCA205` and `DCA206` default severity from `Warning` to `Info`
- Updated README and development guides to describe the current repository state instead of planned-only wording
- Kept the runnable sample project warning-free by moving representative invalid cases into sample documentation snippets
- Expanded public attribute XML documentation to explain normalization, implication, exclusion, and exact-match suppression behavior
- Clarified current design boundaries for hierarchy semantics, `.editorconfig`, namespace inference, suppression, and `DCA101` naming scope
- Simplified Trusted Publishing configuration to use a single `NUGET_USER` setting for both nuget.org and `int.nugettest.org`
- Clarified the large regression suite with section comments and targeted notes for tricky metadata/preset/inference cases
- Consolidated duplicated analyzer-config helper logic and test-verifier compilation setup into shared implementations
- Consolidated duplicated test attribute-source fixtures into a shared helper for verifier and external-metadata tests
- Consolidated duplicated target/scope requirement collection and evaluation into shared analyzer helpers
- Consolidated duplicated target/scope name-resolution traversal and known-name collection into shared analyzer helpers
- Stabilized test-verifier diagnostic ordering with deterministic tie-breakers for same-location diagnostics
- Consolidated global analyzer option parsing so fallback and normalization rules live in one helper
- Stopped namespace fallback inference from flowing in through external dependency base types and interfaces
- Merged local and referenced implication graphs when expanding provided contracts for external dependencies
- Derived dependency-source, namespace-inference, and external-metadata defaults from `behavior_preset` while keeping explicit per-option settings authoritative
- Allowed target/scope requirement evaluation to continue past undeclared checks when that diagnostic family is disabled via configuration

### Fixed

- Source-scoped analyzer options now merge deterministically across partial type declarations instead of depending on the first declaring file
- Aligned invalid `external_dependency_policy` values with the documented preset-derived fallback behavior
- Stopped nested type bodies from contributing object-creation or static-member dependencies to outer types, and now report `DCA203` for empty assembly-level `ContractScope` declarations

### Removed

- The pre-release `ContractAliasAttribute` compatibility layer; implication declarations now standardize on `ContractHierarchyAttribute`
