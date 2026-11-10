-- 039_brand_master.sql : tenant-specific brand master, model-number identification and catalogue reference data.
-- Additive only. catalog.product.brand (free text) is kept untouched; brand_id is an optional link to the master.
-- Brand rows are seeded and backfilled per tenant by the application inside a tenant session (idempotent, ON CONFLICT DO NOTHING).
-- Reverse with db/rollback/039_brand_master.down.sql (never applied by the migrator).

create table catalog.brand (
  tenant_id  uuid not null references core.tenant(id),
  id         uuid not null,
  name       text not null check (length(btrim(name)) between 1 and 80),
  norm_name  text generated always as (catalog.norm_text(name)) stored,
  website    text null check (website is null or (length(website) <= 200 and website ~ '^https?://')),
  aliases    text[] not null default '{}',
  is_active  boolean not null default true,
  is_seeded  boolean not null default false,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  check (norm_name <> ''),
  unique (tenant_id, norm_name)
);

alter table catalog.brand enable row level security;
alter table catalog.brand force row level security;
create policy tenant_isolation on catalog.brand for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on catalog.brand to rbos_app;
grant update (name, website, aliases, is_active) on catalog.brand to rbos_app;

-- Reference data only. Business-specific price, GST, HSN and stock live elsewhere and are never written from catalogue data.
alter table catalog.product
  add column brand_id uuid null,
  add column model_norm text generated always as (catalog.norm_text(model)) stored,
  add column mrp numeric(12,2) null check (mrp is null or mrp >= 0),
  add column specs jsonb not null default '{}'::jsonb check (jsonb_typeof(specs) = 'object');

alter table catalog.product
  add constraint product_brand_fk foreign key (tenant_id, brand_id) references catalog.brand (tenant_id, id);

create index ix_product_model_norm on catalog.product (tenant_id, model_norm) where model_norm <> '';
create index ix_product_brand_id on catalog.product (tenant_id, brand_id) where brand_id is not null;

grant update (brand_id, mrp, specs) on catalog.product to rbos_app;
