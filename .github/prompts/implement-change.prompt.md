---
description: Plan and implement a scoped change with tests and docs sync.
agent: implementer
---

Task: ${input:task:Describe the change to implement}

Workflow:
1. Produce a short plan with scope, files, risks, tests, and which of `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, or `docs/provider-expansion.md` apply.
2. Implement the minimal required changes.
3. Update tests and docs impacted by behavior changes.
4. Run relevant checks for changed surfaces, preferring `dotnet build` followed by `dotnet test --no-build`.
5. Return a concise result summary and verification output.
