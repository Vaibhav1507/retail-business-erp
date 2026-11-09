-- A5: allocate payments to specific bills. Allocation is optional; unallocated remainder stays on account.
create table procurement.supplier_payment_allocation (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  payment_id uuid not null,
  receipt_id uuid not null,
  amount numeric(18,2) not null check (amount > 0),
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, payment_id, receipt_id),
  foreign key (tenant_id, payment_id) references procurement.supplier_payment(tenant_id, id),
  foreign key (tenant_id, receipt_id) references procurement.purchase_receipt(tenant_id, id));
alter table procurement.supplier_payment_allocation enable row level security;
alter table procurement.supplier_payment_allocation force row level security;
create policy tenant_isolation on procurement.supplier_payment_allocation for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on procurement.supplier_payment_allocation to rbos_app;

create table sales.customer_payment_allocation (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  payment_id uuid not null,
  sale_id uuid not null,
  amount numeric(18,2) not null check (amount > 0),
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, payment_id, sale_id),
  foreign key (tenant_id, payment_id) references sales.customer_payment(tenant_id, id),
  foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id));
alter table sales.customer_payment_allocation enable row level security;
alter table sales.customer_payment_allocation force row level security;
create policy tenant_isolation on sales.customer_payment_allocation for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on sales.customer_payment_allocation to rbos_app;
