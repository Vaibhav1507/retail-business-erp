# Create Invoice — product details and assistant launcher follow-up

**Status:** Implementation candidate on `ux/invoice-product-details-assistant-launcher`  
**Baseline:** PR #77 merged to `main` at `4f8fc9c15b029319bc87defb77f9c993b1cf9d40`  
**Scope:** Create Invoice and shared Navigation Assistant launcher only.

## User feedback

- Rename the product photo action from **Import tag** to **Scan tag**; retain the existing camera/gallery picker and scan/extraction behaviour.
- Surface **Brand** in the always-visible product-entry fields rather than hiding it in More item details.
- Keep **Payment mode** visible without expanding Payment & bill details; keep **Sale date** inside the optional disclosure.
- Move the Navigation Assistant launcher from the header to a floating bottom-right control, using a compact message icon rather than the word Assistant.

## Design decisions

- Preserve the existing Generate Invoice hierarchy, compact phone scale, green/white RBOS styling, customer/address layout, electronics Model + Quantity row, optional Serial, sticky Review bill bar and existing form semantics.
- Brand is a single always-visible optional field for all business categories. It remains part of the same `brandText` state and sale-line payload.
- Scan tag is a label-only change to the existing `ItemTagCapture` action. PhotoSourcePicker, validation, barcode fast path, extraction and tag-prefill remain unchanged.
- Payment mode remains bound to the existing `mode` state and saleType/payment logic. Only placement changes. Sale date remains in the optional disclosure.
- The assistant launcher remains the same NavigationAssistant component and retains accessible open/close labels, panel, voice capture and navigation behaviour. It floats above bottom navigation with safe-area spacing; the opened panel remains above page content.

## Non-goals and invariants

- No backend, API, database, accounting, payment allocation, GST, invoice, stock, serial/IMEI/barcode validation, offline, print or WhatsApp behaviour changes.
- No changes to Sales History, Collections, Returns or unrelated navigation.
- No additional dependencies.

## Acceptance criteria

- [ ] Tag action reads **Scan tag** and opens the existing camera/gallery chooser.
- [ ] Brand is visible without expanding More item details for electronics and non-electronics.
- [ ] Brand captured from a tag is shown in the visible field and remains included in the invoice line.
- [ ] Payment mode is visible without expanding Payment & bill details.
- [ ] Sale date remains inside Payment & bill details.
- [ ] Cash, UPI and Credit preserve existing payment behaviour, including Credit paid-now/split-payment fields.
- [ ] Assistant launcher is a compact message icon fixed bottom-right, above bottom navigation and Android safe area.
- [ ] Assistant panel, keyboard interaction, voice capture, accessible name and navigation shortcuts still work.
- [ ] Existing header layout no longer reserves space for the assistant launcher.
- [ ] Existing customer, item, invoice and accounting behaviour remains unchanged.

## Validation

- Focused GenerateInvoice tests cover Scan tag, visible Brand, Payment mode and Sale date placement, and existing tag extraction.
- Run focused NavigationAssistant tests, frontend typecheck/tests/lint/build and current PR CI.
- No exhaustive Playwright matrix for this narrow follow-up; user will provide Android screenshots for visual acceptance.
- Real-device acceptance: launcher placement against bottom navigation, keyboard-open panel, camera/gallery chooser and TalkBack.
- CI and unit tests do not establish visual acceptance. Merge and deployment remain separate and require approval.
