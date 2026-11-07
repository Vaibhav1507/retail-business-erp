-- GST rate applied to a sale at invoice time. Null means the bill was generated without GST.
alter table sales.sale add column gst_rate numeric(5,2) null check (gst_rate is null or gst_rate between 0 and 100);
