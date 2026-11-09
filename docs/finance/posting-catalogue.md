# Accounting posting catalogue

Status: **Proposed. Not professionally approved.** ⚑ = requires CA review.

Common rules for every posting:
- **Idempotency:** key `{doc_type}:{doc_id}:post`; unique journal entry per `(tenant, source_type, source_id, kind)`.
- **Source linkage:** journal entry and movements carry `source_type` + `source_id`; lines carry `party_id` on AR/AP.
- **Reversal:** mirror entry with `reversal_of_id`, plus mirror movements, unless stated otherwise.
- **Period:** effective date must be after `locked_through`.
- Tax figures below use an **illustrative 18% rate, unverified**, intra-state (CGST+SGST). Actual treatment depends on tenant GST profile, place of supply and verified rule versions.

---

### P-01 Opening stock
- **Trigger:** Owner posts opening-stock document.
- **Preconditions:** every posted line has explicit cost; lines without cost remain draft quantities.
- **Dr** Inventory · **Cr** Opening Balance Equity
- **Inventory:** + base qty at stated cost; WAC initialised/updated.
- **Party:** none. **Tax:** none.
- **Reversal:** blocked if quantity already consumed; otherwise mirror.
- **Open question ⚑:** whether Opening Balance Equity should be closed to Capital at review.

### P-02 Opening receivable
- **Trigger:** opening customer balance document.
- **Dr** Trade Receivables (party) · **Cr** Opening Balance Equity
- **Inventory:** none. **Party:** AR up, open item with original date if known. **Tax:** none.
- **Reversal:** mirror; blocked if allocated (remove allocation first).
- **Open question ⚑:** treatment of tax embedded in old invoices.

### P-03 Opening payable
- **Dr** Opening Balance Equity · **Cr** Trade Payables (party). Otherwise mirrors P-02.

### P-04 Capital introduced
- **Trigger:** capital document (1A-complete).
- **Dr** Cash/Bank · **Cr** Owner Capital. No inventory, party or tax effect. Reversal mirror.
- **Open question ⚑:** capital introduced in goods (rare) — not supported in 1A.

### P-05 Drawings
- **Dr** Drawings · **Cr** Cash/Bank (cash drawings) — or **Cr** Inventory at WAC (goods taken; stock − via adjustment reason "Owner consumption").
- **Tax:** goods for personal use may have GST implications ⚑. Reversal mirror.

### P-06 Cash purchase
- **Trigger:** purchase bill posted with full immediate payment.
- **Preconditions:** supplier; lines; paying cash/bank account; period open.
- **Dr** Inventory (landed cost) · **Dr** GST Input (only when ITC-review status = reviewed-eligible; otherwise see P-08/P-09) · **Cr** Cash/Bank
- **Inventory:** + qty at landed cost. **Party:** supplier history (optionally via AP in/out pair for ledger visibility ⚑). **Tax:** input tax recorded.
- **Reversal:** mirror; blocked if stock already consumed.
- **Open question ⚑:** should cash purchases still pass through AP for a complete supplier ledger? Proposal: yes (Cr AP, then payment Dr AP / Cr Cash in the same transaction).

### P-07 Credit purchase with eligible GST treatment
- **Dr** Inventory (landed) · **Dr** GST Input CGST/SGST (or IGST) · **Cr** Trade Payables (party)
- Example: 5 × ₹8,600 = ₹43,000; tax ₹7,740 → Dr Inventory 43,000; Dr CGST In 3,870; Dr SGST In 3,870; Cr AP 50,740.
- **Party:** AP up. **Reversal:** mirror.
- **Open question ⚑:** whether "eligible" may be set at posting or only after review/GSTR-2B match.

### P-08 Purchase with tax charged, ITC pending review
- **Dr** Inventory · **Dr** GST Input – Pending Review · **Cr** AP/Cash
- On review: eligible → reclassify Pending → GST Input; not eligible → reclassify Pending → Inventory (for units still on hand) and COGS (for units already sold) using the cost-correction method.
- **Open question ⚑:** whether pending tax should instead be capitalised until proven eligible.

### P-09 Purchase without eligible ITC (reviewed not eligible)
- **Dr** Inventory (cost **including** non-creditable tax) · **Cr** AP/Cash
- Example: 2 × ₹8,000 + tax ₹2,880 → Dr Inventory 18,880 / Cr AP 18,880.
- **Tax:** tax recorded as charged-but-not-credited for reporting.

### P-10 Purchase without GST documentation
- Evidence = cash memo / informal / none; no tax recorded unless a document states it.
- **Dr** Inventory (amount actually paid/payable) · **Cr** AP/Cash
- **Tax:** none recorded; review flag raised. Never treated as ITC-eligible.
- **Open question ⚑:** reverse-charge applicability for certain unregistered purchases — not implemented in 1A.

### P-11 Purchase return
- **Dr** AP (party) (or Cash/Bank if refunded) · **Cr** Inventory (original landed cost of source line) · **Cr** GST Input (as recorded on source, if any)
- **Inventory:** − qty; WAC recalculated. **Party:** AP down.
- **Reversal:** mirror.
- **Open question ⚑ (PR-02):** original receipt cost vs current WAC.

### P-12 Supplier payment
- **Dr** AP (party) · **Cr** Cash/Bank. Allocation to bills; excess → Supplier Advances (Dr Supplier Advances instead of AP for the unallocated part ⚑).

### P-13 Cash sale
- **Dr** Cash · **Cr** Sales · **Cr** GST Output (if applicable) · **Dr/Cr** Round-off
- Cost leg: **Dr** COGS · **Cr** Inventory (qty × WAC)
- Example: 4 × ₹11,000 = ₹44,000 + tax ₹7,920 → Dr Cash 51,920; Cr Sales 44,000; Cr CGST Out 3,960; Cr SGST Out 3,960. COGS 4 × 8,080 = 32,320.
- **Preconditions:** stock sufficient (negative blocked); verified or flagged tax rule.
- **Reversal:** cancellation document mirrors both legs at the original cost.

### P-14 UPI sale
- As P-13 with **Dr** Bank (UPI clearing account per tenant setting) instead of Cash; UPI reference stored on payment.
- **Open question:** separate "UPI clearing" account vs direct bank — product-owner choice, low risk.

### P-15 Credit sale
- **Dr** AR (party) · **Cr** Sales · **Cr** GST Output; cost leg as P-13. Requires named customer and permission.

### P-16 Partial payment sale
- **Dr** Cash/Bank (paid part) · **Dr** AR (party, remainder) · **Cr** Sales · **Cr** GST Output; cost leg; allocation of paid part to invoice.

### P-17 Customer receipt
- **Dr** Cash/Bank · **Cr** AR (party); allocations; excess → Customer Advances.

### P-18 Sales return
- **Dr** Sales Returns · **Dr** GST Output (original rate) · **Cr** AR (party) or Cash/Bank refund
- Cost leg: **Dr** Inventory · **Cr** COGS at **original issue cost** of the source line.
- **Open question ⚑:** credit-note timing rules and tax reversal eligibility.

### P-19 Expense
- **Dr** Expense category · **Dr** GST Input (only if reviewed eligible ⚑) · **Cr** Cash/Bank/AP. Evidence status recorded as for purchases.

### P-20 Stock write-off / damage
- **Dr** Inventory Write-off · **Cr** Inventory at current WAC. Reason required.
- **Open question ⚑:** input-tax reversal on goods written off/destroyed.

### P-21 Count gain
- **Dr** Inventory · **Cr** Inventory Gain at current WAC (explicit cost if WAC undefined).

### P-22 Round-off
- Not a standalone document: a line within sales/purchase entries for the invoice-level rounding difference, Dr or Cr Round-off. ⚑ PR-05.

### P-23 Loan receipt (future, 1B)
- **Dr** Bank · **Cr** Loans Payable (lender party). Not revenue.

### P-24 Loan repayment and interest (future, 1B)
- **Dr** Loans Payable (principal) · **Dr** Interest Expense · **Cr** Bank. Principal is not an expense. ⚑ allocation per schedule.

### P-25 Cost correction (ILLUSTRATIVE ONLY — NOT IMPLEMENTATION-READY — DEFERRED PENDING CA REVIEW)
- Not to be implemented in Slice 1; the method below is a discussion sketch, not a rule.
- Supplier cost changed after units were sold: difference split between Inventory (on-hand share) and COGS (consumed share); counter-entry AP/supplier credit note. See valuation worked example 12. ⚑
