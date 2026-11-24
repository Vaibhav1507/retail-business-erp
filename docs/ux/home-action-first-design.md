# Home dashboard — action-first design

**Status:** Home design implemented and merged in PR #69; screenshot-driven polish in PR pending final CI  
**Scope:** Home route only (`#/home`)  
**Design references:** [Figma — RBOS Home](https://www.figma.com/design/F5g0huBlQ1fYTE38HWPeTy) · [MagicPath — Home prototype](https://designs.magicpath.ai/v1/warmly-star-1345)  
**Programme:** [RBOS usability and accessibility programme](./rbos-usability-accessibility-programme.md)

## Decision

Use an action-first Home page for the day-to-day shopkeeper workflow. The first screen should make the most common task—creating a bill—obvious, then offer a small set of secondary tasks and a compact business snapshot. Do not put every module shortcut and every insight on the Home page; the existing global navigation already provides access to modules.

## Page structure

1. Welcome heading and short prompt.
2. Primary **Create new bill** action, linked to the existing `#/sell/bill` route.
3. Four secondary actions: Purchases, Check stock, Collect payment and Add customer.
4. A two-column, four-metric overview: Sales today, Money to collect, Low stock and To pay suppliers.
5. A compact Cash and bank strip linked to Finance.
6. Existing seven-item global navigation and Navigation Assistant remain owned by the app shell; this change does not redesign them.

## Implementation constraints

- Dashboard values continue to come from the existing `GET /dashboard` response. Prototype figures are illustrative only and are not used in production.
- Existing route hashes and server-side semantics are preserved.
- English and Hindi catalogue strings are added for the new Home labels.
- Reminders remain available when actionable order or day-closing reminders are returned.
- Summary loading has a status message; a failed request exposes a retry action.
- Styles are scoped to `.home-*` classes so other routes are not unintentionally restyled.
- The Home change does not remove or change the Business Settings screen.

## Accessibility and responsive checks

- Mobile-first two-column action and metric grids collapse without fixed-width overflow.
- Links remain native anchors with visible keyboard focus indicators.
- Loading and error states use status/alert semantics.
- Reduced-motion preferences are respected.
- Touch targets are at least 44px on the small controls and larger for the principal actions.
- On Home, the Assistant launcher stays in document flow below the dashboard rather than floating over figures; its expanded panel remains available as an overlay.
- The mobile shop header uses a compact single-line business name.
- Supplier dues and quick-action helper text use short shopkeeper-friendly wording.
- Day-closing reminders use singular/plural grammar.

## Review checklist

- [ ] Verify primary bill action opens the expected new bill route.
- [ ] Verify all four secondary links open their intended areas.
- [ ] Verify real dashboard values and empty/zero values render correctly.
- [ ] Simulate dashboard failure and confirm Retry works.
- [x] Verify actionable reminders still render; singular/plural copy has been corrected.
- [x] The Home Assistant launcher is moved into document flow; verify the new mobile capture confirms no overlap.
- [ ] Check English and Hindi layouts for wrapping and horizontal overflow, including the shortened supplier label.
- [ ] Run web lint, unit tests, build and the Home visual audit.
- [ ] Review the page with real shopkeeper tasks before starting Billing & Sales UX.

## Next scope

Home direction has been reviewed against real Android screenshots. Finish the screenshot-driven polish and required CI/visual audit, then close the Home phase. Billing & Sales is the next UX scope.
