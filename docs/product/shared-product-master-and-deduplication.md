# Shared product master and duplicate prevention

## Scope

The product catalogue has two layers:

- `catalog.master_product` is application-owned reference data, shared only among businesses with the same `core.business_profile.business_category`.
- `catalog.product` remains tenant-owned. It is the business's operational item and retains its own internal SKU, local category, MRP, barcode links, stock, weighted-average cost, reorder settings, sales/purchase history and serial tracking.

The shared master contains the product identity and reusable facts: name, category label, base unit, brand, manufacturer model, attributes/specifications, HSN and GST reference values. It deliberately does not contain a tenant's SKU, MRP, buying/selling prices, stock, transaction history, source PDF, or supplier data. Source documents and page references remain tenant-private.

A product added manually or imported while a vertical has been selected creates or enriches a shared master item and links the business's local product row to it. Products entered during first-run setup, before a business category exists, remain tenant-local and are published/linked when the owner completes the business profile.

A second business in the same vertical can search **Shared catalogue** and add a master item to its own product list. This creates a separate tenant product row and local SKU; it does not share stock or commercial records. Other verticals cannot read that master item.

## Product identity and duplicate policy

1. When brand and a usable model number are present, identity is `model|normalized-brand|normalized-model`. Display name and tenant category wording do not participate. Case, spaces and punctuation are normalized.
2. Model placeholders such as `N/A`, `unknown`, `generic` and `TBD` are not trusted as identifiers.
3. Without a usable brand/model pair, the tenant product fallback uses normalized product name plus brand/model. The shared-master fallback additionally includes category, so unbranded goods from different categories do not become one shared master item.
4. A barcode is an additional unique identifier in a tenant. Import preview checks existing barcodes, and scanned-product creation links the barcode in the same transaction as the product. A barcode conflict rolls back product creation.
5. A repeated product identity in an import file is classified as a duplicate before commit. The database has unique indexes as the final concurrency-safe guard for active tenant products.

This policy distinguishes a true variant by the actual manufacturer model code. For example, LG French-door model codes `GV-B25FFGMB` and `GV-B25FFLMB` remain distinct even when the catalogue uses the same family description.

## Existing-data reconciliation

Migration `044_shared_product_master_and_identity.sql`:

- Selects a canonical active row using source-document provenance first, then richer specifications/attributes, then creation time.
- Writes a durable `catalog.product_alias` mapping for retired duplicates.
- Merges barcode/serial links, current stock balances, weighted-average inventory quantity/value, reorder levels and unit conversions onto the canonical row.
- Preserves historical purchase/sale/return/order/transfer line foreign keys by retaining the retired product row as inactive. Append-only stock movement history is not rewritten.
- Moves source-reference records to the canonical row. Source documents remain tenant-private.
- Builds one shared master product per vertical/identity and links each tenant's local product row.
- Hides retired aliases from normal product catalogue results, including the inactive-products view.

The migration temporarily removes FORCE RLS only from the relevant tables while the table-owning migrator performs the all-tenant backfill; RLS stays enabled, the runtime role remains subject to its policies, and FORCE RLS is restored before the migration transaction commits.

## Invoice and import flow

The invoice-extraction service uses existing tenant products to map extracted invoice lines; it does not silently create product rows from OCR output. Product creation occurs through the product catalogue/import workflow. Once a business has selected its vertical, those products are linked to the shared master catalogue and become searchable to peer businesses in that vertical.

## Deployment and verification

- Apply through the normal ordered migration runner; do not manually rerun the migration after it has been recorded.
- Run backend build, PostgreSQL migration, integration tests and frontend typecheck/build from CI before merging.
- Take a database backup before production migration. The migration reconciles existing inventory/product references transactionally.
- The uploaded product export used for the investigation contained 671 tenant product rows. It showed four repeated LG brand/model identities under different display names/categories: `T80KMMB3Z`, `FHP1410Z3M`, `FHP1209Z5M`, and `GV-B25FFGMB`. The French-door catalogue's seven distinct model codes remain separate products.

## Known limits

No identity algorithm can safely merge two unbranded products with different names when neither has a model number nor a barcode. Such rows use the conservative name fallback and may require a human review. Brand aliases entered under different text forms may also need future canonical-brand matching; this migration normalizes casing/punctuation but does not assume brand aliases are equivalent.
