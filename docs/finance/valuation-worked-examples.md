# WAC worked examples

Status: **Proposed. Illustrative synthetic numbers. Tax rate 18% is illustrative and unverified.** ⚑ = CA review.

Examples 1–9 are **chained** for one variant: *32 inch LED TV - Local* (`INT-000123`). Examples 10–13 are standalone.

Notation: Q = quantity, V = inventory value (₹), W = WAC (₹/unit).

**Implementation note (2026-11-18):** These examples remain proposed mathematical illustrations, not an approved persistence contract. The live schema stores unit cost/WAC to 4 decimals, quantity to 3 decimals and value to 2 decimals. The six-decimal figures below are not implemented; CA approval is outstanding.

## Chained examples (INT-000123)

| # | Event | Q before | V before | W before | Txn qty | Txn cost | Q after | V after | W after | COGS |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Opening stock @ 8,000 | 0 | 0 | — | +10 | 80,000 | 10 | 80,000 | 8,000.000000 | — |
| 2a | Purchase A @ 8,600 | 10 | 80,000 | 8,000 | +5 | 43,000 | 15 | 123,000 | 8,200.000000 | — |
| 2b | Purchase B @ 7,900 | 15 | 123,000 | 8,200 | +10 | 79,000 | 25 | 202,000 | 8,080.000000 | — |
| 3 | Sale of 4 | 25 | 202,000 | 8,080 | −4 | 32,320 | 21 | 169,680 | 8,080.000000 | 32,320 |
| 4 | Customer return of 1 (from #3) | 21 | 169,680 | 8,080 | +1 | 8,080 | 22 | 177,760 | 8,080.000000 | −8,080 |
| 5 | Supplier return of 2 (from #2b) | 22 | 177,760 | 8,080 | −2 | 15,800 | 20 | 161,960 | 8,098.000000 | — |
| 6 | Damage write-off of 1 | 20 | 161,960 | 8,098 | −1 | 8,098 | 19 | 153,862 | 8,098.000000 | — |
| 7 | Count gain of 1 | 19 | 153,862 | 8,098 | +1 | 8,098 | 20 | 161,960 | 8,098.000000 | — |
| 8 | Purchase D 10 @ 8,000 + direct freight 1,000 | 20 | 161,960 | 8,098 | +10 | 81,000 | 30 | 242,960 | 8,098.666667 | — |
| 9 | Purchase E 2 @ 8,000 + non-creditable tax 2,880 | 30 | 242,960 | 8,098.666667 | +2 | 18,880 | 32 | 261,840 | 8,182.500000 | — |

### Accounting impact, reversal and open questions

**1 Opening stock.** Dr Inventory 80,000 / Cr Opening Balance Equity 80,000. Reversal: mirror, only while no outbound exists. ⚑ OBE closure.

**2a/2b Purchases (eligible ITC assumed for illustration).** 2a: Dr Inventory 43,000; Dr GST Input 7,740; Cr AP 50,740. 2b: Dr Inventory 79,000; Dr GST Input 14,220; Cr AP 93,220. Reversal: mirror if stock allows. ⚑ When eligibility may be asserted.

**3 Sale** (selling price 11,000 each, illustrative). Dr Cash/AR 51,920; Cr Sales 44,000; Cr GST Output 7,920. Dr COGS 32,320 / Cr Inventory 32,320. Reversal (cancellation): mirror at 32,320.

**4 Customer return.** Returned at the original issue cost of the source line (8,080). Here W happens to be unchanged, so W stays 8,080. If W had moved since the sale, returning at original cost would shift W. Dr Sales Returns 11,000; Dr GST Output 1,980; Cr AR/Cash 12,980. Dr Inventory 8,080 / Cr COGS 8,080. ⚑ Original issue cost vs current W.

**5 Supplier return.** Out at original receipt cost of #2b (7,900 × 2 = 15,800), so W rises to 8,098 because units were removed below average cost. Dr AP 18,644; Cr Inventory 15,800; Cr GST Input 2,844. ⚑ PR-02: alternative is current W (8,080 × 2 = 16,160) with the 360 difference to a price-variance account.

**6 Damage.** Out at current W 8,098. Dr Inventory Write-off 8,098 / Cr Inventory 8,098. Reversal: mirror at 8,098. ⚑ Input tax reversal on destroyed goods.

**7 Count gain.** In at current W 8,098. Dr Inventory 8,098 / Cr Inventory Gain 8,098. ⚑ Classification of gain (other income vs COGS reduction).

**8 Direct freight.** Landed cost 80,000 + 1,000 = 81,000 (8,100/unit). Dr Inventory 81,000; Dr GST Input on goods 14,400 (illustrative); Cr AP 95,400 (freight assumed billed by the same supplier with no tax, illustrative). ⚑ PR-03: capitalise vs expense; tax on freight.

**9 Non-creditable tax.** Reviewed not eligible → tax 2,880 capitalised: 16,000 + 2,880 = 18,880. Dr Inventory 18,880 / Cr AP 18,880. If the review was still pending at posting, the 2,880 would sit in GST Input – Pending Review and be reclassified later (P-08). ⚑ PR-04.

**Reconciliation check after #9:** Inventory account balance = 80,000 + 43,000 + 79,000 − 32,320 + 8,080 − 15,800 − 8,098 + 8,098 + 81,000 + 18,880 = **261,840** = V. ✔

## 10. Unknown opening cost (standalone)

Variant: *Generic HDMI cable 1.5 m* (`INT-000210`). The owner counts 50 pieces but does not know the cost.

| Step | Q (posted) | Draft qty | V | W | Accounting |
|---|---|---|---|---|---|
| Capture count, cost unknown | 0 | 50 | 0 | undefined | None (draft only) |
| Sale attempt of 2 by Staff | blocked: "Cost needed for this item. Ask the owner." | | | | None |
| Owner enters cost 60 (owner-supplied; may be marked *Estimated*) and posts | 50 | 0 | 3,000 | 60.000000 | Dr Inventory 3,000 / Cr OBE 3,000 |
| Sale of 2 | 48 | 0 | 2,880 | 60.000000 | COGS 120 |

Reversal: opening document reversible only while no outbound exists; otherwise correct via cost correction (example 12). ⚑ Whether "Estimated" opening costs need a disclosure. See `unknown-opening-cost.md`.

## 11. Backdated receipt (standalone)

Variant: *Universal remote* (`INT-000305`). Today is 20 June.

| Event | Effective date | Q after | V after | W after |
|---|---|---|---|---|
| Receipt 20 @ 150 | 1 June | 20 | 3,000 | 150.000000 |
| Sale 5 | 10 June | 15 | 2,250 | 150.000000 (COGS 750) |

- **Case A — rejected (1A):** receipt of 10 @ 180 with effective date **5 June** (before the 10 June outbound). Accepting it would change the W used for the 10 June sale (would have been (3,000+1,800)/30 = 160 → COGS 800 instead of 750). Message: "This date is before a later sale of this item. Post with today's date or ask your accountant." ⚑ Future forward recompute with the 50 COGS difference posted in the current period.
- **Case B — allowed:** same receipt with effective date **15 June** (after the last outbound): Q 25, V 4,050, W 162.000000 — identical to appending.

## 12. Cost correction after later sales (standalone)

> **ILLUSTRATIVE ONLY — NOT IMPLEMENTATION-READY — DEFERRED PENDING CA REVIEW.** Examples 12 and 13 use the proportional split sketch; it is not an approved method and must not be built in Slice 1.

Variant: *Ceiling fan - Local* (`INT-000412`).

| Event | Q after | V after | W after | COGS |
|---|---|---|---|---|
| Receipt 10 @ 1,500 | 10 | 15,000 | 1,500.000000 | — |
| Sale 6 | 4 | 6,000 | 1,500.000000 | 9,000 |
| Supplier confirms true cost 1,400 → difference −1,000 | | | | |
| Correction: inventory share = −1,000 × 4 / (4 + 6) = −400; COGS share = −600 | 4 | 5,600 | 1,400.000000 | −600 |

Accounting: Dr AP (or supplier credit note) 1,000 / Cr Inventory 400 / Cr COGS 600. Reversal: mirror correction document. ⚑ PR-02 proportional method; tax on the price difference (credit note from supplier).

## 13. Correction crossing a locked period (standalone)

Same facts as example 12, but the receipt and the 6-unit sale happened in **March**, `locked_through = 31 March`, and the correction is discovered on **10 April**.

- The correction is posted with effective date **10 April** (open period). March reports are not restated.
- Inventory −400 and COGS −600 appear in **April**. The correction document references the March receipt.
- Q/V/W outcome identical to example 12 (Q 4, V 5,600, W 1,400).
- ⚑ Whether a material correction requires prior-period disclosure; whether unlocking March is ever preferable (PR-10).
