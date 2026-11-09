# Glossary

Terms are used consistently across all documents. "Shopkeeper term" is the plain-language label proposed for shopkeeper mode (to be validated, P5).

## Tenancy and access

| Term | Definition | Shopkeeper term |
|---|---|---|
| Tenant | An independent business account; the primary data-isolation boundary | My business |
| Business profile | Tenant's legal, tax, language, vertical and operational configuration | Business details |
| Branch | A physical outlet belonging to a tenant | Shop |
| Stock location | A physical place where stock is held (warehouse/godown/shop floor) | Godown / shop |
| Billing counter | Optional sales context within a branch (Phase 1B) | Counter |
| User | An authenticated person | — |
| Tenant membership | Link between a user and a tenant, carrying roles and branch/location access | Staff |
| Role | Named set of permissions (1A: Owner, Staff/Cashier, Accountant Read-Only) | — |
| Active tenant | The tenant the server has resolved for the current request from a valid membership | — |

## Catalogue and inventory

| Term | Definition |
|---|---|
| Vertical template | Platform-owned, versioned configuration of attributes, defaults, units, tracking modes and terminology |
| Tenant vertical configuration | Tenant-owned copy of a template; never silently overwritten |
| Product | Tenant-owned product definition |
| Variant | A sellable/stockable combination (model, size, colour, capacity). Every product has at least one variant; stock is held per variant |
| Generic product | Product without recognised brand/model/barcode, e.g. "32 inch LED TV - Local" |
| Internal SKU | System-generated identifier, prefixed `INT-`, clearly labelled internal |
| Barcode alias | One of several scanned codes mapped to a variant; unique within a tenant |
| Base unit | The unit stock quantity is stored in (e.g. piece) |
| Unit conversion | Explicit factor between an entered unit and the base unit (1 Box = 10 Pieces) |
| Tracking mode | none / serial / IMEI / batch / batch+expiry (only *none* in 1A) |
| Stock movement | Append-only record of a quantity change with a reason and source |
| Stock balance | Projection of quantity per variant per location, rebuildable from movements |
| Item cost | Projection of quantity, inventory value and WAC per variant per tenant |
| WAC | Moving weighted-average cost = inventory value ÷ quantity on hand |
| Landed cost | Purchase cost plus capitalised charges (e.g. direct freight, non-creditable tax) |
| Draft quantity | Opening quantity captured but not posted because cost is unknown |

## Documents and posting

| Term | Definition |
|---|---|
| Draft | Editable document with no stock, financial or tax effect |
| Posting | The one-time, idempotent transition that creates stock, accounting and audit effects |
| Posted | Immutable state; corrections only by reversal or adjustment |
| Reversal | A new linked document/entry that cancels a posted one's effects |
| Adjustment | A controlled document that changes stock or value with a reason |
| Effective date | Business date the event belongs to |
| Posted at | System timestamp of posting |
| Idempotency key | Client- or server-generated key making a retry return the original result instead of posting twice |
| Draft identifier | Internal ID of a draft; never a legal number |
| Legal document number | Number assigned at posting from a controlled series |
| Evidence status | What documentation exists for a purchase (tax invoice, bill of supply, cash memo, informal receipt, other, none) |
| ITC-review status | Whether input tax credit treatment is pending review, reviewed eligible, or reviewed not eligible — never inferred automatically |

## Parties and money

| Term | Definition |
|---|---|
| Party | Person/organisation that may be customer, supplier or both |
| Receivable | Money customers owe (control account + party subledger) |
| Payable | Money owed to suppliers |
| Payment allocation | Link of a payment amount to specific invoices/bills |
| Advance | Unallocated payment held against a party |
| Ageing | Outstanding amounts grouped by days overdue |

## Accounting

| Term | Definition |
|---|---|
| Chart of accounts | Tenant's list of ledger accounts |
| Journal entry | Balanced set of journal lines produced by posting a source document |
| Control account | Ledger account summarising a subledger (Receivables, Payables, Inventory) |
| Subledger | Per-party (or per-item) detail that must reconcile to its control account |
| Period lock | Date through which no posting is permitted |
| Gross profit / operating profit / net result | See `finance/financial-report-definitions.md` |
| Opening Balance Equity | Temporary equity account offsetting opening balances until reviewed |
