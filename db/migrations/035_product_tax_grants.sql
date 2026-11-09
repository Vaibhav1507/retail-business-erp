-- 035_product_tax_grants.sql : A1 - runtime role may update the optional HSN/GST columns added in 027. Executed by rbos_migrator.
grant update (hsn_code, gst_rate) on catalog.product to rbos_app;
