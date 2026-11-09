# ADR-019 … ADR-024: Capabilities and deferrals

---

## Implementation evidence after Slice 4 (status unchanged: all Proposed)

Evidence only; this is not product-owner acceptance.

| ADR | Evidence | Supports proposal? |
|---|---|---|
| 019 | No OCR implemented | Not exercised |
| 020 | No AI integration implemented | Not exercised |
| 021 | No printing implemented; sale detail is an on-screen view only | Not exercised |
| 022 | No offline support; the app is online-only, consistent with deferral | Consistent |
| 023 | No government integrations; consistent with deferral | Consistent |
| 024 | UI is English-only; no translation key infrastructure verified in this reconciliation | Not exercised |

## ADR-019 Free-first OCR
**Status:** Accepted with amendment (2026-11-07, owner). Amendment: Gemini photo reading exists but is optional, key-gated and off by default; local/free paths remain the default.
**Context:** Supplier bills arrive as paper/photos; paid OCR excluded.
**Decision:** Phase 1B: local Tesseract and text-PDF extraction producing **draft** purchase lines only, always human-reviewed. Manual entry remains primary.
**Alternatives:** Paid cloud OCR (excluded without approval).
**Benefits:** Free, private. **Costs:** Limited accuracy on photos.
**Risks:** Users trust bad extraction. **Reversibility:** High.
**Consequences:** No OCR in 1A.
**Required validation:** Accuracy measurement on synthetic bills. **Required tests:** Extraction never posts.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-020 Provider-neutral optional AI
**Status:** Accepted with amendment (2026-11-07, owner). Amendment: current providers (Gemini, MSG91) are optional, key-gated and off by default; keep them behind configuration.
**Context:** AI may help with suggestions; must not compromise correctness or cost.
**Decision:** Optional, disabled by default, behind an interface; local models preferred. AI has **no posting authority** and never owns totals, tax, quantities, valuation, journals, allocations, balances, ITC eligibility or classification. Outputs are drafts requiring human confirmation and deterministic validation.
**Alternatives:** No AI at all; hosted LLM (paid, excluded).
**Benefits:** Future assistance without lock-in. **Costs:** Interface upkeep.
**Risks:** Over-reliance. **Reversibility:** High.
**Consequences:** Product works fully with AI off; no AI in 1A.
**Required validation:** Phase 1B. **Required tests:** No AI path writes posted data.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-021 Browser printing first
**Status:** Accepted (2026-11-07, owner)
**Context:** Shop printers vary; PDF libraries add licence risk.
**Decision:** A4 and 80 mm print-optimised routes using CSS `@media print`; no PDF library and no direct printer integration in 1A.
**Alternatives:** QuestPDF/PdfSharp; ESC/POS via Web Bluetooth/USB.
**Benefits:** Zero dependencies. **Costs:** Browser print dialog friction.
**Risks:** Thermal printer margins/drivers. **Reversibility:** High.
**Consequences:** No claim of universal printer compatibility.
**Required validation:** Test with the shop's printer (V4). **Required tests:** Print-preview snapshots.
**PO decision needed:** Yes (PD-13). **Professional review:** Mandatory invoice fields (PR-07).

---

## ADR-022 Offline synchronisation deferred
**Status:** Proposed
**Context:** Connectivity is unreliable, but offline posting conflicts with numbering, stock and accounting correctness.
**Decision:** Phase 1: online posting only; client draft preservation in IndexedDB with idempotent retry. Offline multi-device sync is a 1B+ design.
**Alternatives:** Offline-first with CRDT/sync.
**Benefits:** Correctness; simplicity. **Costs:** No billing during outages (local-network deployment mitigates).
**Risks:** Outages in the shop. **Reversibility:** Medium.
**Consequences:** Local-first means local deployment, not offline devices.
**Required validation:** Connectivity observation (V3). **Required tests:** Retry after network failure.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-023 Direct government integrations deferred
**Status:** Proposed
**Context:** GST filing, e-invoice, e-way bill need registrations, credentials, compliance and often paid GSPs.
**Decision:** No direct government integration in Phase 1/1B. Produce CA-ready exports instead.
**Alternatives:** GSP integration.
**Benefits:** Avoids paid services and legal exposure. **Costs:** Manual filing by CA.
**Risks:** Users expect filing. **Reversibility:** High.
**Consequences:** Tax data model must be export-friendly.
**Required validation:** CA handover needs (V5). **Required tests:** Export completeness.
**PO decision needed:** Yes. **Professional review:** Yes (export content).

---

## ADR-024 English/Hindi localisation foundation
**Status:** Proposed
**Context:** Shopkeepers may prefer Hindi; accountants English.
**Decision:** i18n keys from Slice 1 (no hard-coded UI strings); English complete; Hindi for shopkeeper screens with native-speaker review; Indian number formatting (lakh/crore grouping); per-user language preference; data (product names) not translated.
**Alternatives:** English only in 1A.
**Benefits:** Avoids retrofit. **Costs:** Translation upkeep.
**Risks:** Poor terminology. **Reversibility:** High.
**Consequences:** Server error messages use codes mapped to localized text.
**Required validation:** Terminology capture V1. **Required tests:** Missing-key test; Hindi flows in Playwright.
**PO decision needed:** Yes. **Professional review:** No.
