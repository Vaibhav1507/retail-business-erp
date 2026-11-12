# Owner phrase intake

Real shop-owner phrases go here as **new versioned files**. Nothing in this folder ever modifies `test.v1.frozen.jsonl`.

## Rules
1. Add a file `owner-YYYYMMDD-<n>.jsonl` using the same schema as the test set, plus:
   - `"prov": "owner"`;
   - `"consent": "<consent record id>"`;
   - `"speakerRegion"` (optional).

   Do not store personal names of speakers. Do not store audio here.
2. When intake is closed, the files are assigned by a seeded hash of each item's text:
   - 70% to `test.v2.frozen.jsonl`;
   - 30% to `tuning.v2.jsonl`.

   Items that duplicate an existing anchor, tuning or test phrase (after L1 normalisation) are dropped and logged.
3. v1 results stay valid and are reported separately. v2 is a new evaluation with re-tuned thresholds. Results are never mixed across versions.
4. Each new file gets a SHA-256 hash in `MANIFEST.sha256`.
