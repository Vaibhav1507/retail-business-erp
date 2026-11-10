-- 040_catalogue_provenance.sql : source document + version tracking for imported catalogues and products.
-- Additive only. Reverse with db/rollback/040_catalogue_provenance.down.sql (never applied by the migrator).

create table catalog.source_document (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  brand            text null check (brand is null or length(btrim(brand)) between 1 and 80),
  document_name    text not null check (length(btrim(document_name)) between 1 and 260),
  source_url       text null check (source_url is null or (length(source_url) <= 500 and source_url ~ '^https?://')),
  published_on     date null,
  version_label    text null check (version_label is null or length(version_label) <= 80),
  content_hash     text null check (content_hash is null or length(content_hash) <= 128),
  document_kind    text not null default 'catalogue' check (document_kind in ('catalogue','price_list','dealer_scheme','other')),
  imported_at      timestamptz not null default now(),
  imported_by      uuid null,
  primary key (tenant_id, id)
);
create index ix_source_document_brand on catalog.source_document (tenant_id, brand);

alter table catalog.source_document enable row level security;
alter table catalog.source_document force row level security;
create policy tenant_isolation on catalog.source_document for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on catalog.source_document to rbos_app;

alter table catalog.product
  add column source_document_id uuid null,
  add column source_page integer null check (source_page is null or source_page > 0);
alter table catalog.product
  add constraint product_source_document_fk foreign key (tenant_id, source_document_id) references catalog.source_document (tenant_id, id);
create index ix_product_source_document on catalog.product (tenant_id, source_document_id) where source_document_id is not null;
grant update (source_document_id, source_page) on catalog.product to rbos_app;

alter table catalog.import_batch add column source_document_id uuid null;
