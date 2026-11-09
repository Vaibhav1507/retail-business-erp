-- B4: per-product reorder level for low-stock alerts.
create table inventory.reorder_level (tenant_id uuid not null references core.tenant(id), product_id uuid not null, level numeric(18,3) not null check (level >= 0), updated_at timestamptz not null default now(), primary key (tenant_id, product_id), foreign key (tenant_id, product_id) references catalog.product(tenant_id, id));
alter table inventory.reorder_level enable row level security;
alter table inventory.reorder_level force row level security;
create policy tenant_isolation on inventory.reorder_level for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update on inventory.reorder_level to rbos_app;
