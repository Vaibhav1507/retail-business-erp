create table catalog.external_barcode_lookup_cache (
  tenant_id      uuid not null references core.tenant(id),
  barcode        text not null check (length(barcode) between 4 and 64 and barcode !~ '\s'),
  provider       text not null check (length(provider) between 1 and 80),
  status         text not null check (status in ('Found','NotFound','Timeout','RateLimited','Unavailable','Error')),
  payload_json   jsonb null,
  attribution    text null,
  looked_up_at   timestamptz not null,
  expires_at     timestamptz not null,
  primary key (tenant_id, barcode, provider)
);

create index ix_ext_barcode_lookup_cache_expiry
  on catalog.external_barcode_lookup_cache (tenant_id, expires_at);

alter table catalog.external_barcode_lookup_cache enable row level security;
alter table catalog.external_barcode_lookup_cache force row level security;
create policy tenant_isolation on catalog.external_barcode_lookup_cache for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, update, delete on catalog.external_barcode_lookup_cache to rbos_app;
