---
description: Use these guidelines when editing MAUI pages, view models, and app bootstrapping.
applyTo: src/MultiAgentTaskSolver.App/**/*.{cs,xaml,csproj}
---

## MAUI guidelines

- Prefer MVVM with `CommunityToolkit.Mvvm`.
- Keep code-behind thin and navigation explicit.
- Use DI for services and view model construction.
- Use readable page layouts over cleverness.
- Avoid platform-specific logic outside app service adapters.
- Run manual smoke coverage after navigation, settings, or task-folder interaction changes.
