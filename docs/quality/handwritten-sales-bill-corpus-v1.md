# Handwritten sales-bill corpus — initial batch v1

**Status:** provisional visual transcription; not yet a gold-standard benchmark  
**Received:** 2026-11-26  
**Samples:** 11 source bills tracked so far (10 in the initial batch plus one additional Anil Sports bill supplied during extraction comparison)  
**Privacy:** customer names, phone numbers and addresses are intentionally excluded. Original photos are not committed to this repository.

## Corpus composition

| Sample | Source format | Items visible | Visible total (provisional) | Initial difficulty notes |
|---|---|---:|---:|---|
| S01 | Monika Electronics | 1 | ₹17,500 | Faint/soft writing; multi-line product description; long handwritten identifier; extra handwritten number near lower description area |
| S02 | Monika Electronics | 1 | ₹11,000 | Brand/product wording and model text need verification; mixed cursive and block characters |
| S03 | Monika Electronics | 1 | ₹7,500 (verify) | Low contrast/soft focus; short product name and long identifier |
| S04 | Monika Electronics | 1 | ₹17,400 | Tilted photo; refrigerator description; long model and serial-like strings |
| S05 | Monika Electronics | 1 | ₹15,800 | Tilted/partially occluded top; description includes a 43-inch LED reference; secondary handwritten string ambiguous |
| S06 | Monika Electronics | 1 | ₹17,500 | Washing-machine description; long model/serial-like strings; slight perspective |
| S07 | Monika Electronics | 2 | ₹23,000 | Two line items; first amount ₹17,000 and second ₹6,000; multiple model/serial-like strings; important row-separation test |
| S08 | Monika Electronics | 1 | ₹15,800 | LED description; model/serial-like strings; relatively clear |
| S09 | Monika Electronics | 1 | ₹12,800 | Washing-machine description; multi-line model/identifier; perspective and handwriting variation |
| S10 | Anil Sports & Stationery — bill no. 468 | App detected 13; source row count needs verification | Main TOTAL box appears ₹10,020; separate ₹10,000 note near signatory | Different invoice template; soft focus; dense list; tightly spaced rows; total conflict |
| S11 | Anil Sports & Stationery — additional ₹5,000 bill | App detected 10 | ₹5,000 | Same retailer/template family; source rates appear blank; quantity and customer-field mapping need verification |

**Important:** visible totals and item counts above are provisional observations from the supplied photographs, not verified accounting ground truth. Do not use them to claim an extraction accuracy score until independently confirmed.

## Provisional line-level observations

The descriptions below preserve only the portions that are reasonably visible. They are deliberately not normalized into catalogue names.

| Sample | Visible description (provisional) | Quantity | Amount | Uncertainty |
|---|---|---:|---:|---|
| S01 | “Voltas Beko WM” (likely washing machine); following model/identifier lines are not yet reliably transcribed | One | ₹17,500 | High for model/identifier; medium for description |
| S02 | Product wording resembles “Fibril/Fibr… ATA electric”; a following word resembles “Jupiter”; identifier resembles “FIBE26J96179” | One | ₹11,000 | High — do not treat these strings as ground truth |
| S03 | “USHA …” (remaining product wording unclear); identifier resembles “VAL…251 / 001620” | One | ₹7,500 | High |
| S04 | “L.G. Refrigerator”; model resembles “GLD 2136ZHEG”; serial-like line resembles “604 NRZX592853” | One | ₹17,400 | Medium for product family; high for exact identifiers |
| S05 | “43-inch LED model …”; following handwritten string is unclear | One | ₹15,800 | High for exact model |
| S06 | “L.G. Washing Machine”; model resembles “PK100B3SK2Z”; serial-like line resembles “G10 NWUP448958” | One | ₹17,500 | Medium/high for exact identifiers |
| S07 / line 1 | “L.G. Washing Machine”; model resembles “PX900H7 …”; serial-like line resembles “G10 NW HS203785” | One | ₹17,000 | High for exact model |
| S07 / line 2 | “BPL Mixer”; model/identifier text is not reliably transcribed | One | ₹6,000 | High |
| S08 | “L.G. LED”; model resembles “32LR600B6LC”; serial-like line resembles “607TYXB02730” | One | ₹15,800 | Medium/high for exact identifiers |
| S09 | “Voltas Beko WM”; model resembles “WFT 80 …”; serial-like line resembles “1200 762A 26C000 134” | One | ₹12,800 | High for exact identifiers |
| S10 | Dense handwritten stationery line items; do not normalize or guess names from this photo | Multiple | Not confirmed | High across row transcription |

## Labeling rules before scoring

1. Keep S01–S09 as the Monika cohort and S10–S11 as the Anil Sports & Stationery cohort. **All eleven remain in the evaluation.**
2. Never silently convert a provisional visual reading into a verified label. Mark each field as `verified`, `uncertain` or `unreadable`.
3. Do not infer a model/serial number from a catalogue, brand pattern or other bills. Similarity is not proof of character identity.
4. Keep the original writing as a verbatim transcription where possible; store a normalized value separately only after the verbatim value is verified.
5. Exclude customer name, phone, address and other personal data from the evaluation fixture and logs.
6. Record item count/order, quantity, unit rate (including blank), line amount, discounts/tax if present, and final total separately.
7. Distinguish a drawn cancellation/continuation stroke in the amount column from an amount or additional row.
8. For S10, establish the exact row count and whether the ₹10,000 signatory-area note changes the ₹10,020 main TOTAL field before scoring. For S11, verify quantities and confirm that blank source rates are not incorrectly scored as extraction misses.

## Next benchmark actions

- Human verification of provisional descriptions, identifiers, line amounts and totals.
- Capture the current RBOS synchronous Generate Invoice extraction output for all eleven source images, with provider/model/config and stage timings.
- Compare output to verified labels field by field; do not score uncertain labels.
- Keep these eleven as the current development/evaluation batch. As the corpus grows, reserve a separate untouched holdout set before tuning prompts/models.
- Do not upload original customer-bearing photos to GitHub. Store any future redacted benchmark images in an access-controlled test fixture location approved for this purpose.

## Current evidence limitations

- The photos are not stored in the repository; sample IDs refer to upload order in the 2026-11-26 conversation.
- This document is a first-pass visual transcription by inspection, not a second-person verified ground truth.
- No RBOS extraction run has yet been performed on these ten samples in this phase.
- No accuracy or latency score is claimed.


## Anil Sports comparison update — 2026-11-26

- Two separate Anil Sports & Stationery bills now have source-to-app comparisons: [ANIL-001 ₹5,000](./anil-sports-import-comparison-v1.md#anil-001--5000-bill) and [ANIL-002 ₹10,020](./anil-sports-import-comparison-v1.md#anil-002--10020-bill-source-bill-no-468).
- ANIL-002 is the Anil source bill from the original ten-photo batch; ANIL-001 is the additional Anil source bill supplied afterward. They are both retained. Current corpus therefore contains 9 Monika bills + 2 Anil bills (11 total).
- This corrects the earlier single-Anil-sample inventory. No source image or customer personal details are committed to Git.
