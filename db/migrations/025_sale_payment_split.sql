-- C1: split payments on a sale (for example cash + UPI).
create table sales.sale_payment (tenant_id uuid not null references core.tenant(id), sale_id uuid not null, method text not null check (method in ('Cash','Bank','UPI')), amount numeric(18,2) not null check (amount > 0), primary key (tenant_id, sale_id, method), foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id));
alter table sales.sale_payment enable row level security;
alter table sales.sale_payment force row level security;
create policy tenant_isolation on sales.sale_payment for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on sales.sale_payment to rbos_app;