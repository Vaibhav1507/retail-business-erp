create table accounting.money_account (
  tenant_id  uuid not null references core.tenant(id),
  id         uuid not null,
  account_id uuid not null,
  name       text not null check (length(btrim(name)) between 1 and 80),
  kind       text not null check (kind in ('Cash','Bank','UPI')),
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, account_id),
  foreign key (tenant_id, account_id) references accounting.account(tenant_id, id)
);
create unique index money_account_name_ux on accounting.money_account (tenant_id, lower(name));

create table accounting.expense (
  tenant_id           uuid not null references core.tenant(id),
  id                  uuid not null,
  number              bigint generated always as identity,
  expense_date        date not null,
  category_account_id uuid not null,
  paid_from_account_id uuid not null,
  amount              numeric(18,2) not null check (amount > 0),
  note                text null check (note is null or length(note) <= 200),
  idempotency_key     text not null check (length(idempotency_key) between 8 and 100),
  request_hash        text not null,
  journal_entry_id    uuid not null,
  created_by          uuid not null,
  created_at          timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, category_account_id) references accounting.account(tenant_id, id),
  foreign key (tenant_id, paid_from_account_id) references accounting.account(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id)
);

create table accounting.owner_transaction (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  number           bigint generated always as identity,
  kind             text not null check (kind in ('Capital','Drawing')),
  txn_date         date not null,
  money_account_id uuid not null,
  amount           numeric(18,2) not null check (amount > 0),
  note             text null check (note is null or length(note) <= 200),
  idempotency_key  text not null check (length(idempotency_key) between 8 and 100),
  request_hash     text not null,
  journal_entry_id uuid not null,
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, money_account_id) references accounting.account(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id)
);

create table inventory.stock_adjustment (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  number           bigint generated always as identity,
  product_id       uuid not null,
  location_id      uuid not null,
  adjust_date      date not null,
  quantity_delta   numeric(18,3) not null check (quantity_delta <> 0),
  unit_cost        numeric(18,4) not null check (unit_cost > 0),
  value_delta      numeric(18,2) not null check (value_delta <> 0),
  reason           text not null check (reason in ('Damage','Theft','Shortage','WriteOff','FoundOnCount','OwnerConsumption','Other')),
  note             text null check (note is null or length(note) <= 200),
  idempotency_key  text not null check (length(idempotency_key) between 8 and 100),
  request_hash     text not null,
  journal_entry_id uuid not null,
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, location_id) references core.stock_location(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id)
);

create table accounting.period_lock (
  tenant_id      uuid primary key references core.tenant(id),
  locked_through date null,
  updated_by     uuid not null,
  updated_at     timestamptz not null default now()
);

create table accounting.period_lock_history (
  tenant_id  uuid not null references core.tenant(id),
  id         uuid not null,
  previous   date null,
  new_value  date null,
  reason     text null check (reason is null or length(reason) <= 200),
  changed_by uuid not null,
  changed_at timestamptz not null default now(),
  primary key (tenant_id, id)
);

do $$ declare t text; begin
  foreach t in array array['accounting.money_account','accounting.expense','accounting.owner_transaction','inventory.stock_adjustment','accounting.period_lock','accounting.period_lock_history'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

grant select, insert on accounting.money_account, accounting.expense, accounting.owner_transaction, inventory.stock_adjustment, accounting.period_lock_history to rbos_app;
grant select, insert, update on accounting.period_lock to rbos_app;

create function accounting.enforce_period_lock() returns trigger language plpgsql as $$
declare v date;
begin
  select locked_through into v from accounting.period_lock where tenant_id = new.tenant_id;
  if v is not null and new.effective_date <= v then
	raise exception 'Books are locked through %. Post in an open period.', to_char(v, 'DD Mon YYYY') using errcode = 'RB001';
  end if;
  return new;
end $$;

create trigger period_lock_guard before insert on accounting.journal_entry
  for each row execute function accounting.enforce_period_lock();
