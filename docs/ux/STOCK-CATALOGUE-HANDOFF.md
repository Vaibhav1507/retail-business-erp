# RBOS Stock & Catalogue — implementation handoff

**Status:** Implementation branch prepared; build failure identified and being corrected. Revised MagicPath artifact could not be re-fetched because the external-agent API quota is exhausted. Implement against the approved repository design specification. Visual audit and unit tests are deferred by the current delivery instruction.  
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
| TypeScript check | Passed in initial CI run; rerun after build fix |\n| Frontend production build | Failed initially due missing `useMemo` import; fix committed, rerun pending |\n| Backend solution build | Passed |\n| Unit tests / visual audit | Deferred by current delivery instruction |
| Full grouped Playwright journeys and responsive screenshots | Deferred by current delivery instruction |
| Visual contact-sheet review | Deferred by current delivery instruction |
| Axe WCAG 2 A/AA | Deferred by current delivery instruction |
| Native Android camera/barcode, keyboard and TalkBack | Pending real-device check |
| User acceptance, merge and deployment | Not performed |

## Next actions

1. Run implementation CI and fix any failures.
2. Review the Stock, My products, Shared catalogue, Categories, Add/Edit and bulk-import journeys with responsive Playwright screenshots.
3. Run Axe on important states and inspect actual screenshots, including scrolled/fixed-overlay states.
4. Keep native-device checks separate from browser evidence.
5. Update this handoff with exact CI/audit evidence before merge. Do not merge or deploy without the appropriate authorisation.


## Screenshot comparison correction — Stock on hand

The first implementation pass did **not** complete the MagicPath Stock on hand card. It retained the original table markup and relied on global mobile CSS to present each table row as a label/value stack. This explains the mismatch visible in the production screenshots; the earlier handoff overstated the completeness of this screen.

A focused correction is now in branch `ux/stock-list-magicpath-alignment`:
- Replace the table-derived row with an explicit product card.
- Put product name, SKU and category in the card header; keep Edit reachable from the card.
- Show Brand/model, Unit, Available, Status and Stock value as a compact two-column fact grid.
- Move supplier, location and supported product attributes into a collapsed More details disclosure.
- Keep Search and Availability full-width, with Category and Supplier side by side on mobile.
- Add Stock on hand heading and Clear filters action.
- Increase Stock page and section heading/type scale to match the supplied MagicPath reference more closely, without changing the global type scale for other modules.
- Format stock values without unnecessary trailing zero decimals while preserving non-zero paise.

This is a targeted implementation correction based on the side-by-side screenshots supplied on 2026-11-25. It does not change inventory data, stock valuation, APIs or posting rules. Build is the current validation gate; visual audit and unit tests are deferred per the current delivery instruction.


## Focused Stock visual-alignment pass — 2026-11-25

Compared the implementation against the user-supplied MagicPath Stock screenshots at the same mobile layout scale. MagicPath's revised project source/render could not be fetched because its external-agent API quota is exhausted; do not describe this as pixel-perfect or independently verified against the live revised artifact.

Scoped adjustments committed in PR #86:
- Mobile Stock content gutter: 16px on both sides (matching the reference's approximately 16px page inset).
- Stock page title: 27px, 500 weight, 1.18 line-height; subtitle: 16px / 1.45.
- Stock action title: 16px / 1.3; supporting copy: 13px / 1.4.
- Stock-on-hand heading: 19px / 1.3, 600 weight.
- Product card Edit action: 16px; product name 16px; SKU/category 12px; facts labels 12px and values 14px; stock value 16px.
- Existing explicit card dimensions remain scoped: inventory padding 16px, product card padding 13px 12px 10px, 15px card radius, 38px identity mark, 2-column fact grid, and 10px card-list gap.
- Stock value formatting suppresses unnecessary trailing zero decimals while preserving non-zero paise.

These values are a screenshot-informed implementation pass, not a claim that MagicPath's original CSS values were extracted. The app retains the existing RBOS system font stack and global tokens; no new font download or global typography change was introduced. Visual audit remains deferred per the current scope. Build is the validation gate; user visual acceptance, merge and deployment remain pending.


## Follow-up — compact Stock overview action cards — 2026-11-25

The user's latest Android screenshots showed that the Stock landing action tiles remained much taller than the MagicPath reference. The cause was the previous three-column flex layout: the icon and arrow consumed horizontal space, forcing action titles and descriptions into narrow stacked lines.

Correction on `ux/stock-list-magicpath-alignment`:
- Put the icon inline with the action title, as in the reference.
- Let the supporting description use the full card width.
- Remove the redundant arrow from each tile; the whole tile remains the navigation target.
- Keep a balanced two-column mobile grid and a compact 104px minimum tile height; use four columns on wider screens.
- Slightly reduce metric-card padding and label/value scale while preserving the two-column mobile layout.
- Keep all route targets, stock calculations, API calls and posting workflows unchanged.

This is a screenshot-driven correction, not a claim of exact pixel identity. The next validation gate is the frontend production build; visual acceptance remains based on the user's next Android screenshot. Unit tests and the full visual audit remain deferred as previously requested.
