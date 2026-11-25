# Buy bottom navigation and review-bar positioning

**Date:** 2026-11-25  
**Scope:** Mobile shell navigation and the Add Purchase fixed review action only.

## Problem
Android screenshots of `#/buy/receive` show the fixed Review purchase bar and bottom navigation too close to the system gesture area, with the navigation appearing clipped or partially obscured. The global shell already fixes the navigation to the viewport, but its declared 64px height did not include the device bottom safe-area inset. The Buy review bar and content clearance must use the same geometry.

## Decision
- Preserve the existing global fixed bottom navigation and its 64px content height.
- Extend the navigation surface through `env(safe-area-inset-bottom)` and reserve that inset inside the nav using bottom padding.
- Keep the Buy Review purchase bar immediately above the full navigation surface using the same `nav height + safe-area inset` offset.
- Reserve matching bottom space in the scrollable content so the final form controls are not hidden behind either fixed surface.
- Apply the override only below the desktop navigation breakpoint; desktop's sticky side navigation is unchanged.

## Non-goals and invariants
- No route, API, purchase/OCR/barcode flow, GST, stock/WAC, payable, idempotency or accounting changes.
- No redesign of other navigation items or the Buy screen.
- No unit tests or Playwright run in this user-directed visual-only pass.

## Acceptance
- Bottom navigation remains fixed while Add Purchase content scrolls.
- All navigation items and the Android gesture-area background remain visible.
- Review purchase stays directly above the navigation without overlap.
- The last form content can scroll clear of the review bar and navigation.
- Desktop navigation behaviour remains unchanged.

## Validation status
Code change only. A fresh Android screenshot is still required to confirm the device/browser's actual safe-area handling; automated tests and Playwright were intentionally skipped per user direction.
