-- 050_durable_item_tag_capture_queue.sql
-- Durable outbox for asynchronous item-tag extraction. Queue payload is identifiers only.
create table integrations.item_tag_capture_queue (
  capture_id uuid primary key,
  tenant_id uuid not null,
  actor_user_id uuid not null,
  available_at timestamptz not null default now(),
  lease_until timestamptz null,
  lease_token uuid null,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  created_at timestamptz not null default now(),
  foreign key (tenant_id, capture_id)
    references sales.item_tag_capture(tenant_id, id) on delete cascade
);
create index ix_item_tag_capture_queue_available
  on integrations.item_tag_capture_queue (available_at, created_at);

alter table integrations.item_tag_capture_queue enable row level security;
alter table integrations.item_tag_capture_queue force row level security;
create policy item_tag_capture_queue_owner_only
  on integrations.item_tag_capture_queue
  for all to rbos_migrator using (true) with check (true);

create or replace function integrations.enqueue_item_tag_capture(
  p_tenant_id uuid, p_capture_id uuid, p_actor_user_id uuid
) returns void
language plpgsql security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  if p_tenant_id is distinct from core.current_tenant_id()
     or p_actor_user_id is distinct from core.current_user_id() then
    raise exception 'Tenant context mismatch' using errcode = '42501';
  end if;
  insert into integrations.item_tag_capture_queue(capture_id, tenant_id, actor_user_id)
  values (p_capture_id, p_tenant_id, p_actor_user_id)
  on conflict (capture_id) do nothing;
end;
$$;

create or replace function integrations.claim_item_tag_captures(
  p_limit integer default 1, p_lease_seconds integer default 300
) returns table (
  capture_id uuid, tenant_id uuid, actor_user_id uuid,
  lease_token uuid, attempt_count integer
)
language plpgsql security definer
set search_path = pg_catalog, integrations
as $$
begin
  return query
  with candidates as (
    select q.capture_id
      from integrations.item_tag_capture_queue q
     where q.available_at <= now()
       and (q.lease_until is null or q.lease_until < now())
     order by q.available_at, q.created_at
     for update skip locked
     limit greatest(1, least(coalesce(p_limit, 1), 10))
  ), claimed as (
    update integrations.item_tag_capture_queue q
       set lease_until = now() + make_interval(secs => greatest(30, least(coalesce(p_lease_seconds, 300), 900))),
           lease_token = gen_random_uuid(),
           attempt_count = q.attempt_count + 1
      from candidates c
     where q.capture_id = c.capture_id
    returning q.capture_id, q.tenant_id, q.actor_user_id, q.lease_token, q.attempt_count
  )
  select c.capture_id, c.tenant_id, c.actor_user_id, c.lease_token, c.attempt_count
    from claimed c;
end;
$$;

create or replace function integrations.finish_item_tag_capture(
  p_capture_id uuid, p_lease_token uuid, p_retry_at timestamptz
) returns boolean
language plpgsql security definer
set search_path = pg_catalog, integrations
as $$
begin
  if p_retry_at is null then
    delete from integrations.item_tag_capture_queue
     where capture_id = p_capture_id and lease_token = p_lease_token;
    return found;
  end if;
  update integrations.item_tag_capture_queue
     set available_at = p_retry_at, lease_until = null, lease_token = null
   where capture_id = p_capture_id and lease_token = p_lease_token;
  return found;
end;
$$;

revoke all on integrations.item_tag_capture_queue from public, rbos_app, rbos_worker;
revoke all on function integrations.enqueue_item_tag_capture(uuid,uuid,uuid) from public;
revoke all on function integrations.claim_item_tag_captures(integer,integer) from public;
revoke all on function integrations.finish_item_tag_capture(uuid,uuid,timestamptz) from public;
grant execute on function integrations.enqueue_item_tag_capture(uuid,uuid,uuid) to rbos_app;
grant execute on function integrations.claim_item_tag_captures(integer,integer) to rbos_worker;
grant execute on function integrations.finish_item_tag_capture(uuid,uuid,timestamptz) to rbos_worker;
