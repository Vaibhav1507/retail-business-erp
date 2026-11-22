-- 056_negative_stock_commitments.sql
-- Physical stock may be negative, but valued inventory remains non-negative.
alter table inventory.stock_balance
  drop constraint if exists stock_balance_quantity_check;

alter table inventory.stock_movement
  drop constraint if exists stock_movement_value_ck;
alter table inventory.stock_movement
  add constraint stock_movement_value_ck
  check ((quantity_delta > 0 and value_delta >= 0) or (quantity_delta < 0 and value_delta <= 0));

alter table sales.sale
  add column inventory_cost_total numeric(18,2) not null default 0 check (inventory_cost_total >= 0),
  add column provisional_cost_total numeric(18,2) not null default 0 check (provisional_cost_total >= 0);

update sales.sale s
set inventory_cost_total = coalesce((
      select sum(l.inventory_cost_amount) from sales.sale_line l
      where l.tenant_id=s.tenant_id and l.sale_id=s.id
    ), s.cost_total),
    provisional_cost_total = coalesce((
      select sum(l.provisional_cost_amount) from sales.sale_line l
      where l.tenant_id=s.tenant_id and l.sale_id=s.id
    ), 0);

alter table sales.sale_return_line
  add column inventory_quantity numeric(18,3) not null default 0 check (inventory_quantity >= 0),
  add column provisional_quantity numeric(18,3) not null default 0 check (provisional_quantity >= 0),
  add column inventory_cost_amount numeric(18,2) not null default 0 check (inventory_cost_amount >= 0),
  add column provisional_cost_amount numeric(18,2) not null default 0 check (provisional_cost_amount >= 0);

update sales.sale_return_line
set inventory_quantity = quantity,
    inventory_cost_amount = cost_amount;

create table inventory.negative_stock_commitment (
  tenant_id uuid not null,
  id uuid not null,
  sale_id uuid not null,
  sale_line_id uuid not null,
  product_id uuid not null,
  location_id uuid not null,
  original_quantity numeric(18,3) not null check (original_quantity > 0),
  remaining_quantity numeric(18,3) not null check (remaining_quantity >= 0),
  provisional_unit_cost numeric(18,4) not null check (provisional_unit_cost > 0),
  remaining_provisional_cost_amount numeric(18,2) not null check (remaining_provisional_cost_amount >= 0),
  settled_quantity numeric(18,3) not null default 0 check (settled_quantity >= 0),
  settled_cost_amount numeric(18,2) not null default 0 check (settled_cost_amount >= 0),
  status text not null check (status in ('Open','Settled','Cancelled')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, sale_line_id),
  foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id),
  foreign key (tenant_id, sale_id, sale_line_id) references sales.sale_line(tenant_id, sale_id, line_id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, location_id) references core.stock_location(tenant_id, id),
  check (remaining_quantity <= original_quantity),
  check (settled_quantity <= original_quantity)
);

create index negative_stock_open_allocation_ix
  on inventory.negative_stock_commitment(tenant_id, product_id, location_id, created_at, id)
  where status='Open' and remaining_quantity > 0;

alter table inventory.negative_stock_commitment enable row level security;
alter table inventory.negative_stock_commitment force row level security;
create policy tenant_isolation on inventory.negative_stock_commitment
  for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, update on inventory.negative_stock_commitment to rbos_app;
