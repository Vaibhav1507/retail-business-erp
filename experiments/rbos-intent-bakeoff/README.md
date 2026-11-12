# RBOS intent bake-off (offline, text-only)

This experiment checks whether a local embedding model resolves Hindi, English, Romanised Hindi and Hinglish navigation phrases to RBOS screens better than a deterministic resolver. It is isolated from RBOS. Nothing here is imported by the `projects/retail-business-os` code.

## Approval status
| Stage | What | Status |
|---|---|---|
| 1 | Data, specs, config, README (this folder) | **Done** |
| 2 | Lexicon generator, normalisation, validator, Baseline A, runner, reporting, tests (no npm packages yet; Transformers.js is added with stage 3) | **Done** |
| 3 | Download about 264 MB of models, then pin revisions and SHA-256 in `config.v1.json` | Waiting for approval |
| 4 | Run tuning, freeze thresholds, then run the test set once | Waiting for approval |

## Layout
- `destinations.v1.json`: frozen route inventory with **40** destinations, reconciled against `web/src/App.tsx` and `Pages.tsx`. The earlier figure of 39 left out the More menu (`#/more`).
- `data/anchors.v1.json`: 5 reference phrases × 40 destinations. Only reference embeddings and the A phrase index are built from this file.
- `data/tuning.v1.jsonl`: 54 items (`d001`–`d054`), used **only** to choose tau/delta.
- `data/test.v1.frozen.jsonl`: 160 items (`t001`–`t160`): 110 navigate, 30 ask, 20 reject. Opened once, after thresholds are frozen.
- `data/owner-phrases/`: versioned intake for real shop-owner phrases. These go into v2 sets and never into v1.
- `specs/baseline-A.md`, `specs/normalisation.v1.1.md` (shared guard, L0/L1/L2, anchor-only lexicon generation), `specs/fairness.md` (equivalence and leakage checklist, limitations).
- `reviews/test-v1-review.md`: quality review of the frozen test set, with questionable items and the sensitivity-scoring rule. v1 is indicative only.
- `config.v1.json`: resolvers, grids, decision rule, pass gate.

## Leakage controls
1. The three phrase sets do not overlap: no shared text after L1 normalisation. This is checked by the stage 2 validator, and a duplicate fails the run.
2. Order of a run: build anchors, tune on the tuning set, write `thresholds.json`, hash the thresholds file, and only then score the test set.
3. `MANIFEST.sha256` records the hash of every data file at freeze time. A run aborts if any hash differs.
4. Any change to the data creates a new version (`*.v2.*`). Results from different versions are never mixed.

## Later stages (for reference only; do not run yet)
```
npm ci                         # stage 2
node scripts/validate.mjs      # stage 2: schema, coverage, overlap, manifest
node scripts/fetch-models.mjs  # stage 3: pin revision and record hashes
node scripts/run.mjs --tune    # stage 4
node scripts/run.mjs --test    # stage 4: refuses to run without frozen thresholds
```
Stage 2 commands: `npm test`, `npm run validate`, `npm run lexicon:check`. `fetch-models.mjs` belongs to stage 3.

## Report (stage 4)
For each resolver (A/C1/C2) × level (L0/L1/L2) × language bucket, the report gives:
- correct-navigate, wrong-navigate, ask, and not-understood rates;
- ask-precision on ambiguous items;
- reject leakage;
- p50/p95 latency;
- model size.

Pass gate: see `config.v1.json` → `gate`.

## Known gaps
- All v1 phrases are synthetic, written by the assistant, and still need the owner's review.
- `lexicon.v1.json` is generated from the anchors only and hashed in the manifest. The template-slot rule found 0 synonym pairs in v1 anchors, so L2 currently adds only screen-group tags.
- Some language buckets are small (about 25–45 items each), so differences under about 5 percentage points are not meaningful.
