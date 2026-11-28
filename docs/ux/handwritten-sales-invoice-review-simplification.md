# Handwritten sales bill review — restore the pre-V2 Generate Invoice behaviour

**Status:** Corrective implementation; automated and visual validation pending  
**Route:** `#/sell/bill`  
**Baseline:** The exact compact review markup from the merge base of PR #120 (`35b9c7cf1cd688b6ae57b82e1e8b9ed12d85c870`).  
**Scope:** Restore the pre-V2 handwritten-bill review interaction. Keep V2 extraction/provider and evidence storage internal.  
**Non-goals:** No New Sale shell redesign, no backend/provider/prompt/schema/OCR matching changes, no change to sale posting, accounting, stock, GST, payment, customer validation, tenant isolation or final confirmation.

## What is being restored

The pre-V2 review flow is the reference, not a new reinterpretation:

1. Original New Sale page and sticky Review bill footer stay unchanged.
2. Source bill number and bill date remain the two editable header values.
3. Handwritten total is displayed as a simple read-only summary, as before.
4. Each compact item card shows Product description, optional Serial number(s), Qty and one Unit price input.
5. Brand, model, HSN and discount remain inside the existing collapsed per-item disclosure.
6. Existing line amount/GST metadata and Add reviewed lines / Discard extraction actions remain in the same locations.
7. The original source image, OCR regions, possible omitted rows and identifier candidate classifier are not shown as new shopkeeper controls.
8. No separate source-rate input, separate selling-price input, field-provenance labels or candidate confirmation checkboxes are shown.
9. Source rate and sale price remain distinct internally. The single visible Unit price input is bound to the RBOS selling price. When the source rate is explicitly absent, it stays blank until the shopkeeper enters the value; no rate is inferred from line amount or bill total.
10. Serial entry remains visible, optional and editable as it was before V2. Uncertain candidate strings are preserved in the verbatim product description and are not silently posted as serials.

## Why PR history matters

PR #117 changed six documentation/quality files only; it did not change the application UI. The UI/extraction rewrite was PR #120. This correction restores the exact review JSX from PR #120's base commit while retaining V2's internal provider, source-document persistence, provenance and server-side validation capabilities.

## Acceptance criteria

- Review block matches the pre-V2 markup and field order.
- No original-image preview disclosure or OCR-region inspection controls in the normal review.
- No identifier classification/confirmation controls.
- Exactly one Unit price field per line; the internal source Rate remains independent.
- Optional serial field remains visible in the compact item card.
- Header has bill number, date and read-only handwritten total as before.
- Existing manual entry, customer prefill, item conversion, duplicate serial checks, financial calculations, GST, payment, offline, print/WhatsApp, tenant rules and final sale confirmation are unchanged.
- Focused tests verify import, blank source-rate behaviour, one price field, optional serial flow, customer prefill and discard/reset.
- Full RBOS Playwright/Axe visual audit remains the acceptance evidence. Native Android camera/gallery, keyboard and TalkBack remain device acceptance.

## Delivery state

Implementation is on `fix/sales-invoice-review-restore-simple-ux`. No merge or production deployment is included.
