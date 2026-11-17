-- 045_auth_sessions.sql
-- Server-authoritative login sessions with an absolute expiry. Device metadata and
-- user-facing revocation management are deliberately added in Phase 3.
create table identity.auth_session (
    id uuid primary key,
    user_id uuid not null references identity."AspNetUsers" ("Id") on delete cascade,
    authenticated_at timestamptz not null,
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    created_at timestamptz not null default now(),
    constraint ck_auth_session_expiry check (expires_at > authenticated_at),
    constraint ck_auth_session_revocation check (revoked_at is null or revoked_at >= authenticated_at)
);

create index ix_auth_session_user_expiry
    on identity.auth_session (user_id, expires_at desc);

create index ix_auth_session_active_expiry
    on identity.auth_session (expires_at)
    where revoked_at is null;

grant usage on schema identity to rbos_app;
grant select, insert, update on identity.auth_session to rbos_app;
