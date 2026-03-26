# Consolidated Findings Report — Cross-Audit Synthesis

**Date:** 2026-03-26  
**Scope:** Re-review of [Report 1: Multi-Repo Analysis](multi_repo_analysis.md) and [Report 2: Advanced Multi-Agent Evolution](advanced_multi_agent_evolution.md), with new findings surfaced by cross-referencing the actual repository state.

---

## Part A — Gaps and Omissions in the Previous Reports

### 1. `mcp.json` Is Referenced But Does Not Exist

Both `docs/ai-workflow.md` files reference `.vscode/mcp.json` as a "shared MCP baseline" or "shared workspace MCP server configuration." However, **no `mcp.json` file exists** in either `multi-agent-task-solver` or `openai-api-service`.

> [!WARNING]
> This is a docs-versus-reality drift that should be fixed. Either create the files with an initial MCP configuration, or remove the references from documentation until MCP servers are intentionally configured.

**Recommended initial `mcp.json` configuration for `multi-agent-task-solver`:**
```json
{
  "servers": {
    "gitkraken": {
      "command": "gitkraken-mcp-server",
      "description": "Git history and blame for reviewer agent context"
    }
  }
}
```

### 2. No Cross-Repo Contract Testing

Report 1 mentioned `openapi.yaml` as the contract between the gateway and the clients. Report 2 suggested evolving the gateway into a Universal Model Router. **Neither report addressed how to prevent contract drift between the three repositories.**

**Missing piece:** There is no automated mechanism to verify that `openai-service-clients/openapi.yaml` stays in sync with `openai-api-service/openapi.yaml`. Today this relies entirely on the human maintenance cadence.

**Recommendation:** Add a CI workflow or a shared script that pulls the upstream `openapi.yaml` and diffs it against the local copy, failing the build on unexpected divergence. This could be a GitHub Action in `openai-service-clients` that fetches from `openai-api-service` on a schedule.

### 3. Observability and Telemetry — Completely Unaddressed

Neither report discussed **distributed tracing or observability** across the agent loop. The system currently persists `usage.json` per step, but there is no mention of:

- **OpenTelemetry integration** for tracing requests from MAUI → gateway → provider
- **Structured logging** with correlation IDs that span the full `review → worker → critic` chain
- **Cost dashboards** or alerting when token usage exceeds thresholds

**Recommendation:** Since the Microsoft Agent Framework natively supports OpenTelemetry, wire this up from day one. Add a `traces/` or `telemetry/` section to the task folder, or emit OpenTelemetry spans that can be collected by any OTLP-compatible backend (Jaeger, Grafana Tempo, Azure Monitor).

### 4. Security Hardening Beyond "Don't Commit Secrets"

Both reports acknowledged the security posture ("never commit secrets"), but neither addressed:

- **Prompt injection defense:** When importing user documents into `inputs/`, a malicious document could contain instructions that hijack the agent's prompt. Neither the gateway nor the MAUI app currently sanitizes or isolates user-provided content from system-level prompts.
- **Sandbox isolation for Computer Use:** Report 2 recommended Anthropic's Computer Use API but did not detail the sandboxing mechanism. On a desktop-first MAUI app, this needs explicit OS-level isolation (e.g., running the Computer Use agent in a locked-down VM or container).
- **Rate limiting at the gateway:** `openai-api-service` should enforce per-user or per-task rate limits to prevent runaway agent loops from burning through budgets.

### 5. No Concrete Package/Library Recommendations

Report 2 recommended the Microsoft Agent Framework and Semantic Kernel but did not list specific NuGet packages or npm modules to install.

**Concrete packages for `multi-agent-task-solver`:**

| Package | Purpose | NuGet/npm |
|---|---|---|
| `Microsoft.SemanticKernel` | Core AI orchestration | NuGet |
| `Microsoft.SemanticKernel.Agents.Core` | Multi-agent framework (RC) | NuGet |
| `Microsoft.SemanticKernel.Connectors.OpenAI` | OpenAI provider connector | NuGet |
| `Microsoft.SemanticKernel.Connectors.Google` | Google/Gemini connector | NuGet |
| `Stateless` | Deterministic state machine engine | NuGet |
| `NJsonSchema` | JSON Schema validation for task manifests | NuGet |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP trace export | NuGet |
| `Polly` | Retry, circuit-breaker, timeout policies | NuGet |

**Concrete packages for `openai-api-service`:**

| Package | Purpose | npm |
|---|---|---|
| `@anthropic-ai/sdk` | Anthropic Claude API client | npm |
| `@google/generative-ai` | Google Gemini API client | npm |
| `zod` | Runtime schema validation (already likely in use) | npm |
| `pino` | Structured JSON logging | npm |
| `@opentelemetry/sdk-node` | Node.js OpenTelemetry SDK | npm |

---

## Part B — Contradictions and Tensions Between the Two Reports

### 6. "Delete Custom Orchestration" vs. "File-First Immutable History"

Report 2 suggests adopting the Microsoft Agent Framework and "deleting all custom orchestration looping code." However, Report 1 emphasizes the project's core principle of **file-first immutable history** with human-readable `step.json`, `prompt.md`, and `response.md` files.

**Tension:** The Microsoft Agent Framework manages its own state internally (in-memory or via its checkpointing system). If adopted wholesale, you lose the human-readable `Task-<id>/runs/` folder structure that is a **core architectural principle**.

**Resolution:** Use the Microsoft Agent Framework for **orchestration only** (deciding which agent runs next, managing approval gates). But **intercept every step boundary** to persist your own canonical `step.json` + `prompt.md` + `response.md` artifacts. The framework's checkpoints become a performance/recovery mechanism; your files remain the audit trail.

### 7. "Universal Model Router" Gateway vs. "Sibling Gateway" Pattern

Report 2 proposes evolving `openai-api-service` into a Universal Model Router (`POST /v1/agent/critic` → OpenAI, `POST /v1/agent/desktop-worker` → Anthropic). But `docs/provider-expansion.md` explicitly recommends **sibling gateways** (`google-api-service`, `anthropic-api-service`).

**Tension:** A single universal gateway is simpler for the MAUI client but harder to maintain. Sibling gateways maintain separation of concerns but increase deployment complexity.

**Resolution:** Use a **hybrid approach**:
- Keep provider-specific gateways (`openai-api-service`, `anthropic-api-service`, `google-api-service`) for credential isolation and independent deployment.
- Add a thin **routing layer** in the MAUI app's Infrastructure project that selects the correct gateway based on the agent role configuration. The MAUI app never hardcodes a provider; it reads from task-level or settings-level configuration which gateway handles which role.

---

## Part C — New Recommendations Not in Either Report

### 8. Prompt Template Versioning

The roadmap lists "prompt template versioning" as a cross-cutting concern, but neither report addressed it concretely.

**Recommendation:** Store prompt templates as versioned files under `config/prompts/v1/review.prompt.md`, `config/prompts/v1/worker.prompt.md`, etc. When assembling a prompt, record the template version in `step.json`. This makes it possible to:
- A/B test different prompt versions
- Replay old tasks with new prompts
- Audit exactly which prompt was used for each historical step

### 9. Task Schema Migration Strategy

The roadmap mentions "migration handling for manifest schema changes" but neither report proposed a solution.

**Recommendation:** Add a `"schemaVersion": 1` field to `task.json`. When loading a task folder, the Infrastructure layer checks this version and runs migration functions if needed (e.g., adding new fields with defaults, renaming keys). This is critical before Milestone 2, because the worker loop will add new fields to `step.json` that Milestone 1 tasks won't have.

### 10. Phased Adoption Roadmap

Neither report provided a **sequenced adoption plan**. Here is a recommended order:

| Phase | What to Adopt | Why First |
|---|---|---|
| **Phase 1** | `Stateless` state machine + `NJsonSchema` validation | Foundation — prevents illegal transitions and malformed manifests before any new agent work |
| **Phase 2** | Structured Outputs in `openai-api-service` + prompt versioning | Enables reliable machine-parseable agent responses |
| **Phase 3** | `Microsoft.SemanticKernel.Agents.Core` for orchestration | Replaces hand-rolled loops with battle-tested framework |
| **Phase 4** | OpenTelemetry tracing + cost dashboards | Visibility into multi-step agent runs |
| **Phase 5** | Anthropic gateway + Google gateway (sibling services) | Multi-provider routing |
| **Phase 6** | LLM-as-a-Judge evaluation suite | Quality assurance across providers |
| **Phase 7** | Computer Use API + heavy sandbox | Desktop automation (highest risk, last) |

### 11. Maintenance Cadence Alignment

The maintenance cadences across the three repos are similar but not identical. `openai-api-service` has "trigger events" (react to major API changes immediately), while `multi-agent-task-solver` does not.

**Recommendation:** Add a "trigger events" section to `multi-agent-task-solver/docs/maintenance-cadence.md` mirroring the gateway's approach. Key triggers should include:
- Major .NET/MAUI SDK releases
- Microsoft Agent Framework GA release
- Breaking changes in any sibling gateway's `openapi.yaml`
- New provider capabilities (e.g., Anthropic adds streaming tool use)

---

## Summary

| # | Finding | Severity |
|---|---|---|
| 1 | `mcp.json` referenced but missing | Medium — docs drift |
| 2 | No cross-repo contract sync | High — silent API breakage risk |
| 3 | No observability/telemetry | High — blind to cost and failures at scale |
| 4 | Prompt injection not addressed | High — security risk |
| 5 | No concrete package list | Low — actionable gap |
| 6 | Framework adoption vs. file-first tension | Medium — needs explicit resolution |
| 7 | Universal router vs. sibling gateways tension | Medium — needs hybrid design |
| 8 | Prompt versioning not implemented | Medium — audit trail gap |
| 9 | No schema migration strategy | High — breaking change risk |
| 10 | No phased adoption order | Medium — execution risk |
| 11 | Maintenance cadence misalignment | Low — consistency gap |
