# PR C — first-time retailer experience and guided onboarding

**Branch:** `feat/guided-first-shop-onboarding`  
**Base SHA:** `eadee5d6151cc355abf90f0e4dbdc62e12212f2c`  
**Status:** implementation in progress; CI, review and manual acceptance pending.

## Scope

Make the existing one-shop capabilities understandable as a coherent first-run journey without adding business modules. The Home checklist has five steps: business profile, catalogue, starting stock, first purchase and first sale. It derives completion from tenant-scoped saved APIs and is optional; unfinished setup does not gate normal navigation.

## Reused capabilities

- `BusinessSetup.tsx` / `GET|PUT /business-profile` — existing profile, address and GST configuration.
- `Catalogue.tsx`, `SetupAssistant.tsx`, existing product import APIs and barcode scanner — existing catalogue creation, editable import preview, duplicate review and barcode linking.
- Opening-stock form in `App.tsx` and `POST /opening-stock` — existing cost-required controlled posting.
- `Procurement.tsx`, supplier master, invoice extraction draft and purchase receipt APIs — existing supplier and purchase workflow.
- `GenerateInvoice.tsx`, item-tag capture, barcode/serial selection and invoice confirmation — existing sale workflow.
- `Home.tsx` and `GET /dashboard` — existing trusted dashboard values.

No parallel catalogue, supplier, purchase, stock or billing implementation is introduced.

## Persistence and security

No new table, migration, API permission, tenant boundary or production data change is intended. Progress is calculated from existing authenticated tenant-scoped GET endpoints: business profile (`onboardingCompleted`), products, stock, purchases and sales. No page visit or unsaved draft completes a step. No sensitive business state is written to localStorage for progress. The guide does not perform a posting action. Existing API authorization, RLS, idempotency and financial controls remain authoritative.

Starting stock is treated as present when saved positive stock exists or a posted purchase receipt has added stock. This lets a shop with empty shelves proceed through its first purchase without creating an artificial zero-cost opening-stock entry. The checklist is recomputed when Home is mounted or refreshed; leaving the page and returning is the pause/resume mechanism. An unsaved in-progress form remains in that screen's current in-memory state only; no new draft store is introduced.

Opening-stock and purchase screens add a client-side review stage only; the existing commands and server validation still perform the actual posting. The sale form retains its existing confirm/print action. Owner-entered costs/prices remain required where existing rules require them. MRP is not a cost or a selling-price default.

## Language

New guide keys use the existing typed English/Hindi catalogue. English is the verified default experience. Hindi copy is draft text pending native-speaker review; this is not a full multilingual rollout and does not translate every existing business screen.

## Validation checklist

- Unit tests: derived progress, empty/incomplete state, saved records, purchase-as-opening-stock evidence and offline-mode suppression.
- Playwright: synthetic owner, electronics profile, product creation, opening-stock review/post, supplier and purchase review/post, first sale confirmation, Home reload and five-step completion at the 390px mobile viewport.
- Regression: existing opening-stock and purchase E2E selectors updated for review checkpoints; tenant isolation tests retained. Collections and sales tests get bounded waits for the post-create product table refresh.
- CI: frontend TypeScript/tests/lint/build/dependency audit; backend build/migration/unit/integration; synthetic Playwright; Docker build. Record exact head SHA and each job result.
- Manual acceptance: first-time retailer can complete and correct the journey on Android; owner/CA reviews GST/non-GST invoice output and financial statements; native Hindi speaker reviews draft copy.

## Deferred / not claimed

No in-app production demo mode, partial-form cloud draft, translation of every screen, new analytics, AI, WhatsApp integration, accounting engine, multi-branch/multi-business capability, full market acceptance, deployment or merge. The Development-only demo login is not promoted to production. PR #47's CA valuation policy and consolidated owner acceptance gates remain separate.
