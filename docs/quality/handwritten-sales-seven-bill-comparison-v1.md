# Handwritten sales-bill extraction — seven-case comparison v1

**Scope:** Existing synchronous Generate Invoice handwritten sales-bill import.
**Evidence date:** 2026-11-26
**Samples:** 7 app extraction runs compared with their source handwritten bills: 2 Anil Sports & Stationery + 5 Monika Electronics.
**Privacy:** Customer names, phone numbers and addresses are omitted. Original photos and application screenshots remain in the conversation and are not committed to Git.

## Executive result

All seven observed extraction runs are now recorded in this comparison report. This is a complete comparison record for the seven runs supplied so far, not a gold-standard accuracy benchmark: several source characters and exact row values still need human confirmation.

| Test | Retailer | Source total (visual) | App total | App detected rows | Initial result |
|---|---|---:|---:|---:|---|
| ANIL-001 | Anil Sports & Stationery | ₹5,000 | ₹5,000 | 10 | Total agrees; likely quantity and description issues; repeated identical warnings |
| ANIL-002 | Anil Sports & Stationery | ₹10,020 | ₹10,000 | 13 | Main TOTAL field differs; at least one likely quantity/amount error |
| MONIKA-001 | Monika Electronics | ₹17,500 | ₹17,500 | 1 | Total/quantity agree; description, model, date and invoice reference appear wrong; identifiers omitted |
| MONIKA-002 | Monika Electronics | ₹11,000 | ₹11,000 | 1 | Amount agrees; date/reference appear wrong; description and model/identifier text garbled |
| MONIKA-003 | Monika Electronics | Appears about ₹750; verify | ₹156 | 1 | Material price/total discrepancy; date appears wrong; description/identifier not cleanly separated |
| MONIKA-004 | Monika Electronics | ₹17,400 | ₹17,400 | 1 | Header and total appear to agree; serial-like identifier absent; source Rate field appears blank |
| MONIKA-005 | Monika Electronics | ₹15,800 | ₹15,800 | 1 | Header and total appear to agree; identifier appears misread; brand/model separation incomplete |

## Case-by-case observations

### ANIL-001 — ₹5,000 bill

- App header: bill reference 192; date 20/11/24; total ₹5,000; 10 detected items.
- Source total appears ₹5,000. Source bill number/date require final label confirmation.
- Several descriptions are questionable. Examples shown by the app include “DAT”, “Ball”, “Foot Bowl”, “Ludo”, “chill”, “Sukhwar”, “Willy Duel” and “Mocal”; these are not treated as verified transcriptions.
- At least one source quantity appears greater than the app value (the Ball row appears source qty 4, app qty 1; the Sukhwar row also appears to have a higher source quantity than app qty 1). Exact row alignment needs verification.
- The source Rate column appears blank. Blank app unit-price fields are not automatically misses; the source has line amounts and total. Do not silently derive a rate and call it source-read.
- The app repeated the same unreadable quantity/price warning across multiple lines. This should be one consolidated warning with affected item/field references.
- The app's customer-name mapping in one screenshot appeared as a number-like value; a later screenshot showed a customer name/phone. Confirm the exact capture session before assessing header field accuracy.

### ANIL-002 — source bill no. 468, ₹10,020

- App header: bill no. 468; date 20/1/26; detected 13 items; extracted total ₹10,000.
- The handwritten value in the printed TOTAL box appears to be ₹10,020. A separate ₹10,000 annotation appears near the authorised-signatory area. Preserve both as distinct source observations; do not substitute the signatory note for the printed total.
- App item “Crown” is shown as qty 2 × ₹80 = ₹160. The corresponding source row appears to show qty 10 and amount ₹800. This is a likely line quantity/amount mismatch.
- App descriptions include several ambiguous spellings (e.g. “Poo chhin”, “rly Dine”, “Hull s”, “D07 Full ice”, “Tenni Dull”, “Shutte”, “Medal”, “Ribbon”, “Plastic Dull”). Do not correct these from product familiarity alone.
- App reported 13 items; source row count and one-to-one row alignment still need verification.
- Some app card values were cropped in the screenshots; no missing values are inferred.

### MONIKA-001 — ₹17,500, Voltas Beko washing-machine bill

- App header: bill no. 153; date 6-11-2020; total ₹17,500; one item; confidence shown 90%.
- Source bill number in red appears approximately 454 (verify the last digit); source date appears 6-11-2026.
- Source product description appears “Voltas Beko WM”. App description reads “Vultas bike lamp”.
- App brand “Vultas” and model “WTL 8036 UAA15” differ from the source's provisional reading (model looks like WTL 8066 UAA / SPCOD; exact characters need verification).
- Two additional identifier strings appear beneath the description: one resembles “1200 644A 76A 112258”; another resembles “26111412945”. The exact semantic role of each (serial vs other code) needs confirmation.
- Neither identifier is shown in the app output, and the review card has no serial field in the baseline.
- Quantity 1, line amount and total ₹17,500 agree. Source Rate column appears blank while app unit price is populated; source-vs-derived provenance is unclear.

### MONIKA-002 — ₹11,000, one-item electronics bill

- App header: bill no. 150; date 6.11.2024; total ₹11,000; one item; confidence shown 85%.
- Source invoice number in red appears to be around 450 (verify); source date appears 6-11-2026.
- Source description spans multiple lines. The visible wording resembles an “... ATA elakki” product followed by “multi-jupiter” and identifier “FIBE26J96179”; exact spelling must be confirmed from the original.
- App description is concatenated/garbled (display begins “F bit 8 ata elakkiMuffin jupiterIN F1BE263…”), and the ending is clipped by the single-line input width.
- The model/identifier has not been separated into its own verified field. The app shows brand/model fields blank.
- App total ₹11,000 and quantity 1 agree at summary level. The source Rate column appears blank, so unit price ₹11,000 may have been carried from the line amount; provenance is not exposed.
- The customer name/mobile shown in the app were populated, but no customer personal data is reproduced here.

### MONIKA-003 — bill no. 451, one-item bill with low-value amount discrepancy

- App header: bill no. 451; date 27/11/2022; total ₹156; one item; confidence shown 90%.
- Source invoice number appears 451 and source date appears 7/11/2026.
- Source amount handwriting is soft; the total appears closer to about ₹750 than ₹156, but this is uncertain and must be verified against the original before it is used as a numeric gold label.
- App description reads “0564 gm Mild stn. VAL07251 001626”. Source description appears to contain a product/brand phrase followed by identifier/model text resembling “VAL07251” and another numeric/alphanumeric fragment.
- App puts VAL07251 in Model while the remaining text is mixed into Description. The exact product wording and second identifier need human transcription.
- Date and total are likely wrong; do not calculate a numeric accuracy score until the source amount is confirmed.

### MONIKA-004 — bill no. 452, LG refrigerator, ₹17,400

- App header: bill no. 452; date 5/11/26; total ₹17,400; one item; confidence shown 85%.
- Source bill number/date and total appear consistent with app at header level.
- Source product: LG refrigerator. Source model appears GLD 2136ZHEG.
- Source also has an additional identifier resembling “604 NRZX592853”. The app description ends with “No” but the identifier is not shown in a dedicated serial field.
- App description reads “L. G. Refreteri Model GLD 2136ZHEG No”; brand/model appear reasonably close, though spelling is not verbatim.
- Quantity 1 appears correct. Source Rate column appears blank; app unit price is blank and warns about unreadable quantities/prices. Blank rate is consistent with the source layout; this warning should be more precise and not imply quantity is unreadable.

### MONIKA-005 — bill no. 453, 43-inch LED, ₹15,800

- App header: bill no. 453; date 5/11/26; total ₹15,800; one item; confidence shown 90%.
- Source bill number/date and total appear consistent with app.
- Source description is “43-inch LED model BISG.” followed by an identifier/model-like string that appears to be “05112026 7816” (exact characters uncertain).
- App description reads “43-inch LED model BISG. 05112026796.” and puts only “BISG.” in Model; Brand remains blank.
- The long identifier appears misread/truncated, and brand/model/identifier boundaries are not reliably extracted.
- Quantity 1 and total/line amount ₹15,800 agree. Source Rate column appears blank; unit price provenance should be explicit.

## Cross-case findings

1. **Identifiers are lost or blended.** MONIKA-001 and MONIKA-004 show separate model/serial-like lines that do not surface in the app review. MONIKA-002 and MONIKA-005 show identifier-like strings merged into the description/model.
2. **Invoice header transcription is inconsistent.** MONIKA-001/002/003 have date-year discrepancies; MONIKA-001/002 invoice numbers appear misread. ANIL-002 has a printed total versus signatory-note conflict.
3. **Product descriptions are not reliably verbatim.** “Vultas bike lamp” for a Voltas Beko washing machine is a material semantic error, not a cosmetic spelling issue.
4. **Line quantity/amount alignment needs row-level validation.** ANIL-001 and ANIL-002 contain likely quantity errors.
5. **Blank source Rate cells must remain blank.** Do not treat blank unit rates as a missing extraction when the original Rate column itself is empty. If a rate is derived from amount/quantity, mark it as calculated, not handwritten.
6. **Model confidence percentages are not calibrated.** A 90% label appeared alongside material field errors.
7. **Warning messages should be consolidated.** The same warning repeated once per affected line makes review noisy.
8. **No image or source handwriting should be normalized away.** Preserve full description text and split structured model/serial fields separately.

## P0 implementation acceptance

- [ ] Full handwritten product description preserved verbatim in a multiline, automatically height-adjusting review field (no manual resize handle).
- [ ] Brand, model and serial numbers exposed as separate structured fields.
- [ ] Serial numbers editable in review and passed into the existing sale-line serials array after user confirmation.
- [ ] Original model/serial text remains in the full description even if the structured classification is uncertain.
- [ ] Invoice number read from its labelled field; leading zeroes preserved.
- [ ] Date read from the labelled date field in Indian DD/MM/YYYY order and returned as ISO date without guessing the year.
- [ ] Printed TOTAL box distinguished from signatory-area notes.
- [ ] One review warning per issue category, not repeated identical warnings for each line.
- [ ] Existing sales posting, stock, GST, tenant, payment, offline and duplicate-serial validations remain authoritative.
- [ ] Existing floating chat launcher remains unchanged.

## Limits of this report

- These are seven recorded app extraction comparisons, not seven independently verified gold-standard transcriptions.
- Some handwritten values (especially MONIKA-003 amount and some model/serial characters) remain uncertain.
- The app screenshots do not reveal provider/model used or stage latency; no p50/p95 or accuracy score is claimed.
- Original images and app screenshots remain in this conversation and are not committed to Git.
