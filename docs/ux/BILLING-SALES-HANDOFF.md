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
| 1 | #/sell/bill | Create Invoice, review and completion | Concept A selected; repository design specification still needs completion | Candidate in PR #74 |
| 2 | #/sell/menu | Sell landing/action hub | Not started | Not started |
| 3 | #/sell/bills | Sales History and historical invoice detail | Not started | Existing baseline only |
| 4 | #/sell/collect | Payment collection/customer dues | Not started | Existing baseline only |
| 5 | #/sell/return | Customer returns/cancellation | Not started | Existing baseline only |

Keep scope to these five routes and necessary shared interactions. Home is already redesigned/merged; do not reopen it. Stock, Purchasing, unrelated shell redesign and backend/domain changes are out of scope unless a proven dependency requires a separately documented decision.

## 3. PRs and evidence — refresh before relying

- **PR #74 — [action-first Create Invoice](https://github.com/Vaibhav1507/retail-business-erp/pull/74)**. Branch: ux/invoice-entry-action-first. Known head: 72cfe96dd8b361d77123d136f1f6f12ad6ac4768. Last inspected: open/draft, not merged. It hides persistent main navigation and Navigation Assistant on the active invoice route; puts Scan Tag beside product entry; keeps Add Product as the sole primary item action; separates handwritten-bill import; progressively discloses optional Brand/Model/Serial; and provides sticky total/review action. No API/business-rule changes intended.
- **PR #74 exact-head CI:** [run #45340114617](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45340114617) passed the known head: frontend TypeScript/tests/lint/build/dependency audit; backend Release build/unit tests/container build (no push); PostgreSQL integration bootstrap/build/tests. Refresh run/head.
- **PR #72 — [Billing & Sales visual audit infrastructure](https://github.com/Vaibhav1507/retail-business-erp/pull/72)**. Last inspected: open/draft, not merged. Refresh status.
- **PR #67 — [UX programme and Billing & Sales handoff](https://github.com/Vaibhav1507/retail-business-erp/pull/67)**. Branch: docs/rbos-usability-accessibility-programme; open/draft and mergeable at last inspection. The current handoff has since received another documentation commit, so fetch the latest branch head and workflow status before relying on it. It contains the living programme and this handoff. **PR #75 — [Figma-optional playbook update](https://github.com/Vaibhav1507/retail-business-erp/pull/75)**, branch docs/rbos-figma-optional-workflow; latest inspected head 442dd452d5d4c6c13f6cbb79288a99583e569f35, open/draft, mergeable. Documentation CI run #45343373924 was in progress at handoff. Neither PR is merged; the changes are not on main until merged. Refresh both PR heads/CI.
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

1. Read current main docs/ux/RBOS-UI-UX-WORKFLOW.md, this handoff, the living programme and route design notes. Inspect current code, screenshots and branches.
2. Refresh PR #67/#72/#74 status, exact heads and workflow runs. Verify this documentation update is present on PR #67.
3. Complete the Create Invoice repository design spec for #/sell/bill: layout hierarchy, actions, small/standard phone, tablet, landscape and desktop; important states; token/component reuse; validation/feedback; accessibility; acceptance criteria.
4. Compare PR #74 with the spec; keep it draft/open and do not merge.
5. Continue in this order: #/sell/menu, #/sell/bills, #/sell/collect, #/sell/return. Audit dependencies and document each route before implementing it. Make routine design decisions; ask only for a genuine product decision or permission blocker.
6. Inspect branch ancestry and choose a coherent implementation lineage. Keep changes reviewable and draft until the five-route group verification.
7. After all five redesigned routes are implemented, run the combined Playwright journeys, responsive screenshots, visual review and Axe described in the workflow/programme. Review artifacts, fix defects and rerun affected coverage.
8. Perform native Android checks and real shopkeeper review. Update this handoff and the living programme with exact evidence. Do not merge/deploy without explicit approval.

## 7. Status ledger

| Item | Status | Evidence / next action |
|---|---|---|
| Five-route baseline audit | Done | Visual Audit #45316812990 / artifact #14885473196 |
| Invoice concept exploration | Done | MagicPath Concept A selected |
| Invoice interaction prototype | Done, prototype only | MagicPath link above |
| Invoice repository design spec | Next / incomplete | Complete before proceeding to route 2 implementation |
| PR #74 invoice implementation | Candidate implemented | Draft PR; CI passed known head; final group verification pending |
| Sell landing | Not started | Second route |
| Sales History | Not started | Third route |
| Collections | Not started | Fourth route |
| Returns | Not started | Fifth route |
| Full 5-route Playwright/screenshot/Axe | Deferred by agreed cadence | Run after all five redesigned and implemented |
| Native Android/shopkeeper acceptance | Not started | After group browser verification |
| Merge/production deployment | Not authorised | Separate explicit approval |
| Figma | Optional/not required | Existing file untouched |
| MagicPath agent/API | Tool-limited at last check | Continue code-first; normal editor only if available |

**No known product decision blocks continuation.** Update this handoff and the relevant route design note at every meaningful pause so another chat can resume from repository state.
