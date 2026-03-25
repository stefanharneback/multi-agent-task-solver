---
description: Use these guidelines when editing .NET, MAUI, and shared project files.
applyTo: src/**/*.{cs,csproj,props,targets,xaml,json}
---

## Dotnet guidelines

- Keep shared domain and provider abstractions in `src/MultiAgentTaskSolver.Core`.
- Keep filesystem and gateway integration in `src/MultiAgentTaskSolver.Infrastructure`.
- Keep `src/MultiAgentTaskSolver.App` as the presentation layer plus platform-specific settings handling.
- Prefer async APIs and cancellation-token aware methods.
- Keep canonical task artifacts human-readable.
- Update `docs/agent-loop.md` when changing run kinds, step states, or approval flow.
- Update `docs/provider-expansion.md` when changing provider contracts or capability shape.
- Add tests for storage shape, provider parsing, gateway error mapping, and state transitions when those surfaces change.
