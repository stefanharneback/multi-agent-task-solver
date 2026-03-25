---
description: Use these guidelines when editing or adding automated tests.
applyTo: tests/**/*.cs
---

## Testing guidelines

- Prefer focused tests with explicit arrange-act-assert structure.
- Use temporary directories for filesystem tests and clean up deterministically.
- Mock gateway HTTP via custom `HttpMessageHandler` stubs rather than live calls.
- Prefer `dotnet build` followed by `dotnet test --no-build` for local verification unless a rebuild is intentional.
- Cover manifest round-trips, folder conventions, import behavior, path-boundary protection, provider config loading, gateway error mapping, and future state-transition logic when those areas change.
- Add manual smoke notes when MAUI UI or task-folder flows change and there is no automated UI coverage.
