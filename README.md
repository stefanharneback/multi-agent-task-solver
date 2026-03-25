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
- a MAUI shell for tasks, task details, run history, and settings

The full review -> worker -> critic execution loop is not implemented yet. The current execution slice covers only the task-review agent path.
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
       |- review workflow, prompt assembly, and file-reference resolution
       |- JSON-backed model catalog
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
  MultiAgentTaskSolver.Core.Tests/
  MultiAgentTaskSolver.Infrastructure.Tests/
config/
  providers/openai.models.json          seeded OpenAI model metadata
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
dotnet restore MultiAgentTaskSolver.sln
dotnet build MultiAgentTaskSolver.sln
dotnet test MultiAgentTaskSolver.sln --no-build
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

Model discovery is seeded locally from `config/providers/openai.models.json` in this milestone.

## Security notes

- Store gateway base URLs and workspace paths in local JSON settings.
- Store gateway bearer keys in MAUI `SecureStorage`.
- Do not commit real workspace roots, local task data, or credentials.

## AI workflow baseline

- `AGENTS.md` is the primary repository contract.
- `docs/ai-workflow.md` defines the planning, implementation, verification, and review loop.
- `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` define upcoming implementation contracts.
- `.github/` contains reusable prompts, instructions, agents, and CI workflow files.
- `.vscode/` contains shared settings, tasks, and MCP baseline.
