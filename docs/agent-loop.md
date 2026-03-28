# Agent Loop

This document defines the planned orchestration contract for the review, worker, critic, and user-decision loop. It is intentionally future-facing, but it should stay aligned with implementation as the loop lands.

## Current implementation note

- The repository currently implements the `task-review` and review-gate `user-decision` run kinds.
- The current path resolves referenced `@alias` artifacts, assembles a review prompt, calls the configured provider, and persists prompt, response, and usage artifacts.
- Review output can now be explicitly approved for worker execution or sent back to `draft`, with the user decision preserved as its own immutable run.
- Worker and critic execution are still not implemented, so `work-approved` is currently the terminal state for the Milestone 1 slice.

## Roles

- User: creates and edits tasks, approves progression, and decides done vs rework
- Review agent: critiques task completeness, ambiguity, and readiness
- Worker agent: performs the requested task once the task is approved
- Critic agent: evaluates worker output against the task and acceptance criteria
- Optional evaluator: future reporting or scoring agent, not required for early milestones

## Invariants

- Every model interaction must map to persisted run and step metadata.
- Prompt, response, and usage artifacts must be preserved without overwriting prior history.
- User approval is required between review and worker execution.
- User approval is required between critic output and final completion or rework.
- Hidden provider chain-of-thought is not treated as a portable artifact. Only explicit summaries, critiques, plans, and outputs are persisted.

## Planned run kinds

- `task-review`
- `worker`
- `critic`
- `user-decision`
- `evaluation`

## Planned task states

- `draft`
- `review-ready`
- `under-review`
- `work-approved`
- `working`
- `under-critique`
- `needs-rework`
- `done`
- `archived`

## Planned run and step states

- `planned`
- `running`
- `completed`
- `failed`
- `cancelled`
- `superseded`

## Persistence expectations

Each run should preserve immutable history under `runs/` using sequence-based folders. Each step should preserve:

- `step.json`
- `prompt.md`
- `response.md`
- `usage.json`

If future orchestration needs richer run-level metadata, add `run.json` rather than overloading `task.json`.

## Approval gates

- Review gate:
  - allowed outcomes: revise task, add or remove inputs, or approve for worker execution
- Critic gate:
  - allowed outcomes: mark done, request rework, or reopen task details

Automatic progression should never bypass these gates in the default workflow.

## Iteration rules

- A new review, worker, or critic attempt creates a new run sequence or a new step attempt. Do not overwrite prior artifacts.
- Model changes between attempts are allowed and must be persisted as part of step metadata.
- Critic feedback used for rework must be preserved as an explicit artifact reference or summary in the next worker run.

## Output expectations

- Worker artifacts belong under `outputs/`.
- Review and critic artifacts belong under `runs/` unless they are promoted into human-facing deliverables.
- Derived machine-only files belong under `cache/`.

## Future acceptance criteria

When the loop is implemented, the following must be true:

- review cannot auto-start worker execution without user approval
- critic cannot auto-close the task without user approval
- each step captures provider, model, timing, token usage, and cost where available
- reruns keep full prior history
- manual smoke coverage exists for the main happy path and rework path

## Required doc updates

Any change to run kinds, state names, approval rules, or persisted artifacts must update:

- `docs/agent-loop.md`
- `docs/testing-strategy.md`
- `README.md`
- relevant prompts and agents under `.github/`
