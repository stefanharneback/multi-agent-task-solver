---
description: Run monthly or quarterly maintenance review for dependency, provider API, AI workflow, and MAUI drift.
agent: reviewer
---

Run a maintenance review using `AGENTS.md`, `README.md`, `docs/ai-workflow.md`, `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, `docs/provider-expansion.md`, and `docs/maintenance-cadence.md`.

Cadence: ${input:cadence:Choose monthly or quarterly}

Research requirement:

- For anything that depends on current external state, use internet or web access when available.
- Prefer official documentation, API references, release notes, changelogs, pricing pages, and primary vendor sources.
- If the tool cannot browse the web, state that explicitly in the report and mark the review as partial rather than guessing.

Review:

1. Dependency, SDK, MAUI, and toolkit drift.
2. OpenAI gateway contract drift and any downstream client impact.
3. AI application drift:
   - provider APIs, model availability, pricing, and capability changes relevant to this repo
   - whether current patterns for prompting, storage, critiques, outputs, retries, and provider usage still fit
4. AI workflow drift:
   - prompts, custom agents, instructions, MCP or editor settings, and evaluation habits
5. CI, docs, and maintenance workflow relevance.

Return:

- create or update a dated report in `docs/maintenance-reviews/` named `YYYY-MM-DD-${input:cadence}.md`
- start from `docs/maintenance-reviews/template.md`
- include review date, cadence, scope, sources checked, commands run, findings, required actions, follow-ups, and limitations
