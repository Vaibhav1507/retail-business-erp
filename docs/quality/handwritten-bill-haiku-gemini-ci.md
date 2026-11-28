# Handwritten sales-bill benchmark: Haiku vs Gemini

**Decision (2026-11-28):** benchmark only Claude Haiku 5.5 first, then Gemini 3.8 Flash, sequentially, against all 11 source bills. This supersedes the seven-sample restriction and exclusion of S06–S09 in the earlier planning note.

## Execution

Manual GitHub Actions workflow: .github/workflows/handwritten-bill-model-benchmark.yml
Runner: experiments/handwritten-bill-benchmark/run_benchmark.py

The workflow is manual-only. It runs all 11 Haiku requests first, then all 11 Gemini requests with the same prompt and image inputs. It records structured extraction, token usage, latency, request IDs and errors. It does not deploy or change application code.

## One-time GitHub setup

Add these repository Actions secrets:
- ANTHROPIC_API_KEY
- GEMINI_API_KEY
- HANDWRITTEN_BILLS_ARCHIVE_URL

HANDWRITTEN_BILLS_ARCHIVE_URL must be a private, access-controlled, time-limited download URL for a ZIP containing exactly S01 through S11 (jpg/jpeg/png/webp). Do not put source images in this public repository, a public release or workflow logs. The archive is downloaded only to the ephemeral runner and is not uploaded as an artifact.

Before creating the ZIP, redact customer names, phone numbers, addresses, GSTINs and signatures. Name files S01.jpg through S11.jpg according to the corpus register; do not infer mappings from opaque filenames. The runner rejects missing, duplicate or unexpected sample names.

After adding the three secrets, open Actions → Handwritten sales bill model benchmark → Run workflow. No terminal or Codespaces steps are required.

## Interpretation

Source labels are provisional and have not been independently verified. This run compares model outputs and operational characteristics; it must not claim verified accuracy. The runner's token-cost estimates use the 2026-11-28 research price snapshot and must be checked against current vendor pricing. It uses exact model IDs claude-haiku-5-5 and gemini-3.8-flash; it does not silently substitute another model.

No production settings, RBOS app code, AI Gateway, supplier invoices, Add Purchase, image-tag capture, product import, stock, GST or accounting rules are changed.
