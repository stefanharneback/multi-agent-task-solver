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

## Test layers

### Core unit tests

- manifest and model serialization
- state transition helpers once orchestration logic is added
- validation rules for task, run, and step metadata

### Infrastructure tests

- filesystem persistence and round-trips
- path-boundary protection
- artifact import behavior
- provider config loading
- gateway request mapping
- gateway error handling
- usage normalization

### App tests

- view model validation and command behavior
- settings and task-workspace coordination logic
- page-level behaviors only if lightweight MAUI test coverage is added later

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
- add manual smoke notes to the change summary when automated UI coverage does not exist

## Definition of done for new behavior

New behavior is not done until:

- automated tests cover the changed contract where practical
- manual smoke coverage is recorded when UI or task-folder flows change
- `AGENTS.md`, `README.md`, prompts, agents, and workflow docs stay aligned

## Future expansion

If the repository later adds UI automation, end-to-end orchestration tests, or evaluation suites, update this document instead of scattering expectations across prompts only.
