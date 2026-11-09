-- 032_purchase_orders.sql : B8 - purchase orders (non-posting; stock/ledger change only when received via a purchase receipt).
create table procurement.purchase_order (
  tenant_id   uuid not null references core.tenant(id),
  id          uuid not null,
  number      bigint generated always as identity,
  supplier_id uuid not null,
  order_date  date not null,
  expected_date date null,
  status      text not null default 'Ordered' check (status in ('Ordered','Received','Cancelled')),
  note        text null check (note is null or length(note) <= 300),
  receipt_id  uuid null,
  created_by  uuid not null,
  created_at  timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, supplier_id) references procurement.supplier(tenant_id, id),
  foreign key (tenant_id, receipt_id) references procurement.purchase_receipt(tenant_id, id)
);

create table procurement.purchase_order_line (
  tenant_id  uuid not null,
  order_id   uuid not null,
  product_id uuid not null,
  quantity   numeric(18,3) not null check (quantity > 0),
  unit_cost  numeric(18,4) not null check (unit_cost >= 0),
  primary key (tenant_id, order_id, product_id),
  foreign key (tenant_id, order_id) references procurement.purchase_order(tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id)
);

do $$ declare t text; begin
  foreach t in array array['procurement.purchase_order','procurement.purchase_order_line'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

grant select, insert on procurement.purchase_order, procurement.purchase_order_line to rbos_app;
grant update (status, receipt_id) on procurement.purchase_order to rbos_app;
