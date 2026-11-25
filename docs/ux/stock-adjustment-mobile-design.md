# Adjust Stock — mobile UX specification

**Status:** Implemented on a focused branch; build validation pending.  
**Reference:** User-supplied MagicPath Adjust Stock Android screenshots (2026-11-25).  
**Route:** Stock → Adjust Stock (`#/stock/adjust`).

## User task
A shopkeeper corrects a recorded quantity after physically checking stock. They must understand whether the selected reason increases or decreases inventory and be warned that the adjustment affects inventory value and accounting entries.

## Screen structure
1. Retain the existing Stock page heading, helper copy, Back to Stock link and shared app shell.
2. Show one white form panel with STOCK CORRECTION eyebrow, Adjust stock title, short helper copy and 01 step marker.
3. Present full-width Product, Location, Date and Reason controls in that order.
4. Show a derived Adjustment direction row immediately after Reason.
5. Present full-width Quantity and Note fields.
6. Show a compact amber accounting-impact warning.
7. Use a full-width primary Review adjustment action on mobile.
8. Keep Recent adjustments within the same panel, using compact cards and a clear empty state.

## Interaction and business rules
- Preserve existing reason codes and mapping: FoundOnCount increases; current other reasons decrease.
- Preserve required quantity (> 0), maximum date (today), and the existing rule that Other requires a note.
- Preserve confirmation before posting, POST /stock-adjustments payload, idempotency key, server-side validation, accounting/valuation behaviour and audit history.
- Do not introduce a separate review/posting endpoint or change stock-adjustment semantics.
- Disable the primary action until products, locations, a positive quantity and any required note are present.

## Visual contract
- Reuse the existing RBOS font stack, green palette, border and muted-text tokens.
- Mobile-first, single-column form; 50px minimum controls, 16px horizontal panel inset, 18px panel radius.
- Keep labels above controls, 16px control text and consistent 16px vertical gaps.
- Direction is explicit: red/muted-red for decrease and green for increase; never rely on colour alone.
- Amber warning must remain readable and must not look like a destructive error.
- Recent adjustment product names wrap; quantity/value remain right-aligned without horizontal overflow.
- No changes to global header, bottom navigation, assistant launcher or typography.

## Acceptance criteria
- Dedicated Adjust Stock view no longer shows redundant Accounts & reports heading or a one-item tab.
- The reference hierarchy and form order are preserved.
- Direction updates immediately when Reason changes.
- Other reason requires a note; all other notes remain optional.
- Existing confirmation and posting behaviour remain unchanged.
- Recent adjustments render readable cards and an empty state.
- Frontend production build succeeds.

## Validation and release
- Focused frontend production build is the requested gate.
- Full tests and grouped Playwright/Axe visual audit remain deferred until the Stock & Catalogue screen group is complete, as agreed.
- Android screenshot acceptance, merge and deployment are separate follow-up milestones.
