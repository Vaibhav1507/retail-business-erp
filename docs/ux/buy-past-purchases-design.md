# Buy screen design — Past Purchases

**Status:** Design specification finalized; implementation pending.  
**Last reviewed:** 2026-11-24  
**Baseline:** main b180f59793f265024650731a1f2bd89ead29b342  
**Route:** #/buy/history  
**Active implementation:** web/src/Pages.tsx → Procurement with show=history.

## 1. User task and outcome

Help a shopkeeper quickly answer:
- When did I buy from this supplier?
- Which supplier invoice/reference was recorded?
- How much was the purchase?
- Is the original supplier bill attached?
- What payments have I made to suppliers?
- What is the current supplier statement/balance?

The primary task is finding and understanding an earlier purchase, not entering a new transaction or reviewing a dense ledger table.

## 2. Dependencies

- UI/state: web/src/Procurement.tsx.
- Receipt history: GET /purchases.
- Supplier payments: GET /supplier-payments.
- Supplier list and balances: GET /suppliers.
- Supplier statement: GET /suppliers/{id}/statement.
- Attached bill: GET /purchases/{id}/invoice.
- Existing paging utility: web/src/Paged.tsx.
- Shared styles and translations: web/src/styles.css, web/src/billing-forms.css and web/src/i18n.ts.

No new endpoint, date filter, reporting feature or supplier-management route is needed.

## 3. Existing workflow and findings

Current implementation loads purchase receipts, supplier payments and suppliers. Purchase records are rendered in a table transformed into mobile key/value blocks by global CSS. Each record contains date, supplier, supplier invoice reference, total and an attached bill link when one exists.

Current problems visible in the screenshots:
- Cards/rows are tall and repetitive; a shopkeeper has to scan several repeated labels.
- The fixed assistant overlaps the lower-right part of the purchase history.
- “Show supplier statement” is a large primary button below the receipt list and is not associated with an explicitly selected supplier.
- Supplier statement currently uses the implicitly selected first active supplier.
- Payment history appears below the statement action and is visually mixed into the same long page.
- There is no explicit loading or empty state in the current view.
- History is bounded by the existing API to the latest 100 receipts and latest 100 payments; no additional filter should be implied.

## 4. Selected design direction

Make the purchase history itself the default and most prominent content. Use compact purchase cards that expose supplier, date, invoice reference and total at a glance. A bill-copy action appears only when the API confirms an attachment. Keep payment history separate and collapsed until requested. Make supplier statement a secondary, supplier-specific disclosure.

Do not force transaction data into a narrow table and do not add date/status/search filters that the API does not currently support.

## 5. Layout and information hierarchy

### Purchase history

- Heading: “Past purchases”.
- One short helper line: “Find an earlier supplier purchase or open its bill.”
- Render a compact list of cards in existing API order (newest first by created_at).
- Each card displays:
  1. Supplier name as the strongest text.
  2. Date and supplier invoice/reference number.
  3. Purchase total, formatted consistently in INR and visually easy to locate.
  4. Bill-copy action when hasInvoice is true.
- If no bill is attached, show a muted “No bill copy attached” status or omit the action; never show a broken View link.
- Do not invent a purchase receipt number in the list; GET /purchases currently returns supplierRef, not receipt number. The detail API has the internal receipt number if the return workflow needs it.
- Keep the card height low by using a two-column summary on phones only when content remains readable. Long supplier names and references wrap rather than clip.

### Payments made to suppliers

- Separate section below purchase history with a collapsed summary such as “Payments made to suppliers (N)”.
- When expanded, show compact payment cards with date, supplier, method, amount and reference when present.
- Keep payment records visually distinct from purchase receipts. Do not present a payment as a purchase or infer bill allocation from its existence.
- Use only existing payment fields; no missing payment number or purchase reference is fabricated.

### Supplier statement

- Place statement access in a separate secondary disclosure below the main history.
- Require an explicit supplier choice before loading the statement. Use the existing SearchableSelect.
- Display supplier name and outstanding balance at the top of the statement panel.
- Render ledger entries as compact rows/cards: type (Purchase, Payment or Return), date, debit, credit and running balance.
- Provide a close/hide control and clear loading/error/empty state.
- This does not filter purchase history or add a new report. It exposes the existing GET /suppliers/{id}/statement result.

## 6. Components and styling

- Use the current Buy page heading and four-route navigation.
- The outer page should be a simple content surface, not another oversized nested dashboard card.
- Add one compact component/class for purchase cards and a separate one for payment history cards.
- Use existing theme tokens and scoped Buy styles.
- Use standard text links/buttons for invoice viewing; maintain a minimum 44px action target.
- The current Paged utility is table-oriented for sort/page manipulation. Do not wrap card content in Paged expecting its table pagination behaviour. The history APIs already return at most 100 records; render the returned records in source order.
- Avoid unnecessary nested card borders and shadows.

## 7. Primary and secondary actions

| Action | Role | Behaviour |
|---|---|---|
| View bill copy | Contextual secondary | Opens existing attached invoice endpoint only when hasInvoice is true |
| Show payment history | Secondary disclosure | Expands/collapses existing payment list |
| Choose supplier | Secondary statement setup | Selects supplier for statement lookup |
| View supplier statement | Secondary | Loads the selected supplier's statement |
| Close statement | Secondary | Hides statement panel |

The screen's primary outcome is not a green “Show supplier statement” button. Finding a purchase should remain the strongest interaction.

## 8. Interactions and navigation

- Initial load requests existing receipt/payment/supplier lists and exposes independent errors where possible.
- Purchase card order remains server order.
- Bill-copy link is displayed only when hasInvoice is true.
- Payment history expands without changing the receipt list.
- Selecting a different supplier clears any prior statement before loading the new one.
- Statement request failure does not erase purchase or payment history.
- Refresh/retry reloads only the failed/current history data. Do not silently navigate away from this route.

## 9. States

| State | Required behaviour |
|---|---|
| Initial loading | Visible status while receipt records load |
| Receipts available | Compact purchase cards |
| No purchases | “No purchases recorded yet” with a clear route to Add Purchase |
| Receipts error | Text error and retry action; keep already loaded records |
| Payment history collapsed | Only the summary/count is visible |
| No payments | When opened, say no supplier payments have been recorded |
| Payment history error | Explain separately; receipt history remains usable |
| No attached bill | No View action; clearly explain there is no attached copy |
| Statement supplier missing | Ask shopkeeper to choose a supplier |
| Statement loading | Visible status |
| Statement error | Text error and retry |
| Statement empty | Explain that no statement entries exist |
| Statement loaded | Supplier identity, outstanding and ledger entries visible |

## 10. Responsive behaviour

- Phone: one compact card per purchase; supplier, date, reference and total are visible without horizontal scrolling. Bill-copy action is a clear full-width/44px minimum target where appropriate.
- Tablet: card list may use a restrained two-column grid only if supplier and total remain easy to scan.
- Landscape: keep a single-column history list to avoid making cards too narrow.
- Desktop: use a bounded list with a compact information row. A table is not required for desktop consistency.
- Payment history and statement must reflow without horizontal overflow.
- Keep sufficient bottom content space for existing bottom nav and Assistant launcher.

## 11. Accessibility and language

- Each purchase/payment card has a meaningful heading or accessible label.
- The bill-copy link has an accessible name that includes context, for example “View supplier bill for Friends Electronics, invoice FE-9263”.
- Expand/collapse controls use native details/summary or equivalent accessible button state.
- Statement ledger entries retain semantic labels; do not rely only on colour for debit/credit.
- Use role=status for loading and role=alert for failures.
- Ensure visible focus, keyboard operation and 44px touch targets.
- New labels and empty/error messages have English and Hindi translations.

## 12. Business/API invariants

- GET /purchases returns the latest 100 receipts with id, date, supplier, supplierRef, total and hasInvoice.
- GET /supplier-payments returns the latest 100 payment records with date, supplier, amount, method and reference.
- GET /suppliers/{id}/statement returns supplier, outstanding and purchase/payment/return ledger entries with debit, credit and running balance.
- Invoice viewing uses GET /purchases/{id}/invoice only when the receipt has an attached file.
- Do not add filters, exports, payment edits, supplier management, account reconciliation or bill-allocation changes on this screen.
- Do not infer that a payment has been allocated to a specific receipt from amount/date proximity.

## 13. Acceptance criteria

- A purchase card makes supplier, date, invoice reference and total easy to find.
- Attached bill View appears only when a file exists.
- Purchase receipts and supplier payments are visually distinct and independently understandable.
- Supplier payment history is secondary and can be expanded/collapsed.
- Statement access requires an explicit supplier selection and displays that supplier's statement.
- Empty/loading/error states are informative and do not leave blank boxes.
- No unsupported filters or new reporting features are introduced.
- Phone, tablet, landscape and desktop layouts avoid overflow and remain readable in English and Hindi.

## 14. Implementation and verification

Files: web/src/Procurement.tsx, Buy stylesheet and web/src/i18n.ts. Reuse SearchableSelect for statement supplier and existing invoice endpoint. Add focused component coverage for receipt cards, missing/available bill copy, payments disclosure, supplier-specific statement, empty/error/loading states. Run build, lint and unit tests. Do not run Playwright for this scope; updated Android screenshots will be supplied by the user.

## 15. References

- [myBillBook: Purchase invoice](https://mybillbook.in/s/features/purchase-invoice/)
- [myBillBook: Daily purchases](https://knowledge.mybillbook.in/articles/8743420-how-to-record-daily-purchases-seamlessly-on-mybillbook)
- [Zoho Inventory: vendor transactions and statements](https://www.zoho.com/in/inventory/kb/bill/transactions.html)
- [Nielsen Norman Group: Mobile usability research](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- [Nielsen Norman Group: Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
