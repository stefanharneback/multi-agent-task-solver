---
description: Run monthly or quarterly maintenance review for dependency, provider API, AI workflow, and MAUI drift.
agent: reviewer
---

References:

- [AGENTS.md](../../AGENTS.md)
- [README.md](../../README.md)
- [docs/ai-workflow.md](../../docs/ai-workflow.md)
- [docs/roadmap.md](../../docs/roadmap.md)
- [docs/agent-loop.md](../../docs/agent-loop.md)
- [docs/testing-strategy.md](../../docs/testing-strategy.md)
- [docs/provider-expansion.md](../../docs/provider-expansion.md)
- [docs/maintenance-cadence.md](../../docs/maintenance-cadence.md)
- [docs/maintenance-reviews/template.md](../../docs/maintenance-reviews/template.md)

Cadence: ${input:cadence:Choose monthly or quarterly}

## Phase 1 — Run verification commands

Run each command below in a terminal. Record the outcome (pass/fail, counts, versions) for the report.

1. `dotnet restore MultiAgentTaskSolver.sln`
2. `dotnet build MultiAgentTaskSolver.sln`
3. `dotnet test MultiAgentTaskSolver.sln --no-build` — record test count and pass/fail
4. `dotnet test MultiAgentTaskSolver.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results` — record coverage percentages for Core, Infrastructure, App
5. `dotnet list src/MultiAgentTaskSolver.Core/MultiAgentTaskSolver.Core.csproj package --outdated`
6. `dotnet list src/MultiAgentTaskSolver.Infrastructure/MultiAgentTaskSolver.Infrastructure.csproj package --outdated`
7. `dotnet list src/MultiAgentTaskSolver.App/MultiAgentTaskSolver.App.csproj package --outdated`

## Phase 2 — Gather current state

Read these files and note their current versions or key values:

- `global.json` — record the SDK version
- `Directory.Build.props` — record shared build properties
- `config/providers/openai.models.json` — record the model list and compare against the gateway's costing catalog
- `src/MultiAgentTaskSolver.Core/MultiAgentTaskSolver.Core.csproj` — record TargetFramework and package versions
- `src/MultiAgentTaskSolver.Infrastructure/MultiAgentTaskSolver.Infrastructure.csproj` — record TargetFramework and package versions
- `.github/workflows/dotnet.yml` — record the steps, .NET version, and coverage thresholds
- `docs/roadmap.md` — note current milestone and scope boundaries

## Phase 3 — Check for drift

Research requirement: use internet/web access when available. Prefer official docs and release notes. If web access is unavailable, state that explicitly and mark affected checks as partial.

1. **Dependency drift**: compare `dotnet list package --outdated` output against stable releases. Check .NET SDK, MAUI workloads, CommunityToolkit.Mvvm, xUnit, coverlet versions.
2. **Gateway contract drift**: compare `config/providers/openai.models.json` against the gateway repo's `src/lib/costing.ts` model list. Note any models in the gateway that are missing from the local catalog.
3. **Provider API drift**: check current OpenAI model availability, pricing, and capability changes.
4. **MAUI/SDK drift**: check current .NET SDK, MAUI workloads, and Windows SDK versions.
5. **AI/agent workflow drift**: check whether `.github/prompts/`, `.github/instructions/`, `.github/agents/`, `.vscode/`, and `AGENTS.md` still match current best practices.
6. **CI drift**: check whether `.github/workflows/` steps, actions versions, and .NET version are current.
7. **Architecture docs drift**: check whether `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` still match the codebase.

If this is a **quarterly** review, also:

8. Reassess architecture boundaries between App, Core, and Infrastructure.
9. Reassess storage shape and whether files-only is still the right tradeoff.
10. Reassess provider strategy, model catalog shape, and normalization boundaries.
11. Review prompt, critique, evaluation, and approval workflow design.

## Phase 4 — Check previous reviews

Read the most recent report in `docs/maintenance-reviews/` and check:

- Were all required actions from the previous review completed?
- Are any follow-up items still open?

## Phase 5 — Write the report

Create a file named `docs/maintenance-reviews/YYYY-MM-DD-${input:cadence}.md` starting from `docs/maintenance-reviews/template.md` with this filled-in structure:

```markdown
# Maintenance Review Template

## Metadata

- Review date: YYYY-MM-DD
- Cadence: ${input:cadence}
- Reviewer: (your identity)
- Scope: (one-paragraph summary)
- Overall outcome: green | amber | red

## Sources checked

- Repository files: (list key files read)
- External sources: (list URLs consulted)
- Notes or limits: (web access availability)

## Commands run

- (list each command and its outcome: pass/fail, counts, coverage percentages)

## Previous review follow-up

- (status of each action/follow-up from the last review)

## Findings

(for each finding:)
- Severity: low | medium | high
- File or area:
- Description:
- Required action:

## Follow-ups

- Owner: (team or individual)
- Due or next review:
- Tracking note:

## Limitations

- Missing access:
- Partial checks:
- Assumptions:
```

## Checklist

Before finishing, verify:

- [ ] All Phase 1 commands were run and results recorded
- [ ] Key file versions were gathered in Phase 2
- [ ] External and contract drift was checked in Phase 3
- [ ] Previous review follow-ups were checked in Phase 4
- [ ] Report file was created with the correct name and all sections filled
