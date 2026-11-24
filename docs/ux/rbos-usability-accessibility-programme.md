# RBOS Usability & Accessibility Programme

- **Status:** IN_PROGRESS — source baseline documented; first route audit reviewed; exhaustive Billing & Sales capture PR #72 is awaiting its CI run and evidence review
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

### 4.8 Expanded-audit harness attempts — 24 November 2026

| Run | Head | Result | Evidence / correction |
|---|---|---|---|
| [45301760436](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45301760436) | `4e87e2b` | FAILED — harness | The test installed a second dialog handler while `signup.ts` already owned one. The helper attempted to accept a dialog already handled by the other listener. The test stopped during the first invoice; no visual artifact was uploaded. |
| [45302085474](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302085474) | `b69b669` | FAILED — harness | Same dialog-ownership failure. Removing listeners from the test did not reliably replace the helper's active handler. |
| [45302508735](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302508735) | `7d3feac` | IN_PROGRESS at last check | Introduced a tracked dialog-handler replacement; verify result before relying on it. |
| [45302584156](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302584156) | `84d7643` | IN_PROGRESS at last check | Switched to the documented listener-removal API; superseded by the single-listener design below. |
| [45302914826](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45302914826) | `db9f0b5` | IN_PROGRESS at last check | Current implementation keeps exactly one dialog listener per page and changes only its active policy. This is the run to inspect first. |

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

For significant decisions, include alternatives considered and why they were not selected. Link relevant PRs, commits, screenshots, audit artifacts, issues and design files.

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

## 10. Current phase handover — Billing & Sales

- **Completed:** initial source-code audit and route map; first route-level artifact downloaded and all 10 screenshots inspected (five routes × mobile/tablet); report.json reviewed.
- **Verified:** run #2 `45299902320` completed; artifact ID `14878326785`; its captured states reported zero horizontal overflow, unnamed controls, undersized controls and Axe WCAG 2 A/AA violations.
- **Verified finding:** the mobile Sales History screenshot captured `Loading…`, while the tablet screenshot showed `No bills found` because the test did not seed a sale. Therefore the green run did not prove a loaded history screen.
- **Verified capture limitation:** the first audit captured only initial route states; it did not exercise bill review/post, dialogs, extraction, payment, return or print flows. Full-page screenshots also place fixed navigation at its viewport coordinate, so scroll-position captures are needed to review overlays accurately.
- **Decided:** retain the existing Home audit as a separate scope; add a dedicated deterministic Billing & Sales suite, seed synthetic data, mock AI extraction and make accessibility/layout checks fail CI. No Product Design plugin; MagicPath and Figma remain the next design stages after audit acceptance.
- **IN_PROGRESS:** PR #72 (`test/billing-sales-visual-audit-coverage`) adds the interaction suite and PR-triggered workflow. The early failures were harness dialog-ownership defects; the current single-listener correction is running in visual audit #45302914826. Its CI and complete visual artifact are not yet accepted.
- **Not started:** MagicPath exploration, editable Figma design and Billing & Sales application implementation.
- **Out of scope:** unrelated Home redesign, Stock, Purchasing, global navigation and backend/business-rule changes unless verified as a dependency.
- **Uncovered until evidence is added/reviewed:** WhatsApp delivery/status variants beyond the seeded state; collection allocation to a specific bill; extraction failure/recovery; real Android camera permission, printer/share sheet and TalkBack; browser-native confirmation is asserted by dialog text but cannot be captured as a page screenshot.
- **Existing findings:** BILL-01…BILL-12 remain source-based findings awaiting current-state visual verification and re-prioritisation; do not silently close them based on code inspection alone.
- **Next action:** review PR #72's latest visual run `45302914826` (and CI run `45302914622`); inspect the complete screenshot/report artifact, resolve harness or UI findings and close the remaining coverage gaps before MagicPath.
- **Documentation branch:** docs/rbos-usability-accessibility-programme (PR #67). Keep related UX programme documentation updates on this existing branch/PR; PR #72 is a separate, justified test/workflow implementation PR.
