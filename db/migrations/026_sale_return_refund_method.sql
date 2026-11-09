alter table sales.sale_return
	add column if not exists refund_method text null
	check (refund_method is null or refund_method in ('Cash','Bank','UPI'));
