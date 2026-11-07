-- Barcodes (GTIN / pack codes) and optional serial-number tracking.
-- A barcode identifies a product or pack, never an individual physical unit. Serials identify units.
alter table catalog.product add column track_serials boolean not null default false;
grant update (track_serials) on catalog.product to rbos_app;

create table catalog.product_barcode (
  tenant_id     uuid not null references core.tenant(id),
  id            uuid not null,
  product_id    uuid not null,
  code          text not null check (length(code) between 4 and 64 and code !~ '\s'),
  symbology     text null check (symbology is null or length(symbology) <= 20),
  pack_quantity integer not null default 1 check (pack_quantity between 1 and 100000),
  created_at    timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  unique (tenant_id, code)
);
create index ix_product_barcode_product on catalog.product_barcode (tenant_id, product_id);

create table inventory.serial_unit (
  tenant_id   uuid not null references core.tenant(id),
  id          uuid not null,
  product_id  uuid not null,
  serial      text not null check (length(btrim(serial)) between 4 and 64),
  status      text not null default 'InStock' check (status in ('InStock','Sold','ReturnedToSupplier')),
  receipt_id  uuid null,
  sale_id     uuid null,
  location_id uuid not null,
  created_at  timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, receipt_id) references procurement.purchase_receipt(tenant_id, id),
  foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id)
);
create unique index ux_serial_unit_serial on inventory.serial_unit (tenant_id, upper(btrim(serial)));
create index ix_serial_unit_product on inventory.serial_unit (tenant_id, product_id, status);

alter table catalog.product_barcode enable row level security;
alter table catalog.product_barcode force row level security;
create policy tenant_isolation on catalog.product_barcode for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, delete on catalog.product_barcode to rbos_app;
grant update (pack_quantity, symbology) on catalog.product_barcode to rbos_app;

alter table inventory.serial_unit enable row level security;
alter table inventory.serial_unit force row level security;
create policy tenant_isolation on inventory.serial_unit for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on inventory.serial_unit to rbos_app;
grant update (status, sale_id, location_id) on inventory.serial_unit to rbos_app;
