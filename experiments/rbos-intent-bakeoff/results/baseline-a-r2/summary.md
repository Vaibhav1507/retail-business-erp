# Run baseline-a-r2

> v1 phrases are synthetic and exploratory; small language buckets (hi-Latn ~25 items) cannot resolve a 5-point difference.

Conclusion: consistent across all-items and debatable-excluded scopes

## Scope: all

| resolver/level | tau | delta | correct-nav | wrong-nav | correct-ask | ask-precision | reject-navigated | not-understood | p50 ms | p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|
| A/L0 | 0.7 | 0.3 | 9.1% | 8.8% | 0.0% | 0.0% | 2 | 83.1% | 1.41 | 3.60 |
| A/L1 | 0.6 | 0.3 | 13.6% | 10.0% | 0.0% | 20.8% | 2 | 65.6% | 1.32 | 2.94 |
| A/L2 | 0.7 | 0.25 | 13.6% | 5.6% | 0.0% | 14.3% | 0 | 80.6% | 2.90 | 7.45 |

| resolver/level | en | hi-Deva | hi-Latn | mixed |
|---|---|---|---|---|
| A/L0 | 13.2% (n=38) | 2.9% (n=34) | 0.0% (n=16) | 18.2% (n=22) |
| A/L1 | 13.2% (n=38) | 14.7% (n=34) | 0.0% (n=16) | 22.7% (n=22) |
| A/L2 | 10.5% (n=38) | 8.8% (n=34) | 0.0% (n=16) | 36.4% (n=22) |

Gate: not evaluated (no C resolver results).

## Scope: excludingDebatable

| resolver/level | tau | delta | correct-nav | wrong-nav | correct-ask | ask-precision | reject-navigated | not-understood | p50 ms | p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|
| A/L0 | 0.7 | 0.3 | 8.6% | 7.2% | 0.0% | 0.0% | 2 | 84.9% | 1.41 | 3.60 |
| A/L1 | 0.6 | 0.3 | 13.3% | 8.6% | 0.0% | 17.4% | 2 | 67.1% | 1.31 | 2.95 |
| A/L2 | 0.7 | 0.25 | 14.3% | 3.9% | 0.0% | 14.3% | 0 | 81.6% | 2.90 | 7.70 |

| resolver/level | en | hi-Deva | hi-Latn | mixed |
|---|---|---|---|---|
| A/L0 | 13.2% (n=38) | 3.0% (n=33) | 0.0% (n=14) | 15.0% (n=20) |
| A/L1 | 13.2% (n=38) | 15.2% (n=33) | 0.0% (n=14) | 20.0% (n=20) |
| A/L2 | 10.5% (n=38) | 9.1% (n=33) | 0.0% (n=14) | 40.0% (n=20) |

Gate: not evaluated (no C resolver results).

