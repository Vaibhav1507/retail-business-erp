-- B1: alternative units per product as a factor to the base unit.
create table inventory.unit_conversion (tenant_id uuid not null references core.tenant(id), product_id uuid not null, unit text not null check (length(btrim(unit)) between 1 and 20), factor numeric(18,4) not null check (factor > 0), primary key (tenant_id, product_id, unit), foreign key (tenant_id, product_id) references catalog.product(tenant_id, id));
alter table inventory.unit_conversion enable row level security;
alter table inventory.unit_conversion force row level security;
create policy tenant_isolation on inventory.unit_conversion for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update on inventory.unit_conversion to rbos_app;
