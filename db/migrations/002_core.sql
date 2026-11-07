-- 002_core.sql : tenancy foundation, RLS helper functions, tenant/membership/profile/branch/location, security log.
-- Executed by rbos_migrator. Tenant context = transaction-local settings app.user_id and app.tenant_id.
-- No SECURITY DEFINER functions. No dynamic SQL.

create schema if not exists core;
create schema if not exists catalog;
create schema if not exists inventory;
create schema if not exists accounting;
create schema if not exists audit;
create schema if not exists platform;
create schema if not exists security;

revoke all on schema core, catalog, inventory, accounting, audit, platform, security from public;
grant usage on schema core, catalog, inventory, accounting, audit, platform, security, identity to rbos_app;

create function core.current_user_id() returns uuid language sql stable
as $$ select nullif(current_setting('app.user_id', true), '')::uuid $$;

create function core.current_tenant_id() returns uuid language sql stable
as $$ select nullif(current_setting('app.tenant_id', true), '')::uuid $$;

create table core.tenant (
  id            uuid primary key,
  name          text not null check (length(btrim(name)) between 1 and 120),
  created_by    uuid not null references identity."AspNetUsers"("Id"),
  bootstrap_key text not null check (length(bootstrap_key) between 8 and 100),
  created_at    timestamptz not null default now(),
  unique (created_by, bootstrap_key)
);

create table core.tenant_membership (
  tenant_id  uuid not null references core.tenant(id),
  user_id    uuid not null references identity."AspNetUsers"("Id"),
  role       text not null check (role in ('Owner')),
  status     text not null check (status in ('Active', 'Revoked')),
  created_at timestamptz not null default now(),
  primary key (tenant_id, user_id)
);

-- Own memberships only; invoker rights (not security definer).
create function core.is_active_member(p_tenant uuid) returns boolean language sql stable
as $$ select exists (select 1 from core.tenant_membership m
					 where m.tenant_id = p_tenant and m.user_id = core.current_user_id() and m.status = 'Active') $$;

create table core.business_profile (
  tenant_id     uuid primary key references core.tenant(id),
  display_name  text not null check (length(btrim(display_name)) between 1 and 120),
  base_currency text not null default 'INR' check (base_currency = 'INR'),
  created_at    timestamptz not null default now()
);

create table core.branch (
  tenant_id  uuid not null references core.tenant(id),
  id         uuid not null,
  name       text not null check (length(btrim(name)) between 1 and 120),
  is_primary boolean not null default true,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id)
);

create table core.stock_location (
  tenant_id  uuid not null,
  id         uuid not null,
  branch_id  uuid not null,
  name       text not null check (length(btrim(name)) between 1 and 120),
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, branch_id) references core.branch(tenant_id, id)
);

-- Global security log: no tenant data, insert-only for the application.
create table security.security_event (
  id             bigint generated always as identity primary key,
  occurred_at    timestamptz not null default now(),
  event_type     text not null check (event_type in ('SignInSucceeded','SignInFailed','LockedOut','Registered','TenantBootstrapFailed')),
  user_id        uuid null,
  correlation_id text null,
  detail         text null check (detail is null or length(detail) <= 200)
);

-- RLS: tenant
alter table core.tenant enable row level security;
create policy tenant_select on core.tenant for select to rbos_app
  using (core.is_active_member(id) or created_by = core.current_user_id());
create policy tenant_insert on core.tenant for insert to rbos_app
  with check (created_by = core.current_user_id());

-- RLS: membership (own rows only; insert only as the bootstrap Owner of a tenant the user created)
alter table core.tenant_membership enable row level security;
create policy membership_select on core.tenant_membership for select to rbos_app
  using (user_id = core.current_user_id());
create policy membership_insert on core.tenant_membership for insert to rbos_app
  with check (user_id = core.current_user_id() and role = 'Owner' and status = 'Active'
			  and tenant_id = core.current_tenant_id()
			  and exists (select 1 from core.tenant t where t.id = tenant_id and t.created_by = core.current_user_id()));

-- RLS: tenant-owned tables (tenant context AND live active membership)
do $$
declare t text;
begin
  foreach t in array array['core.business_profile','core.branch','core.stock_location'] loop
	execute format('alter table %s enable row level security', t);
	execute format('alter table %s force row level security', t);
	execute format('create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))', t);
  end loop;
end $$;

-- Grants (no DELETE anywhere; no blanket grants)
grant execute on function core.current_user_id(), core.current_tenant_id(), core.is_active_member(uuid) to rbos_app;
grant select, insert on core.tenant to rbos_app;
grant select, insert on core.tenant_membership to rbos_app;
grant select, insert on core.business_profile, core.branch, core.stock_location to rbos_app;
grant insert on security.security_event to rbos_app;
grant select, insert, update, delete on all tables in schema identity to rbos_app;
grant usage, select on all sequences in schema security to rbos_app;
grant usage on all sequences in schema identity to rbos_app;
