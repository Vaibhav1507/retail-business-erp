# Flexible billing, tag extraction and mobile invoice fixes — 2026-11-23

- **Repository:** `Vaibhav1507/retail-business-erp`
- **Baseline:** `main` at `3511a5fe69317dc84bc9e650bf2f059a1715653c`
- **Branch:** `fix/flexible-billing-mobile-tag-validation`
- **PR:** Pending creation.
- **Production deployment/migration:** Not performed.

## Scope

Resolve the billing defects reported from the Monika Electronics pilot screenshots in one PR: field-level validation, required customer name/mobile, duplicate serial detection, stable tag-photo action layout, deterministic tag name fallback, optional identifiers on ad-hoc invoice lines, responsive mobile invoice preview and invoice-only printing.

## Implementation

- Removed the domain rule that rejected serial, IMEI and barcode snapshots on ad-hoc (non-catalogue) sale lines. These values remain invoice transaction metadata; barcode learning remains limited to catalogue-linked lines.
- Added invoice-wide, case-insensitive duplicate serial/IMEI rejection before posting.
- Fixed the previous-sale identifier warning query to exclude the current sale and cancelled sales, and compare normalized identifiers case-insensitively. This prevents an invoice from warning about its own newly inserted serials.
- Added an opt-in `requireCustomerDetails` contract for the Generate Invoice flow. The server validates an active customer record with a name and 10–15 digit mobile number; the flag is included in the idempotency request hash. Existing non-invoice API integrations retain their prior compatibility unless they opt into this requirement.
- Added field-level customer/product/quantity/price/discount/serial/IMEI/MRP validation in the invoice editor. Missing serial numbers are a non-blocking review warning; serial, IMEI and barcode remain optional.
- Kept the tag uploader's feedback out of the flex layout so its trigger does not move while extraction is running. Model number is now the deterministic fallback when extraction cannot produce a product name and no catalogue suggestion exists.
- Added a dedicated stacked mobile invoice item-card presentation while preserving the tabular desktop/print layout.
- Added print isolation so only the invoice document is visible in print output; application navigation, floating controls and invoice action buttons are excluded.

## Tests added or updated

- Unit tests for ad-hoc identifier snapshots, missing serial acceptance and duplicate serial rejection across invoice lines.
- PostgreSQL integration tests for ad-hoc serial/barcode persistence, no self-match in previous-sale warnings, repeat-sale warning, duplicate serial rejection and required customer details.
- Updated the existing frontend print test to provide mandatory customer details.

## Validation status

- Code and tests have been committed to the feature branch.
- CI has not yet run on the final branch head. Treat all build/test results as pending until the PR's exact-head workflow completes.
- Android browser print-to-PDF and physical print behaviour still require manual verification on the pilot device.
- No production deployment, database migration, production configuration or customer data change was performed.

## Follow-up

Run frontend, backend unit and PostgreSQL integration CI on the exact PR head. Resolve any failures before merge, then manually verify camera/gallery tag extraction, duplicate serial handling, customer validation, responsive invoice layout and Android invoice-only printing.
