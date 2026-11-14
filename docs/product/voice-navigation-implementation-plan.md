# Voice Navigation — Implementation Plan and Progress

**Status date:** 2026-11-14  
**Working branch:** `feature/voice-provider-boundary`  
**Tracking PR:** [#24 — H2 voice navigation](https://github.com/Vaibhav1507/retail-business-erp/pull/24)  
**Status:** In progress; do not merge as a completed three-level voice feature yet.

## 1. Agreed architecture (source of truth)

The voice-navigation experience has three levels. All three are part of the current scope.

### Level 1 — Basic navigation (default)

- Commonly used, predefined quick actions and basic commands.
- Deterministic keyword/concept resolver for the 35 verified assistant destinations.
- Fast path; no STT model or LLM call is needed for a direct selection or confident deterministic match.
- Preserve clarification for ambiguity and rejection of business-data questions or transaction execution.

### Level 2 — Speech-to-text

1. **Shruti — primary STT.** Local-first browser worker, optimized through evaluation and consented examples for Hindi, Hinglish, Bundelkhandi and Indian English.
2. **Whisper — backup STT.** Invoked when Shruti fails, times out, returns no usable transcript or falls below an explicitly tested transcription-quality threshold. Both providers feed the same transcript normalization and Level 1 resolver.

Whisper is not to replace Shruti. The provider implementation (local versus hosted) must be selected through RBOS-specific accuracy, latency, memory, privacy and cost evaluation. No paid service or external audio transfer may be silently enabled.

### Level 3 — AI / LLM intent understanding

1. **Gemini Flash — primary LLM.**
2. **Groq-hosted Qwen — text fallback.** A separate text-only client uses `qwen/qwen3.8-27b`; do not reuse the vision-only request contract. It is Groq-hosted Qwen, not xAI Grok.

Level 3 is invoked only when Level 1 cannot confidently resolve the normalized transcript or needs contextual interpretation. It returns a constrained structured intent, never an arbitrary URL or direct transaction action. All destinations are validated against the canonical route allowlist. Existing unsaved-work protection remains the final navigation guard.

Provider ordering and retry rules must avoid uncontrolled cascades. Use bounded timeouts, no sensitive request/response logging, server-side credentials, and explicit provider availability/configuration.

## 2. Current implementation baseline (verified on 2026-11-14)

| Area | Current evidence | Status |
|---|---|---|
| Level 1 quick actions | Six common shortcuts are present in `NavigationAssistant.tsx` | Implemented baseline; review coverage |
| Level 1 resolver | Keyword/concept profiles, aliases, ambiguity and rejection handling; 35 canonical destinations | Implemented foundation |
| Shruti primary STT | Local worker, model integrity/cache, push-to-talk and partial transcript flow | Integrated |
| Whisper backup STT | Added Groq Whisper Turbo client, authenticated API endpoint, WAV encoder and UI failover path; feature remains disabled until `Groq:WhisperEnabled=true` is configured | Implemented, CI/acceptance pending |
| Gemini Flash voice intent | Added constrained intent service and endpoint; provider calls remain disabled by default until explicit config | Implemented, tests/CI pending |
| Groq/Qwen voice intent | Separate text-only `GroqTextIntentClient` uses `qwen/qwen3.8-27b`; official docs confirm text input/output and JSON mode | Implemented, tests/CI pending |
| Three-level orchestrator | Shruti→Whisper failover and deterministic→Gemini→Groq Qwen intent path are integrated in the existing assistant flow | Implemented, end-to-end tests/CI pending |
| End-to-end speech tests | Synthetic resolver/provider tests exist; no paired RBOS audio acceptance corpus yet | Pending |
| Device acceptance | Android Chrome and low-end device acceptance outstanding | Pending |

**Important provider distinction:** Grok (xAI) and Groq (inference provider) are different. The repository contains `GroqVisionClient`, configured with a Qwen vision model, used as a fallback in `ProductFileReader` for catalogue photo/PDF extraction and now in `InvoiceExtractionService` for invoice photo/PDF extraction. This is a Groq-hosted Qwen vision integration, not xAI Grok and not a voice intent integration. Voice intent uses a separate text-only Groq client.

## 3. Work plan and acceptance gates

### W0 — Repository/provider audit and documentation
- [x] Re-check PR #24 head, changed files and latest workflow.
- [x] Confirm latest PR workflow #291 passed frontend and backend jobs.
- [x] Correct research document statements that incorrectly deferred Whisper and Level 3.
- [x] Locate Groq integration and its current use: Qwen vision fallback for catalogue photo/PDF extraction in `ProductFileReader`.
- [x] Audit invoice extraction and add Groq Qwen vision as the cross-provider fallback after the Gemini model chain.
- [x] Verify `qwen/qwen3.8-27b` supports text input/output and JSON mode through Groq chat completions; use a separate text-only client rather than reusing the vision contract.
- [ ] Inventory Gemini Flash configuration and distinguish invoice extraction from voice intent use.
- [ ] Record exact provider ownership and deployment configuration without exposing secrets.

### W1 — Level 1 completion
- [ ] Audit the six quick actions against the most common pilot-shop workflows.
- [ ] Ensure typed commands and quick actions use the same deterministic resolver contract.
- [ ] Preserve conservative ambiguity behavior; do not add unsafe phrase-specific auto-navigation.
- [ ] Add regression coverage for the agreed common command set.

**Gate:** Every direct quick action and high-confidence deterministic command resolves to a verified destination; ambiguous commands never navigate without user choice; business questions and transaction execution remain rejected.

### W2 — Level 2 STT failover
- [ ] Define a provider-neutral STT result contract: transcript, language, provider, duration, status and confidence/quality metadata where available.
- [x] Keep Shruti as primary.
- [x] Implement Groq-hosted Whisper (`whisper-large-v3-turbo`) as backup behind the provider boundary; require an explicit `Groq:WhisperEnabled` switch and the existing server-side Groq key.
- [x] Define fallback conditions for Shruti initialization/runtime failure, 12-second timeout, empty output and a non-empty transcript that the deterministic resolver cannot interpret; partial previews remain usable and avoid unnecessary hosted calls.
- [x] Preserve a short-lived PCM copy only until Shruti succeeds or Whisper completes; clear it on success, failure, cancellation and component disposal.
- [x] Encode final audio as bounded 16 kHz mono WAV; backend enforces a 1 MB cap and a 100-request/day per-user pilot quota.

- [x] Ensure both STT outputs enter the same normalization and Level 1 resolver.
- [ ] Add automated orchestration tests for Shruti success, initialization failure, timeout, empty transcript, cancellation, 429 and provider outage; WAV encoder tests are added.

**Gate:** Shruti works without network after model assets are cached; Whisper is invoked only on an eligible Shruti failure/quality condition; a Whisper failure returns a safe clarification/error and never navigates. The adapter and API are implemented but remain disabled by default until `Groq:WhisperEnabled=true` is explicitly set in the deployment environment.

### W3 — Level 3 LLM intent fallback
- [x] Define a strict structured response schema (intent type, canonical destination ID, confidence, clarification candidates/reason).
- [x] Implement Gemini Flash as primary voice-intent provider.
- [x] Verify Groq-hosted Qwen text-generation support and implement a separate text-only fallback client.
- [x] Send transcript and allowlisted current-screen ID only; never send raw audio to Level 3.
- [x] Validate provider output against the 35 allowlisted destination IDs; final app route and permission guards remain authoritative.
- [x] Add bounded 10-second provider timeouts, no automatic retry cascade, explicit provider flags and status-only redacted logs.
- [x] Add tests for Gemini primary success, invalid destination rejection/Groq fallback and disabled-by-default configuration.

**Gate:** No LLM output can supply a URL, invoke a transaction, bypass authorization or bypass unsaved-work protection. Invalid/low-confidence output asks the user.

### W4 — Orchestration and UX integration
- [x] Integrate one assistant orchestration path for Level 1 → Level 2 → Level 3 without changing the UI.
- [x] Direct quick actions and clear typed commands take the fast Level 1 path.
- [x] Voice uses Shruti first; Whisper only as STT backup.
- [x] Run deterministic resolver after transcription and retry unresolvable Shruti output with Whisper before any LLM call.
- [x] Call Gemini only when deterministic resolution is insufficient; fall back to Groq-hosted Qwen text intent when Gemini is unavailable, invalid or low-confidence.
- [x] Preserve current assistant UI, chat history, push-to-talk and Hindi labels.
- [x] Maintain explicit user clarification when providers disagree or confidence is insufficient.

**Gate:** Backend provider contract tests are added. UI-level instrumentation for every transition, real-device acceptance and full CI remain outstanding.

### W5 — Evaluation and pilot readiness
- [ ] Build a consented, anonymized RBOS speech corpus with Hindi Devanagari, Romanized Hindi, Hinglish, Bundelkhandi and Indian English.
- [ ] Include shop noise, silence, far-field audio, short commands and ambiguous references.
- [ ] Measure STT WER/CER, correct navigation, wrong navigation, clarification rate, silence hallucination, p50/p95 latency, memory and provider cost.
- [ ] Require wrong navigation <=2% in quiet and <=3% in noisy tests; silence/noise must never navigate.
- [ ] Run Android Chrome first-use permission, model download/cache, low-end device memory and latency tests.
- [ ] Update this document and the research report with actual measured results.

**Gate:** All automated CI jobs green, no unresolved high-severity review comments, and manual acceptance evidence recorded. Keep PR draft until then.

## 4. Safety, privacy and cost constraints

- Keep Shruti as the local-first primary STT.
- Do not upload raw audio to Level 3. LLM providers receive only the minimum transcript/context needed.
- The owner confirmed that ordinary navigation-command audio may be sent to hosted Whisper. The Level 2 fallback sends audio only after Shruti failure; do not send it to Level 3.
- The hosted Whisper feature is disabled by default in `appsettings.json`; enable with `Groq__WhisperEnabled=true` only after confirming the Groq account remains on its Free plan. The API reuses `Groq:ApiKey`, never exposes it to the browser, caps WAV uploads at 1 MB, and limits each authenticated user to 100 fallback requests per UTC day.
- Groq Free-plan rate limits currently list Whisper at 20 RPM, 2,000 RPD, 7,200 audio seconds/hour and 28,800 audio seconds/day; actual account limits may vary. Paid Developer usage is billed separately. The API adds a 100-request/user/day pilot cap and handles HTTP 429 safely.
- No API keys in frontend bundles, source control, logs or test fixtures. Provider secrets remain server-side.
- No silent paid dependency. Check actual quotas and pricing; temporary free allowances are not permanent zero-cost guarantees.
- Never log audio, full transcripts, authorization headers, provider payloads or sensitive business data.
- LLM output is untrusted input. Validate schema, intent, destination, permissions and current navigation state.
- Never let voice navigation post invoices, sales, purchases, payments or other transactions.

## 5. Progress log

| Date | Change / evidence | Result |
|---|---|---|
| 2026-11-14 | Rechecked PR #24 and latest head/workflow #291 | Frontend and backend CI jobs passed |
| 2026-11-14 | Audited current speech provider and assistant integration | Shruti is the only registered STT provider; assistant calls deterministic resolver directly |
| 2026-11-14 | Audited AI-related backend files | `InvoiceExtractionService` uses Gemini only; `ProductFileReader` uses `GroqVisionClient` with Qwen vision as catalogue image/PDF fallback |
| 2026-11-14 | Owner clarified provider name | Existing integration is Groq, not xAI Grok; it is catalogue extraction only, not voice intent |
| 2026-11-14 | Owner clarified Level 2 audio-transfer constraint | Hosted Whisper is acceptable for ordinary navigation commands; selected Groq Whisper Turbo as the backup candidate, with free-plan limits and explicit enablement documented |
| 2026-11-14 | Level 2 implementation | Added server-side Groq Whisper client, authenticated/CSRF-protected raw-WAV endpoint, 1 MB cap, 100-request/day per-user quota, browser WAV encoder, Shruti-first timeout/error/empty/unresolvable-result failover, PCM cleanup and UI disclosure. Automated transition tests and CI remain pending. |
| 2026-11-14 | Level 3 implementation | Added Gemini Flash primary intent service, separate Groq Qwen text fallback, strict destination allowlist/schema, authenticated endpoint, provider opt-in flags, 100-request/day cap and frontend integration. Added provider fallback tests; CI and real-device acceptance remain pending. |
| 2026-11-14 | Project-wide AI audit | Added Groq Qwen vision fallback to invoice extraction; confirmed product/catalogue extraction already uses Gemini→Groq. Documented the mandatory fallback standard for current and future AI workflows. |
| 2026-11-14 | Updated voice research to restore Whisper backup and Level 3 to current scope | Documentation correction in progress |

## 7. Project-wide AI fallback standard

Every AI-backed workflow must declare a primary provider, an independent provider fallback, and a deterministic/manual recovery path. Provider failures must not silently turn into empty or fabricated business data. Validate the output against the feature's schema and business rules before presenting or persisting it.

| AI workflow | Primary | Independent fallback | Safe final recovery |
|---|---|---|---|
| Product/catalogue photo or PDF extraction | Gemini | Groq Qwen vision | Local OCR/text extraction where available; otherwise manual column mapping |
| Supplier invoice photo/PDF extraction | Gemini Flash model chain | Groq Qwen vision | Manual invoice entry; extracted invoice remains an unsaved draft |
| Voice transcription | Local Shruti | Groq Whisper Turbo | Typed command / deterministic resolver |
| Voice navigation intent | Gemini Flash | Groq Qwen text generation | Deterministic suggestions or ask the user |

Rules for all future AI integrations:
- No feature may depend on one AI provider without a documented independent fallback or a clearly justified deterministic/manual alternative.
- Provider fallback is feature-specific. Do not reuse a vision-only client for text classification or an STT model for intent understanding.
- Keep provider credentials server-side; never log audio, full transcripts, prompts, provider payloads or sensitive business data.
- Bound request size, timeouts, retries and per-user usage. Treat provider output as untrusted and validate it before use.
- Keep provider flags disabled by default until the owner explicitly enables the workflow and confirms the provider account's cost tier.
- If every provider fails, preserve the user's input and offer manual recovery; never silently discard or post unreviewed data.

### Current enablement

- Level 2 Whisper: `Groq:WhisperEnabled=true` plus the existing `Groq:ApiKey`. Default is false.
- Level 3 intent: `VoiceIntent:Enabled=true` and at least one provider flag/key. Gemini: `Gemini:VoiceIntentEnabled=true` with `Gemini:ApiKey`; Groq fallback: `Groq:VoiceIntentEnabled=true` with `Groq:ApiKey`. All flags default false.
- Level 3 uses the exact 35 destination IDs, rejects unknown IDs, requires confidence >= 0.85 for navigation, and can only return navigate/clarify/reject/not-understood. It cannot return a URL or transaction action.
- Both Level 2 and Level 3 endpoints require an authenticated session and CSRF protection on POST requests; each has a 100-request/user/UTC-day pilot limit. The current in-memory counters are per API instance, so provider-level organization quotas remain the final shared limit.

## 6. Decision log / open questions

- **Confirmed:** Level 1 basic common navigation options.
- **Confirmed:** Level 2 Shruti primary, Whisper backup.
- **Confirmed:** Level 3 Gemini Flash primary with a separate Groq Qwen text-generation fallback. Official Groq model documentation confirms text input/output and JSON mode for `qwen/qwen3.8-27b`; this is not xAI Grok.
- **Confirmed:** Preserve current UI and deterministic route safety.
- **Confirmed:** Hosted Whisper is acceptable for ordinary navigation-command audio. Prefer Groq Free-plan use for the pilot; do not upgrade the provider account or enable paid usage without explicit owner approval.
- **To measure:** Shruti→Whisper failover latency and transcript/navigation accuracy on consented RBOS command audio.
- **To configure:** Provider keys, quotas, model IDs and environment-specific enablement without committing secrets.

Update this file at every meaningful implementation milestone, provider decision, test result and deployment/acceptance gate. Keep `voice-navigation-research.md` as the evidence/research companion; this file is the execution tracker.
