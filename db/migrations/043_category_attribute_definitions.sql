-- 043_category_attribute_definitions.sql
-- Tenant-owned, category-specific product attribute schemas. Product values remain in catalog.product.attributes JSONB.

create table catalog.category_attribute_definition (
  tenant_id       uuid not null,
  category_id     uuid not null,
  attribute_key   text not null check (attribute_key ~ '^[a-z][a-z0-9_]{0,59}$'),
  label           text not null check (length(btrim(label)) between 1 and 80),
  data_type       text not null default 'text' check (data_type in ('text','number','decimal','boolean','date','select')),
  unit            text null check (unit is null or length(unit) <= 20),
  is_required     boolean not null default false,
  is_searchable   boolean not null default false,
  is_filterable   boolean not null default false,
  display_order   integer not null default 0 check (display_order between 0 and 1000),
  options         jsonb not null default '[]'::jsonb check (jsonb_typeof(options) = 'array'),
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now(),
  primary key (tenant_id, category_id, attribute_key),
  foreign key (tenant_id, category_id) references catalog.category(tenant_id, id) on delete cascade
);

alter table catalog.category_attribute_definition enable row level security;
alter table catalog.category_attribute_definition force row level security;
create policy tenant_isolation on catalog.category_attribute_definition for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, update, delete on catalog.category_attribute_definition to rbos_app;
