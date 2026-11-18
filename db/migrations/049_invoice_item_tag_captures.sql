-- Private, tenant-owned evidence captured from one physical item's product tag.
-- Item-tag photos are distinct from procurement.invoice_file (supplier documents).
create table sales.item_tag_capture (
  tenant_id uuid not null references core.tenant(id),
  id uuid not null,
  idempotency_key varchar(100) not null check (length(idempotency_key) between 8 and 100),
  content_type text not null check (content_type in ('image/jpeg','image/png','image/webp')),
  size_bytes integer not null check (size_bytes between 1 and 8388608),
  content_sha256 char(64) not null,
  content bytea not null,
  check (octet_length(content) = size_bytes),
  status text not null check (status in ('ReadingTag','ReadyForReview','NeedsAttention','Reviewed','Attached')),
  extraction jsonb not null default '{}'::jsonb,
  suggested_product_id uuid null,
  match_reason varchar(40) null,
  product_id uuid null,
  confirmed_name varchar(160) null,
  confirmed_brand varchar(80) null,
  confirmed_model varchar(80) null,
  confirmed_barcode varchar(64) null,
  barcode_symbology varchar(20) null,
  serial varchar(40) null,
  imei1 varchar(15) null,
  imei2 varchar(15) null,
  mrp numeric(18,2) null check (mrp is null or mrp between 0 and 100000000),
  hsn_code varchar(8) null,
  sale_id uuid null,
  sale_product_id uuid null,
  created_by uuid not null,
  reviewed_by uuid null,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default (now() + interval '30 days'),
  reviewed_at timestamptz null,
  attached_at timestamptz null,
  primary key (tenant_id, id),
  unique (tenant_id, idempotency_key),
  foreign key (tenant_id, suggested_product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, product_id) references catalog.product(tenant_id, id),
  foreign key (tenant_id, sale_id, sale_product_id) references sales.sale_line(tenant_id, sale_id, product_id),
  check ((sale_id is null) = (sale_product_id is null)),
  check ((status = 'Attached') = (sale_id is not null)),
  check (status <> 'Reviewed' or (product_id is not null and reviewed_by is not null and reviewed_at is not null)),
  check (status <> 'Attached' or (attached_at is not null and product_id = sale_product_id))
);

create index ix_item_tag_capture_sale_line
  on sales.item_tag_capture (tenant_id, sale_id, sale_product_id)
  where sale_id is not null;
create index ix_item_tag_capture_status_retention
  on sales.item_tag_capture (tenant_id, expires_at)
  where sale_id is null;

alter table sales.item_tag_capture enable row level security;
alter table sales.item_tag_capture force row level security;
create policy tenant_isolation on sales.item_tag_capture for all to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id))
  with check (tenant_id = core.current_tenant_id() and core.is_active_member(tenant_id));

grant select, insert, update, delete on sales.item_tag_capture to rbos_app;
