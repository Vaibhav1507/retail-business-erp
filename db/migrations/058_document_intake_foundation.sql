-- 058_document_intake_foundation.sql
-- Shared tenant-scoped source-document registry, page evidence and durable processing queue.
-- The pilot stores small, bounded source files in PostgreSQL so uploads survive Cloud Run
-- instance replacement. This is intentionally behind a future storage migration boundary.

create schema if not exists documents;
grant usage on schema documents to rbos_app;

create table documents.document_batch (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  direction text not null check (direction in ('Sale','Purchase')),
  idempotency_key text not null check (length(idempotency_key) between 8 and 100),
  request_hash char(64) not null check (request_hash ~ '^[0-9a-f]{64}$'),
  document_count integer not null check (document_count between 1 and 10),
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key)
);

create table documents.business_document (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  batch_id uuid not null,
  direction text not null check (direction in ('Sale','Purchase')),
  source_kind text not null check (source_kind in ('HandwrittenBill','PrintedInvoice','ThermalReceipt','Other')),
  status text not null default 'Queued' check (status in (
    'Queued','Processing','NeedsReview','ReadyToPost','Posted',
    'FailedRetryable','FailedNeedsAction','DuplicateReview','Rejected','Cancelled'
  )),
  page_count integer not null check (page_count between 1 and 6),
  total_size_bytes integer not null check (total_size_bytes between 1 and 25165824),
  content_sha256 char(64) not null check (content_sha256 ~ '^[0-9a-f]{64}$'),
  created_by uuid not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id, id),
  foreign key (tenant_id, batch_id) references documents.document_batch(tenant_id, id)
);

create index ix_business_document_tenant_created
  on documents.business_document (tenant_id, created_at desc, id);
create index ix_business_document_tenant_status
  on documents.business_document (tenant_id, status, created_at desc);

create table documents.document_page (
  tenant_id uuid not null,
  id uuid not null,
  document_id uuid not null,
  page_number integer not null check (page_number between 1 and 6),
  file_name text not null check (length(file_name) between 1 and 180),
  content_type text not null check (content_type in ('image/jpeg','image/png','image/webp','application/pdf')),
  size_bytes integer not null check (size_bytes between 1 and 8388608),
  content_sha256 char(64) not null check (content_sha256 ~ '^[0-9a-f]{64}$'),
  content bytea not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, id),
  unique (tenant_id, document_id, page_number),
  foreign key (tenant_id, document_id) references documents.business_document(tenant_id, id) on delete cascade,
  check (octet_length(content) = size_bytes)
);

create index ix_document_page_document
  on documents.document_page (tenant_id, document_id, page_number);

do $$
declare t text;
begin
  foreach t in array array['documents.document_batch','documents.business_document','documents.document_page'] loop
    execute format('alter table %s enable row level security', t);
    execute format('alter table %s force row level security', t);
    execute format(
      'create policy tenant_isolation on %s for all to rbos_app using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id)) with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))',
      t
    );
  end loop;
end $$;

grant select, insert on documents.document_batch to rbos_app;
grant select, insert, update on documents.business_document to rbos_app;
grant select, insert on documents.document_page to rbos_app;

-- Queue rows are not directly visible to either runtime role. All access is through
-- SECURITY DEFINER functions with fixed search_path and lease-token ownership.
create table integrations.document_processing_queue (
  document_id uuid primary key,
  tenant_id uuid not null,
  actor_user_id uuid not null,
  available_at timestamptz not null default now(),
  lease_until timestamptz null,
  lease_token uuid null,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  created_at timestamptz not null default now(),
  foreign key (tenant_id, document_id)
    references documents.business_document(tenant_id, id) on delete cascade
);

create index ix_document_processing_available
  on integrations.document_processing_queue (available_at, created_at);

alter table integrations.document_processing_queue enable row level security;
alter table integrations.document_processing_queue force row level security;
create policy document_processing_queue_owner_only
  on integrations.document_processing_queue
  for all to rbos_migrator using (true) with check (true);

create or replace function integrations.enqueue_document_processing(
  p_tenant_id uuid, p_document_id uuid, p_actor_user_id uuid
) returns void
language plpgsql security definer
set search_path = pg_catalog, integrations, core
as $$
begin
  if p_tenant_id is distinct from core.current_tenant_id()
     or p_actor_user_id is distinct from core.current_user_id() then
    raise exception 'Tenant context mismatch' using errcode = '42501';
  end if;
  insert into integrations.document_processing_queue(document_id, tenant_id, actor_user_id)
  values (p_document_id, p_tenant_id, p_actor_user_id)
  on conflict (document_id) do nothing;
end;
$$;

create or replace function integrations.claim_document_processing(
  p_limit integer default 5, p_lease_seconds integer default 300, p_document_id uuid default null
) returns table (
  document_id uuid, tenant_id uuid, actor_user_id uuid,
  lease_token uuid, attempt_count integer
)
language plpgsql security definer
set search_path = pg_catalog, integrations
as $$
begin
  return query
  with candidates as (
    select q.document_id
      from integrations.document_processing_queue q
     where q.available_at <= now()
       and (q.lease_until is null or q.lease_until < now())
       and (p_document_id is null or q.document_id = p_document_id)
     order by q.available_at, q.created_at
     for update skip locked
     limit greatest(1, least(coalesce(p_limit, 5), 10))
  ), claimed as (
    update integrations.document_processing_queue q
       set lease_until = now() + make_interval(secs => greatest(30, least(coalesce(p_lease_seconds, 300), 900))),
           lease_token = gen_random_uuid(),
           attempt_count = q.attempt_count + 1
      from candidates c
     where q.document_id = c.document_id
    returning q.document_id, q.tenant_id, q.actor_user_id, q.lease_token, q.attempt_count
  )
  select c.document_id, c.tenant_id, c.actor_user_id, c.lease_token, c.attempt_count
    from claimed c;
end;
$$;

create or replace function integrations.finish_document_processing(
  p_document_id uuid, p_lease_token uuid, p_retry_at timestamptz
) returns boolean
language plpgsql security definer
set search_path = pg_catalog, integrations
as $$
begin
  if p_retry_at is null then
    delete from integrations.document_processing_queue
     where document_id = p_document_id and lease_token = p_lease_token;
    return found;
  end if;
  update integrations.document_processing_queue
     set available_at = p_retry_at, lease_until = null, lease_token = null
   where document_id = p_document_id and lease_token = p_lease_token;
  return found;
end;
$$;

revoke all on integrations.document_processing_queue from public, rbos_app, rbos_worker;
revoke all on function integrations.enqueue_document_processing(uuid,uuid,uuid) from public;
revoke all on function integrations.claim_document_processing(integer,integer,uuid) from public;
revoke all on function integrations.finish_document_processing(uuid,uuid,timestamptz) from public;
grant execute on function integrations.enqueue_document_processing(uuid,uuid,uuid) to rbos_app;
grant execute on function integrations.claim_document_processing(integer,integer,uuid) to rbos_worker;
grant execute on function integrations.finish_document_processing(uuid,uuid,timestamptz) to rbos_worker;
