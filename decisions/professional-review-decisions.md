# Professional-review decisions (CA / official verification)

Decision owner: **qualified Chartered Accountant** (and official sources for statutory rules), with product-owner acceptance. Nothing here is professionally approved. Status: all **Open**.

| ID | Question | Why it matters | Current proposal | Alternatives | Required evidence | Needed by |
|---|---|---|---|---|---|---|
| PR-01 | Inventory valuation policy | COGS, stock value, tax returns | Moving weighted average per variant per tenant | FIFO; specific identification for serialised items | CA opinion; applicable accounting standards for the entity type | Slice 3 |
| PR-02 | WAC return and cost-correction behaviour | Margin accuracy | Customer return at original issue cost; supplier return at original receipt cost; proportional correction split | Current WAC with variance account | CA opinion | Slice 5/6 |
| PR-03 | Freight capitalisation | Landed cost | Direct inward freight capitalised; shared freight by value | Expense freight | CA opinion | Slice 4 |
| PR-04 | Non-creditable tax treatment | Cost and tax | Capitalise into inventory once decided not eligible; hold pending tax separately | Expense; capitalise until proven eligible | CA opinion | Slice 4 |
| PR-05 | Round-off and rounding rules | Totals, tax | Line-level tax rounding, invoice round to rupee, Round-off account | Invoice-level tax computation | Official rules + CA | Slice 5 |
| PR-06 | Opening-balance treatment | Balance Sheet | Opening Balance Equity, reviewed and reclassified | Direct to Capital | CA opinion | Slice 1 (posting) / Slice 6 |
| PR-07 | GST posting rules and invoice requirements | Legal compliance | Per tenant profile and verified rule versions; mandatory invoice fields and numbering verified | — | Current official GST rules | Slice 5 |
| PR-08 | ITC eligibility behaviour | Tax claims | Pending review by default; explicit decision | Eligible on entry for registered supplier tax invoices | CA opinion; official rules | Slice 4 |
| PR-09 | Tax-document treatment (bill of supply, cash memo, none, reverse charge) | Exceptions | Record honestly; no ITC; flag | — | CA + official rules | Slice 4 |
| PR-10 | Period-close rules | Integrity | Owner lock date; unlock allowed with reason and audit; corrections in open period | No unlock; CA-only unlock | CA opinion | Slice 6 |
| PR-11 | Financial-report presentation | Usability for CA | Sections per `financial-report-definitions.md` | Schedule III-like layout | CA opinion | Slice 6 |
| PR-12 | Write-off and gain classification; ITC on destroyed goods | P&L, tax | Write-off as expense; gain as other income | COGS adjustment | CA + official rules | Slice 6 |
| PR-13 | Depreciation and fixed assets | Later scope | Not in 1A | — | CA | 1B |
| PR-14 | Loan and interest treatment | Later scope | Principal liability, interest expense | — | CA | 1B |
| PR-15 | Discount allowed presentation | P&L | Expense | Contra-revenue | CA | Slice 5 |
