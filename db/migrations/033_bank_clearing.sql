-- 033_bank_clearing.sql : A7 - bank reconciliation markers. Journal entries/lines stay immutable; clearing is a separate marker per ledger line.
create table accounting.bank_clearing (
  tenant_id        uuid not null references core.tenant(id),
  line_id          uuid not null,
  money_account_id uuid not null,
  cleared_by       uuid not null,
  cleared_at       timestamptz not null default now(),
  primary key (tenant_id, line_id),
  foreign key (tenant_id, line_id) references accounting.journal_line(tenant_id, id),
  foreign key (tenant_id, money_account_id) references accounting.money_account(tenant_id, id)
);

alter table accounting.bank_clearing enable row level security;
alter table accounting.bank_clearing force row level security;
create policy tenant_isolation on accounting.bank_clearing for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, delete on accounting.bank_clearing to rbos_app;