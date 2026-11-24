# Billing & Sales — MagicPath invoice fidelity and researched mobile UX contract

**Status:** Implementation candidate on ux/billing-sales-mobile-polish; visual acceptance pending shopkeeper screenshots  
**Base:** main after PR #74 merge (c4d71190b69a581dae389d90bcd1e4e9e58f3f00)  
**Scope:** #/sell/menu, #/sell/bill, #/sell/bills, #/sell/collect, #/sell/return, and the shared Navigation Assistant launcher  
**Primary design source:** Three user-supplied MagicPath Android screenshots (New Sale / Add items / bill summary; 709 × 1536 reference captures).  
**MagicPath link:** [Invoice Entry / action-first direction](https://designs.magicpath.ai/v1/cozily-dusk-6555). MagicPath generation/API quota is exhausted; the supplied screenshots are the authoritative reference for the invoice screen. No attempt is made to reproduce MagicPath-designed pages that were not supplied.

---

## 1. Product goal and design boundary

Make Billing & Sales feel like one coherent, low-friction workflow for an Indian shopkeeper using an Android phone. **Create Invoice is a fidelity task:** implement the supplied MagicPath design, not a new interpretation. **The other pages are a research-led RBOS design task:** use established POS/accounting patterns and RBOS's existing domain behaviour to create appropriate interfaces. Do not copy the invoice card layout and sticky review bar onto every page.

This is a presentation and workflow improvement only. Preserve routes, APIs, server calculations, tenant boundaries, inventory rules, accounting semantics, permissions, offline state and retry safeguards. Do not introduce a new transaction capability merely because a competitor supports it.

## 2. Source and evidence hierarchy

Use this order when resolving a design question:

1. **Create Invoice:** user-supplied MagicPath screenshots are the visual contract. Match the visible hierarchy, component placement, text, proportions, colours, spacing, card treatments and fixed review area.
2. **Business behaviour:** current RBOS implementation, API contracts and accounting/inventory invariants are authoritative.
3. **Other Billing & Sales pages:** the page-specific decisions in §4, informed by the free public sources in §8 and by the existing RBOS screenshots.
4. **Shared RBOS design system:** existing colour, type, focus, button and navigation tokens. Do not invent a second visual system.

MagicPath screenshots define the design intent, not the entire set of runtime states. Loading, validation, empty, network failure, held/offline and confirmation states must remain understandable while preserving the same visual language.

## 3. Create Invoice — exact MagicPath reference

### 3.1 Page shell and header

- A focused invoice workspace: no persistent bottom app navigation and no floating Assistant launcher competing with invoice controls.
- Pale mint/grey page background; white page header; white rounded form cards; dark ink text; deep green primary actions; subtle green-grey borders.
- Header in one row: pale mint rounded-square back action, **New sale**, subtitle **Create a bill for your customer**, and pale mint **DRAFT** pill.
- The header remains visible at the top while the form scrolls.
- Immediately below the header, show the horizontal stepper: active **1 Add items**, divider, inactive **2 Review**.
- On desktop, retain the same hierarchy in a restrained centred content column rather than stretching fields across the entire viewport.

### 3.2 Customer card — section 01

- One white rounded card. Eyebrow **CUSTOMER** on the left and pale circular **01** marker on the right.
- Heading **Who are you billing?**
- First field **Mobile number \***, full width, with hint/placeholder **Enter 10-digit mobile number**.
- Second field **Customer name \***, full width, with hint/placeholder **Name on the bill**.
- Helper copy for a new customer: **New customer? Enter their name and number to add them with this bill.**
- Keep existing customer search/match behaviour and identity selection. Show validation directly beside its field.
- Address, city, state and pincode stay available but collapsed under **Add address (optional)** / **Edit address (optional)**. They must not compete visually with the required two fields.

### 3.3 Items card — section 02

- Separate white rounded card. Eyebrow **ITEMS**, circular **02** marker, heading **Add products**, subtitle **Search your catalogue or enter the item manually.**
- Dashed import callout above the product editor: left copy **Have a handwritten bill?** and right action **Import bill ↗**. It opens the existing camera/gallery/PDF source chooser and existing invoice extraction flow; do not introduce another import path.
- **Item description \*** is the primary full-width search/manual-entry field. The square tag/photo action sits directly to its right, aligned in the same row and never wraps to a second line. Its accessible name remains **Scan tag**.
- **Model number** is a full-width field directly below the item description.
- **Quantity \*** and **Selling price (₹) \*** sit side by side at phone widths.
- **More item details** is a clear disclosure row with **Optional** aligned right. Put line discount and specialist fields such as brand, serial, barcode, IMEI, MRP and HSN inside it. Keep serial, barcode and IMEI optional; any missing-serial warning must not block billing.
- A single full-width deep-green **＋ Add item** action closes the editor. Existing catalogue match, ad-hoc item, tag extraction, identifier duplicate checks and validation remain intact.
- Added lines appear as compact readable item summaries below the add action; they must not create a wide, clipped table on a phone. Removing a line remains explicit.

### 3.4 Bill summary and persistent review action

- Separate white rounded summary card below the Items card.
- GST row: checkbox, **GST invoice**, helper **Include GST details on this bill**. Show GST rate selection only when enabled.
- Summary order: **Subtotal**, **Bill discount (₹)**, then a divider and emphasized **Amount to collect** in green.
- Use existing RBOS calculations. The amount shown in the summary and fixed footer must agree; GST must remain consistent with the existing inclusive/exclusive calculation and server-side invoice rules.
- A separate white sticky bottom bar contains item count and total on the left and **Review bill →** on the right.
- Review remains disabled until at least one valid item has been added. Clicking it still validates required customer name/mobile, focuses the first invalid field and enters the existing review step only when validation passes.
- Sticky bar uses safe-area padding, does not hide the final field, and responds correctly to the Android keyboard/visual viewport.
- **Hold bill**, store, sale date, payment mode and credit split remain available under the collapsed **Payment & bill details** disclosure. Their placement changes; their data and behaviours do not.

### 3.5 Exactness checklist

- [ ] Header copy, back control and DRAFT pill match the supplied reference.
- [ ] Stepper says 1 Add items / 2 Review and has the correct active state.
- [ ] Customer card is above Items, numbered 01 and uses the two vertically stacked required fields.
- [ ] Items card is numbered 02 and includes the title/subtitle and dashed import callout.
- [ ] Item description and square Scan tag action share one row; model number is its own full-width row.
- [ ] Quantity and selling price are side by side.
- [ ] Optional item details and its Optional label are a disclosure row.
- [ ] Add item is the single full-width green primary action.
- [ ] GST / subtotal / discount / amount-to-collect have their own summary card.
- [ ] Item count, total and disabled/enabled Review bill action use the persistent bottom bar.
- [ ] No clipping, horizontal overflow, duplicated back link, floating assistant or persistent navigation on the focused invoice page.
- [ ] Existing functional tests still cover required fields, item add, ad-hoc item, capture/import, duplicate identifiers, offline/held handling and invoice print/WhatsApp separation.

The three supplied captures are the acceptance reference, not a generic inspiration board. Any deviation must be explained by a responsive constraint, accessibility need or preserved business function—not by redesign preference.

---

## 4. Other pages — research-led RBOS designs

These pages do **not** have a MagicPath screen reference. Their layout is derived from POS/accounting workflows and the specific tasks performed by shopkeepers. Keep them within the RBOS visual language: clear page title, one primary task surface, minimal card nesting, plain language, strong labels, restrained green status/action colour, readable rows and phone-first spacing.

### 4.1 Sell landing — #/sell/menu

**Primary job:** choose the next sales task quickly.

- Keep **Create new bill** as the one dominant action.
- Present **Sales history**, **Collect payment**, and **Customer returns** as three balanced compact action rows on phones, each with a plain-language description and arrow. Do not leave a lone secondary card on a second row.
- On wider screens the secondary actions may form three balanced columns.
- The route heading and short hint appear only on this landing page. Subroutes get their own heading and one back link; never repeat the parent heading.
- Do not add sales analytics or extra shortcuts to this hub during this scope.

### 4.2 Sales History — #/sell/bills

**Primary job:** find a past bill, check the balance, open or print it.

**Information hierarchy**
1. Page title **Sales history** and one sentence describing the task.
2. Prominent search field. Current RBOS query supports customer, mobile number and bill/invoice number; label its placeholder accordingly. Do not promise item-name search unless the RBOS endpoint is extended and verified.
3. Compact **Unpaid** filter and current result count.
4. Results: desktop table; phone cards with bill number/date, customer, total, paid amount, remaining balance and clear **View bill** action.
5. Bill details open on deliberate selection. Print A4/80 mm and print-only-invoice behaviour remain in the detail view, not scattered through every row.

**Status and recovery**
- Outstanding balance must be easy to spot without requiring users to calculate total minus paid.
- Show loading/updating, first-use empty, no-match, request-error and retry states distinctly.
- WhatsApp eligibility and status are explained in bill details. Use **Customer consent needed** where consent is absent. Retry and possible-duplicate confirmation remain explicit; never silently resend.
- Keep Refresh compact at the history heading and keep its touch height usable.

**Reasoning:** POS systems such as Shopify and Square support lookup by transaction/order identifiers, customer details and receipt/item references. RBOS should prioritise the search keys its current API actually supports and add additional fields only with a verified server query.

### 4.3 Collections — #/sell/collect

**Primary job:** record money received from a customer and correctly apply or leave it on account.

**Progressive flow**
1. Start with one searchable **Customer** field (name/mobile). Explain that selecting a customer reveals their outstanding balance and payment form.
2. Show the selected customer's **Outstanding balance** prominently.
3. Enter **Amount received (INR)**. Provide **Fill full balance** only when the outstanding balance is positive. Do not prevent recording a legitimate partial payment.
4. Offer **Apply to a bill (optional)** only after customer selection and open-sale lookup. The default remains **On account (no specific bill)**. When no open bill exists, explain that the payment will be recorded on account.
5. Show payment method, payment date and optional UPI/bank reference.
6. Use one primary **Record payment** action and a secondary **View customer statement** action. After success, show the updated balance and refresh the list/statement without losing the confirmation.

**Collection history**
- Keep it as a separate, secondary section below the entry task.
- Desktop uses a table; phone uses receipt cards with receipt number, date, customer, method, amount and **Print receipt**.
- Refresh is compact and distinct from the form CTA. Show loading, empty, error and retry states.

**Business boundary:** current RBOS supports one optional bill allocation or an on-account payment. Accounting products such as Zoho Books can split a receipt across multiple outstanding invoices, but this research is not authorisation to change RBOS's allocation model. Multi-invoice split allocation is explicitly out of scope unless separately designed and approved. Keep idempotency keys and server balance calculation unchanged.

### 4.4 Customer Returns — #/sell/return

**Primary job:** return some remaining units from a known original sale and record the correct refund outcome.

**Progressive flow**
1. Search **Choose the original bill** by bill number/customer/date; show invoice reference, date, customer and bill total in each suggestion.
2. After selection, show a compact original-sale summary (bill, customer, total).
3. Load item rows from that sale. For each row show product, sold quantity, already returned quantity, remaining quantity and one **Return quantity** field. The UI must communicate the remaining quantity limit before submit.
4. **Return all remaining items** is a convenience action, not a default selection.
5. Keep reason optional (current RBOS behaviour) and make **Refund method** visible before posting.
6. Show one clear **Post customer return** action; disable it until a valid positive quantity exists and the original bill lines have loaded.
7. Display the posted result with return total, balance credit and cash refund using the server response.

**Separate whole-sale cancellation**
- **Cancel entire sale** remains visually separate from partial return.
- Require the existing confirmation dialog and explain that stock and books will be reversed.
- Do not make cancellation look like a normal return option or use the same primary action.

**Return history**
- Collapsed initially on phone. On expansion show search, refresh, a compact return list and **View return** detail.
- Desktop may use a table; phone uses return cards.
- Detail shows the original bill reference, customer/date, returned lines, reason, total, balance credit and cash refund.

**Business boundary:** returned quantity remains tied to original sale-line IDs and the server's remaining quantity. Refund method and posted accounting remain server-authoritative. Shopify POS demonstrates partial/full returns, refund-method selection and inventory-restock choices; RBOS should not add a new staff-controlled restock option unless the RBOS backend supports and authorises it. Exchanges are not a new flow in this scope: create the replacement sale as a separate bill, as the current RBOS message instructs.

---

## 5. Shared interaction, accessibility and responsive rules

- **One primary action per task surface.** Supporting actions are visually quieter and do not compete with the user's next step.
- **Progressive disclosure:** hide low-frequency fields such as address, item identifiers/HSN and payment metadata behind clearly labelled summaries. Do not stack nested disclosures or bury required task fields. Keep the maximum disclosure depth shallow.
- **Searchable selection:** combobox has a programmatic label, expanded state, controlled listbox, active descendant and predictable keyboard behaviour. Arrow keys move through suggestions, Enter chooses, Escape dismisses, Tab moves on; focus remains on the input while the active option is announced.
- **Validation:** explain how to fix the issue in plain language; show the error beside its field and make it programmatically associated. Preserve entered values. Submission failures must be announced and focus the relevant field/summary. Never rely on colour alone.
- **Touch and focus:** retain RBOS's 44px minimum target convention where feasible; provide clear focus-visible indication and enough spacing to avoid adjacent accidental taps.
- **Responsive behaviour:** no horizontal page overflow, clipped labels, forced second-line action wraps or fixed controls covering inputs. Tables become structured cards on phone.
- **Sticky UI:** the sticky total/review bar belongs to Create Invoice because that is the MagicPath design and the bill total is needed throughout item entry. Other pages keep their primary action in the task flow.
- **Android viewport:** respect safe-area insets; ensure the visual viewport can resize when the keyboard opens. Do not clear input focus on viewport resize. Native camera/gallery permission, printer/share sheet and TalkBack must be checked on a real Android device; browser automation does not prove them.
- **Assistant:** launcher does not float over page controls. Its panel overlays content only after an explicit open action, has a clear close action and does not permanently occupy the sales task canvas.
- **No unrelated modules:** don't alter purchase, stock, customer master or finance workflows in this change.

---

## 6. Business rules and non-goals

No backend/API/schema/migration, pricing or accounting change is intended. Preserve:

- Tenant isolation, authorisation and location scoping.
- Customer name and mobile are mandatory for invoice generation.
- A bill may be created when stock is zero; manual/ad-hoc invoice lines remain possible.
- Existing catalogue/manual entry, stock policy, server-calculated totals and GST.
- Duplicate serial validation across invoice lines and previous sales. Serial, IMEI and barcode remain optional and warnings do not block generation.
- Invoice extraction and tag-photo capture continue to populate the existing invoice editor; the user reviews extracted values before adding lines.
- Collection amount, single-bill allocation or on-account behaviour, payment modes, balance updates and idempotency.
- Return quantities tied to original sale lines, refund allocation, stock restoration and full-sale cancellation reversal.
- WhatsApp consent, opt-out, retry eligibility and possible-duplicate protection.
- Held bills, offline pending/sync semantics and invoice-only print output.
- Existing routes and no cross-tenant data leakage.

**Explicit non-goals:** new multi-invoice payment split; integrated exchange cart; new return-restock selector; backend search endpoint changes; new sales dashboard metrics; redesign of non-Billing & Sales modules.

---

## 7. Implementation acceptance and proportionate validation

### Focused frontend checks
- Create Invoice: customer-required validation, ad-hoc product/manual model entry, add/remove item, tag capture field transfer, handwritten bill extraction/review, bill discount/GST consistency, review disabled/enabled, hold/offline state and invoice-only print behaviour.
- Sales History: search, unpaid filter, result loading/empty/error/retry, bill detail, consent-needed state, guarded WhatsApp retry, paper size and invoice-only print.
- Collections: customer loading/disabled state, searchable customer and bill choices, no customer/zero amount guard, partial payment, on-account payment, idempotency, success balance, statement and receipt history.
- Customer Returns: searchable original bill, loading/empty returnable lines, return quantity boundaries, partial return success, refund summary, separate full-sale cancel confirmation and collapsed history.
- Sell landing and assistant: navigation, no duplicate titles/back links, portal panel open/close and no persistent overlap.

### Validation approach
- Run current PR CI and the targeted tests above.
- Use only a small smoke journey for touched routes when needed. The user explicitly prefers to provide Android screenshots and has asked not to spend excessive time on a large Playwright screenshot matrix.
- Inspect the user's real-device screenshots against the exact MagicPath New Sale screens and the other-page acceptance rules in §4.
- Validate keyboard, camera/gallery, printing and TalkBack on an Android device where available.
- A green CI run is automated test evidence, **not** proof of visual fidelity. A code implementation is not automatically visual acceptance. User screenshot review is still required.
- Do not merge or deploy without the required explicit authorisation.

---

## 8. Free public research sources and how they informed the design

All links below are publicly accessible; they were used as evidence for workflow patterns, not as a mandate to copy another product's UI.

1. **Shopify POS — Searching and filtering orders**  
   https://help.shopify.com/en/manual/sell-in-person/shopify-pos/order-management/search-orders  
   Describes order lookup by order ID, item name, receipt number and customer name, plus filtering. It supports the decision to make bill/receipt/customer identifiers prominent in Sales History. RBOS's visible search remains limited to the fields its current API supports.

2. **Square — View and search transactions**  
   https://squareup.com/help/us/en/article/5145-transaction-search  
   Describes transaction history lookup by customer information, receipt ID, item/ticket name and card references. It reinforces the need for recognisable search keys and concise transaction summaries.

3. **Zoho Books India — Basic functions in Payments Received**  
   https://www.zoho.com/in/books/help/payments-received/basic-functions.html  
   Explains recording customer receipts, applying them to outstanding invoices, split allocations and unused/excess amounts. RBOS uses the simpler existing single-bill-or-on-account model; the source helps name the distinction without expanding the backend scope.

4. **Zoho Books India — Record payment for an invoice**  
   https://www.zoho.com/in/books/help/invoice/record-payment-for-invoice.html  
   Covers recording partial offline payments and entering amount received/payment date. It supports preserving partial payment and showing the updated outstanding amount clearly.

5. **Shopify POS — Processing returns and refunds**  
   https://help.shopify.com/en/manual/sell-in-person/shopify-pos/order-management/complete-refund-orders  
   Covers partial/full refunds, returned items, refund method, optional partial refund amounts and restocking. It informed the order of original sale → return lines → refund method → confirmation, while RBOS's server rules remain authoritative.

6. **W3C WAI-ARIA APG — Combobox Pattern**  
   https://www.w3.org/WAI/ARIA/apg/patterns/combobox/  
   Defines the labelled input and popup relationship, listbox semantics, expanded state and keyboard/focus behaviour. It informs the reusable customer/original-bill searchable picker.

7. **GOV.UK Design System — Error summary and error message**  
   https://design-system.service.gov.uk/components/error-summary/  
   https://design-system.service.gov.uk/components/error-message/  
   Recommends an error summary alongside field-level messages, clear correction guidance and appropriate focus. It informs required-field validation across invoice, payment and return forms.

8. **Nielsen Norman Group — Progressive Disclosure**  
   https://www.nngroup.com/articles/progressive-disclosure/  
   Explains deferring advanced/rare controls to secondary disclosure to reduce initial complexity. It supports collapsing optional address, item metadata and advanced bill details without hiding the information needed for the core sale.

9. **Android Developers — Understand window insets in WebView**  
   https://developer.android.com/develop/ui/views/layout/webapps/understand-window-insets  
   Explains safe-area inset propagation and visual viewport resizing with the software keyboard. It informs the invoice sticky bar's safe-area treatment and the need for real-device keyboard checks.

10. **W3C — Web Content Accessibility Guidelines (WCAG) 2.2**  
    https://www.w3.org/TR/WCAG22/  
    Used as the accessibility baseline for visible focus, usable target size, keyboard interaction and non-colour-only feedback. RBOS retains its practical 44px touch-target convention.

---

## 9. Current implementation checkpoint

- PR #76 is the active draft; it is not merged or deployed.
- The user-supplied MagicPath screenshots are now available and are the exact New Sale reference. The Create Invoice component has been restructured to implement the specified hierarchy and sticky summary/footer while retaining existing handlers/calculations.
- Other routes remain research-led and page-specific; they are not claimed to be MagicPath replicas.
- The current CI failures were traced to stale portal DOM queries, a non-controlled SearchableSelect test harness and a Collections test typing before its async customer list was enabled. Fixes are included on the branch; the resulting current-head CI is the next evidence checkpoint.
- Visual acceptance requires the user's next Android screenshots. Native camera/gallery, keyboard, print and TalkBack checks remain separate device acceptance.
- Merge and deployment are separate decisions and are not authorised by this document.
