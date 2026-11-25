# RBOS Stock & Catalogue — implementation handoff

**Status:** Implementation branch prepared; automated CI and grouped Playwright/Axe verification pending.  
**Last updated:** 2026-11-25  
**Design contract:** [Stock & Catalogue mobile UX specification](https://github.com/Vaibhav1507/retail-business-erp/blob/ux/stock-catalogue-mobile-design-spec/docs/ux/stock-catalogue-mobile-design.md)  
**Final MagicPath reference:** https://designs.magicpath.ai/v1/richly-mountain-9553  
**Design specification PR:** #83 (open; CI success)  
**Implementation branch:** `ux/stock-catalogue-mobile-implementation`  
**Implementation head:** `962162a15d3ea1df49fbef48cd62afa5663be306`

## Scope implemented

- Stock landing: clearer primary/secondary action hierarchy, action descriptions and summary metrics derived from existing product and stock rows.
- Inventory: existing search, category, supplier and availability filters and server-provided stock value remain in place.
- Catalogue: existing My products, Shared catalogue, Categories, product form, import, barcode and manufacturer-source workflows retained and refined.
- Product form: required basics remain first; brand/model, tax/pricing references and product details are progressively disclosed.
- Catalogue presentation: compact mobile product cards, category management styling, shared-catalogue privacy explanation and similar-category warning styling.

## Boundaries preserved

- No API, database, tenant/security, accounting, stock valuation or posting contract changes are intended.
- Stock values remain sourced from existing stock rows; the UI only formats and displays them.
- Opening Stock, Adjust Stock and Add Recent Purchase remain existing workflow handoffs, not replacement posting flows.
- Similar category names are warnings only; never auto-merge categories.
- Shared catalogue private shop data remains private.

## Validation ledger

| Check | Status |
|---|---|
| Final MagicPath design direction | Complete |
| Repository design specification | PR #83 open; CI success |
| Focused TypeScript/build/lint/tests for implementation | Pending CI |
| Full grouped Playwright journeys and responsive screenshots | Pending after all in-scope screens are implemented |
| Visual contact-sheet review | Pending |
| Axe WCAG 2 A/AA | Pending |
| Native Android camera/barcode, keyboard and TalkBack | Pending real-device check |
| User acceptance, merge and deployment | Not performed |

## Next actions

1. Run implementation CI and fix any failures.
2. Review the Stock, My products, Shared catalogue, Categories, Add/Edit and bulk-import journeys with responsive Playwright screenshots.
3. Run Axe on important states and inspect actual screenshots, including scrolled/fixed-overlay states.
4. Keep native-device checks separate from browser evidence.
5. Update this handoff with exact CI/audit evidence before merge. Do not merge or deploy without the appropriate authorisation.
