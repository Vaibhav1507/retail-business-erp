# Billing & Sales — Create Invoice design specification

**Status:** Design contract completed; PR #74 compared as an implementation candidate, not yet accepted  
**Route:** `#/sell/bill`  
**Screen group:** Billing & Sales five-route redesign (route 1 of 5)  
**Design source of truth:** This repository specification. Figma is optional; no Figma work is required.  
**Supporting exploration:** [MagicPath action-first invoice prototype](https://designs.magicpath.ai/v1/cozily-dusk-6555) — prototype only; native camera/gallery and application services are simulated/not connected.  
**Implementation candidate:** [PR #74](https://github.com/Vaibhav1507/retail-business-erp/pull/74), branch `ux/invoice-entry-action-first`, head `72cfe96dd8b361d77123d136f1f6f12ad6ac4768`.

---

## 1. Problem and intended outcome

A shopkeeper must create a correct sale invoice quickly at a busy counter, usually on an Android phone. The operator may have limited experience with business software and may be entering a catalogue product, an unlisted/manual item, or details read from a product tag/photo or supplier/customer handwritten bill.

The current experience accumulated competing product actions, wrapped the tag-photo action, exposed too many identifiers at once, and made customer requirements/validation easy to miss. The target experience is calm and action-first: establish the customer, enter products in one shared editor, review the bill, then explicitly confirm posting/printing.

**Primary success outcome:** the user can create and review a sale without confusion about which action adds an item, without losing extracted/manual values, and without accidentally posting an incomplete or duplicate transaction.

## 2. Scope and non-goals

### In scope

- Customer identity and lookup (name, mobile, optional address details).
- Invoice metadata and payment selection.
- Catalogue search, manual/ad-hoc product description/model entry and line editing/removal.
- Product-tag camera/gallery capture through the existing source chooser; extracted data returns to the same product editor.
- Handwritten-bill extraction as a separate, secondary import/review path.
- Progressive disclosure for optional product identifiers.
- Quantity, unit price, line discount and bill discount.
- GST selection/rate display, totals/review, confirmation, posted and pending-offline states.
- Held bills and existing offline queue/status UI.
- Invoice document preview and print controls.
- The route-specific focus mode and navigation back to Sell.

### Explicitly out of scope

- Backend/API/data-model changes, invoice posting/accounting rules, GST calculation changes, inventory policy changes, new OCR/AI providers, catalogue auto-creation, customer consent policy, WhatsApp delivery semantics, and changes to offline/idempotency behaviour.
- Redesign of Home, Stock, Purchasing, global navigation or unrelated app shell.
- New PDF/share capabilities not already supported by the application.
- Re-running the existing 178-screenshot pre-redesign baseline.

Any discovered need to change an invariant or API is a separate proposal and approval, not part of visual implementation.

## 3. Existing implementation and dependencies

### Route and component map

- `App.tsx` parses hash routes and renders `SellPage`; PR #74 adds an invoice-focus shell state for `#/sell/bill`, suppressing the persistent main navigation and Navigation Assistant only while the invoice is active.
- `Pages.tsx` / `SellPage` owns the Sell title, route back-link and route selection.
- `GenerateInvoice.tsx` owns customer lookup, invoice state, product line editor, handwritten invoice extraction review, tag capture integration, totals, review, confirmation, held bills and offline queue.
- `ItemTagCapture.tsx` owns the existing photo-source chooser and calls the existing `POST /sales/item-tag-scan` extraction endpoint.
- `SalesInvoiceCapture.tsx` owns handwritten-bill extraction; extracted lines remain editable and unposted until the user adds them to the bill.
- `InvoiceDocument.tsx` renders the customer-facing invoice.
- `invoice.css` and `billing-forms.css` provide invoice and shared billing layouts/print rules.
- Existing APIs include customer lookup/create/update, business profile, products, locations, tag scan, sales post/detail and offline queue operations.

### Dependencies and trust boundaries

- Customer lookup is by mobile and existing customer records. A selected customer can be updated when the invoice is confirmed; a new customer is created as part of the online confirmation flow.
- Product suggestions are catalogue lookups. A free-text line remains an ad-hoc sale line and must not create a catalogue record.
- Tag extraction is assistive only. It populates the same editable fields as manual entry; it never adds a line or posts a sale by itself.
- Handwritten invoice extraction is an editable draft. The operator reviews and adds extracted lines; extraction never posts a sale.
- The server remains authoritative for totals, GST, stock checks, posting, payment allocation, serial history checks, idempotency and accounting.
- Offline bills are queued as pending drafts and must never be described as posted or assigned an official invoice number until the server confirms them.
- Print must render the invoice document only, not the RBOS navigation/application shell.

## 4. Current-state findings and priority

| ID | Priority | Finding | Evidence / status |
|---|---|---|---|
| INV-01 | P0 | Competing product actions and wrapped tag-photo action create uncertainty at the point of adding a product. | User-reported screenshot feedback and PR #74's stated scope. |
| INV-02 | P0 | Customer name and a valid mobile number must be explicit prerequisites to posting, with errors shown beside the relevant fields. | Product invariant and existing UI validation path; verify the final interaction and API rejection behaviour in the integrated journey. |
| INV-03 | P1 | Product tag capture must be contextual to product entry and use a familiar camera/gallery chooser, not another competing add action. | Existing `PhotoSourcePicker` integration in `ItemTagCapture`; PR #74 moves Scan tag beside product description/model. |
| INV-04 | P1 | Brand/model/serial/IMEI/barcode/HSN/MRP add cognitive load when always visible. | Current form structure and user feedback; progressive disclosure is appropriate. |
| INV-05 | P1 | Handwritten-bill import and product-tag capture solve different tasks and must not compete in the item action row. | Existing `SalesInvoiceCapture` separate flow and PR #74 page-header placement. |
| INV-06 | P1 | Review and posting need a stable, always-understandable total/action area on phone without hiding the last form fields or colliding with Android safe areas. | Existing sticky summary implementation candidate; final responsive acceptance deferred to five-route group pass. |
| INV-07 | P1 | The invoice preview/print path must not include app navigation, assistant, form controls or unrelated page content. | Existing invoice-only print CSS; preserve and verify after all routes are implemented. |
| INV-08 | P1 | Extraction output must be treated as suggestions and be reviewable/editable before adding/posting. | Existing shared editor and handwritten extraction review flow. |
| INV-09 | P2 | Held and offline-pending states must be clearly distinguished from posted invoices. | Existing local held-bill storage and offline outbox state model. |

The earlier 178-screenshot run is the pre-redesign baseline only. It is not a visual acceptance result for PR #74 and will not be repeated.

## 5. Selected design direction — Concept A: action-first

### Information hierarchy

1. **Focused page header:** “Create invoice” / “Generate invoice”, short helper sentence and a quiet “Import handwritten bill” action.
2. **Customer card:** mobile lookup first, customer name required, optional address/city/state/pincode. Existing-customer suggestions appear directly under the mobile field.
3. **Invoice details card:** sale date, store only when multiple locations exist, payment mode and conditional split/partial-payment fields.
4. **Add products card:** one shared editor with product description/model, quantity, selling price and discount. “Scan tag” is a contextual secondary control attached to the description/model field. One primary “Add product” action adds the line.
5. **Optional product details disclosure:** Brand, Model, Serial, Barcode, IMEI 1/2, MRP (reference only) and HSN. Closed by default; open automatically when tag extraction supplies details or a serial/IMEI validation error needs correction.
6. **Bill lines:** compact, readable list/table with item description and available metadata, quantity, unit price, discount, line amount and a clear remove action. Mobile uses stacked line cards rather than forcing a wide table.
7. **Sticky summary/action area:** GST toggle and rate when enabled, bill discount, total, primary “Review invoice” action and secondary “Hold bill” action. Respect safe-area inset and leave sufficient scroll padding so the last content is never obscured.
8. **Review state:** customer and item details in a customer-facing invoice document preview; explicit “Confirm & print” primary action and “Back to edit” secondary action.
9. **Posted state:** official invoice number, WhatsApp status, paper-size selection, “Print again” and “New invoice”.
10. **Pending offline state:** explicit “Saved on this device / Pending sync — not posted” status, explanation that server validation/posting is still pending, and a safe return-to-billing action.

### Product-entry interaction

- Search existing products by name; selecting a suggestion sets the catalogue product ID and display name.
- If no catalogue match is selected, the entered description/model is a manual ad-hoc line. Explain that it will not create a catalogue product.
- “Scan tag” opens the existing camera/gallery chooser. After selection, show progress and return extracted fields to this same editor. Do not auto-add the item.
- The operator reviews extracted description, brand/model, serial/IMEI/barcode and other fields, enters quantity and price, then taps the single “Add product” action.
- Keep quantity defaulted to 1. Quantity and selling price are required; discount is optional and cannot be negative or exceed the line amount.
- Serial, IMEI and barcode are optional. Invalid/duplicate identifiers show a field-level correction message. Missing serial is a non-blocking warning during review.
- Handwritten-bill import remains separate in the page header. Its extracted customer and line values remain editable; the user must review and explicitly add the lines.

### Customer interaction

- Mobile is the lookup key and should be the first field in the card.
- Customer name and valid mobile (10–15 digits after normalisation) are required before review/post.
- Show inline field errors, mark invalid fields with `aria-invalid`, associate the message with the field and focus the first invalid field when review is requested.
- Address, city, state and pincode are optional.
- Do not create or update a customer just because a field was typed; retain the existing commit timing and API semantics.

## 6. Component and token contract

Prefer existing RBOS components and tokens; do not add a new UI library for this screen.

| Element | Contract |
|---|---|
| Page/workspace | Existing app shell; invoice-focus modifier only on `#/sell/bill`; retain header and Sell back-link. |
| Page header | Title, one concise instruction and visually quiet handwritten-bill import. |
| Cards | Reuse `.billing-card`, `.billing-grid`, `.billing-field` and existing form styles. |
| Primary action | Existing RBOS primary button styling; only Add product is primary within product editor. Review/confirm is the primary action for its respective stage. |
| Secondary actions | Existing `.secondary` treatment for scan, hold, back, discard and print-again where appropriate. Do not give multiple actions equal visual weight. |
| Photo chooser | Reuse `PhotoSourcePicker` from `ItemTagCapture`; preserve camera/gallery intent, accessible dialog/sheet semantics, close/cancel and file input behaviour. |
| Disclosure | Native `details/summary` is acceptable; preserve keyboard operation and announce expanded state. |
| Bill lines | Existing invoice/billing table styles on wide screens; mobile card presentation with labels for each value. |
| Feedback | Inline field errors; `role=alert` for blocking failures; `role=status` for progress, non-blocking warnings and success/pending state. |
| Invoice document | Reuse `InvoiceDocument`; screen preview and print layout must remain consistent. |

Use existing CSS custom properties such as `--brand`, `--brand-dark`, `--line`, `--muted` and `--danger`. Avoid hard-coded new palette values where an existing token is available. Preserve existing RBOS typography, radii, spacing and focus treatment.

## 7. Responsive behaviour

| Profile | Required layout |
|---|---|
| Small phone (360 × 800) | Single-column cards; product description and Scan tag remain together without wrapping into a second competing action row; inputs and primary actions full-width where needed; bill lines become stacked cards; sticky summary respects safe area and does not cover the last field. |
| Standard phone (390 × 844 / 412 × 915) | Same single-column hierarchy; concise helper text; no horizontal scroll; sticky total/action remains readable and reachable while scrolling. |
| Tablet (768 × 1024) | Centered form with bounded readable width; use two-column customer/invoice fields where natural; product editor remains one coherent group; summary may use a horizontal layout. |
| Landscape phone (844 × 390) | No fixed overlay may consume the working height; summary can compact or flow with content; all fields/actions remain reachable by scroll. |
| Desktop (1280 × 900) | Bounded centered workspace; use existing 12-column billing grid for customer and invoice metadata; product entry is one aligned row where space permits; summary is clearly separated and not excessively wide. |

All interactive targets should be at least 44 × 44 CSS px (prefer 48px where practical). Maintain visible focus, sufficient contrast, readable text and no reliance on colour alone. Ensure the virtual keyboard does not obscure the focused field or the action required to continue. Use `env(safe-area-inset-bottom)` for fixed/sticky mobile controls and reserve bottom padding in the scroll container.

## 8. Important states and required behaviour

| State | Expected behaviour |
|---|---|
| Initial/loading | Page remains usable; customer/product lookups may load independently; do not show stale data as current. |
| Empty invoice | Explain the next action; Review remains unavailable or reports “Add at least one product”. |
| Customer lookup | Loading/no-match/error states are understandable; suggestions are keyboard-operable and selectable. |
| Product lookup | Search suggestions are labelled; no match permits manual description/model entry. |
| Tag capture | Camera/gallery chooser; cancel returns unchanged to form; progress feedback; failure preserves typed values; extraction fills fields but does not add a line. |
| Handwritten import | Upload/extraction status; extracted fields and warnings reviewable; empty/partial extraction permits manual entry; discard returns to the form without posting. |
| Field validation | Name, mobile, product description/model, quantity, price, discount, MRP and serial/IMEI errors appear at the field that needs correction; focus first invalid field on review. |
| Optional identifiers | Serial/IMEI/barcode remain optional; serial absence is a non-blocking warning; duplicate/format validation remains intact. |
| Bill review | Shows complete customer, line, discount, GST and total information; no post occurs before explicit confirmation. |
| Confirm/post | Disable repeat submission while busy; preserve idempotency; show recoverable errors and avoid creating a duplicate sale on uncertain network outcome. |
| Posted | Show official invoice number and WhatsApp delivery state; allow print again and start a new invoice. |
| Offline pending | Clearly state pending/not posted/no official number; preserve local draft and retry/sync path. |
| Held bill | Resume restores values; discard requires confirmation; held is not posted. |
| Print | Only invoice document is visible; A4 and 80 mm options remain; mobile-only item cards are replaced by print table as existing CSS intends. |
| Cancel/back | Leaving a dirty invoice continues to use the existing unsaved-work confirmation; cancellation keeps the operator on the invoice. |

## 9. Business and data invariants — do not change

- Customer name and valid 10–15 digit mobile are required to post an invoice.
- Manual/ad-hoc product lines are allowed when no catalogue item matches; never silently create a catalogue record.
- Negative stock remains allowed under the existing business rule.
- Serial/IMEI/barcode remain optional. Preserve duplicate/format checks and non-blocking missing-serial warning. IMEI retains its 15-digit Luhn validation.
- Server-calculated GST and authoritative totals remain the source of truth.
- Cash, UPI, Credit, split and partial payment semantics remain unchanged.
- Invoice extraction is editable and unposted until explicit confirmation; it must never auto-post.
- Offline held bills and queued bills remain drafts/pending until server-confirmed; never show an official invoice number prematurely.
- Preserve idempotency, stock policy, serial-history checks, accounting postings, inventory valuation, audit trail, WhatsApp consent/opt-out/delivery state and duplicate-send protection.
- Preserve existing route/API contracts and tenant/security boundaries.
- Printed output is the invoice document only, not the app shell.

## 10. Acceptance criteria

### Functional

- [ ] Exactly one primary Add product action exists in the product editor.
- [ ] Scan tag is contextual to the description/model field and opens the existing camera/gallery chooser.
- [ ] Tag extraction populates the shared editor, does not add a line, and preserves manual edits on failure/cancel.
- [ ] Handwritten-bill import is separate from product entry; extracted lines are editable and require explicit add.
- [ ] Customer name and valid mobile are required; errors appear inline and focus the first invalid field.
- [ ] Catalogue selection and manual/ad-hoc lines both work without unintended catalogue creation.
- [ ] Optional identifier disclosure defaults closed and opens after extraction or identifier validation failure.
- [ ] Serial/IMEI/barcode remain optional; duplicate/format rules and missing-serial warning remain.
- [ ] Quantity, price and discount validation works; bill discount and GST display are correct while server remains authoritative.
- [ ] Review/back/edit/confirm/post/new invoice work without losing values or double-posting.
- [ ] Held and offline-pending states remain explicitly non-posted until confirmed.
- [ ] A4 and 80 mm print controls remain and print output contains only the invoice.
- [ ] Existing APIs, routes, tenant boundaries and business invariants are unchanged.

### Usability/accessibility

- [ ] No horizontal overflow or clipped/overlapping controls at all required viewport profiles.
- [ ] Product field and Scan tag remain one coherent action group on small phones.
- [ ] Sticky summary never obscures form content, focused fields or keyboard-required actions.
- [ ] All important controls have accessible names, visible focus and keyboard operation.
- [ ] Field errors are programmatically associated and not conveyed by colour alone.
- [ ] Touch targets are at least 44 × 44 CSS px, preferably 48px where practical.
- [ ] English and Hindi labels/helper text wrap without truncation or collisions.
- [ ] Loading, empty, error, retry, success, cancel, held, pending and print states have understandable feedback.

## 11. PR #74 implementation comparison

PR #74 is an implementation candidate for this contract, not a substitute for the contract or proof of visual acceptance.

| Contract area | PR #74 implementation | Comparison |
|---|---|---|
| Focused workspace | Hides persistent main navigation and Navigation Assistant only on invoice route; retains app header and Sell back-link. | **Aligned.** Confirm the back-link and unsaved-work guard across route navigation in final group journey. |
| Contextual tag capture | Scan tag is beside product description/model and uses the existing source chooser. | **Aligned.** Candidate component test asserts adjacency; camera/gallery native behaviour still needs Android acceptance. |
| Single product action | Add product is the only action in the product action row. | **Aligned.** Handwritten import is not in that row. |
| Handwritten-bill import | Separate page-header action with extraction review. | **Aligned.** Confirm long header copy and small-phone wrapping in group visual review. |
| Progressive disclosure | Optional details closed by default; opens after extraction and serial validation. | **Aligned.** Ensure keyboard/focus does not jump unexpectedly when details open. |
| Customer required fields | Review validates name and 10–15 digit mobile; inline messages and first-invalid focus exist. | **Partially aligned.** Verify phone label visibly communicates required status and field error is associated via `aria-describedby`; add tests for blank/invalid phone and name. |
| Product/pricing validation | Inline errors for product, quantity, price, discount and MRP; serial/IMEI checks retained. | **Aligned in source.** Verify all error states, clear-on-edit behaviour and mobile scroll/focus. |
| Sticky total/action | Summary bar is present with GST, bill discount, total, Review and Hold. | **Direction aligned; visual acceptance pending.** Verify safe-area, keyboard and short-landscape behaviour; do not let fixed summary hide content. |
| Bill line presentation | Mobile line-card CSS exists; desktop table retained. | **Source aligned; screenshots pending.** Check long product names/identifiers and action reachability. |
| Review/posted/print | InvoiceDocument used for review/done; A4/80mm and print-again retained; print CSS isolates invoice. | **Aligned by intent.** Inspect actual print preview and verify no app shell leaks. |
| Offline/held/idempotency | Existing held-bill storage, pending offline queue and idempotency path retained. | **Preservation claimed by PR; targeted regression required.** Do not treat CI alone as proof of offline user experience. |
| Business invariants | PR description says no API/backend/business rule change. | **No intentional change reported.** Verify diff remains limited to frontend, tests and styles; keep all invariants above as regression requirements. |

### PR #74 evidence refreshed 2026-11-24

- PR #74 is **open/draft**, branch `ux/invoice-entry-action-first`, head `72cfe96dd8b361d77123d136f1f6f12ad6ac4768`, base `main`; not merged.
- Changed files: `web/e2e/sales.spec.ts`, `web/src/App.tsx`, `web/src/GenerateInvoice.test.tsx`, `web/src/GenerateInvoice.tsx`, `web/src/ItemTagCapture.test.tsx`, `web/src/ItemTagCapture.tsx`, `web/src/invoice.css`.
- Exact-head standard CI run [#45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) completed **success**: frontend TypeScript/tests/lint/build/dependency audit; backend Release build/unit tests/container build (no push); PostgreSQL integration bootstrap/build/tests.
- Focused component and E2E assertions exist. The full five-route Playwright journey, responsive screenshots, visual review and Axe verification are intentionally deferred until all five routes are implemented.
- Native Android camera/gallery, printer/share sheet, TalkBack and representative shopkeeper acceptance remain pending.
- No merge or deployment has occurred or is authorised.

## 12. Implementation plan and focused checks

1. Keep the current PR #74 branch as the Create Invoice implementation candidate; inspect ancestry before integrating the other route work. Do not merge it independently.
2. Resolve any contract gaps identified above with focused component/unit tests and targeted Playwright assertions only as needed to prevent broken implementation.
3. During invoice implementation changes, run TypeScript, relevant component tests, lint/build and narrow route behaviour checks. Do not run the full visual-audit matrix yet.
4. After the Sell landing, Sales History, Collections and Returns specs and implementations are complete, run the single integrated five-route Playwright + screenshot + Axe pass described in the programme.
5. Inspect screenshots/contact sheets, fix concrete issues and rerun affected journeys only.
6. Then perform native Android checks and shopkeeper acceptance. Keep merge and deployment separate and unauthorised until explicit approval.

## 13. Open risks and deferrals

- **Visual acceptance:** PR #74 has not been reviewed against post-redesign screenshots. Revisit only in the final five-route group pass.
- **Native camera/gallery:** browser tests do not prove Android permission prompts, native chooser behaviour or actual device camera/gallery. Revisit on Android device.
- **Print:** browser print CSS intent is present; actual device/browser printer and thermal output remain unverified.
- **Keyboard/focus:** verify field focus, chooser focus return, details disclosure and sticky summary while the Android keyboard is open in group verification.
- **Customer required semantics:** verify the required marker for mobile and programmatic field-error association; address with focused code/test change if needed.
- **MagicPath:** connected agent/API quota was previously exhausted. Normal editor is optional; if unavailable, continue from this specification and existing prototype. This is not a blocker.
- **Figma:** permanently optional and not required. Do not retry, upgrade or wait for quota.

**Next route:** Sell landing `#/sell/menu`. Audit its existing `SellPage` in `web/src/Pages.tsx`, route/navigation entry points and translations; then create its own repository design specification before implementation.
