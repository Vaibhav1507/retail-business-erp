-- 046_trusted_device_registry.sql
-- User-scoped browser/device registry and append-only authentication security events.
-- Device identifiers are descriptive browser-install labels, not proof of hardware identity.

create table identity.auth_device (
    id uuid primary key,
    user_id uuid not null references identity."AspNetUsers" ("Id") on delete cascade,
    device_key_hash bytea null,
    device_label varchar(120) not null,
    browser varchar(80) not null,
    platform varchar(80) not null,
    user_agent varchar(512) null,
    created_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    last_authenticated_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    constraint ck_auth_device_expiry check (expires_at > created_at),
    constraint ck_auth_device_revocation check (revoked_at is null or revoked_at >= created_at)
);

create index ix_auth_device_user_key
    on identity.auth_device (user_id, device_key_hash, last_seen_at desc)
    where device_key_hash is not null;

-- At most one legacy placeholder device per user, for session rows created before
-- device registration was enabled. Such devices have no reusable browser key.
create unique index ux_auth_device_legacy_user
    on identity.auth_device (user_id)
    where device_key_hash is null;

create index ix_auth_device_user_activity
    on identity.auth_device (user_id, revoked_at, last_seen_at desc);

alter table identity.auth_session
    add column device_id uuid null references identity.auth_device (id) on delete restrict;

-- Preserve any already-issued Phase 2 sessions if this migration is ever applied
-- separately from the planned all-phases rollout.
insert into identity.auth_device (
    id, user_id, device_key_hash, device_label, browser, platform, user_agent,
    created_at, last_seen_at, last_authenticated_at, expires_at, revoked_at
)
select
    gen_random_uuid(),
    s.user_id,
    null,
    'Previously signed-in device',
    'Unknown',
    'Unknown',
    null,
    min(s.created_at),
    max(s.created_at),
    max(s.authenticated_at),
    greatest(max(s.expires_at), min(s.created_at) + interval '1 day'),
    case when bool_and(s.revoked_at is not null) then max(s.revoked_at) else null end
from identity.auth_session s
group by s.user_id;

update identity.auth_session s
set device_id = d.id
from identity.auth_device d
where d.user_id = s.user_id
  and d.device_key_hash is null;

alter table identity.auth_session
    alter column device_id set not null;

create index ix_auth_session_device
    on identity.auth_session (device_id, expires_at desc);

create table identity.auth_security_event (
    id uuid primary key,
    user_id uuid not null references identity."AspNetUsers" ("Id") on delete cascade,
    device_id uuid null references identity.auth_device (id) on delete set null,
    session_id uuid null references identity.auth_session (id) on delete set null,
    event_type varchar(60) not null,
    occurred_at timestamptz not null default now(),
    metadata jsonb not null default '{}'::jsonb
);

create index ix_auth_security_event_user_time
    on identity.auth_security_event (user_id, occurred_at desc);

grant usage on schema identity to rbos_app;
grant select, insert, update on identity.auth_device to rbos_app;
grant select, insert on identity.auth_security_event to rbos_app;
