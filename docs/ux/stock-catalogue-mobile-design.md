# Stock & Catalogue — mobile UX design specification

**Status:** Design direction approved after the final MagicPath refinement; repository specification is the source of truth.  
**Scope:** Stock overview, Manage Products, Add/Edit Product, product list, Shared Catalogue, Categories and bulk product import.  
**Pilot context:** Monika Electronics; design must remain reusable across RBOS retail verticals.  
**Prototype:** https://designs.magicpath.ai/v1/richly-mountain-9553  
**Figma:** Not required. Figma is optional under the RBOS UI/UX playbook; this specification is the authoritative design contract.

## 1. User and job to be done

A small-shop owner needs to find a product, understand whether it is available, add or correct its catalogue details, import many products, reuse a shared catalogue entry, and manage categories without understanding internal inventory terminology.

Optimise for Android phones, shop-counter interruptions, limited digital confidence, one-handed use, readable product names/models and low-risk stock actions.

## 2. Evidence reviewed

### User-supplied Android screenshots

The supplied screenshots show:
- Stock landing with Manage Products, Add Opening Stock, Add Recent Purchase and Adjust Stock actions, plus search/category/supplier/availability filters and stock cards.
- Product management with My products, Shared catalogue and Categories tabs.
- My-products list with search, category filter, inactive toggle, SKU/name/category/unit/brand-model/source and edit actions.
- Add product form with name, category, unit, optional HSN/GST/MRP, generic/unbranded toggle and brand fields.
- Bulk add area with optional Excel/Word/CSV/text-PDF/photo upload, sample data and manufacturer catalogue sources.
- Categories list with product counts and rename actions.
- Shared catalogue explanation, search and product cards with Already added state.
- Inventory/stock-value cards with supplier, SKU, available quantity, stock value and In stock/Out of stock states.

### Current repository evidence

- Current main is 1baf6ab9a911dd431cac803f29afad2aaf485991 (2026-11-25).
- Existing RBOS product identity and shared vertical catalogue work is represented by PR #28.
- Existing first-shop onboarding reuses the catalogue, Setup Assistant, import preview/duplicate handling, barcode functionality and opening-stock flow (PR #49).
- Existing stock workflows and accounting/valuation rules must be preserved; the design must not create parallel product, stock or purchase paths.
- The current repository playbook states that Figma is optional and repository design specifications are authoritative.

### Prototype review and final refinement

The initial MagicPath component source and the supplied Android screenshots were inspected for Stock, My Products, Shared Catalogue, Categories, Add/Edit Product, Bulk Import, Opening Stock, Adjust Stock, barcode capture and the Add Purchase handoff. A later attempt to re-fetch the revised MagicPath revision was blocked by the account's external-agent API quota (50/50 used), so the latest revised revision and its render could not be independently verified. The repository design specification is therefore the approved design contract and records the final decisions. Do not claim the revised MagicPath revision itself is visually verified.

Final refinements:
- Category rows use compact Rename actions, accurate singular/plural count copy and a warning for likely singular/plural duplicate names (for example, Air Cooler / Air Coolers). The prototype does not merge categories automatically.
- Product cards prioritise identity, available quantity and stock status; secondary product/source details are disclosed on demand.
- Optional Brand and product identity fields start collapsed so the required product basics remain the first task.
- The persistent summary/action is contextual: Stock offers Manage products; Opening Stock offers Review starting stock; unrelated catalogue screens do not carry a redundant Stock overview footer.
- The existing RBOS business-category-aware product attributes remain the implementation reference; do not hard-code electronics-only attributes for every business vertical.

MagicPath revised-artifact verification: pending/unavailable due external-agent API quota. The prototype source available before quota exhaustion is a seeded interactive design reference; its local demo actions do not post to RBOS APIs, update live inventory, import files, link barcodes or create accounting entries. The design direction is frozen in this repository specification; no further MagicPath or Figma iteration is planned.

## 3. Prioritised findings

### P1 — High

1. **Product form is a long, undifferentiated scroll.** Group fields into clear sections and disclose genuinely optional/tax details progressively. Keep the product name and category immediately visible.
2. **Bulk import explanation is too technical and verbose.** Lead with a plain-language action, accepted file/photo types and a short processing expectation. Keep sample data secondary.
3. **Shared catalogue and own catalogue need a clear distinction.** Explain that adding a shared item creates a shop-owned product; stock, SKU, cost and selling price remain private to the shop. Keep Already added visibly disabled and unambiguous.
4. **Category management consumes excessive vertical space.** Use compact category rows with name, product count and a clear Rename action; avoid repeating the same field labels in every large card.
5. **Product list cards repeat metadata and push useful actions down.** Prioritise product name, SKU, category, available quantity/status and a single Edit action; place secondary metadata in a compact details area.
6. **Stock landing has four equal-weight actions.** Keep Manage Products as the primary catalogue action; make opening stock, recent purchase and adjustment secondary, visually distinct and clearly named.

### P2 — Medium

1. Replace technical phrases such as “catalogue sources” with plain-language descriptions and contextual help.
2. Keep search and filters close to the list they control; preserve filter values when navigating back from edit.
3. Use consistent labels and formatting for Brand / model, SKU, Unit, Available, Stock value and Status.
4. Ensure long product/model names wrap without clipping and never cause horizontal page overflow.
5. Keep the assistant launcher clear of bottom navigation, sticky actions and important list controls.

## 4. Recommended information architecture

1. **Stock** — inventory snapshot, primary Manage Products action, secondary stock tasks, search and filters, stock list.
2. **Manage Products** — tabs: My products, Shared catalogue, Categories.
3. **My products** — search, category filter, show inactive, compact product rows/cards, Add product.
4. **Add/Edit product** — one focused form with:
   - Basics: Product name (required), Category (required), Unit (required).
   - Identification: Brand, Model, SKU/barcode as applicable; Generic/unbranded toggle.
   - Tax and pricing reference: HSN, GST rate, MRP; clearly optional and never treated as purchase cost or selling price.
   - Details: only existing supported attributes; avoid inventing electronics-only fields in the shared form.
   - Save/cancel with field-level validation and preservation of entered values after recoverable errors.
5. **Bulk add** — upload/select source, processing state, review/mapping/duplicate resolution, explicit import confirmation, success/partial-failure summary. Never silently create products from an unreviewed extraction.
6. **Shared catalogue** — explanation of shared identity vs private shop data, search, compact results, Add to my products / Already added states.
7. **Categories** — category name, product count, Rename; confirmation and clear impact copy where a rename affects every product in that category.

## 5. Interaction and business rules to preserve

- Reuse existing product catalogue, shared catalogue, import and stock endpoints; no parallel workflow or schema is implied by this design.
- Tenant boundaries remain server-enforced. Shared catalogue must not reveal another shop's SKU, stock, buying cost or selling price.
- Product identity/deduplication must use the existing canonical matching rules; do not merge distinct variants solely because names look similar.
- Category rename must preserve the existing relationship semantics and accurately communicate that products in the group follow the rename.
- Bulk import must retain file validation, parsing/OCR/AI status, editable mapping, duplicate handling, row-level errors and explicit user confirmation.
- Add/Edit product must not infer tax rate, HSN, purchase cost, selling price or stock from MRP.
- Opening stock, purchases and adjustments are posting workflows, not catalogue edits. Preserve existing review/confirmation, idempotency, permissions and audit trail.
- Stock value remains based on the existing valuation policy and server-calculated values; the UI must not calculate or mutate valuation independently.
- Do not change accounting, GST, inventory valuation, stock movement or purchase posting rules as part of a visual redesign.

## 6. Responsive and accessibility contract

- Mobile-first baseline: 360–390 CSS px wide Android viewport; support tablet and desktop without changing task semantics.
- Minimum interactive target 44×44 CSS px, visible focus, accessible names, semantic headings and form labels.
- Use field-level validation, clear required/optional markers, helpful empty/loading/error/retry/success states and disabled states with explanations.
- Do not rely on colour alone for In stock / Out of stock / Already added.
- Keep persistent navigation and floating assistant from obscuring form controls, list actions or sticky footers.
- Support keyboard navigation and zoom/reflow; long names and translations must wrap.
- Maintain the existing RBOS green palette, typography and spacing tokens rather than introducing a parallel design system.

## 7. Required states and acceptance criteria

| Area | Acceptance |
|---|---|
| Stock overview | Primary/secondary actions are clear; filters work; stock and value/status are readable; no posting occurs from navigation alone. |
| My products | Search/filter/inactive state are understandable; empty/loading/error/retry states exist; long names wrap; edit opens the correct product. |
| Add/Edit | Required fields are explicit; optional tax/reference fields are grouped; invalid values show field-level errors; save/cancel and retry preserve safe state. |
| Bulk import | Supported formats are clear; upload/progress/failure/retry states are visible; mapping and duplicate resolution are reviewable; import requires explicit confirmation. |
| Shared catalogue | Shared-vs-private boundary is clear; search works; already-added items cannot be added twice; adding creates only the current shop's product record. |
| Categories | Counts are accurate; rename is understandable and confirmed where required; updated names are reflected consistently. |
| Navigation | Back navigation preserves useful search/filter context; bottom navigation and assistant do not cover actions. |
| Accessibility | No horizontal overflow at target viewports; controls have accessible names; keyboard/focus works; touch targets meet 44px; Axe WCAG 2 A/AA passes on important states. |

## 8. States to verify during implementation (not a blocker to design approval)

The supplied screenshots are sufficient to freeze the visual direction. Do not start another screenshot-request loop before implementation. Cover the following states with the existing RBOS workflows and the grouped Playwright/Axe verification after implementation:
- Stock overview at top and scrolled list, including filters expanded/selected and empty/no-results.
- Manage Products tabs and product list at top and bottom.
- Add product full form, validation errors, category selector, brand/model fields, save success and cancel.
- Edit product with populated values and a recoverable save error.
- Bulk import: file selected, processing, preview/mapping, duplicates, row errors, confirmation and success/partial failure.
- Shared catalogue: search results, no results, add action, already-added state and error/retry.
- Category rename: dialog/form, validation, success and cancellation.
- Opening Stock, Adjust Stock and Add Recent Purchase entry/handoff screens; these are visible from Stock but are not sufficiently evidenced by the supplied screenshots.

## 9. Scope and non-goals

**In scope:** design and UX for Stock overview and the Manage Products / Add-Edit Product / Shared Catalogue / Categories / bulk-import experiences.

**Not in scope:** changing API contracts, product identity rules, stock posting, valuation/accounting, purchase/sale behaviour, database schema, tenant/security policy, new AI/OCR providers or replacing existing import functionality.

Opening Stock, Adjust Stock and Recent Purchase are included only as entry-point/handoff consistency checks in this screen group unless separately confirmed as additional redesign screens.

## 10. Delivery and validation plan

1. Complete — MagicPath screen/source inventory reviewed against the supplied screenshots.
2. Complete — one final refinement pass applied to the selected prototype; no further concept or polish cycle is planned.
3. Implement in a focused branch/PR, preserving current routes, APIs and business rules.
4. Run focused component/type/build checks as each screen is implemented.
5. After the entire Stock & Catalogue screen group is implemented, run the integrated Playwright journeys, responsive viewport/full-page/scroll/keyboard screenshots, visual contact-sheet review and Axe WCAG 2 A/AA checks.
6. Track native Android camera/gallery, keyboard, TalkBack and real-device touch behaviour separately.
7. Report design approval, implementation, automated checks, visual acceptance, user acceptance, merge and deployment as separate statuses. No merge or deployment is implied by this design note.


## 11. Final design decision — 2026-11-25

**Decision: approve the Stock & Catalogue design specification for implementation.** The design direction is frozen from the supplied screenshots, inspected initial MagicPath source and documented final refinements. The revised MagicPath artifact could not be re-fetched for independent verification because its external-agent API quota was exhausted. No further prototype screenshots, Figma work or design iteration is planned; implement against this repository specification.

### Implementation boundaries
- Implement Stock overview, Manage Products (My products / Shared Catalogue / Categories), Add/Edit Product and Bulk Product Import using the existing RBOS routes and components.
- Keep Opening Stock, Adjust Stock and Add Recent Purchase as existing workflow handoffs/consistency checks; do not rebuild them as parallel posting flows in this screen group.
- Reuse the existing product form's business-category-aware attributes, category/product APIs, shared catalogue, import preview and duplicate resolution.
- Keep stock values and accounting results server-authoritative.
- Preserve tenant isolation and shared-catalogue privacy; never expose another business's SKU, barcode, stock, buying cost or selling price.
- Do not automatically merge similar category names. Show a review warning and preserve the existing category rename semantics.
- The MagicPath prototype is not production code and its demo-only interactions must be replaced by existing RBOS API behaviour during implementation.

### Final refinement acceptance
- [x] Stock landing hierarchy retained with Manage Products as the primary action.
- [x] Product cards prioritise name, SKU/category, available quantity and status.
- [x] Secondary product details are progressively disclosed.
- [x] Product identity fields are optional and collapsed initially.
- [x] Category rows are compact, counts use correct singular/plural copy and likely duplicate names are flagged without merging.
- [x] Sticky summary/action is shown only where it supports the current task.
- [x] Shared catalogue privacy explanation and Already added state are retained.
- [x] Bulk import remains a four-stage review-before-import journey.
- [x] Opening Stock, Adjust Stock and Add Purchase are kept within their existing workflow boundaries.

### Status ledger
| Milestone | Status |
|---|---|
| Current-main workflow and relevant handoff reviewed | Complete |
| Existing Stock, Catalogue and stock API dependencies inspected | Complete |
| Supplied Android screenshots reviewed | Complete |
| MagicPath source reviewed screen by screen | Complete |
| Revised MagicPath artifact re-fetch / independent verification | Blocked by external-agent API quota |
| Revised MagicPath render visual acceptance | Not verified |
| Repository design contract updated | Complete |
| Repository design specification | Approved for implementation |
| Focused implementation branch / PR | Next |
| Focused checks during implementation | Pending |
| Full grouped Playwright, responsive screenshot review and Axe WCAG 2 A/AA | Pending until all in-scope screens are implemented |
| Native Android camera/barcode, keyboard and TalkBack acceptance | Pending real-device verification |
| User acceptance, merge and deployment | Not yet performed |


## 12. Categories screen implementation detail — 2026-11-25

The Categories view follows the MagicPath reference with a compact **Product categories** heading, a live group-count pill, concise helper copy, a compact similar-name warning, and horizontal category cards. Each card contains a pale-green category mark, category name, singular/plural product count and a right-aligned Rename action. The category footer uses the same three-tier pagination arrangement as the catalogue product list.

The implementation retains the existing RBOS font stack, green palette and page shell. Typography and spacing are scoped to the category view; no external font or global CSS change is introduced. The existing category rename endpoint and relationship semantics remain unchanged. Similar names are warnings only; categories are never merged automatically.

Acceptance:
- [x] Three catalogue tabs remain in one horizontal row.
- [x] Category heading and group count are visible together.
- [x] Similar-name warning uses compact amber treatment and preserves the no-auto-merge explanation.
- [x] Category rows are horizontal cards with icon, name, product count and Rename action.
- [x] Product count copy handles singular and plural correctly.
- [x] Category pagination uses the catalogue footer layout and paginates the rendered cards.
- [x] Existing rename API, category relationships and tenant boundaries are preserved.
- [ ] Fresh Android screenshot comparison and user visual acceptance.
- [ ] Build confirmation on the implementation PR.
