-- 041_product_source_references.sql : a product may keep many (source document, PDF page) references.
-- Additive only. Reverse with db/rollback/041_product_source_references.down.sql (never applied by the migrator).

create table catalog.product_source_reference (
  tenant_id          uuid not null references core.tenant(id),
  id                 uuid not null,
  product_id         uuid not null,
  source_document_id uuid not null,
  source_page        integer null check (source_page is null or source_page > 0),
  created_at         timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product (tenant_id, id),
  foreign key (tenant_id, source_document_id) references catalog.source_document (tenant_id, id)
);
create unique index ux_product_source_reference on catalog.product_source_reference (tenant_id, product_id, source_document_id, coalesce(source_page, 0));
create index ix_product_source_reference_doc on catalog.product_source_reference (tenant_id, source_document_id);

alter table catalog.product_source_reference enable row level security;
alter table catalog.product_source_reference force row level security;
create policy tenant_isolation on catalog.product_source_reference for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on catalog.product_source_reference to rbos_app;

-- carry over references already recorded on the product row (migration 040)
insert into catalog.product_source_reference (tenant_id, id, product_id, source_document_id, source_page)
select tenant_id, gen_random_uuid(), id, source_document_id, source_page
from catalog.product where source_document_id is not null
on conflict do nothing;
