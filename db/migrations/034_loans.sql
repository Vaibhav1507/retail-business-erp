-- 034_loans.sql : A9 - loans taken by the business. Receiving a loan and each repayment post balanced journal entries (liability 2500, interest expense 6100).
create table accounting.loan (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  number           bigint generated always as identity,
  lender           text not null check (length(lender) between 1 and 100),
  principal        numeric(18,2) not null check (principal > 0),
  start_date       date not null,
  interest_rate    numeric(6,2) not null default 0 check (interest_rate >= 0 and interest_rate <= 100),
  money_account_id uuid not null,
  note             text null check (note is null or length(note) <= 200),
  idempotency_key  text not null check (length(idempotency_key) between 8 and 100),
  request_hash     text not null,
  journal_entry_id uuid not null,
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, money_account_id) references accounting.money_account(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id)
);

create table accounting.loan_payment (
  tenant_id        uuid not null references core.tenant(id),
  id               uuid not null,
  loan_id          uuid not null,
  pay_date         date not null,
  principal_paid   numeric(18,2) not null check (principal_paid >= 0),
  interest_paid    numeric(18,2) not null check (interest_paid >= 0),
  money_account_id uuid not null,
  note             text null check (note is null or length(note) <= 200),
  idempotency_key  text not null check (length(idempotency_key) between 8 and 100),
  request_hash     text not null,
  journal_entry_id uuid not null,
  created_by       uuid not null,
  created_at       timestamptz not null default now(),
  primary key (tenant_id, id),
  check (principal_paid + interest_paid > 0),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, loan_id) references accounting.loan(tenant_id, id),
  foreign key (tenant_id, money_account_id) references accounting.money_account(tenant_id, id),
  foreign key (tenant_id, journal_entry_id) references accounting.journal_entry(tenant_id, id)
);

do $$ declare t text; begin
  foreach t in array array['accounting.loan','accounting.loan_payment'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

grant select, insert on accounting.loan, accounting.loan_payment to rbos_app;

-- A10: chart of accounts. Owner-created accounts are custom and renamable; system accounts stay fixed.
alter table accounting.account add column if not exists is_custom boolean not null default false;
grant update (name) on accounting.account to rbos_app;
