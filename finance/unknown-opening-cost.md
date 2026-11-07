# Unknown opening-stock cost

Status: **Proposed recommendation (PD-11). Requires product-owner decision and CA review.**

Rule: unknown cost is never silently converted to zero.

## Options compared

| Criterion | A. Draft quantity-only onboarding | B. Posted provisional valuation |
|---|---|---|
| Quantity | Captured as draft; not in posted stock | Posted |
| Value | None until cost entered | Held in a provisional/suspense treatment (no invented cost possible → value must still be *something*; zero or placeholder) |
| Financial correctness | Reports exclude draft qty; no wrong numbers | Reports carry a known-wrong inventory value plus warning |
| User clarity | "Cost needed" list; simple | Warnings on every report; harder to explain |
| Ease of implementation | Simple: posting requires cost | Needs suspense account, revaluation document, warning plumbing across reports |
| Reversibility | Draft can be edited freely | Requires revaluation adjustment per item |
| Auditability | Clean: one posting with real cost | Two-step history (provisional + revaluation) |
| Completes onboarding without corrupting reports | Yes — trusted reports stay correct; checklist shows incomplete | Only if warnings are honoured everywhere |
| Selling before cost known | Blocked (see below) | Possible, but COGS is wrong |

**Key flaw in B:** "do not invent a cost" means B must post a zero or a sentinel value, which is exactly the silent corruption this policy forbids, or else a non-financial quantity, which is A.

## Recommendation for Phase 1A: **A — draft quantity-only onboarding**

### Behaviour

1. Stock Setup accepts quantity without cost; the line stays **Draft — cost needed**.
2. Draft quantity is shown on the stock screen in a separate column ("Counted, not yet valued"), never in posted on-hand, stock value or financial reports.
3. Setup checklist item "Stock values" stays incomplete with a count of items.
4. The owner may enter a cost from any source: memory, an old bill, current supplier price, or a reasonable estimate, with a flag **Actual / Estimated**. The flag is stored and visible in the audit trail and stock valuation report. The system never proposes a number automatically. (Showing the item's current selling price as context is acceptable; using it as cost is not automatic.)
5. Posting of each line happens only when its cost is entered (per line or in bulk).

### Sales before cost is resolved

- POS shows the item but **blocks posting** a sale line for a variant whose on-hand posted quantity is insufficient — draft quantity does not count as stock (consistent with blocked negative stock).
- Owner shortcut on the block screen: "Enter cost now" → posts that item's opening line, then the sale continues. Staff see "Ask the owner to add the cost".
- This keeps billing possible on day one for owners while preserving correctness.

### Validation

Measure in the shop validation (V2/P1): proportion of items with unknown cost, and how often the shortcut is needed. If the owner finds it obstructive, revisit (decision PD-11).
