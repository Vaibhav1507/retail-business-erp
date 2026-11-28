# Handwritten sales-bill benchmark: Haiku vs Gemini

**Classification:** Optional research-only harness. It is not part of the RBOS V2 production extraction path, and merging this workflow does not authorize executing it.

**Models:** Claude Haiku 5.5 followed by Gemini 3.8 Flash, run sequentially against 11 redacted source bills. The workflow can make up to **22 live model requests** (11 per provider). It is manual-only and requires `authorize_paid_benchmark_calls=true`; the input defaults to false and a run without explicit authorization skips the benchmark job.

## Execution

Manual GitHub Actions workflow: `.github/workflows/handwritten-bill-model-benchmark.yml`
Runner: `experiments/handwritten-bill-benchmark/run_benchmark.py`

The workflow records structured extraction, token usage, latency, request IDs and errors. It does not deploy or change application code. Do not run it merely because it has been merged. Run only after a separate explicit spend/data authorization and after verifying current provider pricing and the provider account billing tier.

## One-time GitHub setup

Add these repository Actions secrets only when a benchmark run is approved:
- `ANTHROPIC_API_KEY`
- `GEMINI_API_KEY`
- `HANDWRITTEN_BILLS_ARCHIVE_URL`

The archive URL must be private, access-controlled and time-limited. The ZIP must contain exactly S01 through S11 (jpg/jpeg/png/webp). Redact customer names, phone numbers, addresses, GSTINs and signatures before packaging. The archive is downloaded only to the ephemeral runner and source images are not uploaded as artifacts. Extracted fields/metrics are uploaded for seven days; review them as potentially commercially sensitive data.

## Interpretation and boundaries

Source labels remain provisional; the workflow compares outputs and operational metrics, not verified accuracy. It must not claim field accuracy until independent gold labels are complete. Token-cost estimates use the research price snapshot in the runner and must be rechecked against current official vendor pricing.

This experiment is separate from the approved V2 provider hierarchy. It does not change the production provider chain, production settings, AI Gateway, supplier invoices, Add Purchase, item-tag capture, catalogue imports, stock, GST or accounting rules. No benchmark run is being started as part of this merge.
