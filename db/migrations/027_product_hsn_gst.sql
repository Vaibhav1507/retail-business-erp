-- A1: HSN code and default GST rate per product (both optional; existing products are unaffected).
alter table catalog.product
  add column hsn_code text null check (hsn_code is null or hsn_code ~ '^[0-9]{4}([0-9]{2}([0-9]{2})?)?$'),
  add column gst_rate numeric(5,2) null check (gst_rate is null or gst_rate between 0 and 100);
