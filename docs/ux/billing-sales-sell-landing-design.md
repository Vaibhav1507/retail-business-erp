# Billing & Sales — Sell landing design specification

**Status:** Design contract completed; implementation not started  
**Route:** `#/sell/menu`  
**Screen group:** Billing & Sales five-route redesign (route 2 of 5)  
**Design source of truth:** This repository specification. Figma is optional.  
**Implementation source:** `web/src/Pages.tsx` (`SellPage`), `web/src/launcher.css`, `web/src/styles.css`, `web/src/i18n.ts`.

---

## 1. User task and intended outcome

A shopkeeper opens Sell because they know they need to perform a sales task but may not know RBOS route names. The landing page should answer one question immediately: **what do you want to do?** The operator should be able to start a new bill or reach sales history, collect money or process a customer return without interpreting icons or navigating a dense submenu.

Primary success outcome: the user chooses the correct sales task in one tap, with Create Invoice clearly dominant and the other three common tasks understandable without relying on emoji.

## 2. Scope and non-goals

### In scope
- The Sell landing at `#/sell/menu`.
- Four existing destinations only: Create Invoice (`#/sell/bill`), Sales History (`#/sell/bills`), Collections (`#/sell/collect`) and Customer Returns (`#/sell/return`).
- Existing Sell title/hint, translations, app shell and selected navigation state.
- Responsive layout, accessible action names, focus/hover/touch feedback and navigation semantics.

### Out of scope
- Home dashboard redesign; global navigation redesign; route/API changes; sales, collection or return business logic; additional Sell destinations; analytics, balances or new API calls; purchasing/supplier returns; unrelated shell changes.

## 3. Current implementation and dependencies

- `App.tsx` parses the hash and renders `SellPage` for the Sell area.
- `Pages.tsx` owns the `SellPage` menu and route switch. The four existing route links are already defined there.
- The current landing uses `.launcher-page` and `.areas` styles from `launcher.css` plus shared `.area` styles from `styles.css`.
- `NAV` provides the persistent Sell section navigation. The landing does not need a data fetch; it is a static route hub.
- The subroutes already show a Sell back-link to `#/sell/menu`; preserve that entry/exit model.
- Current translation keys include `area.sell`, `sell.hint`, `sell.invoice`, `sell.old`, `sell.return` and `sell.due` in English and Hindi.

No backend/API dependency is required for the landing redesign.

## 4. Current-state findings

| ID | Priority | Finding | Evidence / confidence |
|---|---|---|---|
| SELL-01 | P1 | Four large emoji tiles have equal visual weight, so the most frequent/revenue-critical task (new bill) is not clearly prioritised. | **Confirmed — source:** `SellPage` maps four links with the same `.area` treatment. |
| SELL-02 | P1 | Large decorative emoji are doing much of the recognition work; labels have no supporting explanation. | **Confirmed — source:** each link renders an icon and strong label; `.area small` is hidden in `launcher.css`. |
| SELL-03 | P1 | Customer returns are labelled generically as “Return”, which can be confused with supplier returns elsewhere in RBOS. | **Confirmed — source:** Sell route copy uses `sell.return`; the same application also has a separate supplier-return route under Buy. |
| SELL-04 | P2 | The launcher has a centered presentation and fixed-size tile columns; the mobile grid can leave uneven space and does not express a clear primary task. | **Confirmed — source:** `.launcher-page .areas` uses auto-fit fixed-width columns and centered alignment. |
| SELL-05 | P2 | The route has no stateful data requirement; adding counts or outstanding totals would introduce new loading/error semantics without being needed to choose a task. | **Confirmed — source/inference:** current landing has no API calls. Keep it intentionally static. |

No new screenshot baseline is required. The existing Billing & Sales baseline artifact is historical evidence only; final visual review is deferred to the five-route group gate.

## 5. Selected direction — action-first sales hub

### Hierarchy

1. **Page heading:** “Billing & Sales” (existing translated section title) with a short instruction such as “Choose what you want to do.”
2. **Primary action — Create new bill:** visually dominant full-width card, concise explanation (“Make a bill for a customer”), clear forward affordance.
3. **Other sales tasks:** three secondary cards in a simple responsive grid/list:
   - Sales history — “Find a bill or view an earlier sale.”
   - Collect payment — “Record money received from a customer.”
   - Customer returns — “Return items from an earlier sale.”
4. No dashboard metrics, charts, counts, recent activity, extra tabs or duplicated global navigation.

The four destinations remain visible without scrolling on standard phone where practical. On narrow phones, the primary action comes first, followed by a two-column or single-column secondary layout depending on available width and label wrapping. On desktop, use a bounded content width and a three-card secondary grid.

### Why this direction

- It prioritises the highest-frequency task and reduces choice competition.
- It keeps the four existing destinations and avoids feature expansion.
- Plain-language descriptions disambiguate Sales History, Collections and Customer Returns.
- It is consistent with the already-established RBOS Home action-first principle without duplicating Home metrics or its broader quick-action set.

## 6. Interaction and copy contract

| Destination | Primary label | Supporting explanation | Route |
|---|---|---|---|
| Create invoice | **Create new bill** | Make a bill for a customer | `#/sell/bill` |
| Sales history | Sales history | Find a bill or view an earlier sale | `#/sell/bills` |
| Collections | Collect payment | Record money received from a customer | `#/sell/collect` |
| Customer returns | Customer returns | Return items from an earlier sale | `#/sell/return` |

- Each entire card is one link; do not nest buttons or create multiple click targets inside a card.
- The accessible name must include the task label; supporting text may contribute to the accessible name.
- Use a consistent icon as a decorative supplement only (`aria-hidden=true`); do not use emoji as the only task identifier.
- Keep browser-native link semantics so open-in-new-tab, keyboard activation and assistive technology work.
- The current route remains `#/sell/menu`; selecting a card changes only the hash to its existing route.
- When a subroute is opened, retain its existing Sell back-link and route-specific unsaved-work guard behaviour.

## 7. Component and token contract

- Reuse the existing `page-title`, `launcher-page` scope and CSS custom properties.
- Introduce small, route-scoped Sell hub classes rather than changing global `.area` or `.actions` styles (Home and other launchers must not regress).
- Use `--brand`, `--brand-dark`, `--brand-soft`, `--card`, `--line`, `--ink`, `--muted`, `--radius`, `--shadow-1` and `--shadow-2`.
- Primary card: solid brand treatment with white text and a clear icon/arrow; secondary cards: white surface, border and readable text.
- Keep headings at existing RBOS scale; labels at 15–16px minimum; supporting copy at 13–14px with comfortable line-height.
- Avoid hover-only affordances; show clear focus-visible and pressed states.

## 8. Responsive and accessibility rules

| Profile | Required behaviour |
|---|---|
| 360 × 800 small phone | Primary action full width and first. Secondary cards must not clip or force horizontal scrolling; stack if two columns make labels cramped. |
| 390 × 844 / 412 × 915 phone | Primary action followed by three secondary tasks; all four should normally be visible without excessive vertical whitespace. |
| 768 × 1024 tablet | Bounded content width; primary action remains prominent; secondary cards form a balanced row/grid. |
| 844 × 390 landscape | Compact vertical spacing; all links remain reachable and readable; no fixed overlay overlaps. |
| 1280 × 900 desktop | Bounded, left-aligned or comfortably centered content; primary action separated from the three secondary choices; avoid oversized empty gaps. |

- Entire cards are keyboard focusable links with visible focus ring and a minimum 44 × 44 CSS px target (prefer 48px height/spacing where practical).
- Preserve a logical heading hierarchy and one clear page-level heading.
- Icons are decorative; labels and descriptions provide meaning.
- Text and controls meet WCAG 2 A/AA contrast; status is not conveyed by colour alone.
- English and Hindi text must wrap naturally; do not truncate or hide descriptions.
- Respect reduced-motion preference; hover movement is optional and must not be required to understand the card.

## 9. States

- **Default:** all four routes visible with Create new bill first.
- **Keyboard focus:** clear visible outline around the entire link card.
- **Hover/pressed:** subtle surface/border change; no layout shift.
- **Navigation:** normal route transition; no custom loading state because the hub does not fetch data.
- **No empty/error state:** do not add artificial data states to a static route hub.
- **Offline:** links remain visible; destination screens retain their own offline/disabled messaging and behaviour.

## 10. Acceptance criteria

- [ ] The primary Create new bill action is visually dominant and appears first.
- [ ] Sales history, Collect payment and Customer returns remain visible and understandable as separate secondary actions.
- [ ] Customer Returns wording clearly distinguishes it from supplier returns.
- [ ] Every card is a semantic link to the existing route and works with keyboard activation/open-in-new-tab.
- [ ] No new APIs, counts, business rules, destinations or data dependencies are introduced.
- [ ] No global `.area`, Home or unrelated launcher styles are changed.
- [ ] No horizontal overflow, clipping, overlap or awkward English/Hindi truncation at the required viewport profiles.
- [ ] Touch targets meet 44 × 44 CSS px minimum and focus is clearly visible.
- [ ] Reduced-motion preference is respected.
- [ ] Existing Sell subroute back-links and route guards remain unchanged.

## 11. Implementation plan

1. Add route-scoped Sell hub markup/classes in `Pages.tsx` and `launcher.css`; keep the four existing hrefs.
2. Update the Sell-specific English/Hindi copy only; do not alter other area labels.
3. Add focused component assertions for hierarchy, route destinations, accessible names and absence of nested interactive elements.
4. Run focused frontend TypeScript, unit/component tests, lint and production build. Full responsive screenshots, integrated journeys and Axe remain deferred until all five Billing & Sales routes are implemented.
5. Update the living UX programme and Billing & Sales handoff with implementation branch/head and focused check evidence.

## 12. Risks and deferrals

- Final visual acceptance is deferred to the five-route integrated Playwright/screenshot/Axe gate.
- Native Android navigation/back behaviour and shopkeeper comprehension remain for real-device/user acceptance.
- Do not add route counts or balances until a separately justified data contract exists.
- Figma is optional; MagicPath is optional. Continue code-first if exploration tooling is unavailable.

**Next route:** Sales History `#/sell/bills`. Audit its search/filter/list/detail/WhatsApp/print journey and the current `SalesHistory.tsx` dependencies before implementation.
