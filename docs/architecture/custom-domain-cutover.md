# Dhandha Mitra custom-domain cutover

**Domain:** `dhandhamitra.in`  
**Cloud Run service:** `rbos-pilot`  
**Region:** `asia-southeast1`  
**GCP project:** `rbos-pilot`  
**Status:** Mapping resource created; DNS still points to GoDaddy parking; certificate pending. This note is an execution record, not approval to change DNS or deploy.

## Verified state (2026-11-27)

- Google Search Console shows **Ownership verified** using the domain-name-provider method.
- `gcloud domains list-user-verified` returns `dhandhamitra.in`.
- Cloud Run domain mapping `dhandhamitra.in` is attached to service `rbos-pilot` in `asia-southeast1`.
- Mapping condition `DomainRoutable=True`; `CertificateProvisioned=Unknown` with reason `CertificatePending`.
- GoDaddy authoritative nameservers: `ns55.domaincontrol.com`, `ns56.domaincontrol.com`.
- Public DNS still returns GoDaddy parked A records `3.33.130.190` and `15.197.148.33`.
- No DNS records have been changed by this work.
- No Cloud Run environment variables have been changed and no deployment has been performed.

## Google-provided apex DNS records

When the application host-compatibility revision is approved and deployed, replace the two GoDaddy parking A records at host `@` with all four Google A records and add all four AAAA records below.

| Type | GoDaddy host | Value |
|---|---|---|
| A | `@` | `216.239.32.21` |
| A | `@` | `216.239.34.21` |
| A | `@` | `216.239.36.21` |
| A | `@` | `216.239.38.21` |
| AAAA | `@` | `2001:4860:4802:32::15` |
| AAAA | `@` | `2001:4860:4802:34::15` |
| AAAA | `@` | `2001:4860:4802:36::15` |
| AAAA | `@` | `2001:4860:4802:38::15` |

Do not delete unrelated TXT records, GoDaddy domain-verification records, DMARC, or any future Resend DKIM/SPF records. Do not configure `www` yet: no `www.dhandhamitra.in` Cloud Run mapping has been created. Create and verify a separate mapping before adding a `www` DNS record.

## Host and passkey architecture

- ASP.NET Core `AllowedHosts` is the HTTP Host header allowlist. It can contain multiple explicit DNS hostnames separated by semicolons; it must not use wildcard entries when passkeys are enabled.
- `Auth:Passkeys:ServerDomain` is the single WebAuthn relying-party (RP) domain. It must remain exactly one hostname and must be included in `AllowedHosts`.
- Passkey capability endpoints report disabled unless the incoming request hostname exactly matches the configured RP domain. Password and OTP authentication remain available on any other explicitly allowed fallback host.
- Passkeys are origin/RP scoped. Existing credentials created under the old `.run.app` hostname cannot be reused on `dhandhamitra.in`; users must sign in with password/OTP and register a new passkey on the custom domain.
- The old Cloud Run URL is retained as a password/OTP fallback only after the custom-domain RP switch. Verify its exact current hostname using `gcloud run services describe rbos-pilot --region=asia-southeast1 --format='value(status.url)'`; do not guess the hostname.

## Safe staged sequence

1. Review and merge the focused host-compatibility PR only after CI passes.
2. Deploy a revision with `AllowedHosts` containing both `dhandhamitra.in` and the exact current Cloud Run service hostname. Keep `Auth__Passkeys__ServerDomain` on its existing value for this first revision so existing `.run.app` passkeys continue to work while DNS is not yet cut over.
3. In GoDaddy DNS, replace only the two parked apex A records with Google's four A records and add the four AAAA records above. Keep all TXT/MX/DMARC records.
4. Wait until public A/AAAA answers match Google's records and Cloud Run reports `CertificateProvisioned=True` / `Ready=True`.
5. Update `Auth__Passkeys__ServerDomain=dhandhamitra.in` in a separate Cloud Run revision. Keep both exact hostnames in `AllowedHosts`. Passkeys are now available only on `dhandhamitra.in`; password/OTP fallback remains on the old `.run.app` hostname.
6. Verify HTTPS, password login, session persistence, CSRF-protected requests, passkey registration/sign-in on the custom domain, and health checks. Re-register passkeys on the custom domain.
7. Configure Resend DNS records and sender settings as a separate email-delivery step. Domain mapping does not automatically configure email.

## Exact commands (read-only unless stated)

Read the current Cloud Run hostname:

```bash
gcloud run services describe rbos-pilot \
  --region=asia-southeast1 \
  --format='value(status.url)'
```

Inspect mapping/certificate:

```bash
gcloud beta run domain-mappings describe \
  --domain=dhandhamitra.in \
  --region=asia-southeast1 \
  --format=yaml
```

Inspect public DNS after the owner makes the DNS change:

```bash
dig @8.8.8.8 +short A dhandhamitra.in
dig @8.8.8.8 +short AAAA dhandhamitra.in
```

## Acceptance criteria

- [ ] Exact custom domain and existing Cloud Run hostname are both explicitly accepted by host filtering.
- [ ] An unrelated Host header is rejected.
- [ ] Passkey RP remains a single configured DNS hostname and is present in the allowlist.
- [ ] Passkey capability is advertised only on the configured RP hostname.
- [ ] Password/OTP login continues to work on the explicitly allowed Cloud Run fallback hostname.
- [ ] DNS apex A/AAAA records match the Cloud Run mapping requirements.
- [ ] Cloud Run managed certificate is provisioned and mapping is Ready.
- [ ] Custom-domain HTTPS, login, session, CSRF and passkey flows pass.
- [ ] Existing passkeys are re-registered under the new RP domain.
- [ ] Email sender DNS/configuration is verified separately.

## Delivery boundary

This PR changes application host/passkey validation and documentation only. It does not modify DNS, Cloud Run environment variables, Secret Manager, domain mappings, email provider configuration, production data, merge state or deployment state.
