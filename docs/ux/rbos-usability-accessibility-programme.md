# RBOS Usability & Accessibility Programme

- **Status:** BILLING & SALES REDESIGN IN PROGRESS — pre-redesign baseline audit and PR #74 exact-head CI are verified; Create Invoice implementation candidate exists; complete design specifications for the five routes and final grouped verification remain pending. Figma is optional and not a blocker.
- **Repository baseline:** `main` at `17428b20c1974a0a57d00b60774d893831304e64` (24 November 2026; refresh before implementation)
- **Scope:** Screen-by-screen usability, accessibility, responsive-layout and interaction improvements
- **Release boundary:** This document does not authorise application-code changes, production deployment, schema changes or feature expansion.
- **Visual audit sequence:** Home first; then Billing & Sales, including the Sell hub and its sales-related sections
- **First design priority:** Billing / invoice creation and preview

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

### 2.2 Staged visual-audit execution

Do not capture the entire application inventory in one run. Run one connected area at a time, review it, record decisions and only then move to the next area.

| Stage | Included routes | What to review |
|---|---|---|
| **1 — Home** | `#/home` | Dashboard summary tiles, quick actions, insights/reminders, navigation hierarchy, loading and empty/error states |
| **2 — Billing & Sales** | `#/sell/menu`, `#/sell/bill`, `#/sell/bills`, `#/sell/collect`, `#/sell/return` | Sell landing page, new bill, sales history, collecting customer payments and customer returns |
| **Later stages** | Stock, purchasing, people, money, administration and other programme phases | Do not include these in the first two audit runs |

**Execution rules**

- The GitHub Actions visual-audit workflow must offer one scope per run, with **Home** selected by default.
- Home retains its original baseline at 390 × 844 and 768 × 1024.
- Billing & Sales route baselines are captured at 360 × 800, 390 × 844, 412 × 915, 768 × 1024, 1280 × 900 and 844 × 390 landscape. Nested interaction states are captured at compact mobile, standard mobile, tablet and desktop widths; loading/async states use focused captures.
- Route readiness must be proved with screen-specific data markers (for example, an actual Sales History row), not `networkidle` alone. Seed only the synthetic records needed for each journey.
- The Billing & Sales route pass is the baseline, not the final interaction audit. The expanded suite must cover bill creation/review/post/print, extraction, history search/filter/pagination/detail, collections/statement/payment, returns/return detail, held bills and offline-pending states.
- Visual Audit runs automatically on pull requests that change the scoped audit or relevant Billing & Sales UI files; manual dispatch remains available and Home remains the default manual scope.
- Do not create or use real customer, invoice or payment data for visual-audit captures.
- Do not mark any state as audited until its screenshot and corresponding assertions have been reviewed.

## 3. Standard lifecycle for every screen

Follow this sequence consistently. The tools should each have a distinct job; stop using a tool when it does not add measurable value.

| Order | Tool / owner | Responsibility | Required output before handoff |
|---|---|---|---|
| 1 | **ChatGPT — orchestration and source analysis** | Resolve the user task, inspect repository implementation and dependencies, record current behaviour, identify constraints and prioritise risks. | Screen brief, source map, initial findings and acceptance questions. |
| 2 | **RBOS Visual Audit + Playwright — visual capture and accessibility evidence** | Run the repository's scoped GitHub Actions visual-audit workflow, capture real application routes/states with Playwright at agreed viewports, inspect screenshots and review Axe results. | Accepted current screenshots; numbered flow; evidence-linked UX and accessibility findings; explicit capture gaps. |
| 3 | **MagicPath — design exploration** | Turn the agreed brief and audit findings into genuinely different layout/interaction alternatives. | Three distinct options for complex screens, comparison and prototype links/previews. Use fewer only when the screen is genuinely simple and the owner agrees. |
| 4 | **Repository design specification — approved design contract** | Finalize layout, components, tokens, responsive rules, copy, interactions and states in versioned docs/ux documentation. MagicPath remains a supporting prototype/reference when available; Figma is optional. | Complete, reviewable design note with measurable acceptance criteria, screen/state layouts and dependencies. |
| 5 | **GitHub — implementation and focused checks** | Implement each screen in a focused branch/PR or agreed integration branch, preserve business behaviour and run narrow checks as code changes. | Reviewable implementation, focused tests/build evidence and updated handoff. |
| 6 | **Playwright + Axe + Android/user review — group verification** | After all five Billing & Sales routes are redesigned and implemented, run the complete integrated journey, responsive screenshot and Axe pass; then review native Android behaviour and shopkeeper feedback. | Group-level verification report, reviewed screenshots, defects fixed, native/user acceptance status and next screen selection. |

**Important distinction:** GitHub is used now to store this programme document. That is documentation/version control only. The GitHub implementation stage remains last in each screen's design lifecycle.

### 3.1 Evidence required for a screen

Every screen folder/entry in this document should eventually link to:

1. Current flow screenshots with viewport and state noted.
2. Source files and relevant API/data contracts.
3. UX/accessibility findings with severity and confidence.
4. Research references or a clear note that no external research was needed.
5. The selected design direction and rejected alternatives.
6. Approved repository design specification with component/token/state definitions; MagicPath prototype/reference when available. Figma is optional and not a delivery prerequisite.
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

### 4.8 Expanded-audit harness attempts — 24 November 2026

| Run | Head | Result | Evidence / correction |
|---|---|---|---|
| [45301760436](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45301760436) | `4e87e2b` | FAILED — harness | The test installed a second dialog handler while `signup.ts` already owned one. The helper attempted to accept a dialog already handled by the other listener. The test stopped during the first invoice; no visual artifact was uploaded. |
| [45302085474](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302085474) | `b69b669` | FAILED — harness | Same dialog-ownership failure. Removing listeners from the test did not reliably replace the helper's active handler. |
| [45302508735](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302508735) | `7d3feac` | IN_PROGRESS at last check | Introduced a tracked dialog-handler replacement; verify result before relying on it. |
| [45302584156](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302584156) | `84d7643` | IN_PROGRESS at last check | Switched to the documented listener-removal API; superseded by the single-listener design below. |
| [45302914826](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302914826) | `db9f0b5` | IN_PROGRESS at last check | Current implementation keeps exactly one dialog listener per page and changes only its active policy. This is the run to inspect first. |
| [45303312267](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45303312267) | `d0e3fda` | FAILED — harness | The readiness helper expected a Sell-menu hint string that the actual route does not render. Corrected to assert the four `.areas .area` destination links. The initial report is now written before setup and artifact upload includes Playwright failure screenshots/traces. |
| [45303775116](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45303775116) | `85a20e2` | IN_PROGRESS | Latest run after the route-readiness and failure-artifact fixes. Inspect its report/screenshots before proceeding. |

The failure was in the test harness, not evidence of an application invoice defect. Do not treat the two failed attempts as application test failures. The visual audit remains NOT_VERIFIED until a complete artifact is uploaded and reviewed.

### 4.7 First route-audit artifact review — 24 November 2026

**Run:** [RBOS Visual Audit #2](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45299902320)  
**Artifact:** `rbos-visual-audit-billing-sales` (artifact ID `14878326785`)  
**Evidence:** 10 screenshots and 10 report rows: five Sell routes at 390 × 844 and 768 × 1024.

The report recorded zero horizontal overflow, unnamed controls, undersized controls and Axe WCAG 2 A/AA violations for the states it actually captured. This is not proof that the whole flow passes.

**Screenshot-by-screenshot findings:**

- `sell/menu`: route shell and four Sell destinations visible; no navigation interaction was exercised.
- `sell/bill`: initial empty form only; tag/photo chooser, advanced fields, validation, cart, review, success and print were not captured.
- `sell/bills`: mobile screenshot showed `Loading…`; tablet showed `No bills found` with no seeded sales. The test waited for network idle, not the loaded business state.
- `sell/collect`: blank customer/payment form and empty collection history only.
- `sell/return`: blank sale selector and empty return history only.

**Disposition:** route existence is VERIFIED; complete route-state coverage is NOT_VERIFIED. The first audit is a useful shell baseline, not visual acceptance. PR #72 adds synthetic data seeding, explicit readiness assertions, nested-state captures and fail-on-violation gates. Review its artifact before closing this finding.

### 4.6 Viewports and device checks

Minimum capture matrix for this billing group:

- **360 × 800 CSS px** — compact Android viewport.
- **390 × 844 CSS px** — common modern mobile viewport and the existing Playwright mobile project.
- **412 × 915 CSS px** — larger Android viewport.
- **768 × 1024 CSS px** — tablet / narrow desktop transition.
- **1280 × 900 CSS px** — desktop layout and document/table readability.
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
| RBOS Visual Audit + Playwright | Did it capture the real app flow and identify issues that source inspection cannot reveal? | Screenshots and accessibility reports are reproducible, scoped, synthetic-data-only and linked to a workflow run. | The workflow cannot reach a required nested state or the screenshot/report artifact is missing. Record the capture gap and extend the audit before claiming coverage. |
| MagicPath | Are the alternatives materially different and do they clarify interaction/hierarchy? | It lets us compare options before committing to implementation. | It generates decorative variations that do not resolve the identified user problem. |
| Figma | Does it preserve an approved design and reusable components/tokens? | Developers can inspect exact layout, states, spacing and component rules. | It duplicates a disposable exploration or has no approved source to formalise. |
| GitHub | Are changes small, reviewable, traceable and tested? | PRs preserve behaviour and document evidence and acceptance. | It is used to patch CSS without an approved design or regression test. |

### Initial tool-use log

| Sequence | Tool | Status for this programme | Initial assessment |
|---|---|---|---|
| 1 | ChatGPT / repository source analysis | **Used** | Useful for establishing the current implementation and evidence-backed billing questions. Must be followed by live visual/user evidence. |
| 2 | RBOS Visual Audit + Playwright | **IN_PROGRESS** | Run #2 (45299902320) route artifact reviewed: 10 screenshots, five routes × two viewports. It exposed a data-readiness defect (Sales History mobile captured `Loading…`; tablet showed an unseeded empty list). PR #72 adds deterministic data, nested-state capture and enforced checks. Two early runs (45301760436 and 45302085474) stopped at invoice posting because the signup helper and visual test competed to handle the same browser dialog; the helper now uses one mutable dialog policy/listener per page. Latest visual run 45302914826 is in progress and is not yet accepted. |
| 3 | MagicPath | **NOT_STARTED** | Wait for the expanded RBOS Visual Audit findings and confirmed design brief. Do not use the Product Design plugin; MagicPath and Figma remain in the workflow. |
| 4 | Figma | **Not started** | Wait until a design direction is selected; then formalise the approved direction. |
| 5 | GitHub implementation | **NOT_STARTED** | PR #72 is audit-test/workflow infrastructure only. No Billing & Sales application redesign or production deployment has been started or authorised by this programme document. |

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

1. Complete PR #72's RBOS Visual Audit CI run; inspect its screenshots, report and any failed assertions, then fix the audit itself or record verified UI findings.
2. Extend the matrix for any uncovered state discovered during the run (including WhatsApp status variants, collection bill allocation and extraction failure/recovery); do not mark these as covered until evidence exists.
3. Update BILL-01…BILL-12 with screenshot evidence, confidence, severity and any issues already resolved by current code.
4. Only after the capture suite is complete and visually reviewed, explore the highest-value alternatives in MagicPath.
5. Formalise the selected direction in editable Figma with components, tokens, responsive layouts and important states.
6. Implement only the approved Billing & Sales design in a focused application PR; preserve business rules and APIs, then test and validate with synthetic data and representative shop users.

**Current stop point:** the first route-level artifact has been reviewed but is not accepted as complete. PR #72 is expanding capture and enforcement; wait for its CI result, inspect the full artifact and close any missing states before MagicPath or Figma. Do not begin application redesign from the first route-only artifact.

## 9. Mandatory decision, assumption and deferral ledger

**Non-negotiable:** document every decision, recommendation, rejected option, assumption, dependency, discovered issue, deferral, blocker, skipped step and unfinished task. A chat transcript or someone's memory is not the durable source of truth. Another chat or engineer must be able to resume this programme from the repository without asking the user to reconstruct context.

Maintain this section as a living record throughout the phase. Update it at every meaningful pause and before moving to another stage.

### 9.1 What must be recorded

- User requirements, explicit preferences, constraints, approvals and changes to earlier instructions.
- Recommendations and selected decisions, with rationale and alternatives considered.
- Rejected options and the evidence/condition that would justify reconsidering them.
- Assumptions that remain unverified.
- Affected routes, source files, APIs, data contracts, components, integrations and business/accounting invariants.
- Findings with evidence links, screenshot IDs, source references, reproducible steps and confidence.
- Every issue intentionally left unresolved: why it is deferred, impact/risk, priority, dependencies and revisit trigger.
- Blockers and unanswered questions, including the exact missing input and safe work that can continue.
- Items inspected but explicitly kept out of scope.
- Checks run, exact pass/fail/skip outcomes, viewport/state coverage, artifacts and manual validation still outstanding.
- Branches, PRs, commits, review/merge status and deployment status separately.
- A precise handover: next action, prerequisites, relevant links and remaining risks.

### 9.2 Status vocabulary

- **DECIDED** — decision and rationale recorded.
- **IN_PROGRESS** — work started but incomplete.
- **VERIFIED** — supported by direct evidence.
- **DEFERRED** — deliberately postponed with reason and revisit trigger.
- **BLOCKED** — cannot safely proceed until a named dependency/input is resolved.
- **REJECTED** — considered and not selected, with rationale.
- **NOT_RUN** — check/action not performed.
- **OUT_OF_SCOPE** — explicitly excluded from this phase.
- **SUPERSEDED** — an earlier decision replaced by a newer one; link the replacement.

Do not use VERIFIED for assumptions, plans, successful builds that do not cover the claim, or screenshots of a different state.

### 9.3 Decision-log template

| ID / date | Type | Decision, finding or deferred item | Rationale / evidence | Impact and dependencies | Status | Next action / revisit trigger |
|---|---|---|---|---|---|---|
| UX-01 / 2026-11-24 | DECIDED | Use RBOS Visual Audit + Playwright/Axe instead of the Product Design-dependent visual capture stage; retain MagicPath exploration and editable Figma design. | User's explicit RBOS workflow clarification; programme lifecycle section 3. | Changes only the evidence/capture stage; does not remove MagicPath or Figma. | DECIDED | Apply to every future RBOS UX phase. |
| UX-02 / 2026-11-24 | VERIFIED | First Billing & Sales route artifact contains 10 screenshots for five routes at mobile/tablet sizes, with zero reported Axe, overflow, unnamed-control or small-target findings in those captured states. | [Visual Audit run #2](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45299902320), artifact ID `14878326785`, `report.json`; all 10 screenshots inspected. | Does not prove nested-state coverage; Sales History mobile captured `Loading…`, tablet captured an unseeded empty list. | VERIFIED | Preserve as a route-shell baseline only; do not call it visual acceptance. |
| UX-03 / 2026-11-24 | DECIDED | Use deterministic synthetic users/products/sales/payments and mock AI extraction responses in visual CI. | Existing isolated PostgreSQL workflow; no live AI provider is needed to test UI states. | Prevents production-data exposure, paid-provider dependency and flaky extraction output. | DECIDED | Keep all audit fixtures synthetic and isolated. |
| UX-04 / 2026-11-24 | IN_PROGRESS | Expand Billing & Sales coverage to six genuine viewport profiles, seeded route states, nested journeys, print states and enforced layout/accessibility gates. | [PR #72](https://github.com/Vaibhav1507/retail-business-erp/pull/72), branch `test/billing-sales-visual-audit-coverage`. | Depends on stable seeded flows and a complete artifact; no app behaviour changes are included in this PR. | IN_PROGRESS | Review the latest head run #45304729174 and artifact before accepting the audit. |
| UX-05 / 2026-11-24 | VERIFIED | Early audit failures were caused by duplicate Playwright dialog handlers, not invoice/business logic. | Runs #45301760436 and #45302085474 logs show `dialog.accept: Cannot accept dialog which is already handled`. | Test helper ownership was ambiguous; the shared helper now routes all dialog decisions through one mutable listener per page. | VERIFIED | Do not repeat listener registration; the latest head has moved to route/session setup validation. |
| UX-06 / 2026-11-24 | DEFERRED | Real Android camera permissions, physical printer/share sheet and TalkBack cannot be verified in headless Chromium CI. | These depend on a physical device and OS-level UI not exposed to Playwright's headless browser. | Automated viewport checks cannot establish real-device acceptance. | DEFERRED | Revisit after the Figma-approved implementation is available on a test device; record device, Android/browser versions and observed results. |
| UX-07 / 2026-11-24 | IN_PROGRESS | Additional automated states remain to be added or proven: WhatsApp status variants, collection allocation to a specific bill, extraction failure/recovery. | Current PR #72 test source and the implementation state matrix; not yet represented by a completed artifact. | These states affect customer communication, payment allocation and recovery behaviour. | IN_PROGRESS | Add deterministic mocked states and assertions; do not mark them covered until screenshots and reports exist. |
| UX-08 / 2026-11-24 | OUT_OF_SCOPE | Home redesign, Stock, Purchasing, global navigation and backend/business-rule changes remain outside Billing & Sales scope. | Named screen group and programme boundary. | Shared dependencies may be inspected, but changes require a separately documented decision. | OUT_OF_SCOPE | Reopen only if a verified Billing & Sales dependency requires it. |
| UX-09 / 2026-11-24 | NOT_RUN | MagicPath exploration and Figma finalisation have not started. | The expanded visual audit is not yet complete or visually reviewed. | Design direction must be based on the full captured journeys, not only the route-shell screenshots. | NOT_RUN | Start MagicPath after the current head produces a complete artifact; then finalise in Figma. |
| UX-11 / 2026-11-24 | VERIFIED | Visual Audit #45303775116 failed during the desktop route pass because the page remained on the last posted invoice while the test expected the Sell menu. The failure screenshot and `error-context.md` show the invoice overview, back link and saved invoice status; `.areas .area` is present in the actual Sell menu implementation. | [Run #45303775116](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45303775116), artifact `14879344448`; screenshot + Playwright error context inspected. | This is an audit navigation/session-state failure, not evidence of a broken application selector or billing behaviour. | VERIFIED | Fix the test to unlock fresh browser sessions and navigate using the actual Sell navigation/menu links. |
| UX-13 / 2026-11-24 | VERIFIED | The latest visual audit progressed through route and form states, then stalled in tag extraction because its synthetic image was a four-byte JPEG marker, not a decodable JPEG. The browser's local barcode decode did not reach the mocked scan endpoint; the assertion timed out while the uploader remained busy. | [Run #45304729174](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45304729174), artifact `14880556901`, `error-context.md`, trace network and failed screenshot inspected. | This is test-fixture quality, not evidence of a production extraction failure. | VERIFIED | Replace the invalid fixture with a valid 32×32 synthetic JPEG, retain the endpoint mock, and review the next run. |
| UX-14 / 2026-11-24 | VERIFIED | Partial visual evidence contains 60 screenshots across 13 states. The report recorded zero Axe WCAG 2 A/AA violations, zero horizontal overflow and zero unnamed controls. Eight undersized controls were reported, each `button 36x44`, in source-picker states. | Artifact `14880556901`, `ui-audit/billing-sales/report.json`; 60 screenshots reviewed via contact sheet and key mobile screens opened individually. | Coverage is partial: invoice extraction result/review, posting, print, held/offline, collections and returns interaction states were not reached in this run. | VERIFIED | Treat 36px-wide picker buttons as a real usability finding to address in design; do not accept this partial run as a complete audit. |
| UX-15 / 2026-11-24 | DECIDED | MagicPath exploration recommends Concept A: one dominant Add product button; a quiet Scan tag action beside the product field opens the Camera / Choose from device picker. Handwritten invoice import remains a separate invoice-level action. | [MagicPath project](https://www.magicpath.ai/files/475790236023144448), three wireframe directions and decision rationale on canvas. | Reduces competing full-width actions and keeps product-tag capture distinct from handwritten invoice extraction. | DECIDED | Carry Concept A into Figma as the selected design direction; retain the other two concepts as rejected alternatives with rationale. |
| UX-16 / 2026-11-24 | BLOCKED | Figma file `RBOS Billing & Sales — Invoice Entry` was created, but Figma MCP returned a Starter-plan tool-call limit when library discovery began. No screen has been authored or validated in Figma yet. | [Figma file](https://www.figma.com/design/ACpAOSnl3aeIRl1Hxnllzf); `whoami` reported Starter plan / View seat; `get_libraries` returned the rate-limit paywall. | Editable design finalisation is blocked by Figma MCP quota/access, not by a product decision. MagicPath remains the available editable exploration surface, but it is not treated as an equivalent Figma deliverable. | BLOCKED | Resume Figma library discovery and editable screen creation when Figma MCP quota is available; if unavailable, obtain an authorised Figma editing route rather than silently substituting a static image. |
| UX-12 / 2026-11-24 | IN_PROGRESS | PR #72 now uses the actual in-app Sell navigation for route captures and explicitly unlocks fresh desktop/mobile sessions before capturing. | Commit `17a502b874c96284ca9620fba48ad7c60a4fcd6b`; workflows [CI #45304729088](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45304729088) and [Visual Audit #45304729174](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45304729174) started. | Test-only change; no application code, API, accounting or inventory rules changed. | IN_PROGRESS | Review the first completed head run and full artifact; fix only a concrete failing step, then proceed to design exploration. |
| UX-10 / 2026-11-24 | DECIDED | Keep programme documentation changes on the existing PR #67 branch; use PR #72 only for the independent Playwright/workflow implementation. | User's explicit branch-discipline correction. | Avoids duplicate documentation branches/PRs while keeping executable test changes reviewable. | DECIDED | Continue programme-doc updates on `docs/rbos-usability-accessibility-programme`; do not create another docs-only PR. |

| UX-17 / 2026-11-24 | VERIFIED | The latest standard CI run #45306305374 passed all three jobs: frontend (TypeScript, tests, lint, build, dependency audit), backend (Release build, unit tests, deployment-container build) and PostgreSQL integration (isolated database bootstrap, migrations, integration tests). | [CI run #45306305374](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45306305374), latest job results fetched from GitHub Actions. | This validates the PR #72 test/workflow code against configured checks only; it does not establish visual acceptance or user acceptance. | VERIFIED | Preserve as automated CI evidence; await and inspect the separate Visual Audit run #45306305473. |

| UX-18 / 2026-11-24 | VERIFIED | Visual Audit run #45306305473 failed at the held-bill resume assertion because the test expected the product-entry input `#billing-prod` to retain `Manual held item`. The implementation correctly restores saved lines into the invoice cart and leaves the entry input blank for the next item. The test assertion targeted the wrong UI control. | [Run #45306305473](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45306305473), artifact `14882130036`, `error-context.md`; source `web/src/GenerateInvoice.tsx` `resume()` restores `items`, and the cart table renders `items.map(...)`. | No application defect established. Audit stopped before offline pending, collections and returns nested states. | VERIFIED | Correct assertion to verify the resumed cart row, quantity/amount and intentionally blank product input; rerun audit. |
| UX-19 / 2026-11-24 | VERIFIED | Partial run #45306305473 captured 93 screenshots across 24 named states before the held-bill assertion stopped the journey. Report recorded zero page errors, zero horizontal overflow and zero unnamed controls. It reported Axe `color-contrast` (10 nodes) in invoice review/posted/thermal/print states, plus `scrollable-region-focusable` in print media. It also reported 17 undersized controls across tag/invoice source pickers, tablet extraction review and payment selects. | Artifact `14882130036`, `ui-audit/billing-sales/report.json`; tablet invoice-review screenshot visually shows invoice table columns squeezed into narrow vertical text and the fixed navigation/Assistant overlay crossing the invoice. Mobile empty-form screenshot shows the fixed shell overlays the long form in a full-page capture; this needs viewport-scroll validation before treating it as a live collision. | These are real design/a11y findings to take into the design phase; current report is partial and not an accepted complete audit. Tablet invoice layout is a clear visual concern even though the overflow metric is false. | VERIFIED | Include contrast, touch target, tablet invoice density and print scroll semantics in the design brief; verify overlay collisions with viewport-position captures. |
| UX-20 / 2026-11-24 | IN_PROGRESS | The held-bill test assertion was corrected to check the restored `.billing-table` row (`Manual held item`, amount `99.00`) and assert the product-entry field is empty for the next item. | Commit `cf6cadfd37b53e6c4cf8cbfa7be56a092dd0058a` on PR #72 branch `test/billing-sales-visual-audit-coverage`. | Test-only correction; no application code, route, API, inventory, tax, payment or accounting behaviour changed. A new workflow run for this head SHA has not yet been observed. | IN_PROGRESS | Confirm the PR-triggered Visual Audit run uses head `cf6cadfd...`; inspect the next first failure and artifact rather than rerunning stale green CI. |

| UX-21 / 2026-11-24 | BLOCKED | The corrected test commit `cf6cadfd37b53e6c4cf8cbfa7be56a092dd0058a` is on the PR #72 branch, and the PR workflow path filter includes `web/e2e/billing-sales-visual.spec.ts`, but no CI or Visual Audit run has been created for that SHA. The latest Actions list still contains only runs for the previous head `b49841aa...`. | Branch endpoint confirms current head `cf6cadfd...`; Actions runs list queried for the branch after the commit; `.github/workflows/visual-audit.yml` on PR branch includes the changed test file in `pull_request.paths`. | Likely GitHub App/connector-authored commit event suppression or delayed event delivery; no available connected action supports `workflow_dispatch`, and the GitHub fetch action is GET-only. Re-running old run #45306305473 would test the old commit and is not a valid substitute. | BLOCKED | User must trigger the existing `workflow_dispatch` from GitHub Actions UI for `RBOS Visual Audit`, selecting `billing-sales`, or make a normal authenticated push/PR synchronization event. Then inspect the new run; do not merge PR #72 before this evidence exists. |

| UX-23 / 2026-11-24 | VERIFIED | Visual Audit #45314741168 failed at desktop nested-state setup because `desktopContext.close()` ran immediately after the five desktop route-shell captures, but `desktopPage` was reused later for desktop invoice entry, history detail, collections, returns and print captures. The error was deterministic: `page.goto: Target page, context or browser has been closed` at the first reuse. | [Run #45314741168](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45314741168), artifact `14884667961`, `error-context.md`, report (134 screenshots / 35 states), `billing-sales-visual.spec.ts` lines 165 and 410. | This was a test resource-lifetime bug, not an application crash. | VERIFIED | Keep the desktop context alive through all nested desktop captures and close it only after the standalone desktop print capture. |
| UX-24 / 2026-11-24 | IN_PROGRESS | The same report found the Print links in collection success/history were `a` elements around 39×16 on tablet, below the 44×44 touch target. Added a scoped `.billing-action-link` class with inline-flex alignment, 44px minimum width/height and padding to the AfterSales print link helper. | [Run #45314445409](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45314445409), artifact `14885256869`, `report.json`; PR #72 commits `6dfaa34` and `d397270`. | Affects the collection/return history action link only. | IN_PROGRESS | Verify the latest report no longer lists undersized print links. |

| UX-25 / 2026-11-24 | VERIFIED | Visual Audit #45315394423 cleared the desktop-context lifetime failure and reached the desktop collection statement state. It then failed because `getByRole('region', { name: 'Customer statement' })` used Playwright's partial accessible-name matching and matched both the intended statement region and the nested `Customer statement entries` Paged region. | [Run #45315394423](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45315394423), artifact `14885227958`, `error-context.md`; screenshot/report and latest source `AfterSales.tsx`, `Paged.tsx`. | This is a test locator ambiguity caused by the addition of a correctly labelled nested region; the statement content is present, not duplicated. | VERIFIED | Change all named region locators in the visual spec to `exact: true`; preserve the distinct accessible labels in the application. |

| UX-26 / 2026-11-24 | VERIFIED | Visual Audit #45315394423 failed because the desktop collection statement locator used partial accessible-name matching and matched both `Customer statement` and nested `Customer statement entries`. The application labels were valid; the test locator was ambiguous. The fix uses exact accessible-name matching. | [Failed run #45315394423](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45315394423), artifact `14885227958`, error context; subsequent successful runs #45315979703, #45316791983 and #45316812990. | Test selector ambiguity, not application failure. No production code change was needed for this failure. | VERIFIED | Retain exact accessible-name matching for named regions; avoid broad partial role locators in future Playwright assertions. |
| UX-27 / 2026-11-24 | VERIFIED | Latest Visual Audit #45316812990 passed on commit `fc974e4e16ca80a14bb71156f482f333372b4600`. The single Playwright test completed 47 named states and uploaded 178 screenshots across five routes and five viewport profiles. Report records zero page errors, zero horizontal overflow, zero unnamed controls, zero undersized controls and zero Axe violations across captured screen states. | [Visual Audit #45316812990](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812990), artifact `14885473196`, `ui-audit/billing-sales/report.json`; all 178 captures enumerated; contact sheet reviewed. | Print-media captures intentionally skip the screen-media Axe pass; native Android camera, printer/share sheet, TalkBack and live-user usability are not covered by this browser audit. The report's browser dialogs include unsaved-work confirms and a cancellation confirm; these are captured as dialog events, not screenshots. | VERIFIED | Treat automated Billing & Sales audit coverage as complete for the declared mocked browser journeys. Move to visual/design review, then MagicPath and Figma; separately schedule real-device and representative-user acceptance. |
| UX-28 / 2026-11-24 | VERIFIED | Standard CI #45316812983 passed all three jobs on the same commit `fc974e4e...`: frontend (TypeScript, npm tests, lint, build, dependency audit), backend (Release build, unit tests, deployment container build), and PostgreSQL integration (isolated DB bootstrap, migrations, integration tests). | [CI run #45316812983](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812983), job details fetched from GitHub Actions. | This is automated CI evidence only; it does not constitute visual design acceptance, user acceptance, merge or deployment. | VERIFIED | Do not rerun green checks without a code change; proceed to design gates and review the captured screenshots against the agreed brief. |

| UX-29 / 2026-11-24 | VERIFIED — PROTOTYPE BUILT | Created an interactive, responsive MagicPath component for the next design milestone: Invoice Entry — Action-first mobile prototype. It builds on selected Concept A and addresses screenshot-backed friction: fixed navigation overlapping the long form; three competing product actions; customer details not prominent enough; serial/barcode/IMEI cluttering the default path; and weak separation between tag capture and handwritten-bill import. | [MagicPath prototype](https://designs.magicpath.ai/v1/cozily-dusk-6555), component `475857518220939264`, revision `475857518220939265`; build completed. Baseline screenshot: audit artifact `14885473196`, `mobile-390x844-bill-empty-form.png`. | Prototype only: camera/gallery choices are simulated, image input/OCR and customer/product API lookups are not connected, and no application code or business behaviour has changed. Figma authoring remains blocked by the Figma Starter-plan MCP call limit. | VERIFIED | Review and refine the mobile/tablet interaction states in MagicPath. Resume editable Figma authoring once the plan tool limit permits. Do not pause the whole UX programme for that tool constraint. |\n\nFor significant decisions, include alternatives considered and why they were not selected. Link relevant PRs, commits, screenshots, audit artifacts, issues and design files.
| UX-30 / 2026-11-24 | IMPLEMENTED — PR OPEN, VALIDATION IN PROGRESS | Implemented the first action-first Create Invoice slice on focused branch `ux/invoice-entry-action-first`, draft [PR #74](https://github.com/Vaibhav1507/retail-business-erp/pull/74), head `72cfe96dd8b361d77123d136f1f6f12ad6ac4768`. The active invoice route now suppresses the persistent main navigation and Navigation Assistant while retaining the app header/back-link; tag capture is contextual beside product entry; Add product is the only primary item action; handwritten bill import is separated into the page header; Brand/Model/Serial are disclosed as optional details and automatically revealed after tag extraction or serial validation; summary/review actions are sticky with safe-area spacing. | [PR #74](https://github.com/Vaibhav1507/retail-business-erp/pull/74); [commit](https://github.com/Vaibhav1507/retail-business-erp/commit/72cfe96dd8b361d77123d136f1f6f12ad6ac4768). Exact-head CI [#45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) **PASSED** on `72cfe96dd8b361d77123d136f1f6f12ad6ac4768` at 2026-11-24 06:26 UTC. Frontend TypeScript, unit tests, lint, build and dependency audit passed; backend Release build, unit tests and container build (no push) passed; PostgreSQL integration bootstrap/build/tests passed. The earlier intermediate-head run failed because four ItemTagCapture tests expected the old `Add tag photo` label; component and tests were aligned to `Scan tag`, and the final exact-head run is green. | Frontend-only focused UX change. No API, routes, posting rules, GST, payments, inventory, offline/idempotency, serial-validation or accounting contracts intentionally changed. The prior audit/CI #45316812990/#45316812983 remain pre-redesign baseline only. Full Playwright screenshot/Axe validation of this implementation, Android/native behaviour and shopkeeper acceptance are not complete. | IMPLEMENTED — AWAITING VALIDATION | Inspect exact-head CI #45340114617; resolve concrete failures once; add implementation screenshots/Playwright visual audit after the current audit infrastructure PR #72 is available; inspect mobile/tablet/landscape/desktop and focus/keyboard/scroll/print states; update the result before merge. Figma editable authoring and further MagicPath variants remain blocked by current tool quotas, not by a product decision. |

| UX-31 / 2026-11-24 | DECIDED — SUPERSEDES Figma-required workflow | Make Figma optional permanently for RBOS; designate docs/ux repository specifications as the approved design contract; use MagicPath only when available/useful; for a multi-screen group, run focused checks during implementation and defer full integrated Playwright/screenshots/Axe until every route in the named group is implemented. | User's explicit workflow decision in this conversation; see current playbook PR #75 and Billing & Sales handoff PR #67. | Preserves UX/accessibility quality while removing paid design-tool/quota dependency and verifies cross-screen journeys as one integrated experience. | DECIDED | Apply to this five-route Billing & Sales redesign and all future RBOS work. Do not retry Figma or block delivery on MagicPath agent quota. |

### 9.4 No silent leftovers

If an issue is noticed but not fixed now, add it to the deferral/parking-lot record before proceeding. If a stage is skipped because it is unnecessary or unavailable, record the reason, impact and revisit condition. If there is nothing deferred, explicitly write **No known deferred items**; never leave the section blank.

When a user changes an earlier instruction, mark the old decision SUPERSEDED, record the new instruction and ensure there are no contradictory active directions elsewhere in this programme.

### 9.5 Required handover at every pause

At every design gate, implementation gate, meaningful pause and phase closure, update:

1. Completed work and the evidence proving it.
2. Decisions made and their rationale/location.
3. Unfinished, deferred, blocked and unverified items, including why.
4. Explicit out-of-scope items.
5. Exact next action and prerequisites.
6. Relevant branch, PR, commit, MagicPath/Figma links and audit artifacts.
7. Automated tests, visual checks and real-user acceptance still pending.

A screen phase is not documentation-complete until its decision log, deferral list, validation evidence and handover are current. Code completion, visual acceptance, merge and production deployment are separate states.

## 10. Current authoritative handover — Billing & Sales (updated 2026-11-24)

> This section and docs/ux/BILLING-SALES-HANDOFF.md supersede older Figma-required instructions and earlier handover statuses in this document. Historic decision-log entries are retained as history, not as active instructions.

### 10.1 Agreed delivery model

- No blocker to continuing. Figma is permanently optional for RBOS. Do not upgrade, wait for quota reset, retry Figma MCP calls, or delete existing Figma files.
- Design source of truth: the versioned repository design specification in docs/ux/. MagicPath is for visual exploration/prototyping when available; it is not mandatory and its agent/API quota exhaustion does not block code-first specifications.
- Verification cadence for this five-route group: perform focused code checks while implementing each route; defer the comprehensive integrated Playwright journey, screenshot/visual review and Axe pass until all five routes have been redesigned and implemented. Fix findings and rerun affected coverage only.
- No screen-group merge or deployment is authorised by this decision. Keep implementation PRs draft/open until the agreed group-level verification and review gates are complete; merge and deployment remain separate approvals.

### 10.2 Screen group and route inventory

| Order | Route | Screen | Status |
|---|---|---|---|
| 1 | #/sell/bill | Create Invoice / bill entry, review and completion | Implementation candidate in PR #74; design specification still needs completion |
| 2 | #/sell/menu | Sell landing / action hub | Not redesigned |
| 3 | #/sell/bills | Sales History and historical invoice detail | Not redesigned |
| 4 | #/sell/collect | Payment collection / customer dues | Not redesigned |
| 5 | #/sell/return | Customer returns / cancellation journey | Not redesigned |

Keep the redesign bounded to these five routes and their necessary shared interactions. Do not expand into Stock, Purchasing, Home or unrelated shell redesign.

### 10.3 Current implementation and evidence

- PR #74: [action-first Create Invoice](https://github.com/Vaibhav1507/retail-business-erp/pull/74), branch ux/invoice-entry-action-first, known head 72cfe96dd8b361d77123d136f1f6f12ad6ac4768, open/draft and not merged at last inspection.
- PR #74 currently changes GenerateInvoice.tsx, App.tsx, invoice.css, ItemTagCapture.tsx, related unit/component tests and sales.spec.ts. It is the first implementation slice, not final design acceptance.
- Exact-head standard CI: [run #45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) passed on the known PR #74 head. Frontend TypeScript, tests, lint, build and dependency audit; backend Release build, tests and container build (without push); PostgreSQL integration bootstrap/build/tests passed.
- Pre-redesign five-route baseline: [Visual Audit #45316812990](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812990), [artifact #14885473196](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812990/artifacts/14885473196), on baseline commit fc974e4e16ca80a14bb71156f482f333372b4600. It captured 178 screenshots across 47 named states and the five routes at the baseline viewport matrix. It is baseline-only, not validation of PR #74 or the redesigned screens.
- Audit infrastructure: [PR #72](https://github.com/Vaibhav1507/retail-business-erp/pull/72) is open/draft and not merged at last inspection. Preserve its current branch/work; inspect the live PR state before any future action.
- Programme documentation and Billing & Sales handoff: [PR #67](https://github.com/Vaibhav1507/retail-business-erp/pull/67), branch docs/rbos-usability-accessibility-programme, open/draft at last inspection. The permanent playbook update is a separate documentation-only [PR #75](https://github.com/Vaibhav1507/retail-business-erp/pull/75), based on current main.
- Home: PRs #69, #70 and #73 are already merged. This Billing & Sales phase does not reopen Home.

### 10.4 Selected invoice direction and prototype

- Three concepts were explored; Concept A (action-first) was selected: one dominant Add Product action, contextual Scan Tag beside product description/model, and handwritten-bill import separate and quieter.
- Existing MagicPath prototype: [Invoice Entry — Action-first mobile prototype](https://designs.magicpath.ai/v1/cozily-dusk-6555), component 475857518220939264, revision 475857518220939265. It is a prototype, not a complete responsive design spec; camera/gallery is simulated and no real upload/OCR/API/posting/payment/print is connected.
- PR #74 implements part of the selected direction: focused invoice workspace; contextual Scan Tag; single primary Add Product action; separate handwritten-bill import; progressive disclosure for optional product details; sticky total/review area. Actual visual verification remains pending.
- MagicPath agent/API quota was exhausted at last check. Use its normal editor only if available and useful; otherwise continue with repository design specs.
- Figma file [RBOS Billing & Sales — Invoice Entry](https://www.figma.com/design/ACpAOSnl3aeIRl1Hxnllzf) exists, but no editable screen was authored or validated. Leave it untouched. Figma is optional and no longer part of acceptance criteria.

### 10.5 Business invariants — preserve

- Customer name and valid 10–15 digit mobile are required to post an invoice.
- Manual/ad-hoc product lines remain allowed when the catalogue has no suitable item; do not silently create catalogue records. Negative stock remains allowed.
- Serial/IMEI/barcode remain optional; retain duplicate/format checks and non-blocking missing-serial warning. IMEI validation retains its 15-digit Luhn rule.
- GST and authoritative totals remain server-calculated; Cash, UPI, Credit, split and partial payment semantics remain unchanged.
- Returns map to the original sale line and remaining returnable quantity. Accounting posting, inventory valuation, idempotency and audit trail remain unchanged.
- Offline held bills remain pending and must never be represented as posted. WhatsApp consent, opt-out, delivery state and duplicate protection remain intact.
- Invoice extraction remains an editable, unposted draft; it must never auto-post. Print output is the invoice document only, not the application shell.
- Preserve existing routes/API contracts and tenant/security boundaries unless separately documented and approved.

### 10.6 Design specification required for each route

Before implementing a route, its repository design note must define the task/persona; route entry/exit and complete journey; scope/non-goals/dependencies/invariants; screenshot- or source-backed findings; selected hierarchy and interactions; shared component/token reuse; small-phone, standard-phone, tablet, landscape and desktop rules; default/loading/empty/validation/error/retry/success/disabled/cancel/edit/detail/print states as applicable; accessibility and language behaviour; measurable acceptance criteria; and focused checks during implementation.

Repository design notes are the approved design contract. MagicPath is a supporting reference when available; Figma is optional.

### 10.7 Verification and release gates

**While implementing each route:** run focused TypeScript/build/unit/component tests and narrow checks for changed behaviour. Record these as implementation checks, not final visual acceptance.

**After all five routes are redesigned and implemented:** run one integrated Playwright pass across all five routes and shared journeys (invoice create/review/post/print, history/detail, collections/payment, returns/cancellation and cross-navigation); cover key loading/empty/validation/error/retry/success/cancel/edit/held/offline/print states; cover small phone, standard phone, tablet, landscape and desktop; capture viewport and full-page screenshots including scroll, keyboard/focus and fixed/sticky/overlay cases; assert outcomes, semantic names/roles, console/page errors, failed requests, overflow/clipping and touch targets; run Axe WCAG 2 A/AA on important states and review all screenshots. Treat print media separately.

Fix issues from the group pass and rerun affected coverage and necessary regressions. Then perform native Android camera/gallery, TalkBack, share-sheet, printer/print-dialog, keyboard and real-device scrolling checks, followed by representative shopkeeper acceptance. Automated pass, visual acceptance, Android acceptance, user acceptance, merge and deployment are separate statuses.

### 10.8 Exact next actions

1. Refresh PR #67/#72/#74 state, branch heads and workflow runs from GitHub. Read this handoff and current main playbook.
2. Complete the repository design specification for Create Invoice (route #/sell/bill) using the selected action-first direction and PR #74 as an implementation candidate. Do not rerun the 178-screenshot baseline and do not make Figma a dependency.
3. Then continue in order: Sell landing (#/sell/menu), Sales History (#/sell/bills), Collections (#/sell/collect), Returns (#/sell/return). Audit and document each route before its implementation.
4. Inspect branch ancestry and choose a coherent implementation lineage; keep all changes draft/open until the five-route group verification. Do not merge or deploy.
5. At each pause, update docs/ux/BILLING-SALES-HANDOFF.md and this programme with exact route progress, design decisions, code branch/PR/head, focused checks, blockers and next action.

### 10.9 Outstanding items

- Not complete: repository design spec for Create Invoice; design specs and implementation for the other four routes; final combined five-route Playwright/screenshot/Axe verification; native Android checks; shopkeeper acceptance; merge; production deployment.
- Optional/not required: editable Figma. Existing file remains untouched.
- Tool limitation, not a project blocker: MagicPath connected agent/API quota was exhausted at last check. Use normal editor only if available; otherwise continue code-first.
- No known product decision blocks continuation.
