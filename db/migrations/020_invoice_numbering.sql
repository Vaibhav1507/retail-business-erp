-- A3: gapless invoice numbers per tenant and financial year. The counter row is locked inside the sale transaction, so a rolled-back sale releases its number.
create table sales.invoice_counter (tenant_id uuid not null references core.tenant(id), fy_start_year int not null check (fy_start_year between 2000 and 2200), last_number int not null check (last_number >= 0), primary key (tenant_id, fy_start_year));
alter table sales.invoice_counter enable row level security;
alter table sales.invoice_counter force row level security;
create policy tenant_isolation on sales.invoice_counter for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update on sales.invoice_counter to rbos_app;
alter table sales.sale add column invoice_no text null check (invoice_no is null or length(invoice_no) between 1 and 40);
create unique index ux_sale_invoice_no on sales.sale (tenant_id, invoice_no) where invoice_no is not null;
