-- 053_sale_line_scanned_barcode.sql
-- Preserve the exact machine-readable value attached to each posted invoice line.
-- This is separate from the catalogue barcode mapping, which is learned transactionally.
alter table sales.sale_line
  add column barcode text null
    check (barcode is null or (length(barcode) between 4 and 64 and barcode !~ '[[:space:]]')),
  add column barcode_symbology varchar(20) null;
