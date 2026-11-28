-- 062_verbatim_sale_line_description_length.sql
-- Handwritten sales-bill extraction must preserve complete wrapped product descriptions.
-- The column is TEXT already; extend only the validation bound, retaining a bounded payload.
alter table sales.sale_line
  drop constraint if exists sale_line_description_ck;

alter table sales.sale_line
  add constraint sale_line_description_ck
  check (length(btrim(description)) between 1 and 10000);
