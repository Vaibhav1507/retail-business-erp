# RBOS Central AI Gateway — Pilot Architecture and Implementation Plan

**Status:** Proposed implementation specification  
**Target:** Controlled pilot  
**Repository:** `Vaibhav1507/retail-business-erp`  
**Branch:** `docs/pilot-readiness-remediation-plan`  
**Document type:** Architecture and implementation plan  
**Last reviewed:** 17 November 2026

---

## 1. Executive summary

RBOS should expose one central AI Gateway to every application feature that needs an AI capability. Feature modules must not call Gemini, Grok, or another model provider directly.

The gateway will provide:

- A provider-neutral interface for text, structured output, and (when needed) image/document understanding.
- Central provider and model selection.
- Quota-aware credential selection and provider failover.
- Retry, timeout, circuit-breaker, and concurrency controls.
- Per-feature and per-shop usage/cost tracking.
- Configurable budgets and safe degradation when AI is unavailable.
- A controlled credential pool that can contain more than one legitimate Gemini API project/key and more than one provider.

The first release is deliberately small. It is a reusable application capability, not a new microservice or a large AI platform. Keep the implementation consistent with the existing RBOS architecture and inspect the actual solution structure before choosing exact project/folder names.

**Pilot principle:** AI enhances usability; it must not become a dependency for core billing, inventory, accounting, or navigation.

## 2. Goals

1. Give all AI-enabled features one integration point.
2. Allow Gemini to be the default provider and Grok to be an optional fallback.
3. Support multiple configured credentials, including credentials belonging to distinct Gemini projects.
4. Avoid unnecessary provider calls and keep pilot costs predictable.
5. Handle rate limits and provider outages without crashing the application.
6. Make provider/model changes configuration-driven wherever practical.
7. Keep tenant isolation, data minimisation, and auditability mandatory.
8. Preserve the existing local voice-navigation implementation and its no-cloud-cost behaviour.

## 3. Non-goals for the pilot

- Building a general-purpose autonomous agent platform.
- Replacing deterministic business logic or database queries with LLM calculations.
- Using AI to create, edit, approve, or post financial transactions by voice.
- Guaranteeing unlimited or zero-outage AI capacity.
- Depending on account/key rotation to evade provider-imposed limits.
- Introducing a separate AI microservice unless the existing deployment topology makes that necessary.
- Sending all application data or full database records to an AI provider.
- Enabling paid usage or automatic credit top-ups without an explicit owner decision.

## 4. Central architecture

```text
RBOS features
  |-- Voice business Q&A (planned; read-only first)
  |-- Invoice / catalogue extraction
  |-- Product or text assistance
  |-- Future AI-enabled workflows
          |
          v
     IAIService / AI Gateway
          |
          +-- Request validation and task policy
          +-- Tenant / user authorization context
          +-- Provider and model routing
          +-- Credential-pool selection
          +-- Quota and health state
          +-- Retry / timeout / circuit breaker
          +-- Usage, cost and audit recording
          |
          +--> Gemini adapter --> Gemini project credential pool
          |
          +--> xAI adapter ------> Grok API credential(s)
          |
          +--> Future provider adapters / local model adapter
          |
          v
     Normalized AI response / typed result
```

Feature code should depend on the gateway abstraction and task-specific application services, never on vendor SDK types or API keys.

### 4.1 Suggested logical components

| Component | Responsibility |
|---|---|
| AI Gateway / application service | Single entry point for AI requests |
| Provider adapter | Translate normalized requests to provider API calls |
| Provider registry | Read enabled providers, models, capabilities and priorities |
| Credential pool | Hold multiple configured credentials and their project/team identity |
| Routing policy | Choose provider/model/credential based on task, health, quota and budget |
| Quota state store | Track observed rate-limit responses and cooldowns |
| Resilience policy | Timeout, bounded retry, backoff, jitter and circuit breaker |
| Usage recorder | Record token counts, latency, outcome and estimated cost |
| Budget policy | Enforce global, feature and tenant limits |
| Response validator | Validate structured outputs and reject invalid results |

These can initially be implemented as classes/services in the existing backend. Do not introduce a separate deployment solely for this abstraction.

## 5. Provider and API-key quota behaviour

### 5.1 Gemini: quota is project-scoped, not key-scoped

Google's Gemini API documentation says rate limits are applied per Google Cloud project, not per API key. Multiple keys belonging to the same project share that project's quota.

Therefore:

| Configuration | Does it multiply Gemini project quota? |
|---|---|
| Five API keys in one project | No |
| Two projects under one Google account | Each project has project-level quota, subject to account/billing-level constraints |
| Projects belonging to two different Google accounts | They may have independent project quotas, subject to provider terms, eligibility and account-level controls |
| Five keys distributed across projects | The project quotas, not the number of keys, determine capacity |

Google's billing documentation further states that projects linked to the same Cloud Billing account inherit its usage tier, rate limits and account caps. Do not assume that creating more projects creates unlimited capacity.

Official references:
- https://ai.google.dev/gemini-api/docs/rate-limits
- https://ai.google.dev/gemini-api/docs/billing
- https://ai.google.dev/gemini-api/docs/api-key

### 5.2 Multi-account / multi-project credential pool — explicitly in scope

The gateway design must support a configurable list of Gemini credentials, including credentials from distinct projects and, where legitimately authorised, distinct Google accounts.

This is useful for:
- Testing multiple independently configured projects.
- Separating legitimate projects/environments.
- Using separately entitled capacity where the provider permits it.
- Selecting a healthy project when another project has reached its own quota.

**Important restrictions and caveats:**

- Multiple keys from the same project do not increase its quota.
- Separate projects may have separate project quotas, but billing-account, account-level, model-specific, IP, abuse, or other provider restrictions may still apply.
- Separate Gmail accounts are not a guaranteed quota multiplier.
- Do not create or rotate accounts/projects for the purpose of circumventing provider limits or terms.
- Only configure accounts/projects that the project owner is authorised to use and whose use in this application is permitted by the provider.
- Do not describe this feature as unlimited free capacity.
- If provider policy or account eligibility is unclear, use one project or paid capacity instead of relying on the pool.

### 5.3 Grok / xAI

Treat xAI as a separate provider adapter with its own team credentials, model availability, rate limits, billing and error semantics. Multiple keys within one team should not be assumed to multiply team quota. Verify current xAI team limits and terms before configuring additional teams.

Official references:
- https://docs.x.ai/developers/rate-limits
- https://docs.x.ai/developers/pricing

### 5.4 Credential-pool selection — round-robin on every request

**The requested pilot behaviour is round-robin credential rotation: each new request should use the next eligible configured credential, rather than repeatedly using the first key.** Implement this as a deterministic, concurrency-safe round-robin cursor for each provider/model-compatible credential pool.

Example with five configured Gemini credentials:

- Request 1 → Credential 1
- Request 2 → Credential 2
- Request 3 → Credential 3
- Request 4 → Credential 4
- Request 5 → Credential 5
- Request 6 → Credential 1, continuing cyclically

Apply these rules:

1. Rotate to the next eligible credential on every new request. Do not wait for a rate-limit error before rotating.
2. Keep provider, model capability, environment, data-handling policy and budget eligibility as filters before a credential enters the rotation pool.
3. **Group credentials by their real quota boundary.** Multiple keys from the same Gemini project may rotate between requests, but they still share one project quota bucket; key rotation within that group does not increase capacity.
4. Credentials from distinct projects/accounts may be separate quota candidates only where provider terms, account eligibility and billing arrangements permit their use. Do not assume that separate Gmail accounts guarantee independent or additive quotas.
5. Maintain a separate round-robin cursor for each eligible provider/model pool. Make cursor advancement safe under concurrent requests (for example, an atomic counter or equivalent concurrency-safe mechanism).
6. On a 429/quota response, mark the relevant quota bucket (project-level for Gemini, and the applicable team/provider scope for other providers) as cooling down. Skip every credential mapped to that same exhausted quota bucket until its cooldown expires or provider guidance indicates recovery.
7. Continue round-robin selection among remaining eligible independent quota buckets. If none remain, use an approved alternate provider or return a controlled unavailable response.
8. Invalid/disabled credentials are removed from rotation until corrected. Do not retry them on every request.
9. Keep rotation configurable and observable: record a non-secret credential identifier, quota-bucket identifier, selection reason and failover outcome.

This is **round-robin across configured credentials**, not a promise that every credential represents additional quota. It must never be used to evade provider limits or terms. Keep the pool configurable and make it possible to disable multi-credential routing without code changes.

## 6. Request contract and feature integration

Expose a vendor-neutral request/response contract. Exact names should follow existing RBOS conventions.

Illustrative request fields:

- Feature name / capability.
- Task type (classification, text generation, structured extraction, image understanding, business Q&A, etc.).
- Prompt/instructions and minimum required context.
- Requested output format/schema.
- Tenant ID and authenticated actor context supplied by trusted server-side code.
- Sensitivity/data classification.
- Timeout and priority class.
- Optional idempotency/correlation ID.

Illustrative response fields:

- Normalized content or typed result.
- Provider and model identifiers.
- Usage metadata when supplied by provider.
- Correlation ID.
- Completion status.
- Safe error classification.

Never accept tenant identity or permission claims from an untrusted client payload as authoritative. Derive them from the authenticated request and server-side authorization context.

### 6.1 Feature examples

| Feature | Gateway use | Pilot policy |
|---|---|---|
| Existing voice navigation | No gateway call; keep current local implementation | Must remain local |
| Voice business Q&A | Read-only question answering over authorised business data | Planned; not to be advertised as currently available |
| Invoice/photo extraction | Extract candidate fields into a reviewable draft | Only if current feature is enabled; human review before save/post |
| Catalogue/photo extraction | Suggest product attributes | Human review before save |
| Future text assistance | Generate explanatory/help text | Feature-specific enablement |

Business calculations (sales totals, stock quantities, customer dues, supplier balances, profit figures) must be computed by deterministic RBOS services and queried from tenant-scoped records. The LLM may explain those trusted results, not invent or independently calculate the source-of-truth values.

Voice business Q&A is planned product scope, but it must be read-only initially, with permissions, date interpretation, source freshness, calculation definitions, ambiguity handling and drill-down validation. Voice-driven transaction creation/posting is outside this scope and requires a separate decision.

## 7. Routing policy

Use explicit task policies rather than a single universal model.

Example initial policy (configuration, not hard-coded):

| Task | Preferred route | Fallback |
|---|---|---|
| Lightweight classification | Economical Gemini model | Approved economical alternate |
| Simple text response | Economical Gemini model | Grok or another enabled provider |
| Structured extraction | Model with validated structured-output support | Alternate model followed by the same validator |
| Image/document extraction | Model with required image capability | Alternate image-capable model |
| Complex explanation | Stronger model only when justified | Safe unavailable response |
| Financial/business facts | RBOS query/calculation service first; LLM explanation second | Deterministic plain-language template |

Provider selection must consider:
- Task/model capability.
- Provider and credential health.
- Rate-limit state.
- Tenant/global budget availability.
- Data-handling eligibility.
- Latency and request priority.
- Configured provider priority.

Do not fail over to a model that does not support the requested capability or data-handling requirements.

## 8. Rate-limit and failure handling

Recognise provider responses and normalise them into categories such as:

- Rate limited / quota exhausted (commonly HTTP 429).
- Authentication or invalid credential.
- Permission / project not enabled.
- Billing or exhausted credit.
- Provider unavailable / server error.
- Timeout / network failure.
- Invalid request / unsupported model.
- Content safety refusal.
- Invalid or schema-incompatible response.

Recommended behaviour:

1. Retry transient failures only, with bounded exponential backoff and jitter.
2. Honour provider retry-after guidance where present.
3. Do not repeatedly retry invalid credentials, permission failures, billing failures or invalid requests.
4. Apply a per-credential/project cooldown after rate limiting.
5. Use a circuit breaker for repeated provider failures.
6. Fail over only to an eligible provider or independently entitled credential.
7. Avoid duplicate side effects. AI requests should be side-effect free; any later write workflow must use separate idempotent application commands and explicit confirmation.
8. Return a stable application-level error/result so feature modules do not need to understand vendor-specific errors.
9. Never expose API keys, provider raw error payloads, prompts containing sensitive data, or stack traces to end users.

The gateway cannot guarantee zero rate-limit errors or uninterrupted AI service. It should reduce user-visible failures and degrade gracefully when all providers are unavailable.

## 9. Configuration and secret management

Configuration should support:

- Global gateway enabled/disabled flag.
- Provider enabled/disabled flags.
- Provider/model priorities by task.
- Credential entries with provider, project/team identifier, environment label, enabled flag and secret reference.
- Per-credential concurrency and cooldown settings.
- Per-feature allow-list and data-sensitivity policy.
- Request timeouts and retry limits.
- Global and per-tenant usage/budget thresholds.
- Maximum input/output tokens where supported.
- Kill switch for a provider, credential pool or feature.

Secrets must be stored in the deployment's approved secret store or secure environment configuration. Never commit API keys to source control, appsettings files, logs, database seed data or frontend code. Do not send provider credentials to browsers/mobile clients.

Use separate credentials/configuration for local development, test and pilot/production environments. Do not use company/H&M credentials, tenants or subscriptions.

## 10. Usage, cost and operational visibility

Record a minimal usage event for every attempt:

- Timestamp.
- Tenant/shop ID.
- Actor ID only where necessary and permitted.
- Feature/task.
- Provider, model and credential reference (never the secret).
- Input/output token counts when available.
- Estimated cost and currency when pricing is configured.
- Duration/latency.
- Outcome and normalised error category.
- Retry/failover count.
- Correlation ID.

Do not store full prompts or responses by default. If debugging capture is ever enabled, redact sensitive fields, restrict access, define retention and obtain the appropriate approval.

Provide operational views/metrics for:
- Requests by provider/model/feature.
- Success, failure, 429 and timeout rates.
- Provider failover count.
- Credential cooldown and health.
- Token usage and estimated cost.
- Budget consumption by tenant and feature.
- Average and percentile latency.

## 11. Budget and pilot safeguards

The gateway must enforce application-side spending controls independently of provider billing dashboards.

Suggested initial pilot defaults (owner-configurable, not provider guarantees):

| Control | Initial proposal |
|---|---:|
| Global monthly AI budget | ₹1,500 |
| Per-shop monthly soft budget | ₹25–₹50 |
| Warning threshold | 75% |
| Hard stop for paid calls | 100% |
| Automatic paid top-up | Disabled |
| Expensive-model fallback | Disabled unless explicitly approved |

At hard limit:
- Stop paid calls rather than silently spending more.
- Continue non-AI application operations.
- Preserve local voice navigation.
- Queue only suitable non-urgent work if queueing is implemented.
- Show a clear, non-technical availability message.
- Allow an authorised operator to change the budget/configuration.

These amounts are pilot starting hypotheses. Measure real usage, including image and future audio workloads, before setting a customer-facing price.

## 12. Privacy, security and tenant isolation

- Enforce tenant isolation before preparing AI context and again when retrieving source records.
- Send only the minimum information needed for the task.
- Remove unnecessary personal/customer identifiers, secrets, payment details and credentials.
- Do not send entire tables, unrestricted database dumps or full transaction histories.
- Apply role-based access before a business question is answered.
- Treat model output as untrusted input; validate schema, length, content and business rules.
- Never execute generated SQL, code, commands or actions directly.
- Never let model output decide tenant identity or override permissions.
- Review each provider's current data-use, retention, residency and contractual terms before sending real pilot-shop data.
- Use synthetic or anonymised data for development and provider experimentation.
- Maintain auditability without logging sensitive prompt/response content by default.

Google's Gemini API documentation distinguishes free and paid service data-use terms. The pilot must review the current terms and privacy requirements before using real shop data; paid API usage should be preferred for production-sensitive workloads where the applicable terms provide the required protections.

References:
- https://ai.google.dev/gemini-api/docs/billing
- https://ai.google.dev/gemini-api/docs/usage-policies
- https://docs.x.ai/developers/

## 13. Implementation phases

### Phase 0 — Repository and integration discovery

Before coding:
- Inspect the current backend solution, dependency injection, configuration, logging, persistence and test conventions.
- Identify existing AI/provider calls and feature integrations.
- Identify current invoice/catalogue extraction flows and local voice pipeline.
- Confirm target runtime and deployment secret-management mechanism.
- Avoid duplicating an existing abstraction.

**Exit:** A short implementation mapping to actual repository projects/files, with no assumptions about names or existing capabilities.

### Phase 1 — Minimal central gateway

- Define provider-neutral request/response contracts.
- Add gateway interface and application service.
- Add one Gemini adapter.
- Add secure configuration/secret binding.
- Add normalised error handling, timeout and bounded retry.
- Add structured logging and correlation.
- Add unit tests with mocked provider responses.

**Exit:** One pilot feature can call Gemini only through the gateway; no direct vendor dependency in the feature.

### Phase 2 — Credential pool and failover

- Add configurable credential entries.
- Track project identity separately from key identity.
- Implement project-aware quota cooldown.
- Implement concurrency-safe round-robin selection so each new request advances to the next eligible configured credential. Group same-project Gemini keys under one shared quota bucket; on quota exhaustion, skip all keys in that bucket and rotate among remaining eligible independent quota buckets.
- Add optional Grok adapter.
- Add provider-level circuit breaker and capability-aware fallback.
- Add feature flags to disable credential pooling/provider failover.

**Exit:** Tests prove same-project keys share a quota bucket; distinct configured projects are separate candidate buckets subject to provider policy; fallback is attempted only when eligible.

### Phase 3 — Usage and budget enforcement

- Record usage and latency metadata.
- Estimate cost using configurable model pricing.
- Add global and per-tenant budget policies.
- Add soft warning and hard-stop behaviour.
- Add dashboard/reporting or an operator query for pilot usage.
- Keep paid top-up disabled.

**Exit:** Usage and budget tests demonstrate that paid calls stop at the configured hard limit.

### Phase 4 — Feature adoption

Integrate one feature at a time:
1. Existing AI extraction workflow, if present and approved.
2. Future read-only voice business Q&A.
3. Additional AI features only after task-specific privacy, accuracy and UX review.

Preserve local voice navigation. Do not migrate it to a cloud provider as part of this work.

**Exit:** Every AI-enabled feature uses the central gateway; no direct provider calls remain outside approved adapters/tests.

## 14. Acceptance criteria

### Architecture
- [ ] All production feature modules use one central gateway abstraction.
- [ ] Provider SDKs and vendor-specific types are isolated in adapters.
- [ ] Provider/model choice is configurable.
- [ ] The gateway can be disabled without disabling core RBOS functionality.

### Credential pool
- [ ] Multiple credentials can be configured without committing secrets.
- [ ] Credential metadata includes provider and project/team identity.
- [ ] Every new request advances the concurrency-safe round-robin cursor to the next eligible configured credential.
- [ ] Multiple keys in one Gemini project may rotate per request but map to one shared quota bucket and are never counted as separate quota capacity.
- [ ] Distinct project credentials can be selected independently only where their use is authorised and permitted.
- [ ] A 429/quota response cools down the entire applicable quota bucket, not just one API key; other keys in the same exhausted project are skipped.
- [ ] When a quota bucket is cooling down, requests rotate among remaining eligible independent buckets or use an approved provider fallback.
- [ ] No infinite rotation, unbounded retry or claim of unlimited capacity.
- [ ] A provider/credential can be disabled through configuration.

### Resilience
- [ ] 429 responses respect retry guidance and bounded backoff.
- [ ] Invalid credentials are not repeatedly retried.
- [ ] Circuit breaker prevents repeated calls to unhealthy providers.
- [ ] Unsupported tasks do not fail over to incapable models.
- [ ] All-provider outage returns a controlled response.

### Security and privacy
- [ ] Secrets are never exposed to clients or logs.
- [ ] Tenant scope and permissions are enforced before context is sent.
- [ ] Sensitive fields are minimised/redacted.
- [ ] Model output is validated and treated as untrusted.
- [ ] No generated action/transaction is automatically posted.

### Cost and observability
- [ ] Token usage and estimated cost are recorded where available.
- [ ] Global and per-tenant limits are enforced.
- [ ] Paid calls stop at hard budget limit.
- [ ] Provider, feature, latency and failure metrics are available.

### Feature behaviour
- [ ] Existing local voice navigation remains unchanged and local.
- [ ] Core billing, inventory and accounting work without AI.
- [ ] Future voice business Q&A is read-only in its initial scope.
- [ ] Invoice/catalogue extraction remains review-before-save/post.

## 15. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Multiple keys mistaken for multiple quotas | Group credentials by provider project/team quota identity |
| Provider changes free-tier eligibility or terms | Keep pool optional/configurable; review terms before pilot |
| One account/project is suspended or quota-limited | Use an independent provider fallback; graceful degradation |
| Silent cost escalation during failover | Budget check before every attempt; expensive fallback disabled by default |
| Sensitive shop data sent to provider | Data minimisation, provider policy review, synthetic test data |
| AI hallucination of business facts | Deterministic queries/calculations and source drill-down |
| Provider-specific response differences | Normalised contracts and feature-level validation |
| AI outage blocks core business operation | Keep core workflows independent of AI |

## 16. Decisions to preserve

1. Central gateway is the only supported integration point for application AI calls.
2. Gemini is the initial default candidate; Grok is an optional independent fallback.
3. Every request rotates round-robin to the next eligible configured credential. Multiple Gemini credentials/projects are supported, but same-project keys share one quota bucket and do not multiply quota; a 429 cools down that whole bucket.
4. Multi-account/project quota pooling is experimental and must not be used to circumvent provider limits or terms.
5. Paid API use is budget-controlled; no automatic top-ups.
6. Local voice navigation remains local.
7. Voice business Q&A is planned, read-only initially, and not yet a current capability.
8. Voice-driven transaction creation/posting remains a separate future decision.
9. Core business operations must continue if all AI providers are unavailable.
10. No production code, schema, secrets or provider configuration is changed by this document alone.

## 17. Official provider references

- Gemini rate limits: https://ai.google.dev/gemini-api/docs/rate-limits
- Gemini billing and account/project tiers: https://ai.google.dev/gemini-api/docs/billing
- Gemini API keys and project association: https://ai.google.dev/gemini-api/docs/api-key
- Gemini usage policies: https://ai.google.dev/gemini-api/docs/usage-policies
- xAI rate limits: https://docs.x.ai/developers/rate-limits
- xAI pricing: https://docs.x.ai/developers/pricing

Provider limits, model availability, prices and terms change. Re-check official documentation immediately before implementation and before enabling a provider for real pilot-shop data.
