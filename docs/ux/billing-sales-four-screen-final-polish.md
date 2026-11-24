# Billing & Sales — Four-screen final polish specification

**Date:** 2026-11-24  
**Scope:** Final screenshot-led polish of the four existing Sales screens  
**Reference implementation:** Home (PRs #69/#70/#73) and Generate Invoice (PR #74)  
**Documentation lineage:** PR #67, `docs/rbos-usability-accessibility-programme`  
**Implementation baseline:** `main`, after PR #74 merge commit `c4d71190b69a581dae389d90bcd1e4e9e58f3f00`

## 1. Scope and verified routes

| Screen | Route | Current implementation |
|---|---|---|
| Billing / Sales hub | `#/sell/menu` | `SellPage` in `web/src/Pages.tsx` |
| Sales history | `#/sell/bills` | `web/src/SalesHistory.tsx` |
| Collect payment | `#/sell/collect` | `web/src/Collections.tsx` |
| Customer returns | `#/sell/return` | `web/src/CustomerReturns.tsx` |

Generate Invoice (`#/sell/bill`) and Home are references only. Do not modify their behaviour or visual design in this scope. Preserve the shared app shell, navigation, Assistant functionality, APIs, route semantics, accounting rules, payment allocation, return valuation, idempotency, WhatsApp consent/retry rules and print isolation.

## 2. Design direction

Use the existing RBOS visual language: pale neutral page background, white cards, green brand emphasis, dark readable headings, muted helper text, modest borders/radii, restrained shadows, and comfortable mobile spacing. Match the latest Generate Invoice quality without copying its multi-section form layout onto unrelated tasks.

**Decision:** keep all four current information architectures. Apply only the visual hierarchy, density, spacing and state-clarity corrections supported by the supplied Android screenshots. No new features, filters, API calls, metrics, data fields or workflows.

### 2.1 Billing / Sales hub — retain, lightly refine

- Keep the page title and short instruction.
- Keep **Create new bill** as the single full-width primary action.
- Keep Sales history, Collect payment and Customer returns as three secondary action cards in that order.
- Keep plain-language supporting descriptions; they are more useful than icons alone.
- Maintain the existing single-link-per-card semantics and destinations.
- Polish only: align icon containers, title/copy baselines and arrow placement; keep consistent card heights and gaps; avoid excessive empty space on phone; stack secondary cards when narrow labels wrap.
- Do not add counts, balances, recent activity or more destinations.

### 2.2 Sales history — flatten the card hierarchy

**Screenshot finding:** the page reads as a card inside a large page card, followed by a tinted results panel containing another card. The “2 bills” count is styled as a large banner. The floating Assistant bubble visually overlaps the lower bill card in the supplied capture.

**Final layout:**
1. Page heading and one short explanatory line.
2. One compact search/filter surface: labelled search, clear action only when query is non-empty, and “Unpaid only” immediately below/alongside depending on width.
3. Results count as small inline metadata (for example, “2 bills”), not a full-width status banner.
4. Sale cards directly on the page surface; remove the extra tinted wrapper around the cards and avoid nested card-on-card treatment.
5. Each sale card: invoice number/date first; customer name second; Total / Paid / Balance as a clear three-value group; payment mode and WhatsApp status as quiet secondary metadata; one full-width **View bill** action on phones.
6. Keep the wider-screen semantic table where it remains readable.

**Assistant overlap:** ensure the fixed Assistant control does not obscure a bill's balance or View bill action at the bottom of the viewport. Prefer a screen-scoped safe content inset/scroll-padding adjustment if that is sufficient; do not redesign or relocate the global Assistant component as part of this scope.

Preserve debounced server search, unpaid client filter (`balance > 0`), sorting/pagination, distinct empty states, retry/query retention, sale detail dialog focus handling, WhatsApp retry eligibility and duplicate-risk confirmation, A4/80 mm print selection and invoice-only print output.

### 2.3 Collect payment — keep the guided entry and compact history

**Screenshot finding:** the initial state is simple and understandable, but the customer picker card and history card feel oversized relative to their content. The large helper paragraph repeats the page instruction. The history heading, description and Refresh action compete for a full row even when there are no records.

**Final layout:**
1. Page heading and one-line instruction.
2. A compact customer-selection card with visible label and full-width search control.
3. Before selection, show one short instruction: “Choose a customer to see their outstanding balance and record a payment.” Avoid repeating the page description.
4. After selection, reveal a prominent outstanding-balance summary directly beside/below the customer identity, followed by the payment form in task order: amount, optional bill allocation, payment method, date and optional reference.
5. Keep “Fill full balance” as a quiet shortcut adjacent to the amount field; it fills only and never submits.
6. One dominant **Record payment** action. Keep statement as a secondary, on-demand view rather than expanding it inline by default.
7. Keep collection history below the form as a separate, visually lighter section. Use a compact heading/count row and a restrained refresh control. On an empty history, show a short empty-state sentence without a large blank card interior.
8. On phones, history receipts are compact stacked cards; no horizontally scrolling six-column table.

Preserve Cash / UPI / Bank, server-provided outstanding and statement values, optional bill allocation/on-account semantics, idempotency key handling, safe retry, receipt route and print behaviour. Do not change payment or ledger rules.

### 2.4 Customer returns — make the two initial choices clearer

**Screenshot finding:** the initial screen is already clean. The original-bill search card is clear; the history card's “Show history” action is visually detached from its explanatory text and the page has substantial unused vertical space when no bill is selected.

**Final layout:**
1. Page heading and concise instruction.
2. One primary **Choose the original bill** card with the existing searchable bill/customer input.
3. A compact secondary **Customer return history** row with a clearly associated “Show history” disclosure action. Keep history collapsed by default on mobile; when expanded, show search, loading/empty/error and history results within the same section.
4. After a bill is selected, show a compact original-bill summary, then the bill's returnable item lines. Each line keeps product name, sold/returned/remaining quantities together with its quantity input and field-level error.
5. Keep “Return all remaining items” as a fill-only shortcut, separate from the primary submit action.
6. Keep reason optional and Cash / Bank / UPI refund method options.
7. Keep **Cancel entire sale** in a visually separate, clearly destructive secondary section. Require the existing explicit confirmation that names the sale and explains stock/book reversal.
8. After posting, show returned total, customer-balance credit and cash refund as distinct values.

Preserve sale-line IDs, remaining-quantity limits, server authority, refund allocation, inventory valuation/restock, audit trail, idempotency, cancellation semantics and existing endpoints. Do not introduce manual returns, exchanges or supplier returns.

## 3. Shared responsive and accessibility contract

- Phone-first at 360px, 390px and 412px widths; tablet at 768px; landscape at 844×390; desktop at 1280px.
- No horizontal scrolling, clipped values, overlapping controls or fixed-overlay obstruction.
- Keep primary actions obvious and full-width on phone where appropriate.
- Maintain at least 44×44 CSS px for important touch actions (a usability target stricter than WCAG 2.2 AA's 24×24 minimum); maintain visible keyboard focus and logical focus order.
- Use semantic headings, labels, buttons/links, status announcements and associated field errors. Do not rely on colour or emoji alone.
- Preserve English/Hindi wrapping; no truncation of invoice numbers, customer names or action labels.
- Respect reduced motion. Dialogs must have an accessible name, keyboard dismissal, focus containment and focus restoration.
- Loading, error/retry, empty, no-results, success, disabled and confirmation states remain understandable and recoverable.

## 4. Screenshot-led findings and priority

| ID | Priority | Screen | Finding | Decision |
|---|---|---|---|---|
| POL-01 | P1 | Sales history | Nested card/panel treatment makes the result list visually heavy | Flatten to one result surface and direct sale cards |
| POL-02 | P1 | Sales history | Assistant bubble overlaps bill content in the supplied viewport | Add scoped safe space/scroll padding; preserve global Assistant |
| POL-03 | P2 | Sales history | Result count is a large full-width banner | Replace with compact inline count |
| POL-04 | P2 | Collections | Initial customer picker and empty history use excess card space | Compact vertical rhythm and empty history state |
| POL-05 | P2 | Collections | Repeated helper text adds little after page heading | Keep one concise instruction |
| POL-06 | P2 | Customer returns | History action feels detached from its section | Associate it with a compact disclosure row |
| POL-07 | P2 | Customer returns | Empty initial view has a large dead zone | Keep content-height sections; do not add filler |
| POL-08 | P2 | Sell hub | Existing cards are already clear and action-first | Retain; only align spacing and responsive wrapping |

## 5. Research and pattern fit

- **Vyapar** separates Sale Invoices, Payment-In and Sale Return as distinct sales tasks, and exposes customer/party outstanding and statements. This supports RBOS's four clearly named destinations rather than combining collection and returns into a generic transaction screen. Source: [Vyapar feature guide](https://vyaparapp.in/blog/vyapar-app-features/).
- **myBillBook** describes a linked receivables lifecycle: credit sale, outstanding tracking, follow-up, part-payment and balance update. It also distinguishes payment reminders from recording an actual Payment-In. RBOS should therefore keep “Collect payment” as a deliberate accounting action and show the updated outstanding after posting; reminders must never silently alter balances. Sources: [Receivables workflow](https://mybillbook.in/s/accounts-receivable-software/), [Payment reminders](https://mybillbook.in/s/features/payment-reminders/).
- **Nielsen Norman Group** recommends making mobile search/filter, lists and navigation understandable and keeping advanced options progressively disclosed. This supports compact search/filter controls, scannable sale cards and an on-demand statement/history view rather than expanding every secondary detail by default. Sources: [Mobile usability report](https://www.nngroup.com/reports/mobile-website-and-application-usability/), [Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/).
- **W3C WCAG 2.2** requires at least 24×24 CSS px targets or qualifying spacing under AA; RBOS keeps the stricter 44×44 px target as a practical touch-usability standard for shop-counter use. Preserve focus visibility, labels, contrast and status announcements. Sources: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), [Understanding Target Size Minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum).

These are pattern references, not instructions to copy competitor screens. RBOS's current APIs, local retail tasks and established component system take precedence.

## 6. Acceptance criteria

- [ ] The Sell hub remains a one-tap action hub with one primary and three secondary destinations.
- [ ] Sales history has no unnecessary nested result panel/card treatment; result count is compact.
- [ ] Mobile sale cards expose invoice number/date/customer/total/paid/balance/payment mode/WhatsApp status and one clear View bill action.
- [ ] Assistant never obscures the last visible bill action or balance.
- [ ] Search, clear, unpaid filter, pagination/sort, empty states, retry and bill detail continue to work.
- [ ] A4/80 mm print remains invoice-only; WhatsApp retry/duplicate confirmation is unchanged.
- [ ] Collect payment initial state is compact; after customer selection, outstanding and payment details appear in a clear sequence.
- [ ] Full-balance shortcut fills only; payment method/date/reference and optional bill allocation remain supported.
- [ ] Statement is on demand; collection history has compact loading/empty/error/retry and mobile receipt cards.
- [ ] Customer returns retains original-bill search and clearly associated collapsed history disclosure.
- [ ] Returnable quantities, field errors, fill remaining, refund outcomes and cancellation confirmation remain correct.
- [ ] No route, API, accounting, stock, ledger, consent, idempotency or tenant/security behaviour changes.
- [ ] English/Hindi, keyboard/focus, contrast and touch-target requirements are met.

## 7. Implementation and verification plan

1. Implement the small screenshot-led polish on one canonical branch from current `main`; do not create a competing branch/PR if an existing active implementation branch already covers the scope.
2. Keep changes route-scoped; avoid changing shared shell or global components unless a proven overlay defect cannot be resolved locally.
3. Run focused tests for changed components and relevant existing route tests after each screen.
4. After all four screens are complete, run the integrated Billing & Sales Playwright journey, responsive screenshots and Axe WCAG 2 A/AA; inspect the actual screenshot/contact sheet.
5. User Android screenshots remain the visual acceptance evidence. Native Android, printer/share sheet, TalkBack and shopkeeper acceptance are separate checks.
6. Do not merge or deploy without explicit approval.

## 8. Current evidence boundary

The supplied Android screenshots were reviewed for all four screens. The latest main-branch source was inspected for `Pages.tsx`, `SalesHistory.tsx`, `Collections.tsx` and `CustomerReturns.tsx`, with corresponding CSS. PR #74 is merged to main. This specification is a final polish contract, not proof that the polish has been implemented or visually accepted.
