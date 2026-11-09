-- 036_account_active.sql : A10 - custom accounts can be deactivated (never deleted). Executed by rbos_migrator.
alter table accounting.account add column if not exists is_active boolean not null default true;
grant update (is_active) on accounting.account to rbos_app;
