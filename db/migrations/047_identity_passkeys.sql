-- 047_identity_passkeys.sql
-- ASP.NET Core Identity 10 passkey credential store (schema version 3).
-- Credential IDs are globally unique; public-key/authenticator metadata is stored as JSONB.
create table identity."AspNetUserPasskeys" (
    "CredentialId" bytea not null,
    "UserId" uuid not null,
    "Data" jsonb not null,
    constraint "PK_AspNetUserPasskeys" primary key ("CredentialId"),
    constraint "FK_AspNetUserPasskeys_AspNetUsers_UserId"
        foreign key ("UserId") references identity."AspNetUsers" ("Id") on delete cascade
);

create index "IX_AspNetUserPasskeys_UserId"
    on identity."AspNetUserPasskeys" ("UserId");

grant select, insert, update, delete on identity."AspNetUserPasskeys" to rbos_app;
