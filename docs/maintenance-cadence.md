# Maintenance Cadence

## Monthly

- Check `.NET`, MAUI, toolkit, and test dependency updates.
- Review OpenAI gateway contract drift against `openai-api-service`.
- Review current provider APIs, model availability, pricing, and capability changes that could affect this repo.
- Verify current external changes against official online sources when the reviewing tool supports web access.
- Review AI and agent development guidance for drift:
  - prompts, custom agents, and instruction files still match current workflows
  - current approaches for prompting, storage, critiques, structured outputs, retries, and provider abstraction still fit
  - `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` still match the codebase direction
- Re-run basic end-to-end scenarios:
  - create task
  - reload task
  - import files
  - read task details and run history
  - OpenAI gateway health and usage access when configured
- Capture any changes needed in code, docs, or workflow files.
- Write a dated report in `docs/maintenance-reviews/` using `docs/maintenance-reviews/template.md`.

## Quarterly

- Reassess architecture boundaries between `App`, `Core`, and `Infrastructure`.
- Reassess storage shape and whether files-only is still the right tradeoff.
- Reassess provider strategy, model catalog shape, and normalization boundaries.
- Review whether `openai-api-service` and any future sibling gateways still reflect the best current approach.
- Reassess `docs/roadmap.md`, `docs/agent-loop.md`, `docs/testing-strategy.md`, and `docs/provider-expansion.md` for drift.
- Review CI path filters and build duration.
- Review MAUI, Windows-first UX, and folder-import ergonomics.
- Review prompt, critique, evaluation, and approval workflow design for the planned agent loop.
- Clean up stale docs, derived artifact policies, and unused configuration.
- Write a dated report in `docs/maintenance-reviews/` with `docs/maintenance-reviews/template.md`, including sources, findings, actions, and open questions.
