# Handwritten sales-bill source review v1

**Review date:** 2026-11-28  
**Scope:** visual review of the 11 source photographs re-uploaded in the current RBOS conversation.  
**Status:** provisional source transcription only; no field below is promoted to verified gold truth.  
**Privacy:** customer names, phone numbers, addresses and GST identifiers are intentionally omitted. Original photos are not committed to Git.

## Receipt and sample mapping

All 11 expected source photographs are now present in the conversation: nine Monika Electronics bills and two Anil Sports & Stationery bills. Sample IDs follow the established corpus mapping, not a newly inferred upload order.

| Sample | Cohort | Source reference visible | Visible source total | Visible line count | Source review |
|---|---|---|---:|---:|---|
| S01 / MONIKA-001 | Monika Electronics | Red number appears around 45x; exact digits unresolved in this photo | ₹17,500 | 1 | Re-reviewed; retain prior comparison and uncertainty |
| S02 / MONIKA-002 | Monika Electronics | Red number appears around 450; confirm exact digits | ₹11,000 | 1 | Description/model strings need character-level confirmation |
| S03 / MONIKA-003 | Monika Electronics | Red number appears 451 | Looks approximately ₹7,500; verify final digit/mark | 1 | Low confidence; do not score total yet |
| S04 / MONIKA-004 | Monika Electronics | Red number appears 452 | ₹17,400 | 1 | Product appears LG refrigerator; model/serial strings uncertain |
| S05 / MONIKA-005 | Monika Electronics | Red number appears 453 | ₹15,800 | 1 | Product appears 43-inch LED; exact model text uncertain |
| S06 | Monika Electronics | Red number appears 454 | ₹17,500 | 1 | LG washing machine; model/serial-like text uncertain |
| S07 | Monika Electronics | Red number appears 455 | ₹23,000 | 2 | LG washing machine ₹17,000; BPL mixer ₹6,000; preserve two rows |
| S08 | Monika Electronics | Red number appears 456 | ₹15,800 | 1 | LG LED; model/serial-like text uncertain |
| S09 | Monika Electronics | Red number appears 457 | ₹12,800 | 1 | Voltas Beko washing machine; model/serial-like text uncertain |
| S10 / ANIL-002 | Anil Sports & Stationery | 468 | Main TOTAL box appears ₹10,020; separate ₹10,000 note near signatory remains distinct | App reported 13; source count not yet verified | Dense multi-line stationery bill; keep existing comparison |
| S11 / ANIL-001 | Anil Sports & Stationery | Header number/date obscured/uncertain | ₹5,000 | App reported 10; source count not yet verified | Dense stationery bill; rates appear blank; preserve existing comparison |

## Monika source observations (provisional)

The following are visual cues for locating the source text, not approved normalized product names or verified serial numbers.

- **S01:** Description begins “Voltas Beko WM”. A following model line appears to begin WTL 80...; another identifier-like line is visible. Quantity appears one; amount and total ₹17,500. Rate cell appears blank. The red invoice reference is not sufficiently clear to resolve here.
- **S02:** Description appears to contain “... electric” and a second word resembling “Jupiter”. A code-like string appears below. Quantity one; amount/total ₹11,000. Do not promote any exact product/model spelling from this reading.
- **S03:** Description begins “USHA”; remaining wording is unclear. A model/code-like line follows. Quantity one. Amount and total appear around ₹7,500, but the written mark needs confirmation.
- **S04:** Description appears “L.G. Refrigerator”. Two following model/identifier-like lines are present. Quantity one; amount/total ₹17,400. Rate cell appears blank.
- **S05:** Description begins “43-inch LED model” with further abbreviated text. A second code-like line is present. Quantity one; amount/total ₹15,800.
- **S06:** Description appears “L.G. Washing Machine”. Two following model/identifier-like lines are present. Quantity one; amount/total ₹17,500. Rate cell appears blank.
- **S07 line 1:** Description appears “L.G. Washing Machine”; model/identifier text follows. Quantity one; amount ₹17,000.
- **S07 line 2:** Description appears “BPL mixer”; additional code-like text follows. Quantity one; amount ₹6,000. The two rows sum to the visible total ₹23,000.
- **S08:** Description appears “L.G. LED”; model/identifier-like lines follow. Quantity one; amount/total ₹15,800.
- **S09:** Description appears “Voltas Beko WM”; model/identifier-like lines follow. Quantity one; amount/total ₹12,800.

## Anil source observations (provisional)

- **S10 / ANIL-002:** The source header visibly carries bill number 468. The main printed TOTAL box appears to contain ₹10,020. A separate ₹10,000 handwritten note is visible near the authorised-signatory area. Preserve these as separate source regions. Existing app comparison says 13 detected items and ₹10,000 extracted. Source row alignment and the apparent Crown quantity/amount mismatch still need full line-by-line verification.
- **S11 / ANIL-001:** The main TOTAL box appears ₹5,000. The photo shows a dense list with approximately ten visible entries; the exact row count and several quantities/descriptions need careful transcription. Rate column appears blank. Existing app comparison says 10 detected items and ₹5,000 extracted. Do not treat blank rates as extraction misses.

## Gold-label promotion rules applied

1. Every reading in this review remains **provisional**. The photos are visually available, but the handwritten characters have not been independently reviewed by a second human.
2. Do not use the customer fields or copy any personal data into the benchmark.
3. Preserve verbatim handwriting separately from any normalized product interpretation.
4. Record source region for invoice number, date, line amount and main TOTAL box. Do not use a note near the signature as a substitute for the labelled TOTAL field.
5. Blank Rate cells are not_present, not missing extraction, once confirmed.
6. Use uncertain for ambiguous characters; exclude them from exact-match scoring until resolved.
7. The source image is not proof that the corresponding app run exists. Existing seven app runs still map to S01–S05 and S10–S11; S06–S09 still need matching current-app outputs.

## Next actions

- Second-person review the critical header numbers, dates, all line descriptions, quantities, amounts and totals.
- Resolve S03 total and S01/S02 invoice references from the source paper or a clearer crop.
- Transcribe S10/S11 row-by-row and reconcile against the existing app screenshots.
- Capture current-main app outputs for S06–S09.
- Only then mark gold-label rows verified and compute accuracy.
