-- 031_location_moves.sql : B7 - move stock between locations. Total quantity/WAC/ledger are unchanged; only balances and movements change.
create table inventory.location_move (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  move_date        date not null,
  from_location_id uuid not null,
  to_location_id   uuid not null,
  note             text null check (note is null or length(note) <= 300),
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  primary key (tenant_id, id),
  check (from_location_id <> to_location_id),
  foreign key (tenant_id, from_location_id) references core.stock_location(tenant_id, id),
  foreign key (tenant_id, to_location_id) references core.stock_location(tenant_id, id)
);

create table inventory.location_move_line (
  tenant_id  uuid not null,
  move_id    uuid not null,
  product_id uuid not null,
  quantity   numeric(18,3) not null check (quantity > 0),
  primary key (tenant_id, move_id, product_id),
  foreign key (tenant_id, move_id) references inventory.location_move(tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id)
);

do $$ declare t text; begin
  foreach t in array array['inventory.location_move','inventory.location_move_line'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

grant select, insert on inventory.location_move, inventory.location_move_line to rbos_app;
