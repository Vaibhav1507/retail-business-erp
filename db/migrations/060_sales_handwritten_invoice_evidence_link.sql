-- 060_sales_handwritten_invoice_evidence_link.sql
-- Atomically associate a retained sales-side source bill with one posted sale.
-- This is intentionally separate from the generic document processing queue.

create table documents.sales_invoice_sale_link (
  tenant_id uuid not null references core.tenant(id),
  document_id uuid not null,
  sale_id uuid not null,
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, document_id),
  unique (tenant_id, sale_id),
  foreign key (tenant_id, document_id)
    references documents.business_document(tenant_id, id),
  foreign key (tenant_id, sale_id)
    references sales.sale(tenant_id, id)
);

create index ix_sales_invoice_sale_link_sale
  on documents.sales_invoice_sale_link (tenant_id, sale_id);

alter table documents.sales_invoice_sale_link enable row level security;
alter table documents.sales_invoice_sale_link force row level security;
create policy tenant_isolation on documents.sales_invoice_sale_link for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert on documents.sales_invoice_sale_link to rbos_app;
