# Vision, non-goals, users and jobs to be done

## Vision

A single, simple-on-the-surface application that lets a small Indian retailer bill fast, know their stock, know who owes whom, and hand clean records to their CA — backed by a double-entry ledger and a movement-led inventory that never silently lies.

One shell and one set of workflows serve every shop type. Vertical templates change fields, defaults and terminology, not the engine.

## Product principles

1. **Correct before broad.** One trustworthy workflow beats ten approximate ones.
2. **Record what actually happened.** Incomplete documentation is recorded honestly and flagged, never blocked or fabricated.
3. **Nothing posted is silently changed.** Corrections are visible reversals or adjustments.
4. **Plain language on top, ledger underneath.** Shopkeeper mode never shows debits/credits; accountant mode always can.
5. **Every number drills down** to the documents and movements that produced it.
6. **Works without AI, without internet services, without paid anything.**

## Non-goals (Phase 1)

- Not a GST filing, e-invoice, e-way-bill or income-tax filing product.
- Not a replacement for a CA, tax lawyer or statutory auditor; does not perform audits.
- Not a separate app per vertical; not a low-code platform; no user scripting or dynamic schemas.
- Not offline multi-device; not marketplace-, bank-feed- or WhatsApp-integrated.
- Not a payroll, manufacturing or multi-entity consolidation system.
- Not a pharmacy system (regulatory analysis required first).
- Not a commercial SaaS in this phase.

## Target users

| User | Context | Primary needs |
|---|---|---|
| **Owner** (first: father, electronics shop) | Runs the shop; may have moderate digital literacy; Hindi/English | Fast billing, money to collect/pay, stock truth, simple reports, control over staff actions |
| **Staff / cashier** | Bills customers, receives goods | Quick search/scan, clear errors, no accounting exposure |
| **Accountant / CA** (read-only in 1A) | External, periodic | Ledgers, trial balance, P&L, balance sheet, source drill-down, exports |
| **Product owner (developer)** | Builds and learns | Clear boundaries, testable slices |

## Jobs to be done

| When… | I want to… | So that… |
|---|---|---|
| a customer is at the counter | bill items in under a minute, cash/UPI/credit | the queue moves and nothing is lost |
| goods arrive from a supplier | record what arrived and what I owe | stock and payables are right |
| I start using the system | load my existing stock without typing hundreds of long forms | I can start billing within a day or two |
| I sell a local/unbranded item | create it in seconds without a brand or barcode | I am not forced to invent data |
| a customer has not paid | see who owes how much and since when | I can collect |
| a supplier calls for money | see what I owe and what I already paid | I pay correctly |
| month ends | know sales, gross margin, expenses and net result | I understand the business |
| stock sits unsold | see slow/dead items and the value tied up | I can act |
| the CA asks for records | export ledgers and summaries with source documents | handover is clean |
| a mistake was made | correct it visibly | history remains trustworthy |

## Questions the product must answer (owner view)

What stock do I have and where? What came in and went out? What is slow, dead, damaged or missing? What did I buy and sell? What is my gross margin and net result? What cash and bank balance is recorded? Who owes me? Whom do I owe? What capital is tied up in stock and receivables? Which records need attention? What should I do next?
