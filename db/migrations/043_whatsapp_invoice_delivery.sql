-- 043_whatsapp_invoice_delivery.sql
-- Single-shop pilot. Sender credentials live in deployment secrets, never in tenant data.
-- Consent is explicit: Unknown and OptedOut recipients are never queued for automated delivery.

alter table sales.customer
  add column whatsapp_invoice_consent_status text not null default 'Unknown'
    check (whatsapp_invoice_consent_status in ('Unknown','OptedIn','OptedOut')),
  add column whatsapp_invoice_consent_updated_at timestamptz null;

grant update (whatsapp_invoice_consent_status, whatsapp_invoice_consent_updated_at)
  on sales.customer to rbos_app;

create table sales.invoice_snapshot (
  tenant_id uuid not null references core.tenant(id),
  sale_id uuid not null,
  invoice_no text not null,
  payload jsonb not null check (jsonb_typeof(payload) = 'object'),
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

create table sales.invoice_delivery (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  sale_id uuid not null,
  channel text not null check (channel in ('WhatsApp','Email')),
  status text not null check (status in (
    'Pending','Processing','Retry','Accepted','Sent','Delivered','Read','Failed',
    'WaitingForSetup','NoRecipient','ConsentRequired','Suppressed','NeedsReview'
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
create unique index ux_invoice_delivery_provider_message
  on sales.invoice_delivery (tenant_id, provider_message_id)
  where provider_message_id is not null;

alter table sales.invoice_delivery enable row level security;
alter table sales.invoice_delivery force row level security;
create policy tenant_isolation on sales.invoice_delivery for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
grant select, insert, update on sales.invoice_delivery to rbos_app;

-- Queue rows are private to SECURITY DEFINER functions. The runtime role cannot query the queue.
create schema if not exists integrations;
grant usage on schema integrations to rbos_app, rbos_worker;

create table integrations.invoice_dispatch_queue (
  delivery_id uuid primary key,
  tenant_id uuid not null,
  sale_id uuid not null,
  actor_user_id uuid not null,
  available_at timestamptz not null default now(),
  lease_until timestamptz null,
  lease_token uuid null,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  created_at timestamptz not null default now()
);
create index ix_invoice_dispatch_available
  on integrations.invoice_dispatch_queue (available_at, created_at);

-- The queue is global across tenants but remains accessible only to the migration
-- owner through SECURITY DEFINER functions. FORCE RLS is required by the repository
-- schema guard for every table carrying tenant_id.
alter table integrations.invoice_dispatch_queue enable row level security;
alter table integrations.invoice_dispatch_queue force row level security;
create policy invoice_dispatch_queue_owner_only on integrations.invoice_dispatch_queue
  for all to rbos_migrator using (true) with check (true);

create or replace function integrations.enqueue_invoice_delivery(
  p_tenant_id uuid, p_delivery_id uuid, p_sale_id uuid, p_actor_user_id uuid
) returns void
language plpgsql security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  -- The API establishes transaction-local user and tenant context via TenantDb.
  -- Keep the enqueue function tied to that exact caller context; membership and
  -- sale ownership have already been checked by the RLS-protected sale transaction.
  if p_tenant_id is distinct from core.current_tenant_id()
     or p_actor_user_id is distinct from core.current_user_id() then
    raise exception 'Tenant context mismatch' using errcode = '42501';
  end if;
  insert into integrations.invoice_dispatch_queue(delivery_id, tenant_id, sale_id, actor_user_id)
  values (p_delivery_id, p_tenant_id, p_sale_id, p_actor_user_id)
  on conflict (delivery_id) do nothing;
end;
$$;

create or replace function integrations.claim_invoice_deliveries(
  p_limit integer default 10, p_lease_seconds integer default 180
) returns table (
  delivery_id uuid, tenant_id uuid, sale_id uuid, actor_user_id uuid,
  lease_token uuid, attempt_count integer
)
language plpgsql security definer
set search_path = pg_catalog, integrations
as $$
begin
  return query
  with candidates as (
    select q.delivery_id from integrations.invoice_dispatch_queue q
    where q.available_at <= now() and (q.lease_until is null or q.lease_until < now())
    order by q.available_at, q.created_at
    for update skip locked
    limit greatest(1, least(coalesce(p_limit, 10), 25))
  ), claimed as (
    update integrations.invoice_dispatch_queue q
       set lease_until = now() + make_interval(secs => greatest(30, least(coalesce(p_lease_seconds, 180), 900))),
           lease_token = gen_random_uuid(), attempt_count = q.attempt_count + 1
      from candidates c where q.delivery_id = c.delivery_id
    returning q.delivery_id, q.tenant_id, q.sale_id, q.actor_user_id, q.lease_token, q.attempt_count
  )
  select c.delivery_id, c.tenant_id, c.sale_id, c.actor_user_id, c.lease_token, c.attempt_count
  from claimed c;
end;
$$;

create or replace function integrations.finish_invoice_delivery(
  p_delivery_id uuid, p_lease_token uuid, p_retry_at timestamptz
) returns boolean
language plpgsql security definer
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

revoke all on integrations.invoice_dispatch_queue from public, rbos_app;
revoke all on function integrations.enqueue_invoice_delivery(uuid,uuid,uuid,uuid) from public;
revoke all on function integrations.claim_invoice_deliveries(integer,integer) from public;
revoke all on function integrations.finish_invoice_delivery(uuid,uuid,timestamptz) from public;
grant execute on function integrations.enqueue_invoice_delivery(uuid,uuid,uuid,uuid) to rbos_app;
grant execute on function integrations.claim_invoice_deliveries(integer,integer) to rbos_worker;
grant execute on function integrations.finish_invoice_delivery(uuid,uuid,timestamptz) to rbos_worker;
