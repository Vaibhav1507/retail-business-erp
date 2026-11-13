# RBOS intent bake-off (offline, text-only)

This experiment checks whether local multilingual embedding models resolve Hindi, English, Romanised Hindi and Hinglish navigation requests to verified RBOS screens better than the deterministic resolver. It is isolated from the production web app. Nothing here is imported by RBOS.

## Approval and execution status

| Stage | What | Status |
|---|---|---|
| 1 | Frozen destinations, synthetic anchors, tuning set, test set and evaluation policy | **Done** |
| 2 | Normalisation, deterministic baseline, evaluator, leakage controls and tests | **Implemented** |
| 3 | Download and checksum-verify two pinned ONNX int8 models in a temporary CI workspace | **Approved; run by the dedicated public-repository workflow** |
| 4 | Tune thresholds on tuning data, freeze them, then score the frozen test set once | **Approved; run by the same workflow** |
| 5 | Validate with real shop-owner phrases and low-end Android measurements | **Not started; requires a separately consented owner corpus and device tests** |

## Models under comparison

- **A — deterministic baseline:** token/anchor matching with the same L0/L1/L2 preprocessing.
- **C1 — multilingual-e5-small:** MIT, int8 ONNX, with the required query prefix for this semantic-similarity task.
- **C2 — paraphrase-multilingual-MiniLM-L12-v2:** Apache-2.0, int8 ONNX.

Model repositories and their approved commits are checked by the Stage 3 runner. The ONNX model and tokenizer files are SHA-256 verified before inference. The workflow uses @huggingface/transformers 4.3.2 with remote model loading disabled; all inference runs locally on the ephemeral runner.

Approximate downloaded artefacts are 118 MB per ONNX model plus tokenizer/support files. The combined download is roughly 290 MB. Weights live only in the temporary GitHub Actions runner workspace: they are **not committed, cached as project artefacts, included in the CI result artefact, or used by the production app**.

## Dataset and leakage controls

- destinations.v1.json: 40 verified routes, including the More menu.
- data/anchors.v1.json: 5 reference phrases per destination (200 total).
- data/tuning.v1.jsonl: 54 items used only to select thresholds.
- data/test.v1.frozen.jsonl: 160 items (110 navigate, 30 ask, 20 reject) scored only after thresholds are frozen.
- Language buckets: en, hi-Deva, hi-Latn, and mixed.
- MANIFEST.sha256 pins frozen data. The runner changes the model pins and config hash only in its ephemeral workspace; committed test and tuning data remain untouched.
- data/owner-phrases/ is the intake for future consented shop-owner examples. Those must become a separate v2 dataset; v1 is never edited.

## Decision rule and safety gate

For each destination, the embedding score is the maximum cosine similarity against that destination's anchor embeddings. Navigate only when top-1 exceeds the tuning-selected threshold and its margin over top-2 is sufficient. Close candidates produce an ask result; weak candidates produce not_understood.

The model can emit only a destination ID from the verified inventory. The route is looked up from that inventory; model output cannot create URLs or arbitrary actions.

The gate requires each candidate to beat A by at least 5 percentage points in every language bucket, while keeping wrong navigation at or below 2% and reject-item navigation at zero. A failed or inconclusive result means **do not replace the deterministic resolver**.

## Commands

From this directory:

~~~sh
npm ci
npm test
npm run validate
npm run lexicon:check
npm run models:bakeoff
~~~

models:bakeoff pins the current approved model revisions (guarded by expected revision prefixes), downloads and verifies files, updates the config hash in the temporary workspace, tunes A/C1/C2, freezes thresholds, and scores the test set once. CI stores only result summaries, predictions and model-pin metadata as a small report artefact.

## v1 benchmark result (2026-11-13)

The first complete local run is recorded in [reviews/semantic-v1-results-20261113.md](reviews/semantic-v1-results-20261113.md) and workflow [43418998859](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/43418998859).

**Decision: neither C1 nor C2 passes the acceptance gate. Do not integrate either model.**

- E5 is too conservative and misses most requests (2.7–4.5% correct navigation).
- MiniLM L0 reaches 14.5% correct navigation but has 11.3% wrong navigation; L2 reduces the pooled error to 2.5% but does not meet per-language improvement or the 2% safety gate.
- The deterministic baseline itself is weak and must not be presented as production-ready.
- The v1 corpus is synthetic and assistant-authored, so this is a negative model-selection result, not a measure of real shopkeeper accuracy.

The evaluation gate now checks the wrong-navigation ceiling for every language bucket as well as the pooled result. FileAccess also caches checksum-verified inputs so the frozen test file can be hashed and scored from one physical read after thresholds are frozen.

## Known limitations

- v1 is synthetic and assistant-authored; it is a method comparison, not production accuracy.
- The Romanised Hindi bucket is small. Its results are directional and cannot establish a 5-point difference with high confidence.
- Model quality for real shopkeeper speech, noisy ASR transcripts and regional accents remains unverified.
- This text-only experiment does not measure Shruti transcription accuracy, Android memory, battery, first-load time or on-device latency.
- Do not add phrase-specific fallbacks to the production resolver based on one observed example. New examples belong in versioned evaluation data, not runtime special cases.
