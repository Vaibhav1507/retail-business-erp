-- 003_business.sql : catalog, accounting, inventory, audit, idempotency. Tenant RLS + composite tenant FKs.

create table catalog.category (
  tenant_id uuid not null references core.tenant(id),
  id        uuid not null,
  name      text not null check (length(btrim(name)) between 1 and 80),
  primary key (tenant_id, id),
  unique (tenant_id, name)
);

create table catalog.product (
  tenant_id    uuid not null references core.tenant(id),
  id           uuid not null,
  category_id  uuid not null,
  name         text not null check (length(btrim(name)) between 1 and 160),
  is_generic   boolean not null,
  internal_sku text not null check (internal_sku like 'INT-%'),
  base_unit    text not null check (base_unit in ('pcs','set','pack','m')),
  brand        text null check (brand is null or length(btrim(brand)) between 1 and 80),
  model        text null check (model is null or length(btrim(model)) between 1 and 80),
  attributes   jsonb not null default '{}'::jsonb
			   check (jsonb_typeof(attributes) = 'object'
					  and (attributes - array['colour','capacity','connector_type','compatible_with']) = '{}'::jsonb),
  is_active    boolean not null default true,
  created_at   timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, internal_sku),
  foreign key (tenant_id, category_id) references catalog.category(tenant_id, id),
  check (not is_generic or (brand is null and model is null))
);

create table accounting.account (
  tenant_id uuid not null references core.tenant(id),
  id        uuid not null,
  code      text not null,
  name      text not null,
  type      text not null check (type in ('Asset','Equity')),
  primary key (tenant_id, id),
  unique (tenant_id, code)
);

create table accounting.journal_entry (
  tenant_id      uuid not null references core.tenant(id),
  id             uuid not null,
  effective_date date not null,
  posted_at      timestamptz not null default now(),
  source_type    text not null,
  source_id      uuid not null,
  primary key (tenant_id, id),
  unique (tenant_id, source_type, source_id)
);

create table accounting.journal_line (
  tenant_id  uuid not null,
  id         uuid not null,
  entry_id   uuid not null,
  account_id uuid not null,
  debit      numeric(18,2) not null default 0 check (debit >= 0),
  credit     numeric(18,2) not null default 0 check (credit >= 0),
  primary key (tenant_id, id),
  foreign key (tenant_id, entry_id) references accounting.journal_entry(tenant_id, id),
  foreign key (tenant_id, account_id) references accounting.account(tenant_id, id),
  check ((debit > 0 and credit = 0) or (credit > 0 and debit = 0))
);

create table audit.audit_event (
  tenant_id      uuid not null references core.tenant(id),
  id             uuid not null,
  actor_user_id  uuid not null,
  action         text not null,
  entity_type    text not null,
  entity_id      uuid not null,
  summary        text not null check (length(summary) <= 300),
  correlation_id text null,
  occurred_at    timestamptz not null default now(),
  primary key (tenant_id, id)
);

create table inventory.opening_stock_document (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  location_id      uuid not null,
  effective_date   date not null,
  status           text not null check (status in ('Draft','ReadyToPost','Posted')),
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  posted_at        timestamptz null,
  journal_entry_id uuid null,
  audit_event_id   uuid null,
  primary key (tenant_id, id),
  foreign key (tenant_id, location_id) references core.stock_location(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id),
  foreign key (tenant_id, audit_event_id) references audit.audit_event(tenant_id, id),
  check ((status = 'Posted') = (posted_at is not null and journal_entry_id is not null and audit_event_id is not null))
);

create table inventory.opening_stock_line (
  tenant_id   uuid not null,
  document_id uuid not null,
  product_id  uuid not null,
  quantity    numeric(18,3) not null check (quantity > 0),
  unit_cost   numeric(18,4) not null check (unit_cost > 0),
  primary key (tenant_id, document_id, product_id),
  foreign key (tenant_id, document_id) references inventory.opening_stock_document(tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id)
);

create table platform.idempotency_record (
  tenant_id          uuid not null references core.tenant(id),
  operation          text not null,
  key                text not null check (length(key) between 8 and 100),
  request_hash       text not null,
  result_document_id uuid not null,
  created_at         timestamptz not null default now(),
  primary key (tenant_id, operation, key),
  foreign key (tenant_id, result_document_id) references inventory.opening_stock_document(tenant_id, id)
);

create table inventory.stock_movement (
  tenant_id      uuid not null,
  id             uuid not null,
  product_id     uuid not null,
  location_id    uuid not null,
  quantity_delta numeric(18,3) not null check (quantity_delta > 0),
  unit_cost      numeric(18,4) not null check (unit_cost > 0),
  value_delta    numeric(18,2) not null check (value_delta > 0),
  effective_date date not null,
  source_type    text not null,
  source_id      uuid not null,
  occurred_at    timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, location_id) references core.stock_location(tenant_id, id)
);

create table inventory.stock_balance (
  tenant_id   uuid not null,
  product_id  uuid not null,
  location_id uuid not null,
  quantity    numeric(18,3) not null check (quantity >= 0),
  primary key (tenant_id, product_id, location_id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, location_id) references core.stock_location(tenant_id, id)
);

create table inventory.item_cost (
  tenant_id       uuid not null,
  product_id      uuid not null,
  total_quantity  numeric(18,3) not null check (total_quantity >= 0),
  inventory_value numeric(18,2) not null check (inventory_value >= 0),
  wac             numeric(18,4) not null check (wac >= 0),
  primary key (tenant_id, product_id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id)
);

-- Append-only protection (defense in depth beyond grants).
create function core.reject_mutation() returns trigger language plpgsql as $$
begin raise exception 'append-only table: % not permitted on %', tg_op, tg_table_name; end $$;
create trigger no_update_movement before update or delete on inventory.stock_movement for each row execute function core.reject_mutation();
create trigger no_update_journal_entry before update or delete on accounting.journal_entry for each row execute function core.reject_mutation();
create trigger no_update_journal_line before update or delete on accounting.journal_line for each row execute function core.reject_mutation();
create trigger no_update_audit before update or delete on audit.audit_event for each row execute function core.reject_mutation();

-- Deferred balance check: every journal entry must balance at commit.
create function accounting.assert_entry_balanced() returns trigger language plpgsql as $$
declare d numeric; c numeric;
begin
  select coalesce(sum(debit),0), coalesce(sum(credit),0) into d, c
  from accounting.journal_line where tenant_id = new.tenant_id and entry_id = new.entry_id;
  if d <> c then raise exception 'journal entry % is unbalanced (debit %, credit %)', new.entry_id, d, c; end if;
  return null;
end $$;
create constraint trigger journal_balanced after insert on accounting.journal_line
  deferrable initially deferred for each row execute function accounting.assert_entry_balanced();

-- RLS on every tenant-owned business table.
do $$
declare t text;
begin
  foreach t in array array['catalog.category','catalog.product','accounting.account','accounting.journal_entry','accounting.journal_line',
	  'audit.audit_event','inventory.opening_stock_document','inventory.opening_stock_line','platform.idempotency_record',
	  'inventory.stock_movement','inventory.stock_balance','inventory.item_cost'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

-- Grants: least privilege. No DELETE. Append-only tables get no UPDATE.
grant select, insert on catalog.category, catalog.product, accounting.account, accounting.journal_entry, accounting.journal_line,
  audit.audit_event, inventory.stock_movement, inventory.opening_stock_line, platform.idempotency_record,
  inventory.opening_stock_document to rbos_app;
grant update (status, posted_at, journal_entry_id, audit_event_id) on inventory.opening_stock_document to rbos_app;
grant select, insert, update on inventory.stock_balance, inventory.item_cost to rbos_app;
