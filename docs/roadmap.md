# Roadmap

This roadmap defines the next major milestones for the repository. Update it when scope boundaries move, not only when code lands.

## Principles

- Keep the application desktop-first, single-user, and file-first until there is a clear reason to add shared backend orchestration.
- Keep provider abstractions neutral so OpenAI is first, not permanently special.
- Treat every milestone as a contract update across code, tests, docs, prompts, and agents.
- Preserve human-readable canonical task artifacts even when performance-oriented derived files are added.

## Milestone 0

- Status: complete
- Scope:
  - solution bootstrap
  - file-first task storage
  - seeded OpenAI model catalog
  - first gateway adapter
  - MAUI task, details, settings, and run-history shell
- Exit criteria:
  - build and tests are stable
  - docs and workflow baseline exist
  - storage and gateway boundaries are explicit

## Milestone 1

- Name: task review loop
- Scope:
  - review-agent run kind
  - explicit task readiness feedback
  - user approval gate before worker execution
  - persisted prompt, response, and usage artifacts for each review step
- Required tests:
  - state transition coverage for draft -> review -> approved or revise
  - artifact persistence tests for review runs
  - manual MAUI smoke for task edit and review flows
- Docs to update:
  - `docs/agent-loop.md`
  - `docs/testing-strategy.md`
  - `README.md`

## Milestone 2

- Name: worker execution loop
- Scope:
  - worker-agent run kind
  - output artifact creation under `outputs/`
  - iteration support after review approval
  - cost, duration, and model capture per worker step
- Required tests:
  - output artifact persistence and references
  - retry or re-run behavior without overwriting history
  - manual smoke for worker execution and output inspection
- Docs to update:
  - `docs/agent-loop.md`
  - `docs/testing-strategy.md`
  - `README.md`

## Milestone 3

- Name: critic and completion loop
- Scope:
  - critic-agent run kind
  - user decision step for done vs rework
  - feedback handoff back into worker iteration
  - preserved audit trail across review, worker, critic, and approval steps
- Required tests:
  - user-gated progression checks
  - critic-to-worker feedback loop persistence
  - manual smoke for completion and rework flows
- Docs to update:
  - `docs/agent-loop.md`
  - `docs/testing-strategy.md`
  - prompts and reviewer guidance

## Milestone 4

- Name: evaluation and reporting
- Scope:
  - run summary artifacts
  - cost and token rollups
  - evaluation or score artifacts for solution quality
  - maintenance and release reporting improvements
- Required tests:
  - usage rollup calculations
  - evaluation artifact persistence
  - report generation or export checks if added

## Milestone 5

- Name: provider expansion
- Scope:
  - additional provider adapters and settings
  - model catalogs and capability normalization for non-OpenAI providers
  - sibling gateway repos when server-side-only credentials remain a requirement
- Required tests:
  - adapter contract parity tests
  - provider capability and usage normalization tests
  - manual smoke for provider selection and provider-specific failures
- Docs to update:
  - `docs/provider-expansion.md`
  - `docs/testing-strategy.md`
  - `README.md`

## Cross-cutting additions

- attachment preprocessing and extracted-text caching
- structured outputs and evaluation schemas
- prompt template versioning
- migration handling for manifest schema changes
- optional team or shared-backend scenarios if single-user local-first stops fitting

## Update rule

When a task changes milestone scope, agent roles, storage shape, or verification expectations, update this roadmap in the same change.
