-- 055_flexible_sale_lines.sql
-- Sales are immutable line snapshots. A line no longer depends on catalogue identity
-- for invoice history; catalogue linkage is optional and line identity is stable.
alter table sales.sale_line
  add column line_id uuid not null default gen_random_uuid(),
  add column line_number bigint generated always as identity,
  add column description text,
  add column brand text,
  add column model text,
  add column hsn_code text,
  add column is_stock_tracked boolean not null default true;

update sales.sale_line sl
set description = p.name,
    brand = p.brand,
    model = p.model,
    hsn_code = p.hsn_code
from catalog.product p
where p.tenant_id = sl.tenant_id and p.id = sl.product_id;

alter table sales.sale_line
  alter column description set not null,
  alter column product_id drop not null,
  drop constraint if exists sale_line_pkey,
  drop constraint if exists sale_line_tenant_id_product_id_fkey,
  add constraint sale_line_pkey primary key (tenant_id, sale_id, line_id),
  add constraint sale_line_product_fk foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  add constraint sale_line_description_ck check (length(btrim(description)) between 1 and 240),
  add constraint sale_line_stock_tracking_ck check (product_id is not null or not is_stock_tracked);

create unique index sale_line_number_uq on sales.sale_line (tenant_id, sale_id, line_number);
create index sale_line_product_ix on sales.sale_line (tenant_id, product_id, sale_id);

-- Return rows point to the exact sold line, allowing duplicate catalogue products
-- and ad-hoc lines to be returned independently.
alter table sales.sale_return_line
  add column sale_line_id uuid;

update sales.sale_return_line rl
set sale_line_id = sl.line_id
from sales.sale_line sl
where sl.tenant_id = rl.tenant_id
  and sl.sale_id = rl.sale_id
  and sl.product_id = rl.product_id;

alter table sales.sale_return_line
  alter column sale_line_id set not null,
  alter column product_id drop not null,
  drop constraint if exists sale_return_line_pkey,
  drop constraint if exists sale_return_line_tenant_id_product_id_fkey,
  add constraint sale_return_line_pkey primary key (tenant_id, return_id, sale_line_id),
  add constraint sale_return_line_sale_line_fk foreign key (tenant_id, sale_id, sale_line_id)
    references sales.sale_line(tenant_id, sale_id, line_id),
  add constraint sale_return_line_product_fk foreign key (tenant_id, product_id)
    references catalog.product(tenant_id, id);

create index sale_return_line_sale_line_ix on sales.sale_return_line (tenant_id, sale_id, sale_line_id);
