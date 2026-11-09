# RBOS UI assessment (re-run, 390px live DOM checks)

Facts measured: no overflow, no targets under 44px, no text under 14px, all form fields labelled, all routes load without HTTP errors. Every route reports the same document height (1266px), so a fixed or min-height layout is likely. Screenshots were not visually reviewed.

## Proposed top five (one slice each, approval before each)
1. Finish localisation: move remaining hard-coded English (Sell, Buy, Stock, Parties, Money, More, Settings, errors) into the typed catalogue.
2. Add a visible English/Hindi selector in Business Settings, saved to the profile (Hindi wording needs native-speaker review).
3. Simplify the dense forms (collect payment, receive purchase, opening stock): group fields, one primary action, plain-language labels.
4. Helpful empty and error states with a next action (for example the empty Stock page offering Add Product directly).
5. Investigate the identical page height (fixed or min-height layout) and verify mobile bottom-nav spacing and screen hierarchy visually.

Separate: rewrite stale E2E specs (collections, opening-stock, sales).