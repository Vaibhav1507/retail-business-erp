# Financial report definitions

Status: **Proposed. Presentation requires CA review (PR-11).** All figures derive from posted journal lines (and, for stock reports, from movements that reconcile to the Inventory account). No report uses a "sales minus expenses" shortcut.

## 1. Result measures

| Measure | Definition |
|---|---|
| Net sales | Sales − Sales Returns (excluding output tax) |
| COGS | Cost of Goods Sold + inventory write-off/shrinkage if classified in COGS ⚑ − inventory gain if classified there ⚑ |
| **Gross profit** | Net sales − COGS |
| Operating expenses | Expense accounts in the operating group (rent, salary, electricity, transport, discount allowed ⚑, etc.) |
| Other income | Inventory gain (if not in COGS) ⚑, other non-operating income |
| **Operating profit** | Gross profit − Operating expenses |
| Finance cost | Interest expense (1B) |
| **Profit before tax** | Operating profit + Other income − Finance cost. Shown when relevant (proprietorships typically taxed on the owner; label ⚑) |
| **Net result** | Profit before tax − income tax expense (not tracked in 1A; usually equals PBT for a proprietorship) |

## 2. Not profit

| Item | Where it appears |
|---|---|
| Cash movement | Cash account movements; Cash Flow statement (1B) |
| Bank movement | Bank account movements |
| Receivables | Balance Sheet asset; receivable ageing |
| Payables | Balance Sheet liability; payable ageing |
| Stock value | Balance Sheet asset; stock valuation report |
| Owner capital | Equity |
| Drawings | Reduction of equity, never an expense |
| Loan principal | Liability, never income or expense |
| Interest expense | Finance cost (expense) |
| GST input/output | Asset/liability, never income or expense (unless non-creditable, then capitalised or expensed per policy) |

## 3. Reports (1A-complete)

| Report | Definition | Must reconcile |
|---|---|---|
| General Ledger | Per account: opening balance, posted lines in date/seq order with source drill-down, closing balance | Σ closing = TB |
| Trial Balance | Σ debit and Σ credit per account as at a date | Total Dr = total Cr |
| Profit and Loss | For a period, sections as §1 | Result = change in Current Year Result on BS |
| Balance Sheet | As at a date: assets, liabilities, equity (capital, drawings, OBE, retained/current result) | Assets = Liabilities + Equity |
| Receivable ageing | Open invoice items per customer by age bucket (0–30, 31–60, 61–90, >90 days, configurable) | Σ = AR control |
| Payable ageing | Same for suppliers | Σ = AP control |
| Stock valuation | Per variant: Q, W, V; flag Estimated opening costs; draft quantities listed separately and excluded from totals | Σ V = Inventory control |
| Customer / supplier ledger | Party lines with running balance | Party balance |

## 4. Plain-language labels (shopkeeper)

| Formal | Plain (English) | Plain (Hindi, draft, needs review) |
|---|---|---|
| Gross profit | Profit on goods sold | माल बेचने पर मुनाफ़ा |
| Operating profit | Profit after shop expenses | दुकान खर्च के बाद मुनाफ़ा |
| Receivables | Customers owe you | ग्राहकों से लेना |
| Payables | You owe suppliers | सप्लायर को देना |
| Stock value | Value of goods in shop | दुकान में माल की कीमत |

Each plain-language card links to the formal report.
