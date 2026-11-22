-- 057_sales_source_invoice_reference.sql
-- Keep the supplier/customer source invoice reference distinct from the RBOS legal invoice number.
alter table sales.sale
  add column source_invoice_ref text,
  add column source_invoice_date date,
  add constraint sale_source_invoice_ref_length_ck
    check (source_invoice_ref is null or length(btrim(source_invoice_ref)) between 1 and 80);
