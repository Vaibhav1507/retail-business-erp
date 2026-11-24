# Create Invoice — compact mobile and electronics item-entry follow-up

**Status:** Implementation candidate on `ux/invoice-form-compact-electronics-fields`  
**Previous baseline:** PR #76 merged to `main` at `96c8ed9930d4495a0bc032c6d4d2a0e161afe58d`  
**Scope:** Create Invoice only (`#/sell/bill`), including business-category propagation to the invoice form  
**Evidence:** Four Android screenshots supplied after PR #76 was previewed (709 × 1536 captures).

## 1. User feedback and diagnosis

The latest Android review identified four concrete issues:

| Priority | Finding | User impact |
|---|---|---|
| P1 | The invoice page feels too zoomed in: cards, headings, input heights and vertical gaps consume too much of the phone viewport. | Excessive scrolling and too little information visible at once. |
| P1 | Customer mobile and name are followed by an address action separated from the customer card. | Customer details feel fragmented; adding an address takes an unnecessary disclosure step. |
| P1 | Item description input is squeezed beside a square icon-only tag action. | Search loses useful width and the action is unclear without its icon. |
| P1 | Model and Serial are hidden in More item details for the electronics pilot; Quantity is separated from Model. | Frequent electronics identifiers require extra taps and increase entry friction. |

The screenshots are the visual baseline for this follow-up. This is not a new page redesign: retain the established green/white RBOS invoice visual language and hierarchy, but compact it for a phone and optimise the item editor for electronics.

## 2. Selected design

### 2.1 Compact phone scale

Reduce the oversized mobile geometry without making controls difficult to tap:

- Reduce the invoice header height, back control, headings, card padding, section gaps and helper text.
- Reduce mobile input height from the previous 66px target to 56px, with 16px input text.
- Keep all interactive controls at or above the RBOS 44px touch-target baseline.
- Keep the sticky Review bill bar visible and preserve bottom safe-area padding.
- Avoid clipping, horizontal scroll and fixed-footer overlap.

The larger desktop layout remains restrained and readable; this pass primarily targets phone widths.

### 2.2 Customer card — one address field

The customer card now contains, in order:

1. Mobile number (required)
2. Customer name (required)
3. Address (optional) — one free-text field

Remove the separate city, state and pincode inputs from this invoice form. A shopkeeper may enter a complete address in one field. Existing customer address data previously stored as address/city/state/PIN segments is combined into this one field when selected or resumed from a held bill. The existing customer-note storage contract is preserved using the legacy delimiter representation with empty city/state/PIN segments; there is no API or schema change.

### 2.3 Item description and Import tag action

- Keep the item-description search/manual-entry input as the dominant, flexible-width control.
- Place a text-labelled **Import tag** button directly to its right, in the same row.
- The button opens the existing camera/gallery bottom sheet; it does not introduce a new upload path.
- Keep the independent handwritten-bill **Import bill** callout.
- Retain tag extraction, catalogue matching, manual product entry and the existing single shared invoice editor.

### 2.4 Electronics-specific entry

Use the saved RBOS business profile category, not a shop-name heuristic.

For `businessCategory === "electronics_mobile"`:

- Show **Model number** and **Quantity** side by side.
- Show **Serial number (optional)** directly below.
- Keep **Selling price (₹)** as a full-width field below the identifiers.
- Serial stays optional. Missing serial can continue to show its existing non-blocking warning.
- Tag extraction continues to prefill model/serial; More item details can expand after a tag scan so the user can review brand, barcode, IMEI, MRP and HSN.

For all other business categories:

- Keep Model number and Serial number available under **More item details**, collapsed by default.
- Keep the prior Quantity / Selling price arrangement.

This avoids forcing electronics-specific fields onto general retail, garments or other shop types while keeping those fields available when needed.

## 3. Dependencies and preserved behaviour

- `/me` already provides `businessCategory`; existing category key `electronics_mobile` is used. No new profile field or backend contract is introduced.
- `GenerateInvoice` receives that category through the existing App → SellPage props.
- Customer name and mobile validation remain mandatory.
- Existing customer lookup continues to search by phone and hydrate stored address data.
- Held invoices resume by combining any legacy address/city/state/PIN values into the one address field.
- Invoice totals, GST, discount, payment mode, bill allocation, idempotency, server validation, serial/IMEI/barcode duplicate checks, stock rules, offline queueing, handwritten import, tag extraction, print and WhatsApp flows are unchanged.
- No purchase, stock, customer-master or other Billing & Sales page changes are included.

## 4. Acceptance criteria

- [ ] The New Sale page is visibly less oversized on a phone: smaller header, typography, cards, fields and vertical gaps.
- [ ] Mobile number, customer name and one optional Address field appear together inside Customer 01.
- [ ] No separate City, State or Pincode form inputs appear in Create Invoice.
- [ ] Item description gets most of the row width.
- [ ] The adjacent action visibly reads **Import tag** and opens the existing camera/gallery chooser.
- [ ] For `electronics_mobile`, Model number and Quantity occupy one row.
- [ ] For `electronics_mobile`, optional Serial number is visible immediately below Model/Quantity.
- [ ] For other business categories, Model and Serial remain in the collapsed More item details disclosure.
- [ ] Tag capture prefills the visible electronics fields; additional extracted values remain reviewable.
- [ ] All required customer/item validation and non-blocking serial warnings still work.
- [ ] Touch targets remain usable; there is no horizontal overflow or sticky Review bill overlap.
- [ ] Existing customer addresses and held bills retain their address data when loaded.

## 5. Validation plan

- Run focused Create Invoice and ItemTagCapture component tests, including electronics vs general-retail field visibility, the single address field, the Import tag action and tag-prefill behaviour.
- Run the current PR CI (frontend typecheck/tests/lint/build/audit, backend build/unit tests and PostgreSQL integration).
- Do not run a large screenshot matrix for this narrow follow-up. The user will provide the next Android screenshots for visual review.
- Native Android camera/gallery picker, keyboard viewport, fixed footer and TalkBack remain real-device checks.
- CI passing is not visual acceptance. The user screenshots remain the visual acceptance gate.
- No merge or production deployment without explicit authorisation.

## 6. Current delivery status

- PR #76 is merged; it is the baseline, not this follow-up.
- This follow-up is on a separate branch and will be delivered as a new PR.
- Implementation, automated CI, user screenshot acceptance, merge and production deployment are separate statuses.
