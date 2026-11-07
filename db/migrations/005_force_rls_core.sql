-- 005_force_rls_core.sql : force RLS on the remaining core tables (consistency; owner is never the runtime role).

alter table core.tenant_membership force row level security;
alter table core.tenant force row level security;
