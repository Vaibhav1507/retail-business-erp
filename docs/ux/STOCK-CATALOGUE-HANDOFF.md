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


## Final screenshot-driven density correction — 2026-11-25

The latest MagicPath references (Stock overview, Stock on hand filters, and product cards) were compared directly with the current RBOS Android screenshots. The mismatch was caused by both CSS cascade and component content hierarchy, not merely by font sizing.

### Changes on `ux/stock-screen-density-alignment`

- **Filters:** explicitly override the global mobile `div[role="search"]` one-column rule within Stock. Search and Availability stay full-width; Category and Supplier occupy the same two-column row. This fixes the actual cascade conflict that had stacked both selects vertically.
- **Product card facts:** mirror the reference's information matrix: Brand/model + Unit; Details + Source; Available + Stock value; Status as a compact final row. Supplier is now visible as Source, and product attributes/location are shown under Details. Removed the separate More details disclosure row that unnecessarily increased card height.
- **Product card density:** compact identity header, smaller card padding, tighter fact-row gaps, and tighter label/value line-height.
- **Stock overview:** reduce action-card padding/gap, maintain balanced two-column mobile action tiles, and slightly compact the 2x2 metric tiles while retaining comfortable touch targets.
- **Stock panel:** tighter header/filter/note spacing and shorter filter controls; preserve a 16px outer mobile gutter consistent with the MagicPath frame.
- **Scope:** CSS is scoped to `.stock-page`; no app-wide font or typography changes, API changes, stock calculations, tenant rules or accounting behaviour.

### User reference comparison
The supplied MagicPath references show:
1. A compact two-column action grid followed by a compact 2x2 metric grid.
2. Category and Supplier side-by-side within Stock on hand.
3. Product detail pairs laid out horizontally, rather than a vertically stacked detail disclosure.

The implementation now follows that structure. It is screenshot-informed, not a pixel-perfect claim. Build is the validation gate; unit tests and visual audit remain deferred as instructed. User visual acceptance, merge and deployment are separate and still pending.


## Final Stock layout polish — 2026-11-25

Following the user's final Android review, made only these mobile layout adjustments:

- **Add Opening Stock:** place the action across the full action-grid row and keep its title on one line. Its existing helper text remains visible alongside the title.
- **Inventory filters:** Category and Availability share one row; Supplier occupies the full row. Search remains full-width.
- All other Stock action cards, product cards, spacing, navigation, routes, data, and stock workflows remain unchanged.
- CSS remains scoped to the Stock page and mobile breakpoint; no API, database, stock calculation, tenant, or accounting behaviour changed.

**Validation:** source-level change only. No unit tests or Playwright run requested for this visual-only adjustment. Android screenshot confirmation remains pending.


## Follow-up correction — opening stock action card — 2026-11-25

PR #90 was merged with the Add opening stock card spanning both columns. The approved MagicPath reference instead places Manage products and Add opening stock side by side in the first row. This follow-up removes the full-width grid span from the second action card while retaining its single-line title and inline helper copy. The filter layout remains Search full-width, Category + Availability side-by-side, and Supplier full-width.

No markup, routes, APIs, data, stock calculations or business behaviour changed. No tests or Playwright were run per the user's instruction; Android visual confirmation remains pending.



## Android screenshot correction — opening stock action card — 2026-11-25

The first PR #91 correction fixed the grid placement but introduced a content-layout regression: the second card used a horizontal flex row, with a non-shrinking title and helper text forced into the remaining width. At mobile half-card width, the helper collapsed to one character per line and stretched the entire first grid row.

Corrected in `fix/stock-opening-action-card-content`:
- Keep Manage products and Add opening stock in the same two-column row.
- Stack the Add opening stock title and helper copy vertically inside its card; keep the title on one line.
- Give all four mobile action tiles a consistent 184px minimum height to match the supplied MagicPath reference and prevent row-height distortion.
- Keep card content left-aligned and helper text at full available card width.
- No markup, routes, APIs, stock calculations, tenant rules or posting behaviour changed.

**Validation:** CSS and handoff inspection only. Unit tests and Playwright remain intentionally skipped per the user's instruction. Android visual confirmation is still required after deployment.


## Manage Products + Add/Edit Product — MagicPath implementation pass (2026-11-25)

**Implementation PR:** [#93](https://github.com/Vaibhav1507/retail-business-erp/pull/93)  
**Branch:** `ux/manage-products-magicpath-pixel-match`

### Implemented

- The Manage Products route now has its own `My products` heading, helper text and STOCK badge; the redundant Stock heading/back-link is removed from this route.
- My products, Shared catalogue and Categories remain together in one horizontal row.
- Add product and Scan barcode occupy the first action row; Import list is a separate second-row action and opens the existing import assistant in its own view.
- My products now renders compact product cards with identity/SKU/category, Edit, Brand/model + Unit, Available + Status, and a collapsed More details section.
- Available quantities are displayed from the existing stock rows passed into the catalogue; no new stock API or derived valuation is introduced.
- Add/Edit Product now has a dedicated page heading and form panel with a back link, product-details header, required basics, collapsed optional sections, two-column optional fields, equal-width save/cancel actions and the existing catalogue-only note.
- Required asterisks are grouped inline with their field labels so they cannot wrap to a separate line.
- Styling is scoped to Manage Products and Add/Edit Product. The existing global font stack, app shell and shared navigation are retained; no external font dependency is added.
- Pagination is displayed for short non-empty product lists to match the MagicPath reference.

### Preserved

- Existing product CRUD, shared catalogue, category rename, barcode scanning/linking, bulk import and manufacturer-source APIs.
- Product identity, tenant boundaries, stock movements, stock valuation, accounting and purchase posting behaviour.

### Validation

At implementation head `fd9da6c4bf02a1bfa566e62d4084acde63e416b0`, GitHub Actions frontend job passed:
- TypeScript check
- Frontend unit tests
- Lint
- Production build
- Dependency audit

The backend solution build also passed at that check. Backend unit tests and PostgreSQL integration were still running when this note was recorded. No full responsive screenshot comparison or real Android visual acceptance has yet been performed. Merge and deployment are not performed by this PR.


## Shared Catalogue — MagicPath alignment follow-up (2026-11-25)

### Screenshot comparison and implementation

The user's MagicPath Shared Catalogue references and current RBOS Android screenshots were compared. The old RBOS shared-catalogue table rendered each field as stacked label/value rows and did not match the reference card hierarchy.

Implemented in the same PR #93 branch:
- Preserve the existing three catalogue tabs and selected Shared catalogue state.
- Keep the shared-data privacy banner but align its copy with the MagicPath reference: shared catalogue entries are scoped to the same business category; SKU, barcode, stock, buying cost and selling price remain private.
- Keep the lock icon, light-green banner, full-width Search shared products label/input and the reference placeholder.
- Replace the responsive stacked table with compact shared-product cards.
- Each card has a first-character product mark, product name, category + brand/model metadata, visible product attributes/specifications and a left-aligned outlined action.
- Already-added products display a disabled `✓ Already added` action; not-yet-added products use the existing add-to-my-products API action.
- Keep empty-state copy and the existing master catalogue search and add behaviour.
- Show pagination for short result sets as well as larger catalogues.
- Reuse the same three-tier pagination footer layout on My Products and Shared Catalogue: result count, per-page selector and Previous / page count / Next navigation.
- CSS remains scoped to the catalogue screen. No API, shared-catalogue data, duplicate prevention, tenant boundary or product creation behaviour is changed.

### Validation
- Frontend TypeScript/build CI is being run on the updated PR head.
- No full Playwright visual audit has been run. Exact visual acceptance remains pending updated Android screenshots after PR deployment.
- Header differences in the user's current app screenshots are intentionally not changed in this follow-up; the user noted PR #93's heading changes have not yet been deployed.
