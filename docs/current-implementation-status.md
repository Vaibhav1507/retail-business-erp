# Current implementation and repository status

**Reviewed:** 2026-11-14  
**Repository:** `Vaibhav1507/retail-business-erp`  
**Source of truth:** current `main` code, SQL migrations, tests and GitHub Actions. This document is a status index, not a substitute for the detailed architecture, finance, security or workflow specifications.

## Product and pilot status

RBOS is a personal, free-first, India-focused retail operations project. The repository contains a broad pilot implementation, but code presence, a green CI run, production deployment, production database migration, product-owner acceptance and live business activation are separate states. Do not infer one from another.

The app currently includes tenant identity/bootstrap and RLS, catalogue and stock setup, purchasing, sales and returns, customer/supplier balances, finance and reporting, staff/location capabilities, product tax metadata, invoice and catalogue document extraction, barcode/serial support, WhatsApp invoice delivery, and navigation assistance. Consult the code and the linked detailed trackers for exact capability boundaries.

## Current AI workflows

| Workflow | Primary | Fallback | Safe recovery | Default posture |
|---|---|---|---|---|
| Product/catalogue photo or PDF | Gemini | Groq Qwen vision | Local OCR/text extraction where available; manual mapping | External providers require configuration |
| Supplier invoice photo/PDF | Gemini model chain | Groq Qwen vision | Manual invoice entry; extracted data remains a draft | Groq fallback off by default |
| Voice transcription | Local Shruti | Groq Whisper Turbo | Typed command / deterministic resolver | Hosted Whisper off by default |
| Voice navigation intent | Gemini Flash | Groq Qwen text generation | Deterministic suggestions / ask user | Level 3 provider flags off by default |

Provider clients are feature-specific. Qwen vision is not the same client or task as Qwen text generation; Groq is not xAI Grok. Never expose provider keys in the browser or source control. Do not enable paid usage, trials, credits or account upgrades without explicit owner approval. Keep timeouts, payload limits, quotas and schema validation. If all providers fail, preserve the user's input and offer manual recovery.

### Voice navigation contract

- **Level 1:** six quick actions plus deterministic keyword/concept resolution for 35 canonical destinations. Direct, confident matches do not require an STT or LLM call.
- **Level 2:** Shruti is the local-first primary STT. Groq Whisper Turbo is the backup only for Shruti failure, timeout, empty/unusable output or a transcript that the deterministic resolver cannot interpret. Both transcripts pass through the same normalisation and resolver.
- **Level 3:** only after deterministic resolution is insufficient. Gemini Flash is primary; a separate Groq Qwen text client is fallback. The LLM receives transcript and minimal current-screen context, never raw audio.
- Structured intent is validated against the 35 destination IDs. No arbitrary URL, business-data answer or transaction action is accepted. The route allowlist, permission checks and unsaved-work guard remain authoritative.
- Hosted Whisper and hosted intent providers remain disabled by default. Existing Gemini/Groq credentials are reused; no separate voice-specific key type is introduced.

## Verification and acceptance

### Merged implementation

- PR #24, **H2 voice navigation**, merged to `main` on 2026-11-14 (merge commit `fc8ecacd27d91ee4a36eba6c45e56b794471b9a6`).
- Workflow #344 passed on implementation commit `bbb5a27`: frontend TypeScript, Vitest, production build and dependency audit; backend Release build, disposable test-database migration validation, unit tests and deployment-container build.
- Documentation-only commits followed the verified implementation commit. No application-code changes followed within PR #24.
- CI migration validation uses a disposable test database. It is not evidence that a production database is migrated.
- No separate test environment is available. Android Chrome, real-device performance, consented RBOS speech corpus, live provider behaviour and full production pilot acceptance remain manual acceptance work.

### Open and closed pull requests relevant to recent work

| PR | Subject | State | Notes |
|---|---|---|---|
| #24 | H2 voice navigation | Merged | Implementation is in main |
| #22 | Local Qwen LLM intent benchmark | Closed, not merged | Experiment remains separate; no production implementation was merged from this PR |
| #25 | Mobile assistant quick-action wrapping | Open | Preferred focused CSS-only fix; wraps shortcut buttons onto following rows |
| #26 | Duplicate quick-action wrapping fix | Open | Duplicate of #25; do not merge both. Close after confirming #25 is the retained PR |

PR #25 and #26 are UI-only CSS changes and are not part of the merged PR #24 baseline. The main branch remains the source of truth until a UI PR is merged.

## Database and deployment

- PR #24 does not add a voice-navigation database migration. Its CI workflow validated the existing migration chain against a disposable database.
- The repository's SQL migration directory is authoritative for schema evolution; inspect it before claiming a migration is or is not required for any other feature.
- Production deployment, migration status, Cloud Run configuration, Meta sender activation and WhatsApp invoice round-trip are tracked separately in `docs/deployment-runbook.md` and `docs/implementation-plans/whatsapp-invoice-delivery.md`.
- Never put the production migrator connection string in the API or worker runtime. Keep `rbos_app`, `rbos_worker` and `rbos_migrator` separated as documented.
- Do not claim production WhatsApp invoice delivery is ready based only on a successful test `hello_world` message or webhook verification.

## Current documentation map

- Product intent, scope and vocabulary: `docs/product/`
- Architecture and data model: `docs/architecture/`, `docs/adr/`
- Financial rules: `docs/finance/`
- Workflow contracts: `docs/workflows/`
- Security: `docs/security/`
- Quality, tests and risks: `docs/quality/`
- Deployment operations: `docs/deployment-runbook.md`
- WhatsApp delivery: `docs/implementation-plans/whatsapp-invoice-delivery.md`
- Voice implementation and evidence: `docs/product/voice-navigation-implementation-plan.md`, `docs/product/voice-navigation-research.md`
- Item-level improvement ideas: `docs/product/improvements.md`

The discovery report and knowledge-transfer system snapshots are historical investigation records. Preserve them for provenance, but do not use their old inventories, route lists, migration counts, test counts or capability claims as current facts.

## Next acceptance work

1. PR #25 merged the quick-action wrapping fix; PR #26 closed without merging.
2. Run the frontend CI and verify the shortcut row on narrow Android/mobile viewports after the CSS PR is merged.
3. Complete Android Chrome voice acceptance using non-real pilot data and a controlled rollback plan.
4. Build a consented, anonymised Hindi/Hinglish/Bundelkhandi/Indian-English command audio set before making accuracy claims about Shruti or Whisper.
5. Keep hosted voice and invoice fallback switches off until provider quotas/cost tier and pilot approval are confirmed.
6. Verify production migration and WhatsApp invoice delivery gates using the latest workflow and test-sender round trip before activating a real sender.
