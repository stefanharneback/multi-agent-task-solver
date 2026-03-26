# AI Workflow

This repository keeps AI guidance layered so the workflow stays editor-neutral and portable across VS Code, Copilot, Gemini, Codex, and Antigravity.

## Source of truth

- `AGENTS.md`: primary repository contract
- `README.md`: overview, setup, and architecture
- `docs/roadmap.md`: milestone scope and extension points
- `docs/agent-loop.md`: run, step, approval, and critique workflow contract
- `docs/testing-strategy.md`: verification and acceptance coverage expectations
- `docs/provider-expansion.md`: provider-neutral growth rules
- `config/providers/*.json`: local provider and model seed metadata

Editor-specific files refine this guidance and must not replace it.

## Workspace AI files

- `.github/copilot-instructions.md`: repository-wide Copilot instructions
- `.github/instructions/*.instructions.md`: file-pattern guidance
- `.github/prompts/*.prompt.md`: reusable workflows
- `.github/agents/*.agent.md`: custom subagents
- `.vscode/settings.json`: Copilot and chat defaults
- `.vscode/tasks.json`: standard tasks
- `.vscode/mcp.json`: shared MCP baseline
- `.aiexclude`: local context exclusions

## Recommended loop

1. Plan
- Use the `planner` agent for scoped implementation plans.
- Start with `AGENTS.md` and pull in `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` when they apply.
- Identify code, tests, docs, prompts, and task-storage impacts.

2. Implement
- Use the `implementer` agent.
- Keep behavior, storage shape, and docs aligned in the same change.

3. Verify
- Prefer `dotnet build MultiAgentTaskSolver.sln` followed by `dotnet test MultiAgentTaskSolver.sln --no-build`.
- Use the Release equivalent before shipping milestone-sized changes or doing a release check.
- Add automated coverage for storage boundaries, provider mappings, and state transitions when those areas change.
- Add or update `tests/MultiAgentTaskSolver.App.Tests` when MAUI-facing behavior changes.
- Add `AutomationId` values for new interactive MAUI controls so later UI automation stays feasible.
- Verify manual task-folder and MAUI flows when storage, navigation, settings, or page behavior changes.

4. Review
- Use the `reviewer` agent with findings-first output.
- Prioritize correctness, regression risk, security, and missing tests.

5. Record
- Update the roadmap when milestone boundaries or major scope assumptions change.
- Update agent-loop, testing, and provider-expansion docs when behavior or future expectations move.
- Treat maintenance and release checks as incomplete if the docs and prompt/agent layer are out of sync with implementation.

## Maintenance cadence

- Run a monthly quick pass using `docs/maintenance-cadence.md`.
- Use the quarterly pass to review architecture boundaries, provider contracts, MAUI/.NET guidance, and AI workflow patterns.
- Treat the maintenance pass as incomplete until there is a dated report in `docs/maintenance-reviews/`.

## Antigravity note

Treat this file and `AGENTS.md` as canonical workflow references. Tool-specific files are adapters.
