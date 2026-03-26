# Testing Strategy

This document defines the expected automated and manual verification coverage for the repository as features expand.

## Preferred local verification

Use this sequence for normal local verification:

```bash
dotnet build MultiAgentTaskSolver.sln
dotnet test MultiAgentTaskSolver.sln --no-build
```

Use this sequence for release-style verification:

```bash
dotnet build MultiAgentTaskSolver.sln --configuration Release
dotnet test MultiAgentTaskSolver.sln --configuration Release --no-build
```

Use raw `dotnet test` only when you intentionally want it to build.

Use this sequence for local coverage output:

```bash
dotnet tool restore
dotnet test MultiAgentTaskSolver.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet tool run reportgenerator "-reports:artifacts/test-results/**/coverage.cobertura.xml" "-targetdir:artifacts/coverage" "-reporttypes:HtmlInline;Cobertura;MarkdownSummaryGithub"
```

## Test layers

### Core unit tests

- manifest and model serialization
- state transition helpers once orchestration logic is added
- validation rules for task, run, and step metadata

### Infrastructure tests

- filesystem persistence and round-trips
- path-boundary protection
- artifact import behavior
- review workflow persistence
- file-reference resolution
- review prompt assembly
- provider config loading
- gateway request mapping
- gateway error handling
- usage normalization

### App tests

- view model validation and command behavior
- settings and task-workspace coordination logic
- navigation and native-picker behavior through app service abstractions
- prefer stable `AutomationId` values on new interactive controls so end-to-end automation can be added without rewriting the UI
- page-level rendering and native control behavior may still need manual smoke coverage

### Manual smoke coverage

Required when:

- MAUI navigation changes
- settings or secure-storage handling changes
- task-folder interaction changes
- worker, critic, or approval flows are introduced

The minimum smoke checklist should cover:

- open app
- create task
- reload existing task
- import files
- edit task details
- inspect run history
- any newly added workflow path

## CI and security workflow baseline

- `dotnet.yml` should keep build, test, coverage collection, merged coverage reports, and artifact upload working on GitHub Actions.
- `codeql.yml` should stay aligned with the solution build path and only run where repository settings allow it.
- `dependency-review.yml` and `dependabot.yml` are part of the expected supply-chain baseline, not optional extras.
- GitHub-side secret scanning and push protection should be enabled manually in repository settings.

## Feature-specific expectations

### Storage changes

- add automated round-trip tests
- add path-safety regression tests
- verify compatibility with existing task folders when schema changes are introduced

### Provider changes

- add adapter request and response tests
- add provider capability or catalog tests
- add failure-path coverage for auth, validation, and upstream errors

### Agent-loop changes

- add state transition tests
- add approval-gate tests
- add iteration or retry persistence tests
- add usage and cost capture tests where available

### MAUI changes

- keep code-behind thin and logic in testable services or view models
- add or update tests in `tests/MultiAgentTaskSolver.App.Tests` for changed view model or app-service behavior
- add `AutomationId` values for new buttons, entries, pickers, and collections
- add manual smoke notes to the change summary when automated UI coverage does not exist

## Definition of done for new behavior

New behavior is not done until:

- automated tests cover the changed contract where practical
- manual smoke coverage is recorded when UI or task-folder flows change
- `AGENTS.md`, `README.md`, prompts, agents, and workflow docs stay aligned

## Future expansion

If the repository later adds UI automation, end-to-end orchestration tests, or evaluation suites, update this document instead of scattering expectations across prompts only.
