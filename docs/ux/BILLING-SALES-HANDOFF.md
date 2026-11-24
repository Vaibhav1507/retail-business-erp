# Billing & Sales handoff

**Repository:** Vaibhav1507/retail-business-erp  
**Active branch:** ux/billing-sales-mobile-polish  
**PR:** [#76 — UX polish Billing & Sales mobile workflows](https://github.com/Vaibhav1507/retail-business-erp/pull/76)  
**Status:** Draft / open / not merged / not deployed.

## Authoritative design contract

Read [billing-sales-mobile-polish.md](./billing-sales-mobile-polish.md) before changing the scoped screens.

- The three user-supplied MagicPath screenshots are the exact visual reference for **Create Invoice only**.
- Sales History, Collections, Customer Returns and the Sell landing are RBOS-specific, research-led designs documented in §4.
- MagicPath agent/API quota is exhausted. Do not retry the integration to obtain screens that were not supplied. Figma is optional.
- Keep changes within the five Billing & Sales routes and the shared Assistant launcher.

## Current implementation state

- Create Invoice was restructured to the MagicPath New Sale hierarchy: header/Draft, 01 Customer, 02 Items, dashed Import bill callout, model number, quantity/price, optional item details, separate GST/discount/amount summary and sticky item-count/Review bill footer.
- Existing invoice state handlers, calculations, customer lookup, tag scan, handwritten invoice extraction, held/offline queues, review/post/print flow were retained.
- The generic Sell back link is suppressed on Create Invoice because the MagicPath-style invoice header provides its own back action.
- Sales History, Collections and Customer Returns retain page-specific mobile layouts and interaction patterns. They must not inherit the invoice sticky footer.
- Frontend test corrections address portal-based Assistant querying, controlled SearchableSelect test usage and waiting for asynchronous customer loading in Collections.

## Validation / acceptance

1. Check the newest PR #76 rbos-ci run after the final commits. TypeScript, frontend tests/lint/build, backend tests/build and PostgreSQL integration must pass.
2. User will provide Android screenshots. Compare the Create Invoice screen against the exact three MagicPath captures. Review other pages against §4 of the design contract.
3. Do not spend time on an exhaustive Playwright screenshot matrix for this polish pass. Run proportionate route smoke coverage only if needed.
4. Real-device acceptance remains necessary for camera/gallery permissions, keyboard/fixed footer interaction, print/share behaviour and TalkBack.
5. CI passing is not visual acceptance. User screenshot review is not merge approval.
6. Do not merge or deploy without explicit authorisation.

## Preserved business rules

Customer name/mobile required; zero stock does not block billing; ad-hoc invoice lines allowed; optional serial/barcode/IMEI with warnings; duplicate identifier validation; server-authoritative invoice/GST calculations; single optional collection bill allocation or on-account payment; idempotent payment write; returns tied to original sale lines; server-authoritative refund/stock/cancellation accounting; WhatsApp consent/retry/duplicate protection; held and offline sale semantics; invoice-only print.

## Research sources

The implementation rationale and source list are maintained in [billing-sales-mobile-polish.md §8](./billing-sales-mobile-polish.md#8-free-public-research-sources-and-how-they-informed-the-design). It includes Shopify POS, Square, Zoho Books India, W3C APG, GOV.UK Design System, NN/g, Android Developers and WCAG 2.2.
