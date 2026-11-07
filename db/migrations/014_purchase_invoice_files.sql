-- Uploaded supplier invoice photos/PDFs. Staged on upload (receipt_id null), attached when the receipt is posted.
create table procurement.invoice_file (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  receipt_id uuid null,
  content_type text not null check (content_type in ('image/jpeg','image/png','image/webp','application/pdf')),
  size_bytes integer not null check (size_bytes between 1 and 8388608),
  content bytea not null,
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, receipt_id) references procurement.purchase_receipt(tenant_id, id)
);
create unique index ux_invoice_file_receipt on procurement.invoice_file (tenant_id, receipt_id) where receipt_id is not null;

alter table procurement.invoice_file enable row level security;
alter table procurement.invoice_file force row level security;
create policy tenant_isolation on procurement.invoice_file for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on procurement.invoice_file to rbos_app;
grant update (receipt_id) on procurement.invoice_file to rbos_app;
