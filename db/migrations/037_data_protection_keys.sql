-- 037_data_protection_keys.sql : persist ASP.NET Data Protection keys so auth/CSRF cookies survive restarts. Executed by rbos_migrator.
create table if not exists identity.data_protection_key (
	name text primary key,
	xml text not null,
	created_at timestamptz not null default now()
);
grant select, insert on identity.data_protection_key to rbos_app;
