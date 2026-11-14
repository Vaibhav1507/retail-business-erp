-- 044_shared_product_master_and_identity.sql
-- Shared product identities are scoped to a business vertical. Operational product,
-- SKU, stock, barcode, price and transaction records remain tenant-owned.
-- Duplicate legacy product rows are soft-merged: current inventory state is rolled
-- into one canonical row; transaction line history keeps its original product FK.

-- The migrator is intentionally NOBYPASSRLS. It owns these tables, so temporarily
-- disable FORCE (not RLS itself) while it performs the all-tenant one-time backfill.
-- Runtime rbos_app is not the table owner and remains subject to the existing policies.
do $rbos$
declare t text;
begin
  foreach t in array array[
    'core.business_profile','catalog.category','catalog.product',
    'catalog.product_source_reference','catalog.product_barcode','inventory.serial_unit',
    'inventory.stock_balance','inventory.item_cost','inventory.reorder_level','inventory.unit_conversion'
  ] loop
    execute format('alter table %s no force row level security', t);
  end loop;
end $rbos$;


create or replace function catalog.product_identity_key(p_name text, p_brand text, p_model text)
returns text language sql immutable parallel safe
as $$
  select case
    when catalog.norm_text(p_brand) <> ''
      and catalog.norm_text(p_model) <> ''
      and catalog.norm_text(p_model) not in ('na','none','unknown','generic','tbd','notavailable','notapplicable','null')
      then 'model|' || catalog.norm_text(p_brand) || '|' || catalog.norm_text(p_model)
    else 'name|' || catalog.norm_text(p_name) || '|' || catalog.norm_text(p_brand) || '|' || catalog.norm_text(p_model)
  end
$$;

create or replace function catalog.master_product_identity_key(p_name text, p_category text, p_brand text, p_model text)
returns text language sql immutable parallel safe
as $$
  select case
    when catalog.norm_text(p_brand) <> ''
      and catalog.norm_text(p_model) <> ''
      and catalog.norm_text(p_model) not in ('na','none','unknown','generic','tbd','notavailable','notapplicable','null')
      then 'model|' || catalog.norm_text(p_brand) || '|' || catalog.norm_text(p_model)
    else 'name|' || catalog.norm_text(p_category) || '|' || catalog.norm_text(p_name) || '|' || catalog.norm_text(p_brand) || '|' || catalog.norm_text(p_model)
  end
$$;

create table catalog.master_product (
  id               uuid primary key,
  business_category text not null check (business_category in ('electronics_mobile','electrical_hardware','general_retail','garments_apparel','automobile_spares','sports_fitness','pharmacy_medical','other_retail')),
  category_name    text not null check (length(btrim(category_name)) between 1 and 80),
  name             text not null check (length(btrim(name)) between 1 and 160),
  base_unit        text not null check (base_unit in ('pcs','set','pack','m')),
  is_generic       boolean not null,
  brand            text null check (brand is null or length(btrim(brand)) between 1 and 80),
  model            text null check (model is null or length(btrim(model)) between 1 and 80),
  attributes       jsonb not null default '{}'::jsonb check (jsonb_typeof(attributes) = 'object'),
  specs            jsonb not null default '{}'::jsonb check (jsonb_typeof(specs) = 'object'),
  hsn_code         text null check (hsn_code is null or hsn_code ~ '^[0-9]{4}([0-9]{2}([0-9]{2})?)?$'),
  gst_rate         numeric(5,2) null check (gst_rate is null or gst_rate between 0 and 100),
  identity_key     text generated always as (catalog.master_product_identity_key(name, category_name, brand, model)) stored,
  created_at       timestamptz not null default now(),
  updated_at       timestamptz not null default now(),
  check (not is_generic or (brand is null and model is null)),
  unique (business_category, identity_key)
);
create index ix_master_product_lookup on catalog.master_product (business_category, lower(name));
create index ix_master_product_brand_model on catalog.master_product (business_category, catalog.norm_text(brand), catalog.norm_text(model));

alter table catalog.product add column identity_key text generated always as (catalog.product_identity_key(name, brand, model)) stored;
alter table catalog.product add column master_product_id uuid null references catalog.master_product(id);
create index ix_product_identity_key on catalog.product (tenant_id, identity_key);
create index ix_product_master on catalog.product (tenant_id, master_product_id) where master_product_id is not null;

-- Keep a durable mapping for historical transaction lines that still reference
-- a product row which has been retired as a duplicate.
create table catalog.product_alias (
  tenant_id             uuid not null references core.tenant(id),
  alias_product_id      uuid not null,
  canonical_product_id  uuid not null,
  created_at            timestamptz not null default now(),
  primary key (tenant_id, alias_product_id),
  foreign key (tenant_id, alias_product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, canonical_product_id) references catalog.product(tenant_id, id),
  check (alias_product_id <> canonical_product_id)
);

-- Rank duplicates by evidence completeness, then source provenance, then age.
-- Model identity intentionally ignores display-name/category wording. Different
-- model suffixes and finishes therefore remain distinct.
create temporary table _product_duplicate_map on commit drop as
with ranked as (
  select p.tenant_id, p.id,
         first_value(p.id) over (
           partition by p.tenant_id, p.identity_key
           order by
             ((p.source_document_id is not null) or exists (
               select 1 from catalog.product_source_reference sr
               where sr.tenant_id=p.tenant_id and sr.product_id=p.id
             )) desc,
             (jsonb_object_length(p.attributes) + jsonb_object_length(coalesce(p.specs, '{}'::jsonb))) desc,
             p.created_at asc, p.id
         ) as canonical_product_id,
         row_number() over (
           partition by p.tenant_id, p.identity_key
           order by
             ((p.source_document_id is not null) or exists (
               select 1 from catalog.product_source_reference sr
               where sr.tenant_id=p.tenant_id and sr.product_id=p.id
             )) desc,
             (jsonb_object_length(p.attributes) + jsonb_object_length(coalesce(p.specs, '{}'::jsonb))) desc,
             p.created_at asc, p.id
         ) as position
  from catalog.product p
  where p.is_active
)
select tenant_id, id as alias_product_id, canonical_product_id
from ranked
where position > 1;

insert into catalog.product_alias (tenant_id, alias_product_id, canonical_product_id)
select tenant_id, alias_product_id, canonical_product_id
from _product_duplicate_map
on conflict (tenant_id, alias_product_id) do nothing;

-- Preserve non-empty local product metadata on the canonical record.
update catalog.product c
set attributes = a.attributes || c.attributes,
    specs = coalesce(a.specs, '{}'::jsonb) || coalesce(c.specs, '{}'::jsonb),
    hsn_code = coalesce(c.hsn_code, a.hsn_code),
    gst_rate = coalesce(c.gst_rate, a.gst_rate),
    mrp = coalesce(c.mrp, a.mrp),
    source_document_id = coalesce(c.source_document_id, a.source_document_id),
    source_page = coalesce(c.source_page, a.source_page)
from _product_duplicate_map m
join catalog.product a on a.tenant_id=m.tenant_id and a.id=m.alias_product_id
where c.tenant_id=m.tenant_id and c.id=m.canonical_product_id;

-- Carry source references to the canonical product. Source documents remain
-- tenant-private; only the product facts are shared through master_product.
insert into catalog.product_source_reference (tenant_id, id, product_id, source_document_id, source_page)
select distinct m.tenant_id, gen_random_uuid(), m.canonical_product_id, sr.source_document_id, sr.source_page
from catalog.product_source_reference sr
join _product_duplicate_map m on m.tenant_id=sr.tenant_id and m.alias_product_id=sr.product_id
on conflict do nothing;
delete from catalog.product_source_reference sr
using _product_duplicate_map m
where sr.tenant_id=m.tenant_id and sr.product_id=m.alias_product_id;

-- Move identifiers and serials to the canonical product.
update catalog.product_barcode b
set product_id=m.canonical_product_id
from _product_duplicate_map m
where b.tenant_id=m.tenant_id and b.product_id=m.alias_product_id;

update inventory.serial_unit u
set product_id=m.canonical_product_id
from _product_duplicate_map m
where u.tenant_id=m.tenant_id and u.product_id=m.alias_product_id;

-- Consolidate live stock balances without changing historical stock movements.
insert into inventory.stock_balance (tenant_id, product_id, location_id, quantity)
select b.tenant_id, m.canonical_product_id, b.location_id, sum(b.quantity)
from inventory.stock_balance b
join _product_duplicate_map m on m.tenant_id=b.tenant_id and m.alias_product_id=b.product_id
group by b.tenant_id, m.canonical_product_id, b.location_id
on conflict (tenant_id, product_id, location_id)
do update set quantity=inventory.stock_balance.quantity + excluded.quantity;
delete from inventory.stock_balance b
using _product_duplicate_map m
where b.tenant_id=m.tenant_id and b.product_id=m.alias_product_id;

-- Consolidate weighted-average cost by adding inventory quantity and value.
insert into inventory.item_cost (tenant_id, product_id, total_quantity, inventory_value, wac)
select c.tenant_id, m.canonical_product_id, sum(c.total_quantity), sum(c.inventory_value),
       case when sum(c.total_quantity) > 0 then sum(c.inventory_value) / sum(c.total_quantity) else 0 end
from inventory.item_cost c
join _product_duplicate_map m on m.tenant_id=c.tenant_id and m.alias_product_id=c.product_id
group by c.tenant_id, m.canonical_product_id
on conflict (tenant_id, product_id) do update
set total_quantity=inventory.item_cost.total_quantity + excluded.total_quantity,
    inventory_value=inventory.item_cost.inventory_value + excluded.inventory_value,
    wac=case
      when inventory.item_cost.total_quantity + excluded.total_quantity > 0
        then (inventory.item_cost.inventory_value + excluded.inventory_value) /
             (inventory.item_cost.total_quantity + excluded.total_quantity)
      else 0
    end;
delete from inventory.item_cost c
using _product_duplicate_map m
where c.tenant_id=m.tenant_id and c.product_id=m.alias_product_id;

insert into inventory.reorder_level (tenant_id, product_id, level)
select x.tenant_id, m.canonical_product_id, max(x.level)
from inventory.reorder_level x
join _product_duplicate_map m on m.tenant_id=x.tenant_id and m.alias_product_id=x.product_id
group by x.tenant_id, m.canonical_product_id
on conflict (tenant_id, product_id) do update
set level=greatest(inventory.reorder_level.level, excluded.level);
delete from inventory.reorder_level x
using _product_duplicate_map m
where x.tenant_id=m.tenant_id and x.product_id=m.alias_product_id;

insert into inventory.unit_conversion (tenant_id, product_id, unit, factor)
select x.tenant_id, m.canonical_product_id, x.unit, x.factor
from inventory.unit_conversion x
join _product_duplicate_map m on m.tenant_id=x.tenant_id and m.alias_product_id=x.product_id
on conflict (tenant_id, product_id, unit) do nothing;
delete from inventory.unit_conversion x
using _product_duplicate_map m
where x.tenant_id=m.tenant_id and x.product_id=m.alias_product_id;

-- Historical purchase/sale/return/order/transfer line rows keep their original
-- foreign keys for auditability. The alias map allows those references to be
-- resolved to the canonical product. Only the current catalogue row is retired.
update catalog.product p
set is_active=false
from _product_duplicate_map m
where p.tenant_id=m.tenant_id and p.id=m.alias_product_id;

-- Seed the shared vertical catalogue from the canonical, tenant-owned records.
-- Do not copy internal SKU, cost, selling price, MRP or source document metadata.
insert into catalog.master_product
  (id, business_category, category_name, name, base_unit, is_generic, brand, model, attributes, specs, hsn_code, gst_rate)
select distinct on (bp.business_category, catalog.master_product_identity_key(p.name, c.name, p.brand, p.model))
       gen_random_uuid(), bp.business_category, c.name, p.name, p.base_unit, p.is_generic, p.brand, p.model,
       p.attributes, coalesce(p.specs, '{}'::jsonb), p.hsn_code, p.gst_rate
from catalog.product p
join core.business_profile bp on bp.tenant_id=p.tenant_id
join catalog.category c on c.tenant_id=p.tenant_id and c.id=p.category_id
where bp.business_category is not null
order by bp.business_category, catalog.master_product_identity_key(p.name, c.name, p.brand, p.model),
         p.is_active desc, p.created_at asc, p.id
on conflict (business_category, identity_key) do nothing;

update catalog.product p
set master_product_id=mp.id
from catalog.master_product mp
join core.business_profile bp on bp.business_category=mp.business_category
join catalog.category c on c.tenant_id=bp.tenant_id
where p.tenant_id=bp.tenant_id
  and p.category_id=c.id
  and p.master_product_id is null
  and mp.identity_key=catalog.master_product_identity_key(p.name, c.name, p.brand, p.model);

-- From this point onward there is exactly one active tenant product per identity.
drop index if exists catalog.product_active_dup_uq;
create unique index product_active_identity_uq on catalog.product (tenant_id, identity_key) where is_active;
create unique index ux_product_tenant_master_active on catalog.product (tenant_id, master_product_id)
  where is_active and master_product_id is not null;

-- Re-enable FORCE RLS before the migration transaction commits.
do $rbos$
declare t text;
begin
  foreach t in array array[
    'core.business_profile','catalog.category','catalog.product',
    'catalog.product_source_reference','catalog.product_barcode','inventory.serial_unit',
    'inventory.stock_balance','inventory.item_cost','inventory.reorder_level','inventory.unit_conversion'
  ] loop
    execute format('alter table %s force row level security', t);
  end loop;
end $rbos$;



create function catalog.check_product_master_vertical() returns trigger
language plpgsql
as $$
declare tenant_vertical text; master_vertical text;
begin
  if new.master_product_id is null then return new; end if;
  select business_category into tenant_vertical
  from core.business_profile where tenant_id=new.tenant_id;
  select business_category into master_vertical
  from catalog.master_product where id=new.master_product_id;
  if tenant_vertical is null or master_vertical is null or tenant_vertical <> master_vertical then
    raise exception 'Product master belongs to a different business category.';
  end if;
  return new;
end
$$;
create trigger trg_product_master_vertical
before insert or update of master_product_id on catalog.product
for each row execute function catalog.check_product_master_vertical();

alter table catalog.master_product enable row level security;
alter table catalog.master_product force row level security;
create policy master_product_select_same_vertical on catalog.master_product
for select to rbos_app
using (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product.business_category
    and core.is_active_member(bp.tenant_id)
));
create policy master_product_insert_same_vertical on catalog.master_product
for insert to rbos_app
with check (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product.business_category
    and core.is_active_member(bp.tenant_id)
));
create policy master_product_update_same_vertical on catalog.master_product
for update to rbos_app
using (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product.business_category
    and core.is_active_member(bp.tenant_id)
))
with check (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product.business_category
    and core.is_active_member(bp.tenant_id)
));

alter table catalog.product_alias enable row level security;
alter table catalog.product_alias force row level security;
create policy product_alias_tenant_isolation on catalog.product_alias
for all to rbos_app
using (tenant_id=core.current_tenant_id() and core.is_active_member(tenant_id))
with check (tenant_id=core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, update (attributes, specs, hsn_code, gst_rate, updated_at) on catalog.master_product to rbos_app;
grant select, insert on catalog.product_alias to rbos_app;
grant update (master_product_id) on catalog.product to rbos_app;
grant execute on function catalog.product_identity_key(text,text,text),
  catalog.master_product_identity_key(text,text,text,text) to rbos_app;
