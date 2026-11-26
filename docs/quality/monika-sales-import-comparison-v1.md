# Monika Electronics — handwritten sales import comparison v1

**Test ID:** MONIKA-001 (initial corpus sample S01)  
**Source bill:** handwritten invoice number appears to be approximately 454; exact final digit needs confirmation  
**Source total:** ₹17,500  
**Status:** Initial source/app visual comparison. Not yet gold-standard transcription.  
**Privacy:** Customer name, mobile and address are omitted from repository notes. Original source/app screenshots remain in the conversation and are not committed to Git.

## App extraction observed

- App reports source bill no. **153**.
- App reports date **6-11-2020**.
- App reports handwritten total **₹17,500**.
- App detects **1 item**, confidence displayed as 90%.
- Description: `Vultas bike lamp WTL 8036 UAA15PEED`.
- Brand: `Vultas`.
- Model: `WTL 8036 UAA15`.
- Quantity: `1`.
- Unit price: ₹17,500.
- Line amount: ₹17,500.
- No serial-number field is present in the displayed extracted-item review card. No serial/identifier value is displayed.

## Source-photo observations

From the handwritten source image:

- The bill date appears to be **6-11-2026**, not 6-11-2020.
- Handwritten invoice number in red appears approximately **454** (last character/number should be human-verified); app's 153 does not appear to match.
- Product description reads **Voltas Beko WM** (WM appears to denote washing machine in the written description); app's phrase “Vultas bike lamp” is a material transcription error.
- Model text appears visually similar to **WTL 8066 UAA / SPCOD**. Exact model characters need verification against the photo or appliance label. App returns WTL 8036 UAA15, so there is a likely model mismatch.
- An additional identifier line appears as `1200 644A 76A 112258`.
- A second separated identifier appears as `26111412945`, written with hyphens around it.
- The source has two serial/model-like identifier strings beneath the product description. Which is the appliance serial versus another code must be confirmed; neither is surfaced as a serial field in the app output.
- Quantity appears to be one.
- Amount and total appear to be ₹17,500.
- The source Rate column appears blank. The app displays unit price ₹17,500. Because quantity is 1, the line amount and unit price coincide, but the output does not show whether this was copied from a source field or derived from amount ÷ quantity. Preserve provenance; do not label an inferred unit price as source-transcribed.

## Field comparison

| Field | Source observation | App output | Initial assessment |
|---|---|---|---|
| Invoice number | Approximately 454; verify | 153 | Likely mismatch; source digit needs confirmation |
| Date | 6-11-2026 | 6-11-2020 | Year mismatch |
| Customer | Handwritten buyer present | Not shown in review card | Extraction/display not evidenced by supplied screenshot |
| Description | Voltas Beko WM | Vultas bike lamp | Incorrect product transcription |
| Brand | Voltas Beko | Vultas | Brand misspelling/omission of Beko |
| Model | Appears WTL 8066 UAA / SPCOD | WTL 8036 UAA15 | Likely incorrect characters/suffix; verify exact transcription |
| Identifier 1 | 1200 644A 76A 112258 | Not shown | Missing identifier |
| Identifier 2 | 26111412945 | Not shown | Missing identifier; serial role unverified |
| Quantity | 1 | 1 | Match |
| Source rate | Visually blank | ₹17,500 | Provenance unclear; may be amount/quantity derivation |
| Line amount | ₹17,500 | ₹17,500 | Match |
| Total | ₹17,500 | ₹17,500 | Match |

## Findings / severity

- **Critical:** wrong date year and likely wrong source invoice number can corrupt historical bill identification.
- **High:** description/category transcribed as “bike lamp” rather than the visible washing-machine description; brand and model are also inaccurate.
- **High:** serial/model-like identifiers are omitted, and the review card has no serial-number field. The exact role of each source identifier must first be labelled, but the text must not be discarded.
- **Medium:** unit price is populated although the source Rate cell appears blank; provenance must distinguish copied versus deterministically derived values.
- **Correct:** item count (1), quantity (1), line amount and total (₹17,500).

A 90% model confidence display is not evidence of 90% correctness; the observed field errors demonstrate it is not calibrated.

## Required follow-up for the extraction design

1. Preserve a full verbatim description rather than forcing all writing into a product-name field.
2. Add an optional serial/identifier extraction slot to the sales-bill item schema and review experience, without making serial mandatory for ordinary bills. If more than one identifier is visible, retain both with source text/context instead of silently choosing one.
3. Improve handwritten date and invoice-number reading with strict field-level validation and explicit uncertainty.
4. Do not catalogue-normalize or rephrase product descriptions. Keep raw description and optional structured brand/model fields separately.
5. Preserve the exact model text and flag uncertain alphanumeric characters; do not silently substitute a catalogue-near match.
6. Track source-vs-derived provenance for unit price and calculated fields.
7. Keep original synchronous Generate Invoice workflow; no worker, purchase, item-tag or catalogue changes.

## Evidence limitations

- Visual reading is provisional; exact invoice-number last digit, exact model characters and the role of the two identifier strings need confirmation.
- Only the app review screenshots supplied for this sample are used. No raw API payload, provider/model or timing telemetry is available.
- No accuracy percentage is calculated from this one bill.
