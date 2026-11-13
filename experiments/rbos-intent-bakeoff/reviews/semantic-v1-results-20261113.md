# Semantic intent model bake-off — v1 result

Date: 2026-11-13  
Workflow run: [43418998859](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/43418998859)  
Dataset: synthetic, frozen v1 (160 items; 110 navigation, 30 ambiguity, 20 reject)

## Decision

**Neither embedding model is approved for RBOS integration.**

The workflow successfully installed the pinned runtime, verified the model artefacts, ran CPU inference locally on the ephemeral CI runner, tuned thresholds on the separate 54-item tuning set, and scored the frozen v1 test set. The model-selection gate failed for every model/preprocessing combination.

This is a useful negative result. It does not show that semantic understanding is inherently unsuitable; it shows that these two general-purpose multilingual embedding models, with the current anchors, preprocessing and synthetic evaluation set, are not adequate evidence for a production navigation resolver.

## All-items results

| Resolver | Correct navigation | Wrong navigation | Reject items navigated | Not understood | p50 / p95 inference |
|---|---:|---:|---:|---:|---:|
| A/L0 deterministic | 9.1% | 8.8% | 2 | 83.1% | 1.12 / 2.09 ms |
| A/L1 deterministic | 13.6% | 10.0% | 2 | 65.6% | 1.03 / 1.98 ms |
| A/L2 deterministic | 13.6% | 5.6% | 0 | 80.6% | 2.06 / 4.04 ms |
| C1/L0 multilingual-e5-small | 2.7% | 1.3% | 0 | 49.4% | 2.54 / 3.08 ms |
| C1/L1 multilingual-e5-small | 3.6% | 1.3% | 0 | 41.9% | 2.57 / 3.36 ms |
| C1/L2 multilingual-e5-small | 4.5% | 1.9% | 0 | 12.5% | 2.96 / 8.41 ms |
| C2/L0 multilingual-MiniLM | 14.5% | 11.3% | 0 | 65.0% | 2.34 / 10.03 ms |
| C2/L1 multilingual-MiniLM | 5.5% | 7.5% | 0 | 78.1% | 2.22 / 2.68 ms |
| C2/L2 multilingual-MiniLM | 9.1% | 2.5% | 0 | 66.3% | 2.40 / 3.32 ms |

*Times are from the GitHub CPU runner, not an Android phone.*

## Language-specific correct-navigation rate

| Resolver | English | Hindi (Devanagari) | Hindi (Romanised) | Mixed |
|---|---:|---:|---:|---:|
| A/L0 | 13.2% | 2.9% | 0.0% | 18.2% |
| A/L1 | 13.2% | 14.7% | 0.0% | 22.7% |
| A/L2 | 10.5% | 8.8% | 0.0% | 36.4% |
| C1/L0 | 5.3% | 0.0% | 0.0% | 4.5% |
| C1/L1 | 7.9% | 0.0% | 0.0% | 4.5% |
| C1/L2 | 7.9% | 0.0% | 0.0% | 9.1% |
| C2/L0 | 13.2% | 17.6% | 6.3% | 18.2% |
| C2/L1 | 2.6% | 0.0% | 6.3% | 18.2% |
| C2/L2 | 10.5% | 0.0% | 6.3% | 22.7% |

## Gate outcome

The declared acceptance gate requires a candidate to improve correct navigation by at least 5 percentage points over A in every language bucket, with wrong navigation no greater than 2% and no rejected item navigated.

- C1/E5: **FAIL** at L0, L1 and L2. It is very conservative, but it misses too many commands and does not improve correct navigation.
- C2/MiniLM: **FAIL** at L0, L1 and L2. L0 has a small overall gain over A but 11.3% wrong navigation; L2 lowers wrong navigation to 2.5% but loses coverage and does not improve every language bucket.
- The gate also fails under the debatable-items-excluded view; this is not an artefact of the disputed test labels.

The corrected evaluator additionally enforces the wrong-navigation ceiling independently in each language bucket, not only on the pooled total.

## Interpretation and limitations

1. Do not integrate either model into the app.
2. Do not compensate with more phrase-specific stock/customer/purchase fallbacks.
3. Baseline A is also weak. The problem is broader than selecting a model: its synthetic anchor set, normalisation and intent representation are not yet adequate.
4. v1 was written by the assistant, not collected from shopkeepers. Its language distribution and Romanised Hindi sample (only 25 total items) are too small for a production conclusion.
5. This experiment is text-only. It does not measure Shruti ASR quality, real ASR-corrupted transcripts, regional accents, shop noise, Android latency, phone memory, battery or first-load experience.
6. v1 results remain frozen. Real owner/shopkeeper phrases must be collected with consent into a separate v2 dataset and divided into tuning/test before evaluation.

## Next research step

Build a consented, versioned v2 phrase corpus from real shopkeeper requests, including:
- natural spoken Hindi, Romanised Hindi/Hinglish, English and mixed-language utterances;
- multiple speakers, regions, speaking styles and shop-noise conditions;
- paired human transcript and actual ASR transcript to separate ASR errors from intent errors;
- P1 navigation, P2 business-data questions/rejects, and P3 ambiguous intents.

Then compare a broader set of approaches using exactly the same train/tuning/test partitions. Only after a model clears the wrong-navigation gate should we evaluate it on the target Android device. No app integration is approved by this result.
