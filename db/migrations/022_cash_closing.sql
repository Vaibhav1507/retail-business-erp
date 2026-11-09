-- A6: end-of-day cash closing. One count per money account per day; difference is recorded, not auto-posted.
create table accounting.cash_closing (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  money_account_id uuid not null,
  closing_date date not null,
  expected numeric(18,2) not null,
  counted numeric(18,2) not null check (counted >= 0),
  difference numeric(18,2) not null,
  note text null check (note is null or length(note) <= 200),
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, money_account_id, closing_date),
  foreign key (tenant_id, money_account_id) references accounting.money_account(tenant_id, id));
alter table accounting.cash_closing enable row level security;
alter table accounting.cash_closing force row level security;
create policy tenant_isolation on accounting.cash_closing for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on accounting.cash_closing to rbos_app;
