# Provider Expansion

This document defines how the application should grow beyond the initial OpenAI-only gateway integration.

## Principles

- Keep provider identity separate from model identity.
- Keep provider-specific request or response shapes out of `Core`.
- Normalize capability and usage metadata at the adapter boundary.
- Prefer sibling gateway services when credentials must remain server-side.

## Current baseline

- OpenAI is integrated through `openai-api-service`.
- Models are seeded locally from `config/providers/openai.models.json`.
- The MAUI app talks to provider adapters, not directly to provider-native SDKs.

## Planned provider model

For each provider:

- one provider ID
- one adapter implementing the shared contract
- one model catalog or discovery source
- one usage normalizer
- one settings surface for gateway base URL and client credential

## Likely gateway pattern

- `openai-api-service`
- `google-api-service`
- `anthropic-api-service`

The app should not care which provider sits behind the gateway as long as the normalized contract holds.

## Capability contract

Capability metadata should remain provider-neutral. Examples:

- text input
- image input
- audio input
- streaming
- tool calls
- structured outputs
- context window

If a provider adds a new capability type, extend the shared capability model deliberately rather than stuffing raw provider flags into app logic.

## Usage normalization

Every provider adapter should aim to normalize:

- provider ID
- model ID
- request correlation ID
- recorded timestamp
- input tokens
- output tokens
- cached tokens where available
- reasoning tokens where available
- total tokens
- total cost
- duration
- HTTP status

Store raw provider payloads only as supporting artifacts, not as the application contract.

## Add-provider checklist

When adding a new provider:

- add provider configuration seed or discovery implementation
- add adapter and usage normalizer
- add settings handling
- add tests for success, validation failure, auth failure, and usage parsing
- update `README.md`
- update `docs/testing-strategy.md`
- update `docs/provider-expansion.md`
- update prompts or agents if workflow assumptions change

## Anti-patterns

- hardcoding OpenAI message or usage shapes in shared domain models
- leaking provider-specific SDK types into `Core`
- making UI logic special-case a provider when the adapter boundary should handle it
- treating a provider addition as docs-optional

## Future note

If server-side credentials stop being a requirement, revisit this document before introducing direct client-side provider access.
