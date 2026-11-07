-- Mobile OTP challenges (signup, OTP login, mobile change). Codes are stored only as HMAC hashes.
create table identity.otp_challenge (
  id uuid primary key,
  mobile text not null check (mobile ~ '^[6-9][0-9]{9}$'),
  purpose text not null check (purpose in ('signup','login','change')),
  code_hash text not null,
  attempts integer not null default 0,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null,
  verified_at timestamptz null,
  consumed_at timestamptz null
);
create index ix_otp_challenge_mobile on identity.otp_challenge (mobile, created_at desc);

-- One account per mobile number (Identity PhoneNumber is only set after OTP verification).
create unique index "UX_AspNetUsers_PhoneNumber" on identity."AspNetUsers" ("PhoneNumber") where "PhoneNumber" is not null;

grant select, insert, update on identity.otp_challenge to rbos_app;
