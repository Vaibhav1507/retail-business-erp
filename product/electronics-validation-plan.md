# Electronics-shop validation profile and plan

The first validation business is the product owner's father's electronics shop. This file combines the validation profile and the practical validation plan.

## 1. Data boundary (applies to everything below)

Observation may record **workflow patterns, sequence, timing, device/printer models and non-sensitive terminology** only.

Do **not** capture or store — on paper, photos, notes or in the system — real:
customer details, supplier names or balances, purchase prices, sales values, GST information, tax records, financial statements, or personal information. This needs separate future authorisation.

All system testing uses synthetic data shaped like the observed patterns.

## 2. Validation profile (synthetic representation)

| Aspect | Synthetic representation | To confirm by observation |
|---|---|---|
| Tenant | "Synthetic Electronics, Sample Town" (Tenant A) | — |
| Adversarial tenant | "Shadow Traders" (Tenant B), tests only | — |
| Locations | 1 branch, 1 primary stock location (shop + godown treated as one in 1A) | Is a separate godown actually used? |
| People | Owner + 1 cashier | Who bills? Who receives goods? |
| Product mix | Branded TVs, fans, mixers, irons; mobile accessories; local/generic TVs and fans; cables, remotes, chargers; boxes of small items sold loose | Proportions; naming patterns |
| GST profile | **Unknown — not assumed** | Regular / composition / unregistered (Q-3) |
| Tracking | Non-tracked in 1A | Are serials/IMEIs recorded today? (Q-5) |
| Payments | Cash, UPI (manual reference), credit | UPI share; how credit is noted |
| Devices | Unknown | Phone/PC; printer model and width (Q-6) |
| Language | English + Hindi labels | Words actually used ("udhaar", "maal", "bill", "parchi"…) |

## 3. Observation sessions

| # | Session | Observe | Output (non-sensitive) |
|---|---|---|---|
| V1 | One ordinary billing period (2–3 hours) | How items are found, priced, discounted, paid; how credit is recorded; receipts given? | Timed step sequences; terminology list; device/printer notes |
| V2 | One supplier delivery | Documents received; checking quantities; how cost is noted; when supplier is paid | Document types seen (category only); steps; pain points |
| V3 | Stock walk-through | How products are named; generic vs branded; boxes vs loose units; damaged stock handling | 50 product **names/patterns** (no prices/quantities) |
| V4 | Money conversation | How outstanding customers/suppliers are tracked; how dead stock is noticed | Workflow description only |
| V5 | CA handover conversation | What is sent to the CA, how often, in what form | Format list; frequency |

## 4. Prototype experiments (synthetic)

| # | Experiment | Measure |
|---|---|---|
| P1 | Paper/clickable form: create a generic item ("32 inch LED TV - Local") | Time; questions asked; fields skipped |
| P2 | Paper opening-stock sheet for 50 synthetic items mirroring V3 patterns | Time; fields owner can actually supply (esp. cost) |
| P3 | Synthetic CSV imports of 100 / 500 / 1,000 rows (later, with software) | Import time; error rate; duplicate rate |
| P4 | Billing walkthrough of 3-item cash sale and a credit sale (later, with software) | Time; errors; help requests |
| P5 | Hindi label review of shopkeeper screens | Terms misunderstood; preferred alternatives |

## 5. Provisional outcome measures

These are **hypotheses to test, not established facts**. They will be revised after V1–V3.

| Measure | Provisional target |
|---|---|
| Create a generic item | ≤ 20 s |
| 3-item cash sale | ≤ 30 s |
| Credit sale with existing customer | ≤ 45 s |
| 500-row CSV import to commit | ≤ 15 min, ≤ 2% unresolved rows |
| Duplicate products after import | ≤ 1% |
| Reconciliation breaks in synthetic scenarios | 0 |
| Owner confidence (1–5) in stock and dues screens | ≥ 4 |
| Abandonment point | Identified and documented |

## 6. Assumptions this plan must test

A-12 (owner can supply cost for most items), A-13 (generic items are common), A-14 (credit sales are routine), A-15 (browser printing is acceptable), A-8 (Hindi only needed on shopkeeper screens). See `assumptions-register.md`.

## 7. Decision gates

- If V3/P2 show cost is unknown for most stock → revisit unknown-cost policy (`finance/unknown-opening-cost.md`).
- If V1 shows serial/IMEI is recorded for most sales → pull tracking forward from Phase 1B.
- If V1 shows a thermal printer that cannot print from a browser → raise printer decision earlier.
