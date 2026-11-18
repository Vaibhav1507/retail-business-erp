-- 048_ai_gateway_usage_ledger.sql
-- Minimal AI usage metadata with database-serialized global and per-tenant budget reservations.
-- Provider prompts, documents, transcripts, model responses and API credentials are never persisted.
-- The ledger functions execute as rbos_migrator. Because core tenant membership is FORCE RLS,
-- allow that non-runtime role to see only the active membership for the transaction-local actor.
-- This is needed for core.is_active_member() inside SECURITY DEFINER functions; the API role
-- cannot SET ROLE to rbos_migrator and has no direct rights on this table.
create policy membership_ai_gateway_migrator_select
    on core.tenant_membership for select to rbos_migrator
    using (user_id = core.current_user_id() and status = 'Active');

create schema if not exists ai_gateway;
revoke all on schema ai_gateway from public;
grant usage on schema ai_gateway to rbos_app;
create table ai_gateway.usage_event (
 id uuid primary key, tenant_id uuid not null, actor_user_id uuid not null,
 feature varchar(80) not null, task varchar(40) not null, provider varchar(20) not null,
 model varchar(120) not null, credential_id varchar(80) not null, quota_bucket_id varchar(120) not null,
 correlation_id varchar(64) not null, estimated_cost_inr numeric(14,6) not null check (estimated_cost_inr >= 0),
 actual_cost_inr numeric(14,6) null check (actual_cost_inr is null or actual_cost_inr >= 0),
 input_tokens integer null check (input_tokens is null or input_tokens >= 0),
 output_tokens integer null check (output_tokens is null or output_tokens >= 0),
 latency_ms integer null check (latency_ms is null or latency_ms >= 0),
 outcome varchar(16) not null check (outcome in ('Reserved','Succeeded','Failed')),
 error_category varchar(80) null, retry_count integer not null default 0 check (retry_count between 0 and 10),
 created_at timestamptz not null default now(), completed_at timestamptz null,
 constraint ck_ai_gateway_completion check (
  (outcome = 'Reserved' and completed_at is null) or
  (outcome in ('Succeeded','Failed') and completed_at is not null))
);
create index ix_ai_gateway_usage_month on ai_gateway.usage_event (created_at);
create index ix_ai_gateway_usage_tenant_month on ai_gateway.usage_event (tenant_id, created_at);

-- Every tenant-scoped table is forced through RLS. The application role has no
-- direct table grants; only the trusted migration/security-definer owner role
-- can access ledger rows so the global budget calculation can aggregate safely.
alter table ai_gateway.usage_event enable row level security;
alter table ai_gateway.usage_event force row level security;
create policy ai_gateway_usage_event_migrator_all
 on ai_gateway.usage_event
 for all to rbos_migrator
 using (true)
 with check (true);
revoke all on ai_gateway.usage_event from public, rbos_app;

create or replace function ai_gateway.reserve_attempt(
 p_tenant uuid,p_actor uuid,p_feature text,p_task text,p_provider text,p_model text,
 p_credential text,p_quota_bucket text,p_estimated_cost numeric,p_global_limit numeric,
 p_tenant_limit numeric,p_warning_threshold numeric,p_paid boolean,p_allow_paid boolean,p_correlation text)
returns table(reservation_id uuid,allowed boolean,global_month_to_date_inr numeric,
 tenant_month_to_date_inr numeric,warning_threshold_reached boolean,block_reason text)
language plpgsql security definer set search_path = pg_catalog, ai_gateway, core as $$
declare v_global numeric(14,6); v_tenant numeric(14,6); v_id uuid; v_cost numeric(14,6);
begin
 if core.current_user_id() is distinct from p_actor or core.current_tenant_id() is distinct from p_tenant
    or not core.is_active_member(p_tenant) then
  raise exception 'AI usage context is not authorized' using errcode = '42501';
 end if;
 if p_estimated_cost is null or p_estimated_cost < 0 or (p_paid and p_estimated_cost <= 0)
    or p_global_limit is null or p_global_limit < 0
    or p_tenant_limit is null or p_tenant_limit < 0 or p_warning_threshold is null
    or p_warning_threshold <= 0 or p_warning_threshold >= 1 then
  raise exception 'AI budget parameters are invalid' using errcode = '22023';
 end if;
 perform pg_advisory_xact_lock(7272742);
 select coalesce(sum(coalesce(e.actual_cost_inr,e.estimated_cost_inr)),0) into v_global
 from ai_gateway.usage_event e where e.created_at >= date_trunc('month',now());
 select coalesce(sum(coalesce(e.actual_cost_inr,e.estimated_cost_inr)),0) into v_tenant
 from ai_gateway.usage_event e where e.tenant_id=p_tenant and e.created_at >= date_trunc('month',now());
 if p_paid and not p_allow_paid then
  return query select null::uuid,false,v_global,v_tenant,false,'paid_provider_disabled'::text; return;
 end if;
 v_cost := p_estimated_cost;
 if p_paid and (v_global+v_cost > p_global_limit or v_tenant+v_cost > p_tenant_limit) then
  return query select null::uuid,false,v_global,v_tenant,false,'monthly_budget_exceeded'::text; return;
 end if;
 v_id := gen_random_uuid();
 insert into ai_gateway.usage_event(id,tenant_id,actor_user_id,feature,task,provider,model,
  credential_id,quota_bucket_id,correlation_id,estimated_cost_inr,outcome)
 values(v_id,p_tenant,p_actor,left(p_feature,80),left(p_task,40),left(p_provider,20),
  left(p_model,120),left(p_credential,80),left(p_quota_bucket,120),left(p_correlation,64),v_cost,'Reserved');
 return query select v_id,true,v_global+v_cost,v_tenant+v_cost,
  (p_paid and (v_global+v_cost >= p_global_limit*p_warning_threshold
            or v_tenant+v_cost >= p_tenant_limit*p_warning_threshold)),null::text;
end; $$;

create or replace function ai_gateway.finish_attempt(
 p_tenant uuid,p_actor uuid,p_reservation uuid,p_outcome text,p_input_tokens integer,
 p_output_tokens integer,p_actual_cost numeric,p_latency_ms integer,p_error_category text,p_retry_count integer)
returns boolean language plpgsql security definer set search_path = pg_catalog, ai_gateway, core as $$
declare v_updated integer;
begin
 perform pg_advisory_xact_lock(7272742);
 if core.current_user_id() is distinct from p_actor or core.current_tenant_id() is distinct from p_tenant
    or not core.is_active_member(p_tenant) then
  raise exception 'AI usage context is not authorized' using errcode = '42501';
 end if;
 update ai_gateway.usage_event set
  outcome=case when p_outcome='Succeeded' then 'Succeeded' else 'Failed' end,
  input_tokens=case when p_input_tokens >= 0 then p_input_tokens else null end,
  output_tokens=case when p_output_tokens >= 0 then p_output_tokens else null end,
  actual_cost_inr=case when p_actual_cost >= 0 then p_actual_cost else null end,
  latency_ms=greatest(0,least(coalesce(p_latency_ms,0),600000)),
  error_category=left(p_error_category,80),retry_count=greatest(0,least(coalesce(p_retry_count,0),10)),
  completed_at=now()
 where id=p_reservation and tenant_id=p_tenant and actor_user_id=p_actor and outcome='Reserved';
 get diagnostics v_updated = row_count;
 return v_updated=1;
end; $$;

revoke all on function ai_gateway.reserve_attempt(uuid,uuid,text,text,text,text,text,text,numeric,numeric,numeric,numeric,boolean,boolean,text) from public;
revoke all on function ai_gateway.finish_attempt(uuid,uuid,uuid,text,integer,integer,numeric,integer,text,integer) from public;
grant execute on function ai_gateway.reserve_attempt(uuid,uuid,text,text,text,text,text,text,numeric,numeric,numeric,numeric,boolean,boolean,text) to rbos_app;
grant execute on function ai_gateway.finish_attempt(uuid,uuid,uuid,text,integer,integer,numeric,integer,text,integer) to rbos_app;
