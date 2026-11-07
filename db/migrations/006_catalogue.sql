-- 006_catalogue.sql : Slice 2 catalogue depth. Additive only; 001-005 are unchanged.

create function catalog.norm_text(t text) returns text
  language sql immutable parallel safe
  as $$ select regexp_replace(lower(coalesce(t, '')), '[[:space:][:punct:]]+', '', 'g') $$;

alter table catalog.product
  add column dup_key text generated always as
    (catalog.norm_text(name) || '|' || catalog.norm_text(brand) || '|' || catalog.norm_text(model)) stored;
create unique index product_active_dup_uq on catalog.product (tenant_id, dup_key) where is_active;

create unique index category_name_ci_uq on catalog.category (tenant_id, lower(btrim(name)));

create table catalog.sku_counter (
  tenant_id  uuid primary key references core.tenant(id),
  last_value bigint not null check (last_value >= 0)
);

create table catalog.import_batch (
  tenant_id       uuid not null references core.tenant(id),
  id              uuid not null,
  idempotency_key text not null check (length(idempotency_key) between 8 and 100),
  request_hash    text not null,
  created_by      uuid not null,
  total_rows      integer not null check (total_rows >= 0),
  created_count   integer not null check (created_count >= 0),
  skipped_count   integer not null check (skipped_count >= 0),
  created_at      timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key)
);

do $$
declare t text;
begin
  foreach t in array array['catalog.sku_counter','catalog.import_batch'] loop
    execute format('alter table %s enable row level security', t);
    execute format('alter table %s force row level security', t);
    execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

grant select, insert, update on catalog.sku_counter to rbos_app;
grant select, insert on catalog.import_batch to rbos_app;
grant update (category_id, name, is_generic, brand, model, attributes, is_active) on catalog.product to rbos_app;
grant update (name) on catalog.category to rbos_app;