# Documentation freshness review

**Review date:** 2026-11-14  
**Baseline:** `main` after PR #24 merge commit `fc8ecacd27d91ee4a36eba6c45e56b794471b9a6`.  
**Scope:** Project documentation inventory and targeted reconciliation of current implementation, AI provider architecture, voice navigation, PR history, deployment guidance and known historical snapshots. This is a documentation review, not a full code audit of every finance, security and workflow invariant.

## Changes made in this review branch

| Document | Reconciliation |
|---|---|
| `README.md` | Corrected ADR status; linked the consolidated current implementation status; clarified that original delivery slices are not a complete feature inventory. |
| `docs/current-implementation-status.md` | Added current feature/provider map, PR #22/#24/#25/#26 state, CI evidence, production-vs-CI distinction and next acceptance work. |
| `docs/product/improvements.md` | Reconciled H2 as implemented/merged; corrected X1 distinction; recorded the duplicate UI PRs and retained historical caveat for unverified item rows. |
| `docs/product/voice-navigation-implementation-plan.md` | Changed implementation status from feature branch to merged main; updated verification and post-merge acceptance status. |
| `docs/product/voice-navigation-research.md` | Added implementation outcome and clarified that research comparisons are not acoustic validation. |
| `docs/deployment-runbook.md` | Added optional AI/voice configuration, existing-key reuse, disabled-by-default flags and safe rollout guidance. |
| `docs/quality/documentation-freshness-review.md` | Replaced the 2026-11-13 targeted review with this project-wide inventory and remaining debt. |

## Verified current facts

- PR #24 merged on 2026-11-14 as `fc8ecacd27d91ee4a36eba6c45e56b794471b9a6`.
- Workflow #344 passed on implementation commit `bbb5a27`; it included frontend checks, backend Release build, disposable test-database migration validation, unit tests and deployment-container build.
- The CI database is disposable. This does not prove production migrations have run.
- PR #22 is closed without merge. It is an isolated local-model benchmark, not the current voice runtime.
- PR #25 merged the quick-action wrapping CSS fix; duplicate PR #26 closed without merge.
- Current voice runtime is Level 1 deterministic navigation, Level 2 Shruti→opt-in Groq Whisper Turbo, Level 3 Gemini Flash→separate Groq Qwen text fallback. Hosted flags default off.
- No voice-specific DB migration or new voice-specific API key type is introduced by PR #24. Existing server-side Gemini/Groq credentials are reused when the respective provider is explicitly enabled.

## Documentation inventory and treatment

The repository contains product, architecture, finance, security, workflow, quality, decision, ADR, deployment, implementation-plan, research, experiment and knowledge-transfer documents. The root-level `00-discovery-report.md` / `00-guardrails.md` and some root-level mirror folders duplicate content under `docs/`; canonical maintained documentation is under `docs/` unless a root file is explicitly the project entry point.

- Current operational docs are maintained as live references and should be reconciled when code changes.
- The discovery report and knowledge-transfer system snapshots are historical evidence. They must remain labelled historical rather than being silently rewritten into a new investigation.
- ADRs and owner decision registers describe decisions and constraints; do not change their approval status unless supported by the actual decision register.
- Finance/security/architecture/workflow specifications remain normative where they define invariants. Their old implementation inventories or test counts must not be mistaken for current verification evidence.

## Remaining documentation debt

This pass does not claim a line-by-line code-backed revalidation of every domain document. The following need dedicated review before their implementation details are used as a current source of truth:

1. Architecture ERD/database strategy/transaction projections against all current migrations, especially migrations after the original Slice 4 baseline.
2. Finance posting catalogue, accounting/inventory invariants and report definitions against later purchase, returns, allocations, split payments, loans, GST metadata and WhatsApp outbox changes.
3. Security threat model, authorization matrix and security test plan against actual endpoint policies, membership/staff/location features, webhooks and worker database roles.
4. Onboarding, purchase-to-pay, sale-to-cash, returns/adjustments and stock-setup workflows against current screens and API behaviour.
5. Product roadmap and open questions against the current delivery sequence and decisions.
6. Historical knowledge-transfer snapshots should only receive clear current-state notices and links; their underlying historical analysis should not be presented as current.

## Rules for future updates

1. Code, migrations and tests are the evidence hierarchy; docs must not claim a feature is verified merely because a route/table exists.
2. Keep implementation status separate from product-owner acceptance, production deployment, production migration and live activation.
3. Record exact CI run IDs/commit SHAs and explain whether a database is disposable or production.
4. Never record tokens, passwords, connection strings or secret values.
5. Preserve historical snapshots with prominent notices and links to current source-of-truth docs.
6. Update documentation in a review branch and merge through normal review rather than committing directly to `main`.
