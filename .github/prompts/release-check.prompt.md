---
description: Run a release readiness review across code, tests, docs, and workflow sync.
agent: reviewer
---

Perform a release-readiness check:

1. Verify docs, prompts, agents, and code consistency.
2. Verify `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` still match the current implementation and next milestone assumptions.
3. Verify relevant test coverage exists for changed behavior.
4. Verify no secrets or local artifacts are included.
5. Verify build and test commands for changed surfaces are passing.
6. Return a go or no-go verdict with findings.
