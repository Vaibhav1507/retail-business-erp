# Billing & Sales — mobile polish and screenshot review

**Status:** Implementation candidate on `ux/billing-sales-mobile-polish`; screenshot acceptance pending  
**Base:** `main` after PR #74 merge (`c4d71190b69a581dae389d90bcd1e4e9e58f3f00`)  
**Visual reference:** [MagicPath — Invoice Entry / action-first direction](https://designs.magicpath.ai/v1/cozily-dusk-6555)  
**Scope:** `#/sell/menu`, `#/sell/bill`, `#/sell/bills`, `#/sell/collect`, `#/sell/return` and the shared Navigation Assistant launcher.

## 1. Goal

Polish the merged Billing & Sales implementation against the existing MagicPath direction and the shopkeeper's Android screenshots. This is a fidelity and usability correction, not a second redesign. Keep RBOS colours, typography, controls and the established action-first visual language. Keep existing routes and business behaviour.

The shopkeeper will provide the next real-device screenshots for visual review. Do not spend time recreating a large screenshot matrix when the user can supply direct device evidence.

## 2. Screenshot findings and implementation decisions

| Finding | Decision / implementation |
|---|---|
| Floating Assistant launcher overlaps Refresh, sales values and other page content | Move the launcher into the top bar as a compact 44px icon control. Keep the assistant panel available on deliberate activation; it is the panel, not a permanent launcher, that may overlay the page while open. |
| Subroutes repeat “Billing / Sales” and “Choose what you want to do” above their own title | Render the section title/hint only on the Sell landing. On each subroute render one Sell back link and the route's own page heading. |
| Outer page cards contain additional form/history cards, producing excessive borders and shadows | Keep the route shell transparent; use the form, history and statement cards as the meaningful surfaces. Do not remove the separation between destructive full-sale cancellation and partial return. |
| Refresh buttons occupy a full row on mobile | Scope compact width to Refresh controls in history headings. Keep at least 44px height and retain readable headings. Do not weaken the global mobile button rule. |
| Sell landing leaves one secondary card alone on a second grid row | Use three compact full-width secondary action rows on phones, each with task name, short description and forward cue. Retain a balanced three-column secondary layout on wider screens. |
| Collections initially exposes a long form before a customer is selected | Start with customer search and a short instruction. After selection show outstanding balance, amount, optional bill allocation, payment method, date, optional reference and the payment/statement actions. |
| Customer and bill pickers are difficult to use with long lists | Use one reusable searchable combobox for customer name/mobile and original bill number/customer/date. Keep keyboard navigation, clear, Escape, accessible listbox semantics and readable secondary option details. The first ArrowDown must highlight the first result; moving past either end must not wrap unexpectedly. |
| WhatsApp status “Permission not confirmed” is unclear | Use “Customer consent needed” and explain that customer permission must be requested before sending the bill. Keep retry/duplicate protections unchanged. |
| Product field placeholder clips beside Scan tag | Use the shorter “Search product or enter model”. Keep Scan tag in the product-entry row and retain the existing source chooser. |
| Address fields dominate Create Invoice despite being optional | Collapse address, pincode, city and state in “Add address (optional)”; show “Edit address (optional)” when data exists. Preserve values and save behaviour. |
| Return history adds a long block below the return form | Collapse customer return history initially; reveal search, refresh, list and details when explicitly opened. Keep the return form first. |

## 3. Screen-level acceptance criteria

### Sell landing — `#/sell/menu`
- Create new bill remains the single dominant action.
- Sales history, Collect payment and Customer returns appear as three clear, balanced, full-width rows on phone.
- Each destination remains a single semantic link with decorative icon and clear label/description.
- Wider screens retain the established three-card layout.

### Create Invoice — `#/sell/bill`
- The invoice workspace keeps its focused mode and does not show persistent bottom navigation or the Assistant launcher.
- Customer mobile/name remain mandatory; address fields are optional and collapsed by default.
- Product description/model input and Scan tag stay together; the placeholder does not truncate on phone.
- Add product remains the one primary action. Optional identifiers remain progressively disclosed.
- Handwritten-bill import, tag capture, review, posting, held/offline and print behaviour remain unchanged.

### Sales History — `#/sell/bills`
- One page heading, one back link, clear search/unpaid filter, result count, empty/error/retry states and readable phone sale cards.
- Customer consent has clear wording; its explanation appears in bill details.
- Historical bill detail, WhatsApp eligibility and duplicate-send guard, A4/80mm print and invoice-only print output remain unchanged.

### Collections — `#/sell/collect`
- Initial state shows customer search and instruction, not the dependent payment fields.
- Selecting a customer reveals the outstanding balance and payment form.
- Searchable customer and bill pickers work with mouse, touch and keyboard.
- “On account” remains the default when no bill is allocated or no unpaid bills exist. Bill allocation remains optional.
- Record payment remains disabled until a customer and positive amount exist. Amount/date/method/reference semantics and idempotency remain unchanged.
- Statement opens only on request; collection history remains separate, with compact Refresh and receipt actions.

### Customer Returns — `#/sell/return`
- One page heading; searchable original-bill picker by bill number/customer/date.
- Return quantity is still constrained by the server's remaining quantity and submitted by original sale-line ID.
- Partial return remains distinct from separately confirmed full-sale cancellation.
- Return history starts collapsed and can be searched/refreshed/opened without competing with the entry flow.
- Detail and cancellation dialogs retain keyboard focus management and existing accounting/inventory behaviour.

### Shared shell
- Assistant launcher is in the top bar and never floats over a page control.
- The assistant panel opens intentionally, remains keyboard accessible and does not cover the persistent navigation.
- Existing main navigation remains fixed on phone and becomes the side rail on wide screens.
- No global changes to unrelated modules.

## 4. Invariants and non-goals

No backend/API/schema/migration or accounting changes are intended. Preserve:
- Tenant and authorization boundaries.
- Customer identity and invoice validation.
- Catalogue/manual sale-line behaviour, stock policy and server-calculated totals/GST.
- Collection allocation, on-account semantics, customer balances and idempotency.
- Return quantities tied to sale lines, refund accounting, stock restoration and cancellation reversal.
- WhatsApp consent, opt-out, retry and duplicate-send safeguards.
- Offline pending semantics and invoice-only print output.
- Existing navigation routes and profile/business settings.

## 5. Validation plan for this polish pass

The user has explicitly requested a proportionate validation pass and will provide real-device screenshots. Therefore:
- Run the normal PR CI and targeted frontend tests, including the searchable selector, Collections, Returns, Create Invoice, Sales History and Sell landing tests.
- Run only a small Playwright smoke journey for the affected routes and one responsive phone profile if the CI workflow requires browser coverage. Do not repeat the 178-screenshot baseline or run another exhaustive screenshot/viewport matrix for this polish pass.
- Inspect the user-provided Android screenshots against this note and the MagicPath visual reference. Record each new observation and fix it in the same polish scope.
- Native camera/gallery permission, Android keyboard/viewport behaviour, printer/share sheet and TalkBack remain device checks; browser tests do not prove them.

A green CI result is code/test evidence, not visual acceptance. Visual acceptance is pending the shopkeeper's next screenshots. Merge and production deployment are separate; this branch is not deployed.

## 6. Current implementation checkpoint

The branch already includes the screenshot-driven implementation for the items above. A focused follow-up fixes keyboard navigation so ArrowDown starts at the first matching result instead of skipping it, with a regression test. Remaining acceptance is:
1. Current-head PR CI passes.
2. Review the user's next phone screenshots and address any concrete remaining mismatch.
3. User acceptance, then merge when authorised.
4. Production deployment only after separate explicit approval.
