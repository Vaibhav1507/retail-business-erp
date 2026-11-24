# RBOS Billing & Sales — Working Handoff

**Last updated:** 2026-11-24  
**Repository:** [Vaibhav1507/retail-business-erp](https://github.com/Vaibhav1507/retail-business-erp)  
**Workflow:** [RBOS-UI-UX-WORKFLOW.md](./RBOS-UI-UX-WORKFLOW.md)  
**Living UX programme:** [rbos-usability-accessibility-programme.md](./rbos-usability-accessibility-programme.md)

> Continuation point for a new ChatGPT conversation. Refresh PR/branch/head details from GitHub before acting. Current repository docs and code outrank chat memory.

## 1. Agreed workflow

- RBOS is **Figma-optional**. Do not upgrade, wait for quota resets, retry Figma MCP or delete existing Figma files. Figma is not an acceptance or delivery gate.
- The versioned repository design specification under docs/ux/ is the authoritative design contract: layout, hierarchy, components, reuse of existing tokens, responsive rules, copy, interactions, important states, accessibility and acceptance criteria.
- MagicPath is optional visual exploration. Use its normal editor only when available and useful. Its connected agent/API quota exhaustion does not block delivery; continue code-first.
- This is one connected five-route redesign. Run focused checks as each route is implemented to avoid accumulating broken code. Run the **full integrated Playwright, responsive screenshots, visual review and Axe verification only after all five routes have been redesigned and implemented**.
- Fix any defects from the final pass and rerun affected coverage plus necessary regression tests. Do not rerun the whole audit without a reason.
- Native Android and shopkeeper acceptance follow browser verification. Automated tests, visual review, device/user acceptance, merge and deployment are separate gates.
- No merge or production deployment is authorised by this handoff.

## 2. Five-route scope and status

| Order | Route | Screen | Design | Implementation |
|---|---|---|---|---|
| 1 | #/sell/bill | Create Invoice, review and completion | **Completed:** [Create Invoice design specification](./billing-sales-create-invoice-design.md); PR #74 compared | Candidate in PR #74; focused CI passed; group visual acceptance pending |
| 2 | #/sell/menu | Sell landing/action hub | **Completed:** [Sell landing design specification](./billing-sales-sell-landing-design.md) | Not started |
| 3 | #/sell/bills | Sales History and historical invoice detail | **Completed:** [Sales History design specification](./billing-sales-sales-history-design.md) | Existing baseline only |
| 4 | #/sell/collect | Payment collection/customer dues | Not started | Existing baseline only |
| 5 | #/sell/return | Customer returns/cancellation | Not started | Existing baseline only |

Keep scope to these five routes and necessary shared interactions. Home is already redesigned/merged; do not reopen it. Stock, Purchasing, unrelated shell redesign and backend/domain changes are out of scope unless a proven dependency requires a separately documented decision.

## 3. PRs and evidence — refresh before relying

- **PR #74 — [action-first Create Invoice](https://github.com/Vaibhav1507/retail-business-erp/pull/74)**. Branch: `ux/invoice-entry-action-first`. Refreshed head: `72cfe96dd8b361d77123d136f1f6f12ad6ac4768`. Open/draft, not merged. It hides persistent main navigation and Navigation Assistant on the active invoice route; puts Scan Tag beside product entry; keeps Add Product as the sole primary item action; separates handwritten-bill import; progressively discloses optional details; and provides sticky total/review action. Exact-head standard CI #45340114617 is completed/success. The implementation is a candidate; visual acceptance remains pending.
- **PR #74 exact-head CI:** [run #45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) passed the known head: frontend TypeScript/tests/lint/build/dependency audit; backend Release build/unit tests/container build (no push); PostgreSQL integration bootstrap/build/tests. Refresh run/head.
- **PR #72 — [Billing & Sales visual audit infrastructure](https://github.com/Vaibhav1507/retail-business-erp/pull/72)**. Last inspected: open/draft, not merged. Refresh status.
- **PR #67 — [UX programme and Billing & Sales handoff](https://github.com/Vaibhav1507/retail-business-erp/pull/67)**. Branch: `docs/rbos-usability-accessibility-programme`; open/draft. Latest documentation commit before this handoff update: `d91b891dea84031a6253410d0dbe8ffce1907dd3` (programme update); handoff is being updated in the same branch. Exact-head CI #45343697012 passed the previous head `fe6f8ca835e94c7404dfbdb51c0e5c51e2cc4902`; refresh CI after the latest documentation commit. **PR #75 — [Figma-optional playbook update](https://github.com/Vaibhav1507/retail-business-erp/pull/75)** is merged; the current main playbook has been fetched and read.
- **Pre-redesign baseline only:** [Visual Audit #45316812990](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812990), artifact [#14885473196](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45316812990/artifacts/14885473196), baseline commit fc974e4e16ca80a14bb71156f482f333372b4600. Captured 178 screenshots across 47 named states and all five routes. The report found zero page errors, horizontal overflow, unnamed controls, undersized controls and Axe violations in captured screen states. This predates PR #74 and is not redesigned-UI verification. Do not rerun to recreate baseline.
- Home PRs #69, #70 and #73 are merged; not part of this group.

## 4. Invoice design direction and prototype

- User feedback: Add Tag Photo wrapped to another line; competing prominent product actions were confusing; the user wanted a camera/gallery chooser instead of extra buttons. Other requirements: mandatory customer name/mobile, field-level validation, optional serial/barcode/IMEI, consistent extraction values and mobile/print usability.
- Three concepts were explored; **Concept A — action-first** was selected: one dominant Add Product action, contextual Scan Tag by product description/model and handwritten-bill import separate/quieter.
- Existing prototype: [Invoice Entry — Action-first mobile prototype](https://designs.magicpath.ai/v1/cozily-dusk-6555), component 475857518220939264, revision 475857518220939265. This is a prototype only: camera/gallery selection is simulated; native permission, file picker, upload/OCR, customer/product lookup, posting, payment and print are not connected.
- MagicPath connected agent/API was at its quota ceiling when last checked. Do not promise agent-driven changes or repeatedly retry. If its normal editor is unavailable, use repository design specifications.
- Figma file [RBOS Billing & Sales — Invoice Entry](https://www.figma.com/design/ACpAOSnl3aeIRl1Hxnllzf) exists but no editable invoice screen was authored/validated. Leave untouched; Figma is optional.
- PR #74 is based on the selected direction, not a complete approved design spec. Complete the Create Invoice repository design spec, compare PR #74 against it, and record design/code differences before moving to route 2.

## 5. Business invariants — preserve

- Customer name and valid 10–15 digit mobile required to post an invoice.
- Manual/ad-hoc lines allowed when catalogue match is absent; do not silently create catalogue records. Negative stock remains allowed.
- Serial/IMEI/barcode optional; retain duplicate/format checks and non-blocking missing-serial warning. IMEI retains 15-digit Luhn validation.
- GST and authoritative totals server-calculated. Cash/UPI/Credit/split/partial payment semantics unchanged.
- Returns map to original sale lines and remaining returnable quantity. Preserve posting, inventory valuation, idempotency and accounting/audit trail.
- Offline held bills remain pending, never posted. Preserve WhatsApp consent/opt-out/delivery status/duplicate protection.
- Invoice OCR remains an editable, unposted draft; never auto-post. Print output is the invoice only, not app shell.
- Preserve APIs/routes/tenant and security boundaries unless a separate documented decision approves a change.

## 6. Next actions for the next chat

1. Audit Sell landing `#/sell/menu` from current `Pages.tsx`, route/navigation entry points and translations; create its repository design specification before implementation.
2. Continue in order: Sales History `#/sell/bills`, Collections `#/sell/collect`, Returns `#/sell/return`. Audit and document each route before implementation.
3. Inspect branch ancestry and choose a coherent implementation lineage for all five routes. Keep PRs open/draft until group verification; do not merge or deploy.
4. Run focused checks during implementation. After all five routes are redesigned and implemented, run the single integrated Playwright journey, responsive screenshot, visual review and Axe pass; inspect actual artifacts and fix findings.
5. Perform native Android checks and representative shopkeeper acceptance after browser verification. Keep these separate from automated CI, merge and deployment.

## 7. Status ledger

| Item | Status | Evidence / next action |
|---|---|---|
| Five-route baseline audit | Done | Visual Audit #45316812990 / artifact #14885473196 |
| Invoice concept exploration | Done | MagicPath Concept A selected |
| Invoice interaction prototype | Done, prototype only | MagicPath link above |
| Invoice repository design spec | **Done** | [billing-sales-create-invoice-design.md](./billing-sales-create-invoice-design.md); compared with PR #74 |
| PR #74 invoice implementation | Candidate implemented | Draft PR; CI passed known head; final group verification pending |
| Sell landing | Design done; implementation next | Second route |
| Sales History | Design done; implementation not started | Third route |
| Collections | Design done; implementation not started | Fourth route |
| Returns | Design done; implementation not started | Fifth route |
| Full 5-route Playwright/screenshot/Axe | Deferred by agreed cadence | Run after all five redesigned and implemented |
| Native Android/shopkeeper acceptance | Not started | After group browser verification |
| Merge/production deployment | Not authorised | Separate explicit approval |
| Figma | Optional/not required | Existing file untouched |
| MagicPath agent/API | Tool-limited at last check | Continue code-first; normal editor only if available |

**No known product decision blocks continuation.** Next concrete deliverable is the Sell landing design specification. Update this handoff and the relevant route design note at every meaningful pause so another chat can resume from repository state.


## 8. Execution update — 2026-11-24: Create Invoice design gate completed

- **Repository design specification:** [billing-sales-create-invoice-design.md](./billing-sales-create-invoice-design.md) is complete and authoritative for `#/sell/bill`. It includes layout, actions, interactions, states, responsive rules, component/token reuse, accessibility and measurable acceptance criteria.
- **PR #74 comparison:** refreshed source head `72cfe96dd8b361d77123d136f1f6f12ad6ac4768`; aligned with Concept A for focus mode, contextual Scan tag, single Add product action, separate handwritten import, progressive disclosure and sticky summary. One focused follow-up is to verify the mobile required marker and programmatic error association. No full visual review has been run.
- **PR #74 CI:** exact-head run [#45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) passed. Full five-route Playwright/screenshots/Axe remain deferred.
- **PR #75:** merged; Figma-optional workflow is on main. Do not retry Figma. MagicPath remains optional.
- **PR #67:** open/draft. Programme update commit `d91b891dea84031a6253410d0dbe8ffce1907dd3`; handoff update follows in this commit. CI #45343697012 passed the prior head `fe6f8ca835e94c7404dfbdb51c0e5c51e2cc4902`; refresh CI on the resulting branch head.
- **PR #72:** open/draft, head `fc974e4e16ca80a14bb71156f482f333372b4600`; preserve existing audit infrastructure.
- **Next:** Sell landing `#/sell/menu` source audit and design specification. Then Sales History, Collections and Returns in the agreed order.
- **No merge/deployment:** neither is authorised. Native Android and shopkeeper acceptance remain pending.


## 9. Execution update — 2026-11-24: all five design specifications complete

- Repository design contracts are complete for Create Invoice, Sell landing, Sales History, Collections and Customer Returns. See the five linked route notes in section 2 and the living programme.
- Create Invoice was compared against PR #74. PR #74 remains open/draft and its exact-head CI #45340114617 passed. Visual acceptance is deferred to the group gate.
- Implementation begins with Sell landing `#/sell/menu`, then proceeds Sales History, Collections and Customer Returns in order.
- The shared component/data dependencies and existing business invariants are documented in each route specification. No API or accounting change is included in the design contracts.
- PR #75 is merged. PR #67 remains open/draft; refresh its current head and CI after this documentation update.
- PR #72 remains open/draft; preserve its audit infrastructure and the existing 178-screenshot baseline artifact. Do not recreate the baseline.
- Full integrated Playwright, responsive screenshot review and Axe are deferred until all five routes are implemented. Focused checks are required during each implementation slice.
- No merge or deployment is authorised. Native Android and representative shopkeeper acceptance remain pending.


## 2026-11-24 handoff checkpoint

### Scope and implementation
- Five routes are implemented in PR #74: Create Invoice, Sell landing, Sales History, Collections and Customer Returns.
- Current PR #74 head: `b5d4236a8c45c552fbb034d328d1b7dd20a8c5a8` on `ux/invoice-entry-action-first`.
- PR #72 was merged to main as `87b1c6892e7a1ad2251d207ab463a20c72f8067f`; the visual-audit workflow and shared accessibility improvements are integrated into the PR #74 branch through merge commit `50185a1a7bfc65a78ca3b626fb5efa0de9ba25d5`.
- The audit journey was updated to use the redesigned Sell hub, invoice tag-photo source picker, mobile/desktop Sales History cards/table, Collections form, Customer Returns flow and in-app whole-sale cancellation confirmation.

### Verification status at this checkpoint
- Frontend: TypeScript, **207 tests / 22 files**, lint, build and dependency audit passed on [CI run #45359742851](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45359742851).
- Backend build/unit tests and PostgreSQL integration were still running when this checkpoint was written.
- Full five-route Playwright/Axe run: [#45359742608](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45359742608), still running.
- PR #72's 178-screenshot pre-redesign run #45316812990 remains the historical baseline; do not repeat it. Only the current post-redesign run can support group visual acceptance.
- PR #74 remains draft/open and has not been merged. No deployment has been performed.

### Immediate next actions
1. Finish the exact-head standard CI and integrated visual/Axe run.
2. Download and inspect the current audit report/contact sheets; fix any functional, responsive, overflow, touch-target or Axe failures and rerun affected coverage.
3. If all gates pass, mark PR #74 ready and merge it; do not deploy without separate approval.
4. Keep native Android camera/gallery permissions, printer/share sheet, TalkBack and shopkeeper acceptance as separate post-merge checks.


## 10. Execution update — 2026-11-24: four-screen screenshot-led final polish

### Scope clarified
- The remaining Sales scope is exactly four screens: Sell hub `#/sell/menu`, Sales history `#/sell/bills`, Collect payment `#/sell/collect`, and Customer returns `#/sell/return`.
- Home and Generate Invoice are established references only. Generate Invoice is `#/sell/bill`; it is not part of this final polish scope.
- Current main-branch route wiring was verified in `web/src/Pages.tsx`. Current implementations inspected: `SalesHistory.tsx`, `Collections.tsx`, `CustomerReturns.tsx` and their route-scoped CSS.
- PR #74 is now **merged** to main (merge commit `c4d71190b69a581dae389d90bcd1e4e9e58f3f00`, merged 2026-11-24 09:03 UTC). Its five-route implementation is the current code baseline. The old statements above that PR #74 was open/draft are historical checkpoints and superseded by this update.
- PR #67 remains open/draft on `docs/rbos-usability-accessibility-programme`, head observed before this update: `9b806cf6a37ca4352c24000e2069d6332b29340e`. Refresh exact PR head/CI and mergeability after documentation commits.

### Final polish contract
- Added [Four-screen final polish specification](./billing-sales-four-screen-final-polish.md) to this PR #67 branch.
- Sell hub: retain the current action-first layout; spacing/alignment polish only.
- Sales history: flatten nested result panels/cards, make result count compact, keep mobile sale summaries scannable, and prevent the fixed Assistant launcher from obscuring balances or View bill actions without redesigning the global Assistant.
- Collect payment: compact the initial customer selection and empty-history states; preserve the guided outstanding → payment details → record payment sequence; keep statement on demand.
- Customer returns: retain the original-bill-first journey; make return history a compact, clearly associated disclosure; keep full-sale cancellation separate and destructive.
- Preserve existing routes, APIs, server-derived balances/totals, allocation, idempotency, WhatsApp consent/retry, returns/cancellation accounting, stock valuation, tenant/security and print semantics.
- Research references and rationale are recorded in the new specification. Competitor patterns are references only; RBOS APIs and business invariants remain authoritative.

### Evidence and status
- Reviewed the supplied Android screenshots for all four screens. These are screenshot findings, not proof of the underlying states beyond what is visible.
- Read current `main` workflow and route wiring; read existing route design specifications from the PR #67 documentation branch; inspected current `main` implementation files.
- PR #74's merge makes the five-route implementation present on main. The screenshots now identify a small final-polish gap, not a reason to reopen the overall information architecture.
- **Design:** final polish decisions documented.
- **Implementation:** not yet started for this four-screen polish.
- **Focused tests:** not run for these polish changes because no code has changed.
- **Final integrated Playwright/screenshots/Axe:** pending until all four polish changes are implemented.
- **Android visual review:** supplied screenshots reviewed; updated screenshots required after implementation.
- **Merge/deployment:** no authorisation implied. PR #67 is documentation only; code polish must use one canonical implementation branch/PR after refreshing current GitHub state.

### Next concrete action
Refresh current open branches/PRs and CI after this handoff update, then create or continue exactly one implementation branch from current main for the four-screen polish. Implement the Sell hub light polish, Sales history hierarchy/Assistant clearance, Collections compact states and Customer Returns history disclosure in that order. Run focused checks as changes land, then perform the agreed four-screen integrated Playwright/screenshot/Axe review and request fresh Android screenshots from the user.


### Implementation update — PR #80
- Created one clean implementation branch from current main: `ux/billing-sales-four-screen-polish`.
- PR [#80 — four-screen Sales mobile polish](https://github.com/Vaibhav1507/retail-business-erp/pull/80) is open/draft. It is based on main commit `1158e049e135f19586fc0eee15b7d7e81660e983` and currently changes only:
  - `web/src/sales-history.css`
  - `web/src/collections.css`
  - `web/src/customer-returns.css`
- Sell hub is intentionally unchanged because the supplied screenshot already meets the approved action-first design. No unnecessary redesign was added.
- PR #80 head at creation: `f4ee4fd19000e95d0459edead3b662f4280c9877`. CI and visual screenshots must be refreshed/checked against this exact head.
- PR #80 remains draft; no merge or deployment.
