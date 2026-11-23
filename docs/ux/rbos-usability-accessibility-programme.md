# RBOS Usability & Accessibility Programme

- **Status:** Living programme; initial source-code baseline documented, visual audit pending
- **Repository baseline:** `main` at `4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0` (23 November 2026)
- **Scope:** Screen-by-screen usability, accessibility, responsive-layout and interaction improvements
- **Release boundary:** This document does not authorise application-code changes, production deployment, schema changes or feature expansion.
- **First screen group:** Billing / invoice creation and preview

## 1. Mission

Make RBOS comfortable and dependable for Indian small-shop owners and staff with a wide range of digital confidence, reading ability, age, language and smartphone experience.

A person who mainly uses calls and WhatsApp should be able to complete common shop tasks without needing to understand software terminology, accounting concepts or hidden gestures. RBOS should guide the next step, prevent avoidable mistakes, explain problems in familiar language and make recovery straightforward.

The goal is not to make RBOS look basic. The goal is a professional retail application that is calm, predictable and easy to operate.

### 1.1 Intended users

Use the repository's existing personas as the foundation:

- **Owner:** runs the shop; needs fast billing, stock confidence, money-to-collect/pay visibility and understandable reports.
- **Staff / cashier:** bills customers and receives goods; needs simple, permission-aware workflows and clear recovery.
- **Older or low-digital-confidence operator:** may have limited experience with forms, mobile menus or business apps; needs readable content, explicit actions and guided steps.
- **Accountant / CA:** periodic, often read-only user who needs accurate records, drill-down and exports.

Do not assume every older person has the same needs. Test with people who actually represent the intended usage context. Do not use the label “uneducated” in product copy or research records.

### 1.2 Design principles

1. **One obvious next action.** Give each step one clearly dominant action. Supporting actions must not compete with it.
2. **Plain language first.** Prefer shop language over technical or accounting jargon. Explain unfamiliar terms when they cannot be avoided.
3. **Visible labels.** Do not rely on placeholder-only fields or icon-only actions for important tasks.
4. **Comfortable reading.** Use readable body text, line height, contrast and spacing. Avoid dense microcopy.
5. **Easy tapping.** Design Android touch controls for at least 48dp where practical, with enough separation to avoid accidental taps.
6. **Show only what is needed now.** Keep advanced or infrequent fields available through clear progressive disclosure, not mixed into the primary path.
7. **Predictable behaviour.** Keep navigation, terminology, button placement and feedback consistent.
8. **Prevent errors and make recovery clear.** Preserve user-entered data, explain what needs fixing and show the correction path.
9. **Make state obvious.** Clearly distinguish draft, pending sync, posted, failed, cancelled and completed states.
10. **No essential AI dependency.** Core billing, stock and money workflows must remain usable without AI or hosted services.
11. **Respect business truth.** Never hide uncertainty, invent missing data or silently change a posted transaction.
12. **Keep customer documents professional.** Mobile previews may be optimised for phones; printable A4 and thermal outputs remain separate, explicit regression targets.

These principles extend the existing RBOS product principle: “Plain language on top, ledger underneath.”

## 2. Screen inventory and programme phases

This is a UX work order, not a claim that every listed screen or future capability is already implemented. Before work is scheduled, verify the route, component, permissions, API contract, current feature status and product-scope approval on the active `main` branch.

| UX phase | Screen groups in scope | Why this order |
|---|---|---|
| **Phase 0 — Shared foundations** | App shell; top and bottom navigation; page titles and breadcrumbs; global search; shared buttons; fields and validation; dialogs and bottom sheets; alerts and status messages; loading/empty/error states; responsive spacing and typography; language and help/voice entry points | Shared defects repeat across screens. Fixing foundations first prevents one-off CSS patches and inconsistent patterns. |
| **Phase 1 — Billing and daily selling** | New bill entry; customer lookup and customer details; product search/manual entry; barcode scan; product-tag photo capture; advanced product identifiers; bill/cart lines; totals/discount/tax/payment summary; bill review; confirmation; new invoice preview; invoice share/WhatsApp/PDF/print actions; Sales History; historical invoice detail; unpaid bills/collections; customer list and quick customer detail | Highest-frequency, revenue-critical workflow and the first pilot priority. |
| **Phase 2 — Stock and product catalogue** | Stock overview; product search/list; product details; add/edit product; generic/unbranded product; barcode and tag capture; opening stock; stock additions and movements; adjustments; low-stock/ageing views; stock count where implemented and authorised | Billing depends on trustworthy stock and product data. Simplify the catalogue without weakening inventory controls. |
| **Phase 3 — Purchasing and suppliers** | Supplier list and detail; purchase entry; supplier invoice photo/PDF upload; extraction review and correction; purchase lines; receipt/stock update; supplier payment; purchase history; purchase returns | This workflow supplies catalogue and stock data and needs careful document-review/error handling. |
| **Phase 4 — After-sales and money collection** | Customer dues; receive payment/collection; payment allocation; returns; exchanges where supported; cancellation/reversal where supported; customer purchase history; supplier dues/payments; confirmation and receipt screens | These are sensitive financial interactions where mistakes must be prevented and history must remain clear. |
| **Phase 5 — Owner finance and insights** | Cash/bank overview; expenses; capital/drawings; receivable/payable ageing; stock valuation; P&L; balance sheet; general ledger/trial balance when implemented; reports; GST workspace; CA workspace and exports | Keep shopkeeper language as the default and progressively expose detailed accounting information to authorised users. |
| **Phase 6 — Administration and trust** | Business profile and setup; branches/locations; staff/team access and permissions; devices; app lock; audit history; period lock; security/session states; integration settings; help and onboarding | Less frequent, but high-impact for access, security and ongoing operation. |
| **Phase 7 — Future / separately authorised** | Advanced GST reconciliation, GSTR-2B, offline multi-device, advanced roles, bank reconciliation, purchase orders as separate documents, deeper electronics serial/warranty lifecycle and other deferred scope | UX design must not silently pull deferred features into the current product phase. Follow the approved product scope and decision register. |

### 2.1 Phase rules

- Work one bounded screen or connected mini-flow at a time.
- Start with the highest-frequency task in the phase, not the easiest screen to style.
- Audit the preceding and following step when a screen's usability depends on them.
- Do not redesign screens based only on filenames, screenshots from old branches or assumptions about the live app.
- Keep a screen marked **not audited** until current visual evidence exists.
- Do not treat a mockup or green build as proof of usability.
- Changes to product rules, API contracts, permissions, accounting or data retention require a separate technical/product decision; they are not “CSS fixes”.

## 3. Standard lifecycle for every screen

Follow this sequence consistently. The tools should each have a distinct job; stop using a tool when it does not add measurable value.

| Order | Tool / owner | Responsibility | Required output before handoff |
|---|---|---|---|
| 1 | **ChatGPT — orchestration and source analysis** | Resolve the user task, inspect repository implementation and dependencies, record current behaviour, identify constraints and prioritise risks. | Screen brief, source map, initial findings and acceptance questions. |
| 2 | **Product Design — UX and accessibility audit** | Open the actual app, capture the real flow and states at realistic viewports, inspect screenshots and audit the experience. | Accepted current screenshots; numbered flow; evidence-linked UX and accessibility findings; explicit capture gaps. |
| 3 | **MagicPath — design exploration** | Turn the agreed brief and audit findings into genuinely different layout/interaction alternatives. | Three distinct options for complex screens, comparison and prototype links/previews. Use fewer only when the screen is genuinely simple and the owner agrees. |
| 4 | **Figma — approved design source of truth** | Formalise the selected direction using reusable components, variables, typography, spacing and responsive behaviour. | Approved frame(s), component references, variables/tokens and documented states. |
| 5 | **GitHub — implementation and verification** | Implement the selected design in scoped components/styles, preserve business behaviour, run tests and open a reviewable PR. | Small PR, before/after evidence, test results, regression checklist and documentation update. |
| 6 | **ChatGPT + real users — acceptance** | Compare the implementation with the approved Figma design and audit findings; review actual task completion with representative users. | Accepted/rejected checklist, remaining risks and next screen selection. |

**Important distinction:** GitHub is used now to store this programme document. That is documentation/version control only. The GitHub implementation stage remains last in each screen's design lifecycle.

### 3.1 Evidence required for a screen

Every screen folder/entry in this document should eventually link to:

1. Current flow screenshots with viewport and state noted.
2. Source files and relevant API/data contracts.
3. UX/accessibility findings with severity and confidence.
4. Research references or a clear note that no external research was needed.
5. The selected design direction and rejected alternatives.
6. Approved Figma frame and component references.
7. Implementation PR and relevant test results.
8. Real-device and user-test observations.
9. Final acceptance status and any deferred work.

### 3.2 Evidence labels

Use these labels in audit notes:

- **Confirmed — source:** directly visible in current source code, API contract, test or current app.
- **Confirmed — screenshot:** directly visible in a screenshot captured during this audit run.
- **Observed — user test:** directly observed while a representative user completed a task.
- **Likely risk:** suggested by code or design but not yet verified in the running UI.
- **Open question:** requires a product-owner decision or further evidence.
- **Not checked:** not inspected; do not imply it passed.

## 4. Billing — initial source-code observations

### 4.1 Current baseline

The baseline is `main` at `4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0`. PR #66 (“Fix flexible billing validation, tag capture and mobile invoice”) is merged at this commit. It already introduced responsive mobile invoice item cards, field-level validation, required customer details in the invoice UI contract, tag-action layout stabilisation and invoice-only print isolation.

This is a **source-code baseline, not a completed visual UX/accessibility audit**. No screenshot from the live RBOS application has been captured for this programme yet.

Relevant implementation:
- [GenerateInvoice.tsx](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/src/GenerateInvoice.tsx)
- [InvoiceDocument.tsx](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/src/InvoiceDocument.tsx)
- [SalesHistory.tsx](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/src/SalesHistory.tsx)
- [invoice.css](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/src/invoice.css)
- [styles.css](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/src/styles.css)
- [SalesPosting.cs](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/src/Rbos.Api/Application/SalesPosting.cs)
- [InvoicePdfRenderer.cs](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/src/Rbos.Api/Application/InvoicePdfRenderer.cs)
- [ui-audit.spec.ts](https://github.com/Vaibhav1507/retail-business-erp/blob/4cfee7d4e1c3560fcdc7e28b8d5823afea14efb0/web/e2e/ui-audit.spec.ts)

### 4.2 Current billing journey

1. **New invoice form:** customer details, product lookup/manual entry, tag/photo capture and optional advanced tag fields, bill lines, discounts and GST controls.
2. **Review:** `Generate invoice` advances to an invoice overview with `Confirm & print` and `Back to edit`.
3. **Done:** shows saved invoice number and WhatsApp delivery status, paper size (A4 or 80 mm), `Print again` and `New invoice`.
4. **Sales History:** searching sales and opening `View` opens an invoice detail modal.
5. **Historical invoice actions:** WhatsApp status and conditional retry, paper-size selector, `Print bill` and `Close`.

The new invoice uses an inline overview; historical invoices use a modal. These should be compared as two entry paths to the same customer-facing document experience.

### 4.3 Strengths already present — preserve these

- The new invoice flow has explicit form, review, done and pending-offline states.
- Invoice UI validation includes customer name/mobile and field-level errors for several product and pricing fields.
- Product tag details are progressively disclosed rather than all shown in the main product editor.
- A dedicated mobile item-card presentation exists below the 720px breakpoint; the desktop/print table remains available.
- Print CSS hides mobile cards and application UI so the printable invoice uses the table layout.
- The historical invoice modal is above the app navigation and floating assistant in the declared z-index order.
- A4 and 80 mm paper choices are already present.
- The sale API returns additional item metadata, and the invoice domain model has places for some identifiers. The redesign should use that data rather than remove it.

### 4.4 Initial findings and questions

| ID | Priority | Finding | Evidence / confidence | Next verification |
|---|---|---|---|---|
| BILL-01 | **P1** | Historical invoice preview drops item metadata that the sale-detail API returns. | **Confirmed — source.** API detail includes SKU, IMEI 1/2, barcode/symbology, model, brand and HSN. `SalesHistory`'s detail type and mapping pass product, quantity, rate, discount and serials only to `InvoiceDocument`. | Capture a historical sale containing each identifier; compare API response with the visible preview and browser print. |
| BILL-02 | **P1** | Share and PDF download are not explicit actions in the visible invoice controls. | **Confirmed — source.** New-invoice Done controls show WhatsApp status, paper size, Print again and New invoice. History controls show WhatsApp status/retry, paper size, Print bill and Close. No direct Share or Download PDF control is present in these components. | Check the actual consent/delivery experience and Android share-sheet/browser support; decide desired primary action before design. |
| BILL-03 | **P1** | The WhatsApp-generated PDF renderer does not currently mirror all identifier fields stored in the immutable invoice snapshot. | **Confirmed — source.** Snapshot stores serials, both IMEIs, barcode, model, brand and HSN. The current PDF renderer's line content prints item name and serial values, not all of those fields. | Generate a synthetic WhatsApp PDF with all identifiers and compare it with the approved document content. Treat this as a document-parity task, not merely a CSS issue. |
| BILL-04 | **P2** | Seller details are repeated in the invoice header and again in the Seller block. | **Confirmed — source.** Business name, address, phone and GSTIN appear in both structures. Mobile CSS stacks seller and bill-to sections. | Inspect at 360–390px width; assess whether a single compact seller identity block preserves required document information. |
| BILL-05 | **P2** | The action toolbar is in normal document flow rather than pinned near the viewport edge. | **Confirmed — source; impact unverified.** `.invoice-tools` has no sticky positioning. | Test long invoices and repeated scroll-to-action tasks on Android before choosing sticky toolbar or bottom action bar. |
| BILL-06 | **P2** | Sales History mobile rows may lose their column labels. | **Confirmed — source.** Global mobile table CSS hides table headers and generates labels from `td[data-label]`; Sales History cells do not set `data-label`. | Capture the mobile history list and verify each amount/date/customer value remains unambiguous. |
| BILL-07 | **P2** | The historical modal has accessibility behaviours that need keyboard/focus verification. | **Likely risk — source.** It is a `div` with dialog roles and Escape/backdrop close behaviour. Focus entry, focus containment and focus restoration are not evident in the component. | Test keyboard-only navigation and TalkBack/screen-reader announcement in the running app. |
| BILL-08 | **P2** | The global floating assistant could obscure lower invoice content on the inline preview. | **Likely risk — source.** The assistant launcher is fixed at z-index 30; the app navigation is fixed at z-index 6. The historical modal is z-index 50, but the inline invoice has no overlay. | Capture before/after scrolling with assistant open/closed, including shorter-height and landscape viewports. Do not change z-index before observing it. |
| BILL-09 | **P2** | Invoice item cards need a visual readability review for long identifiers, larger quantities and GST detail. | **Open visual question.** The mobile card markup includes model, HSN, serials, IMEI 1/2, barcode, quantity, rate, discount and GST rows, but no accepted screenshot exists. | Test long model/serial strings, multiline values, 1/2/10+ item invoices and GST/non-GST states. |
| BILL-10 | **P2** | Brand and SKU do not have a clear presentation path in the invoice document model. | **Confirmed — source.** `InvoiceItem` includes model, HSN, serials, IMEIs and barcode, but not brand or SKU. | Decide whether brand/SKU are customer-facing requirements for electronics invoices; then align UI mapping, API data and PDF snapshot output. |
| BILL-11 | **P3** | Some shared controls are below the intended comfortable touch size. | **Confirmed — source; practical impact unverified.** Global buttons are generally 46px high; assistant quick actions can be 36–38px, and the assistant close control is smaller. | Audit actual hit areas, spacing and Android scaling. Adopt 48dp as the RBOS design target where practical; do not blanket-change every control without checking layout. |
| BILL-12 | **P2** | `Confirm & print` bundles posting confirmation and printing in one label. | **Confirmed — source; comprehension unverified.** The review action is named `Confirm & print`. | Ask pilot users what they expect from the button and whether they understand that confirmation posts the sale. Consider a clearer primary action and explicit next-step feedback. |

**Severity is provisional.** Priority reflects potential business impact and the evidence available from code. It must be re-ranked after current screenshots and user observation.

### 4.5 Billing states to capture in the visual audit

Capture the following using synthetic data only:

- New invoice, empty state.
- Customer selected and new customer entered; validation errors and suggestions.
- Product search, manual/ad-hoc line, barcode scan result and tag-photo result.
- Advanced tag details expanded and collapsed.
- One-line and multi-line cart, long model/serial/IMEI/barcode values.
- GST and non-GST totals; discount and partial-credit payment summary.
- Review screen, validation failure and back-to-edit path.
- Posted/done screen, WhatsApp status variants and paper-size selector.
- Historical invoice modal with a product that has all available identifiers.
- Long historical invoice; toolbar after scrolling; close/backdrop/Escape behaviour.
- Browser print preview for A4 and 80 mm; verify that mobile cards, app navigation, assistant and action buttons do not print.

### 4.6 Viewports and device checks

Minimum capture matrix for this billing group:

- **360 × 800 CSS px** — compact Android viewport.
- **390 × 844 CSS px** — common modern mobile viewport and the existing Playwright mobile project.
- **412 × 915 CSS px** — larger Android viewport.
- **768 × 1024 CSS px** — tablet / narrow desktop transition.
- **Landscape / short height** — check fixed controls, action access and virtual keyboard interactions.
- **Real Android device** — verify actual browser viewport, safe-area/system bars, print behaviour, camera permission, share sheet and TalkBack.

Use the exact current app state and record device/browser/viewport, orientation, account role, synthetic data set and screenshot time for each capture.

## 5. Accessibility and usability standards

Use these as testable design constraints, not as a claim that RBOS is already compliant:

- Design mobile interactive targets for **at least 48dp** where practical, with adequate spacing; Android guidance recommends 48dp and suggests 8dp separation.
- Check WCAG 2.2 AA target-size minimum (24 × 24 CSS px, subject to its exceptions). RBOS's comfortable design target should be more generous than the minimum.
- Check contrast, text resizing/zoom, keyboard access, visible focus, accessible names, labels, reading order, errors and status announcements.
- Use short, step-by-step instructions near the relevant task. Do not assume that an icon or an error message alone explains what to do.
- Keep language plain. Validate Hindi wording with actual operators; translation alone does not prove comprehension.
- Avoid requiring drag gestures, precise taps, hidden long-presses or memorised navigation.
- Preserve data after recoverable errors and explain the next action.
- Do not use colour alone to convey status.

### External references

- [Android Accessibility — Touch target size](https://support.google.com/accessibility/android/answer/7101858)
- [Android Developers — Make apps more accessible](https://developer.android.com/guide/topics/ui/accessibility/apps)
- [W3C — WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C WAI — Clear step-by-step instructions (cognitive accessibility pattern)](https://www.w3.org/WAI/WCAG2/supplemental/patterns/o4p07-step-instructions/)
- [GOV.UK Service Manual — Understanding users who do not use digital services](https://www.gov.uk/service-manual/user-research/understanding-users-who-dont-use-digital-services)

## 6. How to test whether the tools are useful

Do not use a tool simply to satisfy a checklist. Record the contribution and the decision it enabled.

| Tool | Value test | Keep using it when… | Do not use it when… |
|---|---|---|---|
| ChatGPT | Did it find dependencies, conflicts, source evidence and acceptance risks? | It reduces missed requirements and produces traceable decisions. | It repeats generic design advice without inspecting the code or app. |
| Product Design | Did it capture the real app flow and identify issues that source inspection cannot reveal? | Its screenshots and evidence-linked audit improve understanding and prioritisation. | It cannot access the running app or provide accepted screenshots. Do not call a source-only review a screenshot audit. |
| MagicPath | Are the alternatives materially different and do they clarify interaction/hierarchy? | It lets us compare options before committing to implementation. | It generates decorative variations that do not resolve the identified user problem. |
| Figma | Does it preserve an approved design and reusable components/tokens? | Developers can inspect exact layout, states, spacing and component rules. | It duplicates a disposable exploration or has no approved source to formalise. |
| GitHub | Are changes small, reviewable, traceable and tested? | PRs preserve behaviour and document evidence and acceptance. | It is used to patch CSS without an approved design or regression test. |

### Initial tool-use log

| Sequence | Tool | Status for this programme | Initial assessment |
|---|---|---|---|
| 1 | ChatGPT / repository source analysis | **Used** | Useful for establishing the current implementation and evidence-backed billing questions. Must be followed by live visual/user evidence. |
| 2 | Product Design | **Blocked — visual capture not available in this chat** | The audit workflow requires a live browser and screenshots. No accepted visual audit is claimed. Resume in a Work environment with browser capture, or use a controlled Playwright/device capture path and record it as an alternative. |
| 3 | MagicPath | **Not started** | Wait for the Product Design audit findings and confirmed design brief. |
| 4 | Figma | **Not started** | Wait until a design direction is selected; then formalise the approved direction. |
| 5 | GitHub implementation | **Not started** | The current GitHub use is repository inspection and this documentation PR only. No application code or deployment is authorised by this programme document. |

## 7. Acceptance checklist for each redesigned screen

A screen is not complete until the relevant checks are evidenced:

- [ ] A representative first-time / low-digital-confidence user understands where to start.
- [ ] The primary action is clear and does not compete with unnecessary actions.
- [ ] Labels and instructions are understandable without technical explanation.
- [ ] Text is comfortable to read at normal phone distance.
- [ ] Interactive targets are large and sufficiently separated.
- [ ] No horizontal scrolling or clipped important content at supported widths.
- [ ] Fixed navigation, floating controls, keyboards and system bars do not obscure content or actions.
- [ ] Loading, empty, error, success, pending/offline and disabled states are clear.
- [ ] Errors preserve entered data and show an obvious recovery path.
- [ ] Keyboard and screen-reader behaviour is checked where relevant.
- [ ] Existing business logic, API contracts, permissions, audit trail and financial invariants are preserved.
- [ ] Desktop behaviour remains acceptable.
- [ ] A4 / 80 mm print output is regression-tested where the screen produces a document.
- [ ] Automated tests/build checks pass for the changed surface.
- [ ] Real-device check and a representative-user task test are recorded.
- [ ] Documentation, screenshots, Figma reference and PR are linked.

## 8. Immediate next actions

1. Complete the Product Design visual audit of the Billing flow using the capture matrix in section 4.5.
2. Update BILL-01…BILL-12 with screenshots, confidence, severity and any issues already resolved by current code.
3. Choose the top design problems and ask MagicPath for alternatives only where the choice benefits from exploration.
4. Formalise the selected design in Figma.
5. Implement only approved changes in a scoped GitHub PR; keep business logic changes separate.
6. Test the resulting flow with synthetic data and representative shop users before accepting Billing and moving to the next phase.

**Current stop point:** the source-code baseline is documented. The Billing visual audit and three design alternatives are pending. Do not begin implementation from this document alone.
