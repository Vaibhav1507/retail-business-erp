-- 038_purchase_gst.sql : keep supplier GST on purchase receipts. Executed by rbos_migrator.
-- Receipt total stays the grand total payable to the supplier (taxable value + GST).
-- unit_cost stays the pre-GST rate; landed_unit_cost is what one unit really cost (includes GST unless GST is claimable).
alter table procurement.purchase_receipt
	add column if not exists gst_total numeric(18,2) not null default 0 check (gst_total >= 0),
	add column if not exists gst_claimed boolean not null default false;

alter table procurement.purchase_receipt_line
	add column if not exists gst_amount numeric(18,2) not null default 0 check (gst_amount >= 0),
	add column if not exists landed_unit_cost numeric(18,4) null check (landed_unit_cost is null or landed_unit_cost > 0);
