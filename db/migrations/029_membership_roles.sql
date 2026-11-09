-- 029_membership_roles.sql : D2/G1 - roles become bundles of permissions (see Rbos.Domain.Permissions).
-- Existing rows are all 'Owner' and stay valid. Executed by rbos_migrator.

alter table core.tenant_membership drop constraint if exists tenant_membership_role_check;
alter table core.tenant_membership
  add constraint tenant_membership_role_check check (role in ('Owner','Manager','Accountant','Cashier'));
