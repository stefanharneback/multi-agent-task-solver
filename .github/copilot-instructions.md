# Copilot Repository Instructions

Use `AGENTS.md` as the primary repository contract and `docs/ai-workflow.md` as the workflow reference.

- This is a MAUI task workspace with file-first task storage and provider-neutral abstractions.
- Keep UI layers thin over shared services in `Core` and `Infrastructure`.
- Preserve readable task artifacts and stable manifest shapes.
- Keep provider-specific request and usage handling isolated from app workflows.
- Pull in `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` when the task touches those areas.
- When changing behavior, update implementation, tests, docs, and prompts/agents together.
- Do not commit secrets, bearer keys, local task data, local overrides, or editor-private state.
- Before finishing code changes, prefer build plus `dotnet test --no-build` over a second implicit build.
