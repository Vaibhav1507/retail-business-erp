# Anil Sports & Stationery — handwritten sales import comparison v1

**Date recorded:** 2026-11-26  
**Scope:** Source-photo-to-RBOS-output comparison for two already-issued handwritten sales bills.  
**Status:** Initial visual comparison. Not yet a fully verified gold-standard transcription.  
**Privacy:** Customer identity, personal phone numbers and addresses are omitted. Original photos and app screenshots remain in the conversation; they are not committed to Git.

## Comparison summary

| Test ID | Source bill | Source total field | RBOS extracted header | Detected lines | Initial result |
|---|---|---:|---:|---:|---|
| ANIL-001 | First Anil Sports bill | ₹5,000 | ₹5,000 | 10 | Total and line count agree at summary level; several line descriptions/quantities and customer-field mapping need checking |
| ANIL-002 | Second Anil Sports bill, bill no. 468 | ₹10,020 | ₹10,000 | 13 | Header total conflict; at least one visible line quantity/amount mismatch; possible line-count issue to verify |

The two bills must remain separate test cases. The second case is not a retry of the first. Do not merge their labels or extraction outputs.

## ANIL-001 — ₹5,000 bill

### App output captured

- RBOS summary: 10 detected items.
- App-extracted source bill number: 192.
- App-extracted date: 20/11/24. Source date/invoice-number transcription still needs direct verification because the header writing is soft/partly obscured.
- App-extracted handwritten total: ₹5,000.
- App displayed the same warning repeatedly: “Some line quantities or prices are unreadable. Review and enter them manually.”
- Visible line examples:
  - Item 1: description “DAT”, quantity 4, line amount ₹1,200; unit price blank.
  - Item 2: “Ball”, app quantity 1; the source row appears to show quantity 4 (likely quantity mismatch).
  - Item 3: “Foot Bowl”, quantity 1, line amount ₹650; unit price blank.
  - Item 4: “Ludo”, quantity 8, line amount ₹400; unit price blank.
  - Item 6: “chill”, quantity 2, line amount ₹200; unit price blank.
  - Item 7: “Sukhwar”, quantity 1, line amount ₹600; unit price blank.
  - Item 9: “Willy Duel”, quantity 1, line amount ₹650; unit price blank.
  - Item 10: “Mocal”, quantity 10, line amount ₹150; unit price blank.
- Customer form screenshot shows customer name value “4970” and an empty required mobile field. This is suspicious and requires checking against the source; do not accept it as verified customer data.

### Source comparison notes

- The source bill total is ₹5,000, matching the app's header total.
- The source has handwritten line amounts, while the Rate column appears blank on this source photo. Therefore, blank unit-price fields are not automatically an extraction failure. Do not infer rates merely to fill them; a derived rate may be calculated only under an explicitly approved deterministic rule and must be distinguishable from a source-transcribed rate.
- The source quantity next to the line which RBOS labels “Sukhwar” also appears higher than the app's quantity 1 (provisionally 12). Mark this as a likely quantity error pending final row-to-description alignment.
- The app descriptions “DAT”, “Foot Bowl”, “Ludo”, “chill”, “Sukhwar”, “Willy Duel” and “Mocal” are provisional visual readings, not approved product-name ground truth.
- Repeated identical warnings create noise. Report one consolidated warning with a list of affected item/field pairs rather than one repeated bullet per line.

### Initial classification

- Header total: matches.
- Detected item count: 10 according to the app; source-to-output row-by-row count still requires verification.
- Unit price: blank values may be correct because the source Rate column appears blank.
- Quantity: at least one likely mismatch (Sukhwar line).
- Description: needs manual transcription/verification.
- Customer data: suspicious mapped name; mobile empty.
- Warning presentation: duplicate warning spam.

## ANIL-002 — ₹10,020 bill (source bill no. 468)

### App output captured

- RBOS summary: 13 detected items.
- App-extracted bill number: 468.
- App-extracted date: 20/1/26 (as displayed).
- App-extracted handwritten total: ₹10,000.
- The visible cards show confidence values mostly between 60% and 90%. These model-provided confidence percentages are not calibrated accuracy measurements.
- The customer-name field is populated as “Middle school, Tikapur”; the mobile field is blank.

### Visible extracted line values

Only values visible in the supplied app screenshots are recorded below. Missing screenshot portions are marked not visible, not inferred.

| App item | Extracted description | Qty | Unit price | Displayed line amount | Initial note |
|---:|---|---:|---:|---:|---|
| 1 | Sticker | 20 | ₹40 | ₹800 | Arithmetic consistent |
| 2 | Poo chhin | 10 | ₹60 | ₹600 | Description uncertain |
| 3 | rly Dine | 10 | ₹60 | ₹600 | Description uncertain |
| 4 | Crown | 2 | ₹80 | ₹160 | Source row appears qty 10, rate ₹80 and amount ₹800; likely quantity/amount extraction error |
| 5 | Fest Rod | Not visible | Not visible | Not visible | Lower part of card not supplied |
| 6 | Not visible in supplied screenshots | — | — | — | Cannot assess |
| 7 | Hull s | 2 | ₹650 | ₹1,300 | Arithmetic consistent; description uncertain |
| 8 | D07 Full ice | 12 | ₹75 | ₹900 | Description may be a misread; verify from source |
| 9 | Tenni Dull | Not visible | Not visible | Not visible | Only description visible |
| 10 | Shutte | 4 | ₹200 | ₹800 | Source needs direct row alignment; do not assume correct |
| 11 | Medal | 2 | ₹150 | ₹300 | Arithmetic consistent |
| 12 | Ribbon | 12 | ₹25 | ₹300 | Arithmetic consistent |
| 13 | Plastic Dull | 12 | ₹10 | ₹120 | Arithmetic consistent |

### Source comparison notes

1. The handwritten value in the printed TOTAL box appears to be **₹10,020**. A separate handwritten **₹10,000** appears below in/near the authorised-signatory area.
2. RBOS reports ₹10,000. This may mean the extractor selected the separate lower annotation rather than the value in the actual TOTAL field. Preserve both source observations; do not automatically overwrite one with the other.
3. The user identifies this test bill as the ₹10,020 bill. The final accounting interpretation of the lower ₹10,000 annotation is not established by the photo alone.
4. Item 4 is a clear candidate error: app shows Crown qty 2 × ₹80 = ₹160; the corresponding source row appears to show qty 10 and amount ₹800. Verify row alignment during gold-label creation.
5. The source table is dense and contains tightly spaced rows, wrapped descriptions, and handwritten quantity/rate/amount columns. The app's 13-item count must be checked against every source row; a row may be omitted, merged or split. Do not claim a confirmed missed row until the row-by-row transcription is verified.
6. Items 5, 6 and 9 cannot be fully evaluated from the supplied app screenshots because their values are cropped/not present.
7. App confidence percentages (60–90%) do not prove correctness. The quantity/total discrepancies show why confidence must be evaluated against ground truth.

### Initial classification

- Bill number/date: app display appears consistent with source header; date format should be verified.
- Total: conflict — source TOTAL box appears ₹10,020; app says ₹10,000; separate lower annotation also says ₹10,000.
- Item count: app says 13; verify source row count.
- Line arithmetic: item 4 likely wrong.
- Descriptions: several uncertain/possibly misread.
- Confidence: not calibrated.
- Source structure: dense multi-row handwritten table; needs row segmentation and column alignment evaluation.

## Cross-bill findings

| Finding | ANIL-001 (₹5,000) | ANIL-002 (₹10,020) |
|---|---|---|
| Total header | App matches visible total | App differs from main TOTAL box; lower annotation is ambiguous |
| Item cards | 10 | 13 |
| Handwritten descriptions | Some questionable transcriptions | More description ambiguity and dense rows |
| Quantity | At least one likely mismatch | Item 4 likely qty mismatch |
| Unit price | Source rate cells appear blank; do not score blank as miss yet | Many visible extracted prices; row alignment must be checked |
| Warnings | Same warning repeated many times | No equivalent repeated-warning evidence in supplied screenshots |
| Customer mapping | Suspicious “4970”; mobile blank | Name field populated; mobile blank |
| Confidence | 60–90% range visible | 60–90% range visible; not calibrated |

## Required next steps before scoring

1. Independently transcribe every source line for both bills, preserving the original handwriting verbatim where legible.
2. Record source row index, description, quantity, rate, line amount and any overwritten/corrected value separately.
3. Distinguish blank source fields from missed extraction. In particular, do not penalise ANIL-001 for blank unit prices until the original Rate column has been verified as blank.
4. Resolve ANIL-002 total-field semantics with the shopkeeper: main TOTAL box versus the separate ₹10,000 signatory-area notation. Keep both labels in the dataset until clarified.
5. Verify whether ANIL-002 has 13 or 14 source item rows, and align each app card to a source row.
6. Once labels are verified, calculate field exact-match, row precision/recall, quantity/amount exact-match, total-field exact-match and unsupported-value rate.
7. Capture current pipeline provider/model/config and stage-level timings during an actual controlled extraction run. The supplied screenshots alone do not reveal which model ran or how long each stage took.

## Evidence limitation

This is an initial comparison based on source photographs and app screenshots in the 2026-11-26 conversation. Some source handwriting and app card values are cropped or ambiguous. No extraction accuracy percentage is claimed. Original images/screenshots are intentionally not stored in Git.
