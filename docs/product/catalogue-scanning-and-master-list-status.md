# Catalogue scanning and product master list: work since the last prod deployment

Baseline: the frontend in `Personal-Lab\_upload` (21 files, including App.tsx, ProfileMenu.tsx, profile-menu.css, logo-mark.svg) is deployed to prod.
Everything below was done after that point and is **not deployed to prod** unless stated.

Note on sources: the user's earliest prompts are known only from a conversation summary, so they are paraphrased. Later prompts are quoted.

## 1. What the user asked for (in order)

1. "continue" on the existing catalogue / product master work (manufacturer registry, catalogue import, provenance).
2. Feedback that edits kept failing and the same method was being reused: "you are repeated getting failures in edits then also you keep on switching to same method".
3. "finish these 3 first then other 2". The three were:
   - local OCR for scanned/image catalogues
   - configurable category-specific attributes
   - validation against the seven LG PDFs
   The other two were the database work (backup, migrations) and the remaining tests.
4. "I don't have any path check how it was done earlier": find the existing local PostgreSQL instead of asking for a path.
5. "yes": go ahead with DB-backed integration and tenant-isolation tests.
6. "Yes, for the frontend": expose source document and page in the product list (see section 5, not done yet).
7. This request: summarise everything done, asked and pending.

## 2. What was done

### Backend and domain
- **Manufacturer registry:** structured status flags (official website verified, catalogue discovered, downloadable file available, product pages available, manual upload required, access restricted) in `Rbos.Domain/ManufacturerSources.cs`. Entries are conservative and make no unverified catalogue claims.
- **Provenance on import:** `CatalogueService` records a source document (brand, document name, URL, publication date, version, content hash, kind, importer) per import. Each product stores `source_document_id` and `source_page`. Endpoint: `GET /api/catalogue/source-documents`.
- **Validation of source metadata:** name required, kind limited to catalogue / price_list / dealer_scheme / other, URL must be http(s), date must parse, length limits.
- **Page-aware PDF parsing:** `CatalogueFileParsers.ReadPdfPaged` returns rows with a page number. Each text-bearing content stream is treated as a page, which is best-effort and may not equal logical pages. `ReadPdf` remains as a compatibility wrapper.
- **Document classifier:** `ClassifyDocument` labels a document as catalogue, price_list or dealer_scheme using generic, brand-neutral heuristics.
- **Local OCR adapter:** `LocalOcr.cs` runs Tesseract on image uploads (from `Ocr:TesseractPath` or PATH), falls back to the existing Gemini path, and degrades gracefully when Tesseract is missing.
- **Configurable attributes:** `Rbos.Domain/AttributeDefinitions.cs` has brand-neutral definitions for washing machine, refrigerator/freezer, air conditioner, TV/monitor and generic, with alias and value extraction.

### Frontend
- `ManufacturerRegistry.tsx`: browse the registry, search, status labels, source links, last-checked.
- `SetupAssistant.tsx`: source metadata fields in step 2, PDF page numbers carried into rows, `source` sent in preview and commit, restart resets.
- `csv.ts`: `ImportRow.sourcePage`.

### Database
- Migrations `039_brand_master.sql` and `040_catalogue_provenance.sql` written. `040` adds `catalog.source_document` with tenant-scoped keys, row-level security (enabled and forced) and grants.
- Found the existing local PostgreSQL 17 (`C:\Users\A1137066\pg17\pgsql\bin`, data in `C:\Users\A1137066\pgdata17`) and started it. Docker was not usable because it needs hmgroup sign-in.
- Took backups and verified restores for `rbos_dev` (61 tables) and `rbos_test` (62 tables). Dumps are in `%TEMP%\rbos-backups`.
- Repaired `scripts/database/backup-restore-drill.ps1`: truncated guard line, harmless Postgres notices aborting the run, mixed-case table names breaking row counts.
- **Applied 039 and 040 to local `rbos_test` and `rbos_dev` only.**

### Validation and tests
- Seven LG PDFs parsed through the real parser (rows / pages / kind): Kookmin 166/16/catalogue; AI DD Front Load 749/24/catalogue; Chest Freezer 884/24/catalogue; Diwali 4392/99/catalogue; Top Load AIDD 493/16/catalogue; Smart Inverter 497/16/catalogue; Scheme 621/23/dealer_scheme. Cross-document model-like overlaps: 69. The scheme file is classified `dealer_scheme`; the six catalogues are classified `catalogue`.
- Unit tests: 81 pass. Integration tests: 135 pass (124 existing plus 11 new in `CatalogueProvenanceTests.cs`). Frontend: typecheck clean, 60 tests in 9 files pass.
- New integration tests cover source recording, no-source import, all-rows-skipped, invalid source rejection, idempotent replay and changed-source conflict, tenant isolation, and 401 when anonymous.

## 3. Deployment status: what prod does and does not have

| Item | Status |
|---|---|
| Frontend in `_upload` (21 files) | Deployed |
| Backend changes from this phase (provenance, OCR, parsers, registry) | **Not deployed** |
| Migrations 039 and 040 | **Applied locally only; not applied to prod** |
| Frontend changes after `_upload` (registry UI, source fields, page numbers) | **Not deployed** |

Backend and migrations must go together. The new frontend sends `source` and `sourcePage`, so the backend must be deployed first or at the same time.

Deployment checks still to do:
- `_upload` contains no backend files. Confirm earlier phases (GST, invoices, security) are already on the server, because I cannot tell from here.
- `_upload` is at `Personal-Lab\_upload`, not inside `projects\retail-business-os`. Confirm that is the intended source.
- `_upload` holds source (`.tsx`, `.css`), not a build. The server must run `npm run build`, or `dist` must be uploaded instead.

## 4. Known limitations

- **Scanned PDFs are not OCR'd.** There is no PDF-page-to-image step, and Tesseract is not installed on this machine, so the real OCR path has never been run.
- **PDF "pages" are approximations** (one per text content stream).
- **`AttributeDefinitions` is not wired into import or persistence.** It is only unit-tested.
- **Cross-document model overlaps (69) are reported but not resolved.** There is no merge or dedupe policy between a catalogue and a dealer scheme.
- **The product list does not return source document or page**, so per-product page storage is untested.
- **The local PostgreSQL runs as a detached process** and stops on logout or reboot. Its log is `%TEMP%\pg17.log`.

## 5. Pending

Needs your decision:
- Expose source document and page in the product list and show them in the UI (you said yes; not started).
- Whether to wire `AttributeDefinitions` into import and the product form.
- Whether scanned-PDF OCR is required (needs Tesseract plus PDF-to-image).
- Policy for the 69 overlapping models: merge, keep separate, or flag for review.

Needs doing:
1. Back up the prod database, then apply 039 and 040 to prod. Not started.
2. Deploy the backend, then the frontend (with `npm run build`).
3. Add a test that reads back the stored page per product once the list exposes it.
4. Add purchase/sales regression coverage tied to the new schema (existing flows passed, no new tests).
5. Install Tesseract and test the OCR path end to end.
6. Update `improvements.md` (D-series items) to reflect migrations 039/040, the repaired drill script and the new tests.
7. Re-run the backup drill on prod before and after migration.

## 6. Rollback

- `db/rollback/039_brand_master.down.sql` and `db/rollback/040_catalogue_provenance.down.sql`.
- Pre-migration dumps of local `rbos_dev` are in `%TEMP%\rbos-backups` (named `rbos_dev_pre039_*` and `rbos_dev-*`). They live in a temp folder, so copy them somewhere durable if you want to keep them.

## Final status (2026-11-08)
Implemented and tested locally: migration 041 multi-source references, actual PDF page extraction, mixed-PDF OCR via Tesseract, category attributes, duplicate review flags, Source column UI, tenant isolation, import-purchase-sale regression. Totals: unit 86, integration 142, frontend 62 passing. Unverified: end-to-end ProductFileReader on a mixed PDF, dedicated product details view, production behaviour. See improvements.md change log for limitations.


## Local-pilot readiness verification (2026-11-08)

Verification only. No features added, nothing deployed, no production migration run.

### Verified
- Unit 86/86, integration 142/142, frontend 62/62 (10 files), tsc clean. API build: 0 errors, 1 warning (CA1416).
- Migrations 039, 040, 041 are in public.schema_migrations in rbos_dev and rbos_test with identical checksums (87E58EFC, D811A1C9, F9DFBB2D).
- Seven LG PDFs parsed with the actual-page reader. Rows never exceed the page count. Catalogues: 1442 WM TL (16 pages, 141 rows), 772 front load (22, 405), Chest Freezer (23, 501), Diwali (95, 1983), Top Load AIDD (16, 320), Top Load Smart Inverter (15, 273). Scheme for Nov26 (13, 298) classified dealer_scheme, separate from catalogues. One page in each of Diwali, AIDD, Smart Inverter and Scheme needs OCR.
- Provenance, multiple source references, duplicate flags (brand + normalized model, never merged), tenant isolation, attribute persistence and imported-product purchase/sale regression covered by integration tests.
- Existing purchase, sales, inventory and accounting tests pass within the 142.
- Tesseract 5.4.0 at C:\Program Files\Tesseract-OCR\tesseract.exe, languages eng and osd. Real OCR unit test passes.

### Not verified
- The seven PDFs were verified at parser level, not committed through the import API (rbos_dev has 0 source documents, 36 products). OCR of the scanned pages of those real files was not run.
- ProductFileReader mixed-PDF flow has no end-to-end test.
- Linux container PDF rendering and OCR not run.

### OCR setup on deployment
- Windows: winget install UB-Mannheim.TesseractOCR (5.x, keep English data); default path is auto-detected, or set Ocr:TesseractPath.
- The Dockerfile runtime image (aspnet:10.0, Linux) does not include Tesseract. Add tesseract-ocr and tesseract-ocr-eng to it, or set Ocr:TesseractPath. Not changed here (deployment file). Without Tesseract, text pages import and scanned pages are reported as skipped.

### CA1416
- Raised at PdfPageReader.cs line 48 (PDFtoImage Conversion.ToImage). PDFtoImage supports Windows, Linux, macOS, Android, iOS, browser; the analyzer cannot prove the platform at the call site.
- No effect on Windows (pilot) or Linux x64 in principle. Native libraries (pdfium, SkiaSharp, fonts) on Linux are untested; verify in the container before relying on OCR there.
