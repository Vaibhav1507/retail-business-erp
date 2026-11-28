# Handwritten sales bill review — restore simple Generate Invoice UX

**Status:** Corrective implementation; visual and automated validation pending  
**Route:** `#/sell/bill`  
**Scope:** Only the post-extraction review surface for handwritten sales-bill import.  
**Baseline:** Existing New Sale screen and compact extracted-line review-card contract in `invoice-handwritten-import-ux-refinement.md`.  
**Non-goals:** No redesign of New Sale, no API/schema/provider changes, no accounting/stock/GST/payment/serial-validation changes, no change to posting confirmation, no redesign of the floating assistant.

## Problem

The merged V2 extraction work exposed implementation diagnostics as shopkeeper tasks. The review surface showed raw OCR alternatives, image-region inspection controls, per-field provenance labels, identifier classification dropdowns and confirmation checkboxes, and a separate source-rate field. On a phone this is long and confusing. It also repeats review warnings in several places.

The intended user is a small retailer who wants to import a handwritten bill and correct only the few values that need attention. The UI must not ask that user to debug OCR alignment or classify every alphanumeric token.

## Approved direction

Keep the existing New Sale shell, customer fields, sticky bill summary/footer and synchronous import flow. Restore a compact, task-focused review:

1. Keep one compact review card per detected physical product line.
2. Show only the product description, quantity and RBOS selling price in the primary card.
3. Keep serial number entry optional inside **More details (optional)**. The complete original description remains verbatim, so model/serial-like strings are not discarded. The operator can enter a serial when needed; candidate metadata is never silently copied into sale serials.
4. Keep brand, model, HSN and discount inside the same per-line disclosure.
5. Remove raw OCR transcripts, per-token classification dropdowns, confirmation checkboxes and repeated source-read/provenance labels from the default view. These are internal evidence, not primary shopkeeper tasks.
6. Keep a single plain-language review notice when any extraction warning exists. Continue to show specific correction errors beside the relevant editable field.
7. Show source bill number, date and total as simple editable values; retain subtotal/tax in a collapsed disclosure.
8. Put the retained original bill and OCR region overlay behind a collapsed **View original bill** disclosure. Authenticated source access, region highlights and evidence retention remain intact.
9. Keep Add reviewed lines and Discard extraction actions, current customer prefill, validation, Review bill footer, and final sale confirmation unchanged.
10. Distinguish a genuinely blank Rate cell from an OCR failure using one helper under the selling-price field: “No rate is written on the source bill. Enter your selling price.” Never infer a selling price from line amount or total.

## Extraction/provider boundary

This is a UI correction only. Do not change Haiku, Cloud Vision, optional verifier/fallback configuration, prompts, extraction schema, matching threshold, source-document persistence or API response contracts in this PR. The UI must not display raw provider/model/OCR details. Backend telemetry remains the source for provider diagnostics. Do not claim a value is accurate merely because a provider returned it.

## Acceptance criteria

- Existing New Sale layout and sticky footer are unchanged.
- Handwritten import remains synchronous and returns to the existing review flow.
- Compact extracted-line cards show description, quantity and one selling-price field.
- A blank source Rate cell does not display as a missing/failed AI value and is never populated automatically.
- Serial entry remains optional under More details; no candidate classification controls or confirmation checkboxes appear.
- Source bill preview is available but collapsed by default.
- No raw OCR transcript, competing model text or repeated provenance labels appear in the primary review.
- One concise review notice is shown at the top when warnings exist; field-level correction issues remain adjacent to editable fields.
- Add/discard actions, customer requirements, duplicate serial validation, sale totals, GST, stock/WAC, payment, offline, print/WhatsApp, tenant boundaries and posting confirmation remain unchanged.
- Tests cover import, simplified review, blank source-rate helper, optional serial entry, customer prefill and discard/reset.
- Full Playwright/Axe screenshot validation is run for the focused screen after implementation. Native Android camera/gallery, keyboard and TalkBack remain real-device acceptance.

## Evidence and diagnosis notes

The current implementation marks a product description ambiguous when an independent OCR text-alignment score is below 0.55 or no line is aligned. That is an engineering signal, not proof that the Haiku transcription is wrong: handwriting, word splitting and table layout can reduce text similarity. This PR deliberately does not change that algorithm; it removes raw mismatch diagnostics from the normal UI. Accuracy should be assessed against independently verified source labels and provider telemetry.

The supplied invoice capture appears to have a blank source Rate cell while the line amount/printed total is ₹17,500. That is consistent with `unitPrice = null` and `fieldStates.unitPrice = not_present`; it is not evidence that Haiku should invent a rate. The RBOS selling price is a separate operator-entered value.

## Delivery state

The corrective implementation is on `fix/sales-invoice-review-restore-simple-ux`. Automated checks and visual acceptance are pending. No merge or production deployment is included.
