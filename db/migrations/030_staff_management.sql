-- 030_staff_management.sql : G1 - owners can list/add/change staff of their own tenant. Executed by rbos_migrator.
-- Invoker rights; reads only the caller's own membership row (visible under the existing own-row policy).

create function core.is_owner(p_tenant uuid) returns boolean language sql stable
as $$ select exists (select 1 from core.tenant_membership m
					 where m.tenant_id = p_tenant and m.user_id = core.current_user_id()
					   and m.status = 'Active' and m.role = 'Owner') $$;

grant execute on function core.is_owner(uuid) to rbos_app;

create policy membership_owner_select on core.tenant_membership for select to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_owner(tenant_id));

create policy membership_owner_insert on core.tenant_membership for insert to rbos_app
  with check (tenant_id = core.current_tenant_id() and core.is_owner(tenant_id) and role <> 'Owner' and status = 'Active');

create policy membership_owner_update on core.tenant_membership for update to rbos_app
  using (tenant_id = core.current_tenant_id() and core.is_owner(tenant_id) and user_id <> core.current_user_id())
  with check (tenant_id = core.current_tenant_id() and core.is_owner(tenant_id) and role <> 'Owner');

grant update (role, status) on core.tenant_membership to rbos_app;
