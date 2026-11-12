# Baseline A: deterministic resolver spec

No model. Pure function: `resolveA(text, level) -> {kind: navigate|ask|not_understood, ids[]}`.

1. Preprocess the input at level L0, L1 or L2 (see `normalisation.v1.1.md`). The prepared string is the same one C receives.
A has no extra reject rules, because C has none either.
3. Build a phrase index from `anchors.v1.json` **only**, using the tokens of each anchor phrase. Label tokens are not added, because C does not get them either.
4. Score each destination:
   - `s = max over anchors( |tokens ∩ anchorTokens| / |anchorTokens| )`;
   - no A-only bonus. At L2, `@tag` tokens are ordinary tokens in both the query and the anchors (same as C);
   - token match allows Damerau-Levenshtein ≤ 1 for tokens of length ≥ 5.
5. Decision: the same `tau`/`delta` rule as C, with A's own grid tuned on `tuning.v1.jsonl` (score range 0–1, grid `tau ∈ {0.3, 0.4, …, 0.9}`, `delta ∈ {0.05, 0.10, …, 0.30}`).
6. Output only destination ids. The hash is looked up from `destinations.v1.json`, so it is never constructed from text.

Expected behaviour:
- Exact or near-exact anchor wording navigates.
- Single generic words (`return`, `payment`, `history`) match several destinations equally and should therefore ask.
- Unseen paraphrases ("a buyer brought back a fan") are expected to fail at L0/L1. This is the gap C is meant to close.
