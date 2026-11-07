# Assumptions register

Every assumption is reversible unless marked otherwise. "Validate by" names the evidence that would confirm or overturn it.

| ID | Assumption | Why reasonable | Reversal cost | Validate by | Status |
|---|---|---|---|---|---|
| A-1 | Phase 1A UI supports one branch and one stock location per tenant; the model supports many | Matches the first shop; keeps UI simple | Low (model already multi) | V1/V3 observation | Open |
| A-2 | INR only | Indian small retail | Low | — | Open |
| A-3 | Financial year April–March, configurable per tenant | Indian norm | Low | CA | Open |
| A-4 | Moving weighted average is the valuation method | See `finance/valuation-decision.md` | Medium before go-live, high after | CA review | Open |
| A-5 | Negative stock is **blocked** in Phase 1A (no override) | Protects valuation correctness | Medium (future exception policy) | V1 (how often stock is sold before being recorded) | Open |
| A-6 | Cookie-based local authentication, same-origin SPA | Simplest secure option; no external IdP | Medium | Slice 1 | Open |
| A-7 | Browser printing (A4, 80 mm) is sufficient initially; no PDF library | Zero dependency | Low | V1 printer check | Open |
| A-8 | Hindi needed on shopkeeper screens only in 1A | Accountants generally use English | Low | P5 | Open |
| A-9 | Three fixed roles suffice in 1A | Small staff count | Low | V1 | Open |
| A-10 | PostgreSQL 17 (or current supported major) | RLS maturity | Low | Slice 1 environment check | Open |
| A-11 | Modules are folders in one application project initially | Avoid premature structure | Low | Slice 1–3 | Open |
| A-12 | Owner can supply purchase cost for most opening stock | Shops usually know approximate cost | Medium (affects onboarding design) | V3, P2 | Open |
| A-13 | Generic/unbranded items are a significant share | Common in town electronics shops | Low | V3 | Open |
| A-14 | Credit sales are routine | "Udhaar" is common | Low | V1, V4 | Open |
| A-15 | GST profile is unknown; system supports regular, composition and unregistered profiles as data | Not assumed | — | Q-3 | Open |
| A-16 | Electronics in 1A are non-tracked; serial/IMEI later | Reduces Slice 1–5 scope | Medium | V1 | Open |
| A-17 | One active tenant in real use initially; Tenant B exists only in tests | First validation business | None | — | Open |
| A-18 | Prices are entered tax-exclusive or tax-inclusive per tenant setting, default to be confirmed | Shops differ | Low | V1 | Open |
| A-19 | Server-side posting runs in a single PostgreSQL transaction; no outbox in 1A | Single database | Medium | Slice 1/3 | Open |
| A-20 | Docker is an acceptable way to run local PostgreSQL | A Docker client was observed in Phase 0; ownership, registry and licence status not established | Low | PO decision PD-06 (OPEN) | Open |
