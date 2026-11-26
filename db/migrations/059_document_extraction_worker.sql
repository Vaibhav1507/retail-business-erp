-- 059_document_extraction_attempts.sql
-- Durable, auditable extraction attempts and review drafts. The worker receives no direct
-- table access: all source/result operations are guarded by the active document queue lease.

create table documents.document_extraction_attempt (
  tenant_id uuid not null,
  id uuid not null,
  document_id uuid not null,
  attempt_number integer not null check (attempt_number between 1 and 10),
  state text not null check (state in (
    'Started','NeedsReview','FailedRetryable','FailedNeedsAction'
  )),
  extractor text not null check (extractor in ('SalesInvoiceCapture','SupplierInvoiceCapture')),
  schema_version text not null check (length(schema_version) between 1 and 40),
  provider text null check (provider is null or length(provider) <= 40),
  model text null check (model is null or length(model) <= 100),
  safe_error_code text null check (safe_error_code is null or safe_error_code ~ '^[a-z0-9_]{1,64}$'),
  processing_ms integer null check (processing_ms is null or processing_ms between 0 and 600000),
  started_at timestamptz not null default now(),
  completed_at timestamptz null,
  primary key (tenant_id, id),
  unique (tenant_id, document_id, attempt_number),
  foreign key (tenant_id, document_id)
    references documents.business_document(tenant_id, id) on delete cascade
);

create index ix_document_extraction_attempt_document
  on documents.document_extraction_attempt (tenant_id, document_id, attempt_number desc);

create table documents.document_extraction_draft (
  tenant_id uuid not null,
  document_id uuid not null,
  extraction_attempt_id uuid not null,
  schema_version text not null,
  provider text null,
  model text null,
  extracted_data jsonb not null check (jsonb_typeof(extracted_data) = 'object'),
  warnings jsonb not null default '[]'::jsonb check (jsonb_typeof(warnings) = 'array'),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id, document_id),
  foreign key (tenant_id, document_id)
    references documents.business_document(tenant_id, id) on delete cascade,
  foreign key (tenant_id, extraction_attempt_id)
    references documents.document_extraction_attempt(tenant_id, id)
);

create index ix_document_extraction_draft_attempt
  on documents.document_extraction_draft (tenant_id, extraction_attempt_id);

alter table documents.document_extraction_attempt enable row level security;
alter table documents.document_extraction_attempt force row level security;
create policy document_extraction_attempt_tenant_read
  on documents.document_extraction_attempt for select to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
create policy document_extraction_attempt_migrator
  on documents.document_extraction_attempt for all to rbos_migrator
  using (true) with check (true);
grant select on documents.document_extraction_attempt to rbos_app;

alter table documents.document_extraction_draft enable row level security;
alter table documents.document_extraction_draft force row level security;
create policy document_extraction_draft_tenant_read
  on documents.document_extraction_draft for select to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));
create policy document_extraction_draft_migrator
  on documents.document_extraction_draft for all to rbos_migrator
  using (true) with check (true);
grant select on documents.document_extraction_draft to rbos_app;

-- Atomically transition a claimed document into Processing and create its versioned attempt.
-- The queue lease is the worker's authority; actor membership is not impersonated.
create or replace function integrations.start_document_processing(
  p_tenant_id uuid,
  p_document_id uuid,
  p_lease_token uuid
) returns table (
  attempt_id uuid,
  attempt_number integer,
  direction text,
  source_kind text
)
language plpgsql security definer
set search_path = pg_catalog, integrations, documents
as $function$
declare
  v_attempt_number integer;
  v_direction text;
  v_source_kind text;
  v_attempt_id uuid := gen_random_uuid();
begin
  select q.attempt_count, d.direction, d.source_kind
    into v_attempt_number, v_direction, v_source_kind
    from integrations.document_processing_queue q
    join documents.business_document d
      on d.tenant_id = q.tenant_id and d.id = q.document_id
   where q.tenant_id = p_tenant_id
     and q.document_id = p_document_id
     and q.lease_token = p_lease_token
     and q.lease_until > now()
     and d.status in ('Queued','Processing','FailedRetryable')
   for update of q, d;

  if not found then return; end if;
  if v_attempt_number > 4 then return; end if;

  update documents.business_document
     set status = 'Processing', updated_at = now()
   where tenant_id = p_tenant_id and id = p_document_id;

  insert into documents.document_extraction_attempt (
    tenant_id, id, document_id, attempt_number, state, extractor, schema_version
  ) values (
    p_tenant_id, v_attempt_id, p_document_id, v_attempt_number, 'Started',
    case when v_direction = 'Sale' then 'SalesInvoiceCapture' else 'SupplierInvoiceCapture' end,
    case when v_direction = 'Sale' then 'sale-v1' else 'purchase-v1' end
  );

  return query select v_attempt_id, v_attempt_number, v_direction, v_source_kind;
end;
$function$;

-- Read source page bytes only while the worker owns the live lease for this tenant/document.
create or replace function integrations.load_document_processing_pages(
  p_tenant_id uuid,
  p_document_id uuid,
  p_lease_token uuid,
  p_attempt_id uuid
) returns table (
  page_number integer,
  file_name text,
  content_type text,
  size_bytes integer,
  content_sha256 char(64),
  content bytea
)
language sql security definer
set search_path = pg_catalog, integrations, documents
as $function$
  select p.page_number, p.file_name, p.content_type, p.size_bytes, p.content_sha256, p.content
    from integrations.document_processing_queue q
    join documents.business_document d
      on d.tenant_id = q.tenant_id and d.id = q.document_id
    join documents.document_extraction_attempt a
      on a.tenant_id = d.tenant_id and a.document_id = d.id
    join documents.document_page p
      on p.tenant_id = d.tenant_id and p.document_id = d.id
   where q.tenant_id = p_tenant_id
     and q.document_id = p_document_id
     and q.lease_token = p_lease_token
     and q.lease_until > now()
     and d.status = 'Processing'
     and a.id = p_attempt_id
     and a.state = 'Started'
   order by p.page_number;
$function$;

-- Persist a review draft or classify a failure and retire/reschedule the queue lease in the
-- same transaction. The worker never inserts/updates these tables directly.
create or replace function integrations.finish_document_processing(
  p_tenant_id uuid,
  p_document_id uuid,
  p_lease_token uuid,
  p_attempt_id uuid,
  p_final_state text,
  p_provider text,
  p_model text,
  p_extracted_data jsonb,
  p_warnings jsonb,
  p_safe_error_code text,
  p_processing_ms integer,
  p_retry_at timestamptz
) returns boolean
language plpgsql security definer
set search_path = pg_catalog, integrations, documents
as $function$
declare
  v_attempt_number integer;
  v_schema_version text;
begin
  if p_final_state not in ('NeedsReview','FailedRetryable','FailedNeedsAction') then
    raise exception 'Invalid document processing result state' using errcode = '22023';
  end if;
  if p_warnings is not null and jsonb_typeof(p_warnings) <> 'array' then
    raise exception 'Document warnings must be a JSON array' using errcode = '22023';
  end if;
  if p_final_state = 'NeedsReview' and
     (p_extracted_data is null or jsonb_typeof(p_extracted_data) <> 'object') then
    raise exception 'A reviewable extraction draft is required' using errcode = '22023';
  end if;
  if p_final_state = 'FailedRetryable' and (p_retry_at is null or p_retry_at <= now()) then
    raise exception 'Retryable failures need a future retry time' using errcode = '22023';
  end if;
  if p_final_state <> 'FailedRetryable' and p_retry_at is not null then
    raise exception 'Only retryable failures may retain a queue lease' using errcode = '22023';
  end if;
  if p_safe_error_code is not null and p_safe_error_code !~ '^[a-z0-9_]{1,64}$' then
    raise exception 'Invalid safe error code' using errcode = '22023';
  end if;

  select a.attempt_number, a.schema_version
    into v_attempt_number, v_schema_version
    from integrations.document_processing_queue q
    join documents.document_extraction_attempt a
      on a.tenant_id = q.tenant_id and a.document_id = q.document_id
   where q.tenant_id = p_tenant_id
     and q.document_id = p_document_id
     and q.lease_token = p_lease_token
     and q.lease_until > now()
     and a.id = p_attempt_id
     and a.state = 'Started'
   for update of q, a;

  if not found then return false; end if;

  update documents.document_extraction_attempt
     set state = p_final_state,
         provider = nullif(left(coalesce(p_provider, ''), 40), ''),
         model = nullif(left(coalesce(p_model, ''), 100), ''),
         safe_error_code = p_safe_error_code,
         processing_ms = greatest(0, least(coalesce(p_processing_ms, 0), 600000)),
         completed_at = now()
   where tenant_id = p_tenant_id and id = p_attempt_id;

  if p_final_state = 'NeedsReview' then
    insert into documents.document_extraction_draft (
      tenant_id, document_id, extraction_attempt_id, schema_version,
      provider, model, extracted_data, warnings
    ) values (
      p_tenant_id, p_document_id, p_attempt_id, v_schema_version,
      nullif(left(coalesce(p_provider, ''), 40), ''),
      nullif(left(coalesce(p_model, ''), 100), ''),
      p_extracted_data,
      coalesce(p_warnings, '[]'::jsonb)
    )
    on conflict (tenant_id, document_id) do update
      set extraction_attempt_id = excluded.extraction_attempt_id,
          schema_version = excluded.schema_version,
          provider = excluded.provider,
          model = excluded.model,
          extracted_data = excluded.extracted_data,
          warnings = excluded.warnings,
          updated_at = now();
  end if;

  update documents.business_document
     set status = p_final_state, updated_at = now()
   where tenant_id = p_tenant_id and id = p_document_id;

  if p_retry_at is null then
    delete from integrations.document_processing_queue
     where tenant_id = p_tenant_id
       and document_id = p_document_id
       and lease_token = p_lease_token;
  else
    update integrations.document_processing_queue
       set available_at = p_retry_at, lease_until = null, lease_token = null
     where tenant_id = p_tenant_id
       and document_id = p_document_id
       and lease_token = p_lease_token;
  end if;
  return true;
end;
$function$;

revoke all on function integrations.start_document_processing(uuid,uuid,uuid) from public, rbos_app;
revoke all on function integrations.load_document_processing_pages(uuid,uuid,uuid,uuid) from public, rbos_app;
revoke all on function integrations.finish_document_processing(uuid,uuid,uuid,uuid,text,text,text,jsonb,jsonb,text,integer,timestamptz) from public, rbos_app;
grant execute on function integrations.start_document_processing(uuid,uuid,uuid) to rbos_worker;
grant execute on function integrations.load_document_processing_pages(uuid,uuid,uuid,uuid) to rbos_worker;
grant execute on function integrations.finish_document_processing(uuid,uuid,uuid,uuid,text,text,text,jsonb,jsonb,text,integer,timestamptz) to rbos_worker;
