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
- `.editorconfig` policy toggles for method parameters, properties, object creation, and static member dependency analysis
- Dependency analysis for non-constructor method parameters
- Dependency analysis for property types
- Dependency analysis for `new` expressions, including target-typed object creation
- Dependency analysis for static member usage
- `DCA101` contract naming validation for lower-kebab-case contract names and alias endpoints
- `ContractScopeAttribute` and `RequiresContractOnScopeAttribute` for scope-based contract rules
- `ContractTargetAttribute` and `RequiresContractOnTargetAttribute` for target-based contract rules
- `ContractAliasAttribute` for assembly-level contract implication rules
- Scope-based analyzer evaluation, including assembly-level default scopes and empty-scope validation
- Target-based analyzer evaluation, including inherited target matching and empty-target validation
- Alias-based contract resolution, including transitive alias chains and cycle detection
- Rule-definition diagnostics for undeclared and unused target/scope requirements
- Regression tests for scope matching, duplicate requirements, and assembly/type scope interaction
- Regression tests for target matching, duplicate requirements, and multi-target declarations
- Regression tests for alias resolution, duplicate aliases, empty alias names, and cyclic aliases
- Regression tests for undeclared and unused target/scope requirements
- Regression tests for non-constructor method parameter dependencies
- Regression tests for property-only dependencies across dependency, target, and scope rules
- Regression tests for `new` expression dependencies across dependency, target, and scope rules
- Regression tests for static member usage across dependency, target, and scope rules
- Regression tests for `DCA101` contract naming validation

### Changed

- Lowered `DCA205` and `DCA206` default severity from `Warning` to `Info`
- Updated README and development guides to describe the current repository state instead of planned-only wording
- Clarified current design boundaries for alias semantics, `.editorconfig`, namespace inference, suppression, and `DCA101` naming scope

### Fixed

- _None_

### Removed

- _None_
