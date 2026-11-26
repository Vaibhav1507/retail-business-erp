# RBOS UI/UX design and implementation playbook

**Purpose:** Repeat the same end-to-end, design-led process whenever a screen or user journey in RBOS needs a meaningful usability improvement. This is the operating procedure for Home, Billing & Sales, and later modules.

**Product context:** RBOS is an India-first retail business app for small shopkeepers, including people with limited digital confidence. Default to simple language, mobile-first layouts, visible actions, clear feedback, accessible controls and minimal cognitive load. The electronics-shop pilot is the current proving ground, but avoid hard-coding electronics-only assumptions into shared UX.

**Primary tools:** ChatGPT as orchestrator; GitHub for source, tests, branches, PRs and CI; MagicPath for visual exploration/prototyping when its editor is available; the RBOS repository design specification as the authoritative design contract; React as implementation truth; and Playwright as the end-to-end, screenshot, interaction and visual-regression engine, together with Axe accessibility checks. Figma is optional and is not a delivery gate. Use web research when current conventions, standards, platform behaviour or competitor patterns need verification. Do not use the Product Design plugin/skill for RBOS.

---

## 1. The exact method followed for Home

1. **Established a strict screen boundary.** Home was the only implementation scope. Billing & Sales and the rest of the modules were explicitly kept out until Home had a usable direction.
2. **Audited the existing page and shell.** We reviewed the existing Home structure, route links, dashboard/reminder APIs, global navigation, assistant launcher, Business Settings, translations and existing styles. We preserved existing routes and business semantics rather than redesigning the whole app shell.
3. **Looked at the mobile baseline.** The earlier Home visual audit was used as a baseline. It reported no horizontal overflow, no axe WCAG 2 A/AA violations, no small touch targets below 44px and no unnamed controls. This was a baseline of the previous screen, not proof that the redesigned Home passed.
4. **Defined the product direction.** We chose an action-first shop dashboard: one dominant “Create new bill” action, four secondary actions, four business metrics and a compact Cash & bank strip. We deliberately removed the old equal-weight set of nine module shortcuts and did not add more analytics to Home.
5. **Explored the design in MagicPath.** We used it to make the direction tangible and review its hierarchy and layout before implementation.
6. **Formalized the direction in Figma.** We created an editable mobile-first Home design, reusable quick-action and metric components, a navigation-item component and a core token collection.
7. **Documented the design decision.** The repository received a Home design note describing the page structure, preserved APIs/routes, responsive/accessibility constraints and review checklist.
8. **Implemented in a focused branch and PR.** PR #69 implemented the action-first Home, continued using live dashboard values, preserved existing routes and shell, added English/Hindi copy, handled loading/error/retry and reminders, and added scoped responsive styles and tests.
9. **Reviewed the actual Android result, not just the mockup.** The user shared screenshots and identified practical issues: Assistant launcher obscured a metric, supplier wording was too long, helper text was unclear, reminder grammar was awkward and the mobile header was taller than needed.
10. **Converted screenshot feedback into a small polish pass.** PR #70 moved the Home Assistant launcher into page flow on mobile while retaining its expanded panel, shortened labels/helper copy, corrected singular/plural reminder wording in both languages, compacted the mobile header, updated tests and revised documentation.
11. **Validated and closed the implementation phase.** PR #69 and PR #70 were merged. PR #70 CI passed frontend tests, TypeScript, lint, build, dependency audit, backend build/unit tests and PostgreSQL integration tests. The fresh post-polish visual audit and live shopkeeper review remain follow-up checks; do not claim they were completed.

References:
- [PR #69 — action-first Home redesign](https://github.com/Vaibhav1507/retail-business-erp/pull/69)
- [PR #70 — screenshot-driven Home polish](https://github.com/Vaibhav1507/retail-business-erp/pull/70)
- [Figma — RBOS Home](https://www.figma.com/design/F5g0huBlQ1fYTE38HWPeTy)
- [MagicPath — Home prototype](https://designs.magicpath.ai/v1/warmly-star-1345)
- [Home design decision and implementation record](./home-action-first-design.md)

---

## 2. Required workflow for every future UI/UX task

### Stage A — Understand and constrain the problem

Before proposing a visual solution:

- Identify the exact screen(s), route(s), subflows and user task being improved.
- State the target user, their likely context (especially phone use, shop-counter conditions, time pressure and digital confidence), and the main job they are trying to complete.
- Inspect the current implementation, route map, API calls, data models, existing components, design tokens, translations, validation, errors, loading/empty states, tests and dependencies.
- Trace upstream/downstream dependencies. Determine what is safe to change in the UI and what must remain unchanged in backend contracts or accounting/business behaviour.
- Inspect existing screenshots, recordings and audit artifacts. Capture a baseline at realistic viewport sizes when possible.
- Separate verified facts from assumptions. Research current platform/industry patterns and accessibility standards when that research could change the recommendation.
- Agree a bounded scope and explicit non-goals. Do not redesign neighbouring modules by accident.

**Output:** concise UX problem brief, affected routes/flows, dependency notes, baseline issues and measurable acceptance criteria.

### Stage B — Diagnose before designing

- Map the current user journey from entry point to successful completion, including validation, cancellation, retry and failure paths.
- Identify friction: competing actions, clutter, ambiguous labels, repeated steps, hidden state, avoidable typing, poor hierarchy, mobile overflow, poor feedback, accessibility or trust concerns.
- Prioritise findings by user impact and severity (P0/P1/P2 or Critical/High/Medium); distinguish must-fix behaviour from visual preferences.
- Recommend the simplest flow that solves the task. Do not add AI, automation, new screens or extra steps unless there is a clear user benefit.

**Output:** prioritised problem list, target journey and design principles for the screen.

### Stage C — Explore in MagicPath

- Prepare a clear design brief from verified problem findings, not a vague prompt such as “make it modern”.
- Use MagicPath to explore a small number of materially different layout/interaction directions where a genuine design choice exists.
- Use the current RBOS palette, type scale, spacing, controls and brand language unless there is evidence they are part of the problem.
- Evaluate each direction against the target task, number of taps, information hierarchy, phone ergonomics, content density, readability and accessibility.
- Select and explain one recommended direction. Do not keep cycling through concepts after a direction has been selected.

**Output:** tangible prototype and concise design decision explaining why the selected direction is best.

### Stage D — Finalize the design (Figma-optional)

**The repository design specification is the authoritative design source of truth. Figma is optional and is not a project or delivery gate.** RBOS must not require a paid Figma plan, editor seat, quota reset or Figma MCP availability to complete a redesign.

- When MagicPath's normal editor is available, use it to refine the selected visual direction and interaction prototype. Do not assume its connected agent/API is available; if it is quota-limited, record that and continue code-first.
- When MagicPath is unavailable or adds no further value, finalize the design directly in the repository design note. Use annotated layouts, state tables, responsive rules, content examples and (where useful) Mermaid or simple diagrams.
- Define reusable components and variants, design tokens (colour, typography, spacing, radius, border, elevation and interaction states), responsive behaviour and all important states.
- Cover default, loading, empty, success, validation/error, disabled, long text, required languages and relevant edge cases.
- Keep the design aligned with existing RBOS components and realistic backend data. Mark proposed API/data changes separately.
- Do not retry known Figma quota/paywall failures. Existing Figma files may remain untouched; do not delete them or claim they are current/approved designs.

**Output:** a complete, reviewable repository design specification, with a MagicPath prototype/reference when available. Figma links may be recorded as optional references only; no editable Figma file is required for approval or delivery.

### Stage E — Record the decision in the repository

Before implementation, add or update a design note under docs/ux/ containing:

- Problem statement and target user/task.
- Scope, non-goals, affected routes and dependencies.
- Current-state evidence and prioritised findings.
- Selected direction and rejected/alternative options (briefly).
- MagicPath prototype/link when available; Figma reference is optional and never required.
- Screen structure, interaction rules, content/copy decisions and state handling.
- Accessibility/responsive rules and acceptance criteria.
- Risks, assumptions and decisions that must not change.
- Implementation plan and test/visual audit plan.

Keep the design note current as decisions or implementation change. The repository design note is the approved design contract and must be sufficient for a new developer/chat to implement and verify the screen without opening Figma. Do not leave stale claims or broken reference links.

### Stage F — Implement safely

- Create a focused feature branch and PR for the agreed screen scope.
- Implement the approved design; do not quietly substitute a different layout during coding.
- Preserve route semantics, API contracts, tenant/security boundaries, accounting/business rules and existing workflows unless a separately analysed change is explicitly included.
- Prefer existing components and dependencies. Do not add a library just to solve a small styling problem.
- Keep CSS/components scoped to the target screen where possible; avoid global regressions.
- Implement all agreed states and accessible labels/semantics, keyboard focus and touch targets (minimum 44×44 CSS px unless platform constraints require a documented exception).
- Make all required languages consistent; avoid awkward truncation and validate long translated strings.
- Add/update unit, component and integration tests for behaviour, not just snapshots.
- Update documentation and PR description with design links, scope, behaviour changes, tests and known limitations.

### Stage G — Validate like a real user

**Playwright is mandatory; verification cadence follows the agreed screen-group scope.** For a multi-screen redesign such as the five Billing & Sales routes, do targeted implementation checks as each screen is changed (type-check/build/unit or component tests and focused behaviour checks needed to avoid accumulating broken code), but defer the **full combined Playwright journey, responsive screenshot review and Axe verification until all five screens have been redesigned and implemented**. Then run one coherent final verification pass across all five routes and their shared journeys/states. If that final pass reveals defects, fix them and rerun only the affected journey/state plus any required regression checks; do not repeat the entire audit without a reason. For a single-screen scope, perform its full Playwright/Axe validation when that screen is complete. Full coverage includes every in-scope route and critical journey, meaningful interaction/validation/loading/empty/error/retry/success/cancel/edit/print states, and the supported small-phone/standard-phone/tablet/landscape/desktop viewport matrix. Capture and inspect viewport and full-page screenshots, including scrolled positions, keyboard-open states and fixed/sticky overlay collisions. Assert user outcomes, accessible roles/names, console/page errors, failed requests, overflow, clipping, focus/keyboard operation and touch targets. Run Axe WCAG 2 A/AA on important states and report print-media checks separately. Use deterministic fixtures and fresh sessions; reuse the complete RBOS Visual Audit workflow. Browser tests do not prove native camera, printer, share sheet or TalkBack behaviour; label these for real-device acceptance.


Run the repository's actual CI checks. At minimum, where applicable:

- TypeScript/type checks.
- Frontend unit/component tests.
- Lint and production build.
- Backend build and unit tests if shared/backend code changed.
- Database integration/migration tests if persistence or API behaviour changed.
- Dependency/security audit if included in CI.
- Playwright end-to-end and screenshot capture for the affected route/flow.

Perform a visual and accessibility review at realistic viewports:
- Narrow Android phone (390px width or actual target-device width), a smaller phone if relevant, tablet and desktop.
- No horizontal scrolling, clipped controls, hidden values, overlapping floating controls, fixed-navigation collisions or unsafe-area problems.
- Check the entire scroll range, not only the initial viewport.
- Buttons/links have clear names, visible focus, adequate contrast and approximately 44px touch targets.
- Check axe WCAG 2 A/AA or the repo's configured accessibility scan; investigate every violation rather than claiming a pass from a different screen or earlier baseline.
- Check English and Hindi wrapping and all key loading/empty/error/success states.
- Compare final Playwright screenshots with the approved repository design specification, MagicPath reference when available, and the user's real-device screenshots.

**Important:** A green CI run proves the configured automated checks passed; it does not prove that the live screen looks right or that a shopkeeper can complete the task. Keep code checks, visual audit and real-user acceptance as separate statuses.

### Stage H — Review feedback and iterate

**No-stall / momentum rule:** once scope and direction are clear, move to the next unblocked deliverable. Do not repeatedly re-audit, re-plan or ask for minor choices. Diagnose a tool failure once; record quota/permission/connector limitations and continue using authorised alternatives. Do not rerun green checks without a code change or a specific evidence gap. Keep a Done/In progress/Blocked/Deferred/Not started ledger; every blocked item must have an unblock condition and a parallel next action. A status update must not substitute for execution.


- Ask the user to review a concrete running implementation or captured screenshots, not an abstract promise.
- Convert each observation into a specific issue, its impact, priority and proposed correction.
- Use real screenshots and user feedback as evidence; do not dismiss feedback just because automated tests pass.
- Fix the highest-impact issues in a focused follow-up PR. Avoid reopening accepted design decisions without new evidence.
- Do not repeatedly ask the user to choose between minor options. Make a reasoned recommendation and proceed; ask only when a genuine product decision or missing information blocks safe progress.
- Re-run relevant checks after each correction.

### Stage I — Merge, deploy and close the phase

- Report exact CI outcomes and any remaining visual/manual checks.
- Merge only when the PR is clean and the user has asked to merge or the task clearly authorises it.
- Treat production deployment as a separate action; never imply a merge equals deployment. Deploy only with explicit authorisation and the agreed release process.
- After merge, verify the merge commit and update the design note with status, PRs, test results, visual-audit status and known follow-ups.
- Close the phase only when the agreed implementation and checks are complete. Label remaining live/field validation honestly as follow-up if it has not happened.
- Move to the next screen group only after the current scope is bounded, documented and stable.

---

## 3. Operating rules for ChatGPT

When the user asks for a UI/UX improvement, ChatGPT must:

1. Act as the UX/engineering orchestrator, not only a code generator.
2. Read this playbook and the latest relevant design notes before starting, including in a new chat.
3. Inspect the live repository state and current screenshots before relying on old chat context.
4. Use MagicPath for exploration when available and finalize the approved design specification in docs/ux/. Figma is permanently optional for RBOS and must never be retried as a prerequisite or treated as a delivery gate. Never invoke the Product Design plugin/skill for RBOS. For a tiny isolated bug/copy fix, explain why a full design cycle is unnecessary and keep the scope proportionate.
5. Trace dependencies and protect backend/API/business behaviour.
6. Produce an actionable recommendation and proceed through the workflow without asking the user to repeat known context.
7. Keep scope narrow and stage-gated. Do not start the next screen group prematurely.
8. Maintain repository documentation, tests, PRs and status so another ChatGPT conversation can resume from the source of truth.
9. Distinguish completed, automated, visually verified, user-accepted, merged and deployed states. Never claim one proves another.
10. Give the user useful checkpoints: brief/decision, repository design specification and optional prototype, implementation PR, automated/visual results, and closure summary.

---

## 4. Copy-paste kickoff prompt for a new chat

Use this at the start of a new ChatGPT chat in the RBOS project:

> We are continuing UI/UX work on RBOS (Vaibhav1507/retail-business-erp). Before doing anything, read and follow docs/ux/RBOS-UI-UX-WORKFLOW.md from the repository's current main branch, then read the relevant existing design note and inspect the current code/screenshots/PR state. Treat the repository docs as the source of truth, not previous chat recollection.
>
> Follow the full workflow: understand the user/task and dependencies; audit the current flow and baseline; research only where it affects the decision; explore in MagicPath when available; finalize the design specification in docs/ux/ with components, tokens, responsive rules and important states (Figma is optional); implement in a focused branch/PR; perform targeted checks while building and the complete Playwright journey/viewport/state screenshot audit plus Axe at the agreed screen-group completion gate; inspect actual screenshots and iterate; then report merge/deployment status separately. Do not use the Product Design plugin/skill. Do not retry Figma or let design-tool quotas stop work.
>
> Keep the scope limited to the screen group I name. Preserve existing routes, APIs, accounting/business rules and shared shell unless the documented analysis justifies a change. Default to simple, mobile-first, accessible UX for Indian shopkeepers with limited digital confidence. Use the existing RBOS design system. Do not make me repeat this process, do not keep asking me to choose minor design options, and do not jump to implementation before the design direction is clear. Make reasoned decisions and continue; ask only if a genuine blocker or product decision requires me.
>
> At the end of every phase, update the repository design note and docs/ux/BILLING-SALES-HANDOFF.md (for Billing & Sales) so the next chat can continue without re-explaining context. Do not claim visual validation, merge or production deployment unless it actually happened. My next scope is: [name the screen or flow].

---

## 5. Recommended new-chat setup

For RBOS work, create one ChatGPT Project named **RBOS — Product, UX & Engineering**. Add this playbook as a project source and paste a shortened version of the ChatGPT operating rules into Project instructions. Start each new RBOS chat from inside that Project. This keeps the instructions and RBOS context available across multiple chats in that Project.

For a general rule across unrelated ChatGPT chats too, add a short version of the operating preference to global Custom Instructions. Keep the detailed RBOS process in the RBOS Project and repository so generic chats are not forced to use RBOS-specific tools or constraints.

Project instructions and global custom instructions are product settings; the repository playbook remains the versioned source of truth for the actual UX process.


## 6. Non-negotiable RBOS execution rules (added 2026-11-24)

Use the repository playbook, MagicPath when available, repository design specifications, Playwright and Axe.
- **Playwright: complete coverage required.** For meaningful UI/UX work, use Playwright as the end-to-end journey runner and screenshot/visual audit tool across all in-scope routes, key states and responsive profiles. Capture and review viewport and full-page states, including scrolled/fixed-overlay cases. Assert real user outcomes, console/network errors, accessibility semantics, overflow and touch targets. Run Axe on relevant states. Explicitly separate browser coverage from native-device checks.
- **Do not let tools stop product delivery.** Figma quota/access failure is a documented constraint, not a reason to stop MagicPath, documentation, implementation preparation or other safe work. Never misrepresent a MagicPath prototype as completed Figma work.
- **No repeated loops.** Diagnose a failure from current evidence, fix its root cause once, rerun the smallest relevant check, then continue. Do not rerun green CI/audits without a code change or a clearly identified evidence gap.
- **No unnecessary user gates.** Make routine design and engineering decisions using the established RBOS context. Ask only for a real product decision, missing required permission or material risk. Do not repeatedly ask the user to choose minor visual options or reconfirm an already approved workflow.
- **Always maintain momentum.** Every update must identify what was actually delivered, what remains blocked and why, what can continue in parallel, and the next concrete action. A status update must not substitute for execution.
- **Keep evidence honest.** Report design prototype, editable Figma, implementation, automated tests, visual review, real-device acceptance, user acceptance, merge and deployment as separate statuses.



## 7. Figma-optional delivery and grouped verification (effective 2026-11-24)

- Figma is optional for all RBOS work. The authoritative design contract is the versioned specification under docs/ux/; a MagicPath prototype is a supporting exploration artifact, not the implementation source of truth.
- MagicPath is used when its editor/API is available and materially helps. Quota exhaustion does not block repository-based design specification or implementation.
- For a named multi-screen group, use per-screen design/implementation gates, but defer the full integrated Playwright, visual screenshot and Axe verification pass until every screen in that group has been redesigned and implemented.
- While building each screen, run only focused checks necessary to prevent broken work accumulating (format/typecheck, component/unit tests, build, and narrow behaviour checks for changed code). Do not represent these as final visual verification.
- At group completion, run the entire combined user journey across all included routes, state transitions, error/retry/cancel paths, viewport profiles, scroll/fixed overlay/keyboard/print states and Axe WCAG 2 A/AA. Review all generated screenshots.
- Fix issues from that pass and rerun affected coverage. Group verification is not a reason to leave known defects unaddressed during implementation.
- Keep automated checks, final visual verification, native Android acceptance, user acceptance, merge and deployment as distinct statuses.
- Every significant pause must update the living programme and docs/ux/BILLING-SALES-HANDOFF.md with the exact next task and latest branch/PR/commit/test evidence.


## 8. App-wide visual consistency contract (effective 2026-11-26)

Every new screen, route, option, modal, drawer, form, empty state or workflow must look and behave like part of the existing RBOS application. The recent app-wide UI/UX improvements are the baseline to extend, not a reason to introduce a parallel visual system.

### Required before implementation

- Inspect at least one current, polished screen that serves a similar task, plus the app shell and its responsive behaviour. Reuse existing components and patterns wherever possible.
- Read the actual shared styles and tokens in `web/src/styles.css` and `web/src/ui-polish.css`; inspect screen-specific styles such as `launcher.css` when relevant. Treat the repository implementation as the source of truth, not a newly invented mockup.
- Preserve the existing font stack: `system-ui, 'Segoe UI', Roboto, sans-serif`. Do not add a new font family or import a font for an isolated feature.
- Reuse the existing CSS variables for colours, type sizes, spacing, radii, shadows, focus rings, touch targets and navigation/safe-area sizing. Do not hard-code a second palette or create duplicate tokens without an approved design-system change.
- Use existing buttons, fields, cards, tabs, status/alert patterns, navigation and responsive conventions. Add scoped component styles only where the current system has no suitable pattern.

### Interaction and hierarchy

- Default to **action-first**: show the user's next meaningful action prominently, then supporting information and secondary actions. Do not make every action visually equal or bury the primary task beneath explanation or decoration.
- Keep labels and helper copy short, plain and familiar to Indian shopkeepers. Use progressive disclosure for advanced/rare options.
- Match existing primary/secondary/destructive action hierarchy, loading/disabled/error/success feedback, confirmation patterns and keyboard focus behaviour.
- New options must fit the existing page flow; do not add a new screen, modal or step unless it materially simplifies the task or is required for safety.
- Keep mobile layouts first-class: respect the existing content width, fixed bottom navigation, safe-area padding, compact top bar, 44px minimum targets and established breakpoints. Verify long English/Hindi labels and narrow devices.

### Acceptance criteria for every new UI surface

1. Same typography/font family, palette, spacing rhythm, radii, controls and interaction language as current RBOS screens.
2. One obvious primary action where the task has a clear next step; secondary actions are visually subordinate.
3. Existing app shell/navigation and route semantics remain intact.
4. Loading, empty, validation, error, success and retry states use established patterns where applicable.
5. Responsive and accessibility checks follow Stage G; screenshots are compared against adjacent polished RBOS screens, not only the feature's own mockup.
6. Any genuinely necessary design-system extension is documented first in `docs/ux/`, uses existing tokens, and is scoped to avoid regressions.

This contract applies to all future RBOS feature work, including Document-to-Books inbox/review screens, settings/options and smaller additions to existing pages.
