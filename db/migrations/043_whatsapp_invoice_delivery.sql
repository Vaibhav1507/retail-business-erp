-- 043_whatsapp_invoice_delivery.sql
-- Tenant-owned invoice snapshots and delivery history. Cross-tenant dispatch coordination
-- is isolated behind SECURITY DEFINER functions; the backing tables are not directly granted.
create schema if not exists integrations;
grant usage on schema integrations to rbos_app;

alter table sales.customer
  add column whatsapp_invoice_opted_out boolean not null default false;

create table sales.invoice_snapshot (
  tenant_id uuid not null references core.tenant(id),
  sale_id uuid not null,
  invoice_no text not null,
  payload jsonb not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, sale_id),
  foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id)
);

alter table sales.invoice_snapshot enable row level security;
alter table sales.invoice_snapshot force row level security;
create policy tenant_isolation on sales.invoice_snapshot for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert on sales.invoice_snapshot to rbos_app;

create table integrations.whatsapp_connection (
  tenant_id uuid primary key references core.tenant(id),
  waba_id text not null check (length(btrim(waba_id)) between 1 and 80),
  phone_number_id text not null unique check (length(btrim(phone_number_id)) between 1 and 80),
  encrypted_access_token text not null,
  template_name text not null check (length(btrim(template_name)) between 1 and 512),
  template_language text not null default 'en' check (length(btrim(template_language)) between 2 and 12),
  enabled boolean not null default false,
  permission_policy_attested_at timestamptz null,
  configured_by uuid not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table integrations.whatsapp_connection enable row level security;
alter table integrations.whatsapp_connection force row level security;
create policy tenant_isolation on integrations.whatsapp_connection for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update, delete on integrations.whatsapp_connection to rbos_app;

create table sales.invoice_delivery (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  sale_id uuid not null,
  channel text not null check (channel in ('WhatsApp','Email')),
  status text not null check (status in (
    'Pending','Processing','Retry','Accepted','Sent','Delivered','Read','Failed',
    'WaitingForSetup','NoRecipient','Suppressed'
  )),
  recipient text null,
  provider_message_id text null,
  provider_status_at timestamptz null,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  last_error text null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, sale_id, channel),
  foreign key (tenant_id, sale_id) references sales.sale(tenant_id, id)
);

create index ix_invoice_delivery_sale on sales.invoice_delivery (tenant_id, sale_id, created_at desc);
create unique index ux_invoice_delivery_provider_message on sales.invoice_delivery (tenant_id, provider_message_id)
  where provider_message_id is not null;

alter table sales.invoice_delivery enable row level security;
alter table sales.invoice_delivery force row level security;
create policy tenant_isolation on sales.invoice_delivery for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update on sales.invoice_delivery to rbos_app;

-- These two tables are internal coordination indexes. They deliberately have no direct
-- grants to rbos_app; all access is through the restricted functions below.
create table integrations.invoice_dispatch_queue (
  delivery_id uuid primary key,
  tenant_id uuid not null,
  sale_id uuid not null,
  configured_by uuid not null,
  available_at timestamptz not null default now(),
  lease_until timestamptz null,
  lease_token uuid null,
  attempt_count integer not null default 0,
  created_at timestamptz not null default now()
);
create index ix_invoice_dispatch_available
  on integrations.invoice_dispatch_queue (available_at, created_at)
  where lease_until is null;

create table integrations.whatsapp_phone_tenant_lookup (
  phone_number_id text primary key,
  tenant_id uuid not null,
  configured_by uuid not null,
  updated_at timestamptz not null default now()
);

create or replace function integrations.register_whatsapp_phone(
  p_tenant_id uuid, p_phone_number_id text, p_configured_by uuid
) returns void
language plpgsql
security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  if p_tenant_id is distinct from core.current_tenant_id()
     or not core.is_active_member(p_tenant_id) then
    raise exception 'Tenant context mismatch' using errcode = '42501';
  end if;
  insert into integrations.whatsapp_phone_tenant_lookup(phone_number_id, tenant_id, configured_by)
  values (p_phone_number_id, p_tenant_id, p_configured_by)
  on conflict (phone_number_id) do update
    set tenant_id = excluded.tenant_id,
        configured_by = excluded.configured_by,
        updated_at = now()
    where integrations.whatsapp_phone_tenant_lookup.tenant_id = excluded.tenant_id;
  if not found then
    raise exception 'WhatsApp phone number is already linked to another tenant' using errcode = '23505';
  end if;
end;
$$;

create or replace function integrations.enqueue_invoice_delivery(
  p_tenant_id uuid, p_delivery_id uuid, p_sale_id uuid, p_configured_by uuid
) returns void
language plpgsql
security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  if p_tenant_id is distinct from core.current_tenant_id()
     or not core.is_active_member(p_tenant_id) then
    raise exception 'Tenant context mismatch' using errcode = '42501';
  end if;
  insert into integrations.invoice_dispatch_queue(delivery_id, tenant_id, sale_id, configured_by)
  values (p_delivery_id, p_tenant_id, p_sale_id, p_configured_by)
  on conflict (delivery_id) do nothing;
end;
$$;

create or replace function integrations.claim_invoice_deliveries(
  p_limit integer default 10, p_lease_seconds integer default 180
) returns table (
  delivery_id uuid, tenant_id uuid, sale_id uuid, actor_user_id uuid,
  lease_token uuid, attempt_count integer
)
language plpgsql
security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  return query
  with candidates as (
    select q.delivery_id
    from integrations.invoice_dispatch_queue q
    where q.available_at <= now()
      and (q.lease_until is null or q.lease_until < now())
    order by q.available_at, q.created_at
    for update skip locked
    limit greatest(1, least(coalesce(p_limit, 10), 50))
  ),
  claimed as (
    update integrations.invoice_dispatch_queue q
       set lease_until = now() + make_interval(secs => greatest(30, least(coalesce(p_lease_seconds, 180), 900))),
           lease_token = gen_random_uuid(),
           attempt_count = q.attempt_count + 1
      from candidates c
     where q.delivery_id = c.delivery_id
    returning q.delivery_id, q.tenant_id, q.sale_id, q.configured_by, q.lease_token, q.attempt_count
  )
  select c.delivery_id, c.tenant_id, c.sale_id, c.configured_by, c.lease_token, c.attempt_count
    from claimed c;
end;
$$;

create or replace function integrations.finish_invoice_delivery(
  p_delivery_id uuid, p_lease_token uuid, p_retry_at timestamptz
) returns boolean
language plpgsql
security definer
set search_path = pg_catalog, integrations
as $$
begin
  if p_retry_at is null then
    delete from integrations.invoice_dispatch_queue
     where delivery_id = p_delivery_id and lease_token = p_lease_token;
    return found;
  end if;
  update integrations.invoice_dispatch_queue
     set available_at = p_retry_at, lease_until = null, lease_token = null
   where delivery_id = p_delivery_id and lease_token = p_lease_token;
  return found;
end;
$$;

create or replace function integrations.lookup_whatsapp_tenant(p_phone_number_id text)
returns table (tenant_id uuid, actor_user_id uuid)
language sql
security definer
set search_path = pg_catalog, integrations
as $$
  select l.tenant_id, l.configured_by
    from integrations.whatsapp_phone_tenant_lookup l
   where l.phone_number_id = p_phone_number_id
$$;

revoke all on integrations.invoice_dispatch_queue from public, rbos_app;
revoke all on integrations.whatsapp_phone_tenant_lookup from public, rbos_app;
revoke all on function integrations.register_whatsapp_phone(uuid,text,uuid) from public;
revoke all on function integrations.enqueue_invoice_delivery(uuid,uuid,uuid,uuid) from public;
revoke all on function integrations.claim_invoice_deliveries(integer,integer) from public;
revoke all on function integrations.finish_invoice_delivery(uuid,uuid,timestamptz) from public;
revoke all on function integrations.lookup_whatsapp_tenant(text) from public;

grant execute on function integrations.register_whatsapp_phone(uuid,text,uuid) to rbos_app;
grant execute on function integrations.enqueue_invoice_delivery(uuid,uuid,uuid,uuid) to rbos_app;
grant execute on function integrations.claim_invoice_deliveries(integer,integer) to rbos_app;
grant execute on function integrations.finish_invoice_delivery(uuid,uuid,timestamptz) to rbos_app;
grant execute on function integrations.lookup_whatsapp_tenant(text) to rbos_app;

grant update (whatsapp_invoice_opted_out) on sales.customer to rbos_app;
