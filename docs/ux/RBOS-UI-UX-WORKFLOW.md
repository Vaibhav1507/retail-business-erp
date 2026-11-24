# RBOS UI/UX design and implementation playbook

**Purpose:** Repeat the same end-to-end, design-led process whenever a screen or user journey in RBOS needs a meaningful usability improvement. This is the operating procedure for Home, Billing & Sales, and later modules.

**Product context:** RBOS is an India-first retail business app for small shopkeepers, including people with limited digital confidence. Default to simple language, mobile-first layouts, visible actions, clear feedback, accessible controls and minimal cognitive load. The electronics-shop pilot is the current proving ground, but avoid hard-coding electronics-only assumptions into shared UX.

**Primary tools:** ChatGPT as orchestrator; GitHub for source, tests, branches, PRs and CI; MagicPath for rapid visual exploration/prototyping; Figma for the selected editable design and reusable components/tokens; Playwright and automated accessibility checks for implementation validation. Use web research when current conventions, standards, platform behaviour or competitor patterns need verification.

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

### Stage D — Finalize in Figma

- Translate the selected direction into an editable Figma screen or flow. Do not treat a static image or MagicPath preview as the final source of truth.
- Use actual screen sizes and content density expected in the app; include narrow Android phone and any relevant tablet/desktop layouts.
- Reuse/create components and variants for repeated controls, cards, actions, status states and navigation elements.
- Define or reuse design tokens for colour, typography, spacing, radius, border, elevation and interaction states.
- Cover key states: default, loading, empty, success, validation/error, disabled, long text, Hindi/other required language, and relevant edge cases.
- Keep the design aligned with existing RBOS components and realistic backend data. Mark any proposed API/data changes separately.

**Output:** editable Figma design, reusable components/tokens, interaction notes and responsive/state coverage.

### Stage E — Record the decision in the repository

Before implementation, add or update a design note under docs/ux/ containing:

- Problem statement and target user/task.
- Scope, non-goals, affected routes and dependencies.
- Current-state evidence and prioritised findings.
- Selected direction and rejected/alternative options (briefly).
- MagicPath and Figma links.
- Screen structure, interaction rules, content/copy decisions and state handling.
- Accessibility/responsive rules and acceptance criteria.
- Risks, assumptions and decisions that must not change.
- Implementation plan and test/visual audit plan.

Keep the design note current as decisions or implementation change. Do not leave stale claims or broken reference links.

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
- Compare Playwright screenshots with the approved Figma design and the user's real-device screenshots.

**Important:** A green CI run proves the configured automated checks passed; it does not prove that the live screen looks right or that a shopkeeper can complete the task. Keep code checks, visual audit and real-user acceptance as separate statuses.

### Stage H — Review feedback and iterate

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
4. Use MagicPath for exploration and Figma for the selected editable design when the task is a meaningful redesign. For a tiny isolated bug/copy fix, explain why a full design cycle is unnecessary and keep the scope proportionate.
5. Trace dependencies and protect backend/API/business behaviour.
6. Produce an actionable recommendation and proceed through the workflow without asking the user to repeat known context.
7. Keep scope narrow and stage-gated. Do not start the next screen group prematurely.
8. Maintain repository documentation, tests, PRs and status so another ChatGPT conversation can resume from the source of truth.
9. Distinguish completed, automated, visually verified, user-accepted, merged and deployed states. Never claim one proves another.
10. Give the user useful checkpoints: brief/decision, prototype/Figma, implementation PR, CI/visual results, and closure summary.

---

## 4. Copy-paste kickoff prompt for a new chat

Use this at the start of a new ChatGPT chat in the RBOS project:

> We are continuing UI/UX work on RBOS (Vaibhav1507/retail-business-erp). Before doing anything, read and follow docs/ux/RBOS-UI-UX-WORKFLOW.md from the repository's current main branch, then read the relevant existing design note and inspect the current code/screenshots/PR state. Treat the repository docs as the source of truth, not previous chat recollection.
>
> Follow the full workflow: understand the user/task and dependencies; audit the current flow and baseline; research only where it affects the decision; explore concepts in MagicPath; finalize an editable design with components/tokens/states in Figma; document the decision in docs/ux/; implement in a focused branch/PR; run behaviour tests, CI and Playwright visual/accessibility checks; review actual screenshots and iterate on feedback; then report merge/deployment status separately.
>
> Keep the scope limited to the screen group I name. Preserve existing routes, APIs, accounting/business rules and shared shell unless the documented analysis justifies a change. Default to simple, mobile-first, accessible UX for Indian shopkeepers with limited digital confidence. Use the existing RBOS design system. Do not make me repeat this process, do not keep asking me to choose minor design options, and do not jump to implementation before the design direction is clear. Make reasoned decisions and continue; ask only if a genuine blocker or product decision requires me.
>
> At the end of every phase, update the repository design note so the next chat can continue without re-explaining context. Do not claim visual validation, merge or production deployment unless it actually happened. My next scope is: [name the screen or flow].

---

## 5. Recommended new-chat setup

For RBOS work, create one ChatGPT Project named **RBOS — Product, UX & Engineering**. Add this playbook as a project source and paste a shortened version of the ChatGPT operating rules into Project instructions. Start each new RBOS chat from inside that Project. This keeps the instructions and RBOS context available across multiple chats in that Project.

For a general rule across unrelated ChatGPT chats too, add a short version of the operating preference to global Custom Instructions. Keep the detailed RBOS process in the RBOS Project and repository so generic chats are not forced to use RBOS-specific tools or constraints.

Project instructions and global custom instructions are product settings; the repository playbook remains the versioned source of truth for the actual UX process.
