-- 051_shared_master_product_barcodes.sql
-- Shared barcode intelligence is scoped to a retail vertical. A business contributes
-- at most once per code, and contributions happen only in the posted sale transaction.
create table catalog.master_product_barcode (
  business_category text not null check (business_category in (
    'electronics_mobile','electrical_hardware','general_retail','garments_apparel',
    'automobile_spares','sports_fitness','pharmacy_medical','other_retail'
  )),
  code text not null check (length(code) between 4 and 64 and code !~ '[[:space:]]'),
  master_product_id uuid not null references catalog.master_product(id),
  symbology varchar(20) null,
  confirmed_business_count integer not null default 0 check (confirmed_business_count >= 0),
  created_at timestamptz not null default now(),
  last_verified_at timestamptz not null default now(),
  primary key (business_category,code)
);
create index ix_master_product_barcode_product
  on catalog.master_product_barcode (business_category,master_product_id);

create table catalog.master_product_barcode_confirmation (
  business_category text not null,
  code text not null,
  tenant_id uuid not null references core.tenant(id),
  confirmed_at timestamptz not null default now(),
  primary key (business_category,code,tenant_id),
  foreign key (business_category,code)
    references catalog.master_product_barcode(business_category,code) on delete cascade
);

create function catalog.check_master_product_barcode_vertical() returns trigger
language plpgsql
as $$
declare master_vertical text;
begin
  select business_category into master_vertical
  from catalog.master_product where id=new.master_product_id;
  if master_vertical is null or master_vertical <> new.business_category then
    raise exception 'Shared barcode and product must belong to the same business vertical.';
  end if;
  return new;
end
$$;
create trigger trg_master_product_barcode_vertical
before insert or update of business_category,master_product_id
on catalog.master_product_barcode
for each row execute function catalog.check_master_product_barcode_vertical();

alter table catalog.master_product_barcode enable row level security;
alter table catalog.master_product_barcode force row level security;
create policy master_product_barcode_select_same_vertical
on catalog.master_product_barcode for select to rbos_app
using (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product_barcode.business_category
    and core.is_active_member(bp.tenant_id)
));
create policy master_product_barcode_insert_same_vertical
on catalog.master_product_barcode for insert to rbos_app
with check (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product_barcode.business_category
    and core.is_active_member(bp.tenant_id)
));
create policy master_product_barcode_update_same_vertical
on catalog.master_product_barcode for update to rbos_app
using (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product_barcode.business_category
    and core.is_active_member(bp.tenant_id)
))
with check (exists (
  select 1 from core.business_profile bp
  where bp.tenant_id=core.current_tenant_id()
    and bp.business_category=catalog.master_product_barcode.business_category
    and core.is_active_member(bp.tenant_id)
));
grant select,insert on catalog.master_product_barcode to rbos_app;
grant update (confirmed_business_count,last_verified_at) on catalog.master_product_barcode to rbos_app;

alter table catalog.master_product_barcode_confirmation enable row level security;
alter table catalog.master_product_barcode_confirmation force row level security;
create policy master_product_barcode_confirmation_insert_own_business
on catalog.master_product_barcode_confirmation for insert to rbos_app
with check (
  tenant_id=core.current_tenant_id()
  and exists (
    select 1 from core.business_profile bp
    where bp.tenant_id=core.current_tenant_id()
      and bp.business_category=catalog.master_product_barcode_confirmation.business_category
      and core.is_active_member(bp.tenant_id)
  )
);
grant insert on catalog.master_product_barcode_confirmation to rbos_app;
