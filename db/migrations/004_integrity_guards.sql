-- 004_integrity_guards.sql : one business per owner (Slice 1) and one posted opening-stock document per location.

create unique index ux_tenant_one_per_creator on core.tenant (created_by);

create unique index ux_opening_stock_one_posted_per_location
  on inventory.opening_stock_document (tenant_id, location_id)
  where status = 'Posted';
