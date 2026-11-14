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
2. **Grok — existing integration to reuse as the next fallback.**
3. **Qwen — additional fallback candidate**, subject to verifying the exact existing model/client and its capability for text-only intent classification.

Level 3 is invoked only when Level 1 cannot confidently resolve the normalized transcript or needs contextual interpretation. It returns a constrained structured intent, never an arbitrary URL or direct transaction action. All destinations are validated against the canonical route allowlist. Existing unsaved-work protection remains the final navigation guard.

Provider ordering and retry rules must avoid uncontrolled cascades. Use bounded timeouts, no sensitive request/response logging, server-side credentials, and explicit provider availability/configuration.

## 2. Current implementation baseline (verified on 2026-11-14)

| Area | Current evidence | Status |
|---|---|---|
| Level 1 quick actions | Six common shortcuts are present in `NavigationAssistant.tsx` | Implemented baseline; review coverage |
| Level 1 resolver | Keyword/concept profiles, aliases, ambiguity and rejection handling; 35 canonical destinations | Implemented foundation |
| Shruti primary STT | Local worker, model integrity/cache, push-to-talk and partial transcript flow | Integrated |
| Whisper backup STT | Research exists; no adapter or automatic failover in `speech-provider.ts` | Pending |
| Gemini Flash voice intent | Gemini is used by invoice extraction, not yet voice intent routing | Pending for voice |
| Grok voice intent | Owner confirms Grok is already integrated; locate and verify exact client/configuration before reuse | Audit required |
| Qwen voice intent | `GroqVisionClient` exists and defaults to a Qwen vision model; this is not proof of a text-only Qwen intent integration | Audit required |
| Three-level orchestrator | `NavigationAssistant.tsx` currently calls `resolveNavigation()` directly after transcription | Pending |
| End-to-end speech tests | Synthetic resolver/provider tests exist; no paired RBOS audio acceptance corpus yet | Pending |
| Device acceptance | Android Chrome and low-end device acceptance outstanding | Pending |

**Important provider distinction:** Grok (xAI) and Groq (inference provider) are different. The repository contains `GroqVisionClient` configured with a Qwen vision model. Do not treat that class as the Grok integration. The owner's statement that Grok is already integrated is retained; the exact existing Grok client must be found and reused rather than duplicated.

## 3. Work plan and acceptance gates

### W0 — Repository/provider audit and documentation
- [x] Re-check PR #24 head, changed files and latest workflow.
- [x] Confirm latest PR workflow #291 passed frontend and backend jobs.
- [x] Correct research document statements that incorrectly deferred Whisper and Level 3.
- [ ] Locate the existing Grok integration, configuration keys, calling pattern, timeout and error handling.
- [ ] Inventory Gemini Flash configuration and distinguish invoice extraction from voice intent use.
- [ ] Inventory Qwen integration; verify model ID, provider and text-generation capability.
- [ ] Record exact provider ownership and deployment configuration without exposing secrets.

### W1 — Level 1 completion
- [ ] Audit the six quick actions against the most common pilot-shop workflows.
- [ ] Ensure typed commands and quick actions use the same deterministic resolver contract.
- [ ] Preserve conservative ambiguity behavior; do not add unsafe phrase-specific auto-navigation.
- [ ] Add regression coverage for the agreed common command set.

**Gate:** Every direct quick action and high-confidence deterministic command resolves to a verified destination; ambiguous commands never navigate without user choice; business questions and transaction execution remain rejected.

### W2 — Level 2 STT failover
- [ ] Define a provider-neutral STT result contract: transcript, language, provider, duration, status and confidence/quality metadata where available.
- [ ] Keep Shruti as primary.
- [ ] Implement Whisper as backup behind the provider boundary.
- [ ] Define deterministic fallback conditions (init/runtime error, timeout, empty transcript, unusable output); do not confuse transcript confidence with intent confidence.
- [ ] Ensure both STT outputs enter the same normalization and Level 1 resolver.
- [ ] Test audio buffer cleanup, cancellation, retries and provider failure.

**Gate:** Shruti works without network after model assets are cached; Whisper is invoked only on an eligible Shruti failure/quality condition; a Whisper failure returns a safe clarification/error and never navigates.

### W3 — Level 3 LLM intent fallback
- [ ] Define a strict structured response schema (intent type, canonical destination ID, confidence, clarification candidates/reason).
- [ ] Implement Gemini Flash as primary intent provider.
- [ ] Reuse the verified existing Grok integration as fallback.
- [ ] Add Qwen only after confirming the existing integration is suitable for text-only intent understanding and the owner-approved fallback order.
- [ ] Send transcript and minimal screen context only; never send raw audio to Level 3.
- [ ] Validate provider output against the allowlisted destination IDs and current user permissions.
- [ ] Add bounded timeouts, retry limits, provider availability checks and redacted logs.

**Gate:** No LLM output can supply a URL, invoke a transaction, bypass authorization or bypass unsaved-work protection. Invalid/low-confidence output asks the user.

### W4 — Orchestration and UX integration
- [ ] Implement one orchestration service for Level 1 → Level 2 → Level 3.
- [ ] Direct quick actions and clear typed commands take the fast Level 1 path.
- [ ] Voice uses Shruti first; Whisper only as STT backup.
- [ ] Run deterministic resolver after transcription before spending an LLM call.
- [ ] Call Gemini only when deterministic resolution is insufficient; fallback to Grok and then approved Qwen policy.
- [ ] Preserve current assistant UI, chat history, push-to-talk and Hindi labels.
- [ ] Maintain explicit user clarification when providers disagree or confidence is insufficient.

**Gate:** Instrumented tests prove each transition and prove no unnecessary provider call occurs on the Level 1 fast path.

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
- Whisper hosted mode, Gemini and Grok may transmit transcript/audio outside the device depending on the adapter; make the exact data flow explicit.
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
| 2026-11-14 | Audited visible AI-related backend files | Gemini invoice extraction exists; GroqVisionClient uses Qwen vision; neither proves voice intent integration |
| 2026-11-14 | Owner confirmed Grok is already integrated | Must locate and reuse exact Grok implementation |
| 2026-11-14 | Updated voice research to restore Whisper backup and Level 3 to current scope | Documentation correction in progress |

## 6. Decision log / open questions

- **Confirmed:** Level 1 basic common navigation options.
- **Confirmed:** Level 2 Shruti primary, Whisper backup.
- **Confirmed:** Level 3 Gemini Flash primary, Grok fallback; Qwen also part of the fallback strategy.
- **Confirmed:** Preserve current UI and deterministic route safety.
- **To verify:** Exact existing Grok integration location and whether it is an xAI Grok API client or a Groq-hosted model integration. These are not interchangeable.
- **To decide after audit:** Whether Qwen is a second fallback after Grok or a provider selected by availability/capability. Do not invent the order.
- **To measure:** Best viable Whisper backup deployment under the zero-cost/privacy/performance constraints.
- **To configure:** Provider keys, quotas, model IDs and environment-specific enablement without committing secrets.

Update this file at every meaningful implementation milestone, provider decision, test result and deployment/acceptance gate. Keep `voice-navigation-research.md` as the evidence/research companion; this file is the execution tracker.
