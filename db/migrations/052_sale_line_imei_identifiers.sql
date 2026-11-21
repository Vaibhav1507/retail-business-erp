-- 052_sale_line_imei_identifiers.sql
-- IMEI1/IMEI2 identify a device but are not additional physical units. Keep them
-- separate from sale_line.serials so a dual-SIM phone still counts as one unit.
alter table sales.sale_line
  add column imei1s text[] not null default '{}',
  add column imei2s text[] not null default '{}';

create index ix_sale_line_imei1s on sales.sale_line using gin (imei1s);
create index ix_sale_line_imei2s on sales.sale_line using gin (imei2s);
