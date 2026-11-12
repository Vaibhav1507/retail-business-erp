# Fairness and leakage checklist (A vs C1 vs C2)

## Equivalence of input preparation
| Element | A | C1 | C2 | Equal? |
|---|---|---|---|---|
| Guard G | shared | shared | shared | yes; guard hits reported separately |
| L0/L1/L2 string | `prepared/<level>` | same | same | yes; same file and hash |
| Reference set | 200 anchors, prepared at the same level | same | same | yes |
| Score | max over the anchors of each destination | max cosine over anchors | max cosine over anchors | same aggregation |
| Model prefix | none | `query: ` on **both** queries and anchors (symmetric matching) | none | yes |
| Threshold tuning | own tau/delta grid on `tuning.v1` | own grid on `tuning.v1` | own grid on `tuning.v1` | same data, same objective |
| Ask/reject rule | config `decision.rule` | same | same | yes |

## A is not handicapped
- A gets the same lexicon and tags, plus fuzzy token matching (Damerau-Levenshtein ≤ 1 for tokens of length ≥ 5).
- A's score scale differs from cosine, so it gets its own grid instead of C's 0.70–0.90 range.
- The comparison is **best A level vs each C at the same level**, and also best A vs best C overall. A is never compared only at L0.

## No test data in references or tuning
- Anchors, tuning and test do not overlap after L0 normalisation. This was checked: 0 shared items.
- Before scoring, the validator also checks overlap after L1, plus near-duplicates (token Jaccard ≥ 0.8). Any hit aborts the run.
- Stage 4 runs in two separate commands:
  - `--tune` writes `thresholds.json` and its hash;
  - `--test` refuses to run unless that hash exists. It reads the test file exactly once per resolver × level, and logs this.
See `normalisation.v1.1.md` section 4.

## Remaining limitations (could bias results)
1. **One author wrote all sets** (review S1). Probably favours A, because shared vocabulary helps token matching. A real C advantage could be understated, and A's absolute scores overstated.
2. **Labelling inconsistency** (review S2 and section 3). Sensitivity scoring is mandatory.
3. **Small buckets.** The hi-Latn CI is about ±18 points, so a 5-point gate is not decidable per bucket in v1. v1 is therefore **indicative only**. A go/no-go decision needs v2 with owner phrases (at least 50 navigate items per bucket).
4. **Multiple comparisons.** 3 resolvers × 3 levels × 4 buckets means some "wins" will appear by chance. Report paired differences with exact McNemar tests on the same items.
5. The models were chosen before any data existed, so there is no selection bias from tuning on test. However, the threshold grids were chosen by the author without data, and that choice is kept fixed.
6. **Text only.** No STT noise. Results do not carry over to voice until experiment 3 has run.
