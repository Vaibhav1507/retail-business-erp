# Stock & Catalogue — mobile UX design specification

**Status:** Design review in progress; repository specification is the source of truth.  
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

### Prototype review limitation

The MagicPath component build completed, but its generated preview was not available for a full visual inspection in this review. A successful build is not visual acceptance. The user screenshots are therefore the verified visual baseline; the prototype is an exploration reference pending a full screen-by-screen review.

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

## 8. Screenshots still required before design approval

The current screenshot set does not fully evidence every state. Capture:
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

1. Complete the MagicPath screen/state inventory against the screenshots and correct any prototype interaction gaps.
2. Refine the selected mobile direction once; do not create multiple near-identical prototypes.
3. Implement in a focused branch/PR, preserving current routes, APIs and business rules.
4. Run focused component/type/build checks as each screen is implemented.
5. After the entire Stock & Catalogue screen group is implemented, run the integrated Playwright journeys, responsive viewport/full-page/scroll/keyboard screenshots, visual contact-sheet review and Axe WCAG 2 A/AA checks.
6. Track native Android camera/gallery, keyboard, TalkBack and real-device touch behaviour separately.
7. Report design approval, implementation, automated checks, visual acceptance, user acceptance, merge and deployment as separate statuses. No merge or deployment is implied by this design note.
