# Repository Agents Guide

This repository is a .NET MAUI task workspace for file-first multi-agent task solving on top of gateway services such as `openai-api-service`.

## Project goals

- Keep the first milestone desktop-first, single-user, and foundation-oriented.
- Treat each task as a readable folder with stable manifests and artifacts.
- Keep provider integration behind explicit abstractions so OpenAI is first, not forever special.
- Preserve a strong paper trail for prompts, responses, usage, cost, and user approvals.
- Keep docs, prompts, custom agents, and tests aligned with behavior changes.

## Working agreement

- Read `README.md`, `AGENTS.md`, and `docs/ai-workflow.md` before broad changes.
- Read `docs/roadmap.md` when planning milestone work or changing scope boundaries.
- Read `docs/agent-loop.md` when changing run types, step states, approvals, or critique flow.
- Read `docs/testing-strategy.md` when adding tests or changing verification expectations.
- Read `docs/provider-expansion.md` when touching provider abstractions or adding providers.
- Treat this repo as security-sensitive: never commit secrets, bearer keys, tokens, or local machine credentials.
- Prefer minimal diffs and keep MAUI UI code thin over shared services.
- When changing behavior, update implementation, tests, docs, and relevant prompt/agent files together.
- Keep canonical task files human-readable. Only `cache/` may contain machine-oriented derived artifacts.
- Do not assume hidden model chain-of-thought is portable or storable. Persist explicit summaries and critiques instead.

## Commands

- Restore: `dotnet restore MultiAgentTaskSolver.sln`
- Build: `dotnet build MultiAgentTaskSolver.sln`
- Test: `dotnet test MultiAgentTaskSolver.sln --no-build`
- Verify locally: run build first, then run tests with `--no-build`
- MAUI workload restore: `dotnet workload restore`

## Maintenance cadence

- Run a lightweight maintenance pass monthly.
- Run a deeper architecture and tooling review quarterly.
- Use `docs/maintenance-cadence.md` as the checklist.
- Treat provider API drift, model drift, MAUI/.NET SDK drift, and AI workflow drift as normal maintenance work.

## Done criteria

- `dotnet build MultiAgentTaskSolver.sln` passes.
- `dotnet test MultiAgentTaskSolver.sln --no-build` passes for code changes after a successful build.
- Manual smoke coverage is run when MAUI navigation, settings, or task-folder flows change.
- Docs, prompts, and agents are updated when workflow behavior changes.
- Secrets and local task data remain out of version control.
