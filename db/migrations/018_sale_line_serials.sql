alter table sales.sale_line add column if not exists serials text[];
create index if not exists ix_sale_line_serials on sales.sale_line using gin (serials);
