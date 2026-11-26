-- 060_password_reset_email_otp.sql
-- Single-use, short-lived email challenges for account password recovery.
create table identity.password_reset_otp (
    id uuid primary key,
    user_id uuid not null references identity."AspNetUsers" ("Id") on delete cascade,
    code_hash char(64) not null check (code_hash ~ '^[A-F0-9]{64}$'),
    attempts integer not null default 0 check (attempts between 0 and 5),
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    consumed_at timestamptz null,
    constraint ck_password_reset_otp_expiry check (expires_at > created_at)
);
create index ix_password_reset_otp_user_created on identity.password_reset_otp (user_id, created_at desc);
create index ix_password_reset_otp_active_user on identity.password_reset_otp (user_id, created_at desc) where consumed_at is null;
grant usage on schema identity to rbos_app;
grant select, insert, update on identity.password_reset_otp to rbos_app;
