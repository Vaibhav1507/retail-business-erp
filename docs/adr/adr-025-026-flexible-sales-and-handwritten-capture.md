# ADR-025 and ADR-026: Flexible sales, provisional negative-stock cost and document capture

## ADR-025 — Physical negative stock with provisional cost and later reconciliation

**Status:** Proposed pending owner policy and CA review.
**Context:** Shopkeepers sometimes sell goods before the system has recorded the receipt, or have a location-level stock deficit. A physical stock prohibition blocks a real sale; simply deleting the check creates unbounded valuation errors. Current stock is location-scoped while WAC is product-wide.
**Decision proposal:** Permit physical location quantity to become negative, but never allow the valued inventory quantity/value projection to become negative. Cost any short quantity using a positive, explicit provisional estimate and persist it against the exact sale line. Later receipts must clear deficits deterministically and post linked cost-of-sales adjustments for actual-versus-provisional differences. Do not silently use zero, rewrite historical sales, or treat negative physical quantity as a negative inventory asset.
**Alternatives:** Reject the sale (current behaviour); remove stock validation and allow negative WAC; post zero-cost sales and adjust later; create a payable/stock receipt automatically (rejected: the system cannot invent a purchase).
**Benefits:** Captures genuine shop activity while preserving an explainable audit trail. **Costs:** Additional allocation ledger, journal rules, reconciliation and operational alerts.
**Risks:** Wrong fallback estimate, cross-location allocation ambiguity, date/period-lock interactions and misleading gross margin until adjustment. **Reversibility:** Medium.
**Consequences:** A negative-stock exposure report and controlled receipt allocation are required. Owner must approve fallback hierarchy and same-location versus tenant-wide deficit allocation. CA must approve cost variance account, journal direction, effective date and financial statement treatment.
**Required validation:** Negative sale → receipt → adjustment; inventory-to-GL and sale COGS reconciliation; multi-location and concurrent posting. **PO decision needed:** Yes. **Professional review:** Yes.

## ADR-026 — Handwritten sales invoice capture as an unposted draft

**Status:** Proposed; product scope approved. Extraction must follow the existing direct image-extraction pipeline; the AI Gateway is explicitly excluded.
**Context:** Shopkeepers receive handwritten customer invoices and need to record the sale without retyping every line. Existing supplier invoice extraction is a different workflow and explicitly ignores handwriting.
**Decision proposal:** Add a separate sales-document extraction contract that uses the existing direct vision-provider pattern used by item-tag and supplier-invoice image extraction: bounded Gemini model attempts, optional Groq fallback only when its existing feature flag is enabled, `VisionProviderFallback`, bounded local OCR fast path, strict file validation and safe metadata-only logs. Do not call `AiGateway`, create an AI Gateway usage-ledger entry, or add provider credentials/configuration. Extraction returns an editable draft with confidence/warnings and preserves source evidence. The operator must review and confirm. The accepted draft is posted only through the existing sales posting service. AI has no posting authority and cannot decide authoritative tax, totals, inventory, cost or accounting.
**Alternatives:** Tesseract-only handwriting (rejected as primary; designed for printed text); a separate AI posting pipeline (rejected); automatic posting (rejected).
**Benefits:** Less manual entry without bypassing controls. **Costs:** Provider budget, review UX and test corpus. Follow the existing image-upload evidence-retention policy; do not add a separate storage path without a demonstrated requirement.
**Risks:** Misread model numbers, quantities, handwritten totals, customer/seller confusion and prompt injection in uploaded documents. **Reversibility:** High.
**Consequences:** Unknown values remain null; source invoice number/date are references, not RBOS legal numbering. Low-confidence data is highlighted. No extracted draft writes sale, stock, journal or balance records.
**Required validation:** Synthetic handwritten corpus, field-level correction workflow, extraction failure and prompt-injection tests, tenant isolation, zero-posting extraction guarantee. **PO decision needed:** Yes. **Professional review:** Tax/accounting rules remain subject to CA review.
