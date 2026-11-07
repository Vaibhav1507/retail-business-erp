# Purchase evidence and tax status

Status: **Proposed. Not professionally approved.** No statutory rule is asserted here; all tax treatment depends on verified rule versions and CA review.

## 1. Separate concepts (never collapsed into one field)

| Concept | Values | Set by |
|---|---|---|
| Evidence status | GST tax invoice · Bill of supply · Cash memo · Informal receipt · Other document · No supporting document | User at entry |
| Supplier registration status | Registered · Unregistered · Unknown · Not applicable · Requires verification | Party master; snapshotted on the bill |
| Tax-document type | Derived label for reports from evidence status | System |
| Tax charged | Amounts as printed on the document (per component) or none | User, from the document |
| Tax recorded | Amounts posted to tax accounts | System, per posting rule |
| ITC-review status | Not applicable · Pending review · Reviewed | System/Owner/CA |
| ITC eligibility decision | Undecided · Eligible · Not eligible · Partially eligible (later) | Owner/CA explicit action, with reason |
| Payment status | Unpaid · Partly paid · Paid | Derived from allocations |
| Inventory cost treatment | Tax excluded (creditable) · Tax pending · Tax included (non-creditable) | Derived from eligibility decision |

## 2. Non-inference rules

ITC eligibility is never inferred from evidence status alone, supplier registration alone, the presence of a GSTIN, or the presence of a tax amount. A tax invoice from a registered supplier still starts as **Pending review** unless the tenant's (CA-approved) policy explicitly allows "eligible on entry" for that combination (PR-08).

## 3. What the system never does

- Fabricate documents, GSTIN, HSN or tax amounts.
- Convert an undocumented purchase into an ITC claim.
- Block honest recording because documentation is incomplete.

## 4. Effects of incomplete evidence

| Area | Effect |
|---|---|
| Review status | Tax present + any doubt → Pending review. No tax document → Not applicable (no ITC possible), plus an exception flag |
| Tax treatment | Only tax shown on a document is recorded; no-document purchases record none |
| Inventory cost | Amount actually paid/payable, including any non-creditable tax once decided |
| CA handover | Export lists purchases by evidence status, with supplier registration snapshot and review status |
| Exception reporting | "Purchases without tax invoice", "Tax invoice missing GSTIN", "Pending ITC review older than N days" |
| Audit trail | Evidence status changes and eligibility decisions are audited with user and reason; eligibility changes after posting create reclassification entries, never edits |

## 5. User messaging (plain language)

- "This purchase has no tax invoice. Tax credit cannot be assumed. Your CA may review it."
- "Supplier GSTIN is missing. You can still save. Add it later if you get it."

## 6. Tenant GST profile

Not assumed. Profile values: Regular · Composition · Unregistered · Not yet known. "Not yet known" permits purchase recording and blocks posting outward tax invoices until set. Treatment per profile is a CA decision (PR-07, PR-08).
