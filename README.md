# Multi Agent Task Solver

Desktop-first `.NET MAUI` workspace for structured multi-agent task solving against gateway services such as `openai-api-service`.

## Current milestone

This repository currently delivers the foundation slice:

- repo scaffolding and AI workflow baseline
- a `.NET 10` / `MAUI 10` solution with shared core and infrastructure libraries
- file-first task storage with one root folder per task
- provider abstractions and a first OpenAI gateway adapter
- explicit task, run, and step workflow states
- task file-reference resolution for `@alias` references
- a first task-review execution path with persisted prompt, response, and usage artifacts
- an explicit review approval or revise gate with persisted `user-decision` runs
- a first worker execution path with persisted run artifacts and markdown output under `outputs/`
- a MAUI shell for tasks, task details, flow/history, and settings
- app-level automated tests for MAUI viewmodels and navigation/picker seams

The full review -> worker -> critic execution loop is not implemented yet. The current execution slice covers the task-review path, the user approval gate, and a first worker run that persists output and leaves the task in `working` until the critique loop lands.
The next milestones and extension points are tracked in `docs/roadmap.md`.

## Architecture

```text
MAUI App
  |- task list, create task, details, runs, settings
  |- settings JSON + SecureStorage secret handling
  |- DI and navigation shell
  |
  +- Core
  |    |- task manifests and run/step types
  |    |- provider abstractions
  |    |- task folder conventions
  |
  +- Infrastructure
       |- filesystem task workspace store
       |- review and worker workflows, prompt assembly, and file-reference resolution
       |- gateway-backed model catalog with local JSON fallback metadata
       |- OpenAI gateway client and usage normalization
```

## Task storage

Each task lives in its own root folder:

```text
Task-<id>/
  task.json
  task.md
  inputs/
    documents/
    transcripts/
    interviews/
    articles/
    rules/
    notes/
    standards/
  runs/
  outputs/
  cache/
```

Canonical task artifacts stay human-readable. The `cache/` folder may contain machine-oriented derived files.

## Project structure

```text
src/
  MultiAgentTaskSolver.App/             MAUI shell and app services
  MultiAgentTaskSolver.Core/            shared domain types and interfaces
  MultiAgentTaskSolver.Infrastructure/  filesystem and gateway services
tests/
  MultiAgentTaskSolver.App.Tests/
  MultiAgentTaskSolver.Core.Tests/
  MultiAgentTaskSolver.Infrastructure.Tests/
config/
  providers/openai.models.json          fallback OpenAI model metadata
docs/
  ai-workflow.md
  agent-loop.md
  maintenance-cadence.md
  maintenance-reviews/
  provider-expansion.md
  roadmap.md
  testing-strategy.md
```

## Planning docs

- `docs/roadmap.md`: milestone plan and extension points for upcoming implementations
- `docs/agent-loop.md`: planned review, worker, critic, and user-approval workflow contract
- `docs/testing-strategy.md`: automated and manual verification expectations
- `docs/provider-expansion.md`: rules for adding Gemini, Claude, and other providers later
- `docs/maintenance-reviews/template.md`: standard template for monthly and quarterly reports

## Commands

```bash
dotnet workload restore
dotnet tool restore
dotnet restore MultiAgentTaskSolver.sln
dotnet build MultiAgentTaskSolver.sln
dotnet test MultiAgentTaskSolver.sln --no-build
```

For app-layer-only verification, run:

```bash
dotnet test tests/MultiAgentTaskSolver.App.Tests/MultiAgentTaskSolver.App.Tests.csproj --no-build
```

For local coverage output, run:

```bash
dotnet test MultiAgentTaskSolver.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet tool run reportgenerator "-reports:artifacts/test-results/**/coverage.cobertura.xml" "-targetdir:artifacts/coverage" "-reporttypes:HtmlInline;Cobertura;MarkdownSummaryGithub"
```

For release-style verification, run:

```bash
dotnet build MultiAgentTaskSolver.sln --configuration Release
dotnet test MultiAgentTaskSolver.sln --configuration Release --no-build
```

## Provider baseline

The first integrated provider is `openai-api-service`. This app currently assumes the gateway exposes:

- `POST /v1/llm`
- `GET /v1/usage`
- `POST /v1/whisper`

Model discovery for OpenAI now uses the live gateway `GET /v1/models` endpoint when the gateway base URL and bearer token are configured. `config/providers/openai.models.json` remains as fallback metadata and as the source for richer display/capability hints when live model IDs are returned.

## Security notes

- Store gateway base URLs and workspace paths in local JSON settings.
- Store gateway bearer keys in MAUI `SecureStorage`.
- Do not commit real workspace roots, local task data, or credentials.
- GitHub-side secret scanning and push protection still need to be enabled in repository settings because they are not fully repo-file driven.

## CI and Coverage

- [dotnet.yml](/C:/Users/stefa/OneDrive/Programming/VSCode_WithAI/multi-agent-task-solver/.github/workflows/dotnet.yml) builds, tests, collects coverage, generates merged reports, uploads artifacts, and can publish coverage to Codecov.
- [codeql.yml](/C:/Users/stefa/OneDrive/Programming/VSCode_WithAI/multi-agent-task-solver/.github/workflows/codeql.yml) adds CodeQL analysis for C#.
- [dependency-review.yml](/C:/Users/stefa/OneDrive/Programming/VSCode_WithAI/multi-agent-task-solver/.github/workflows/dependency-review.yml) checks new dependency risk on pull requests.
- [.github/dependabot.yml](/C:/Users/stefa/OneDrive/Programming/VSCode_WithAI/multi-agent-task-solver/.github/dependabot.yml) keeps NuGet packages and GitHub Actions updated.
- Coverage reports are always available as workflow artifacts. Codecov upload is enabled automatically for public repos and can be enabled for private repos with the `ENABLE_CODECOV=true` repository variable.
- CodeQL runs automatically for public repos. For private repos, set `ENABLE_CODEQL=true` only after GitHub Code Security is available for the repository.

## AI workflow baseline

- `AGENTS.md` is the primary repository contract.
- `docs/ai-workflow.md` defines the planning, implementation, verification, and review loop.
- `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` define upcoming implementation contracts.
- `.github/` contains reusable prompts, instructions, agents, and CI workflow files.
- `.vscode/` contains shared settings, tasks, and MCP baseline.
- New MAUI controls should include stable `AutomationId` values so the UI stays automation-ready.
