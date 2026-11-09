# Pilot deployment runbook (Cloud Run + Neon PostgreSQL)

Single container serves the API and the built React app from one origin.
Pilot settings: OTP off, registration closed after the first owner.
Every step that creates accounts, cloud resources or touches the production database needs explicit approval first.

## 1. Prerequisites
- Personal GitHub, Google Cloud and Neon accounts (not company accounts).
- Verify current free-tier limits and billing alerts on the official pricing pages before relying on them.

## 2. Database (Neon)
1. Create a project and a database.
2. Create two roles:
   - `rbos_app`: runtime role, no DDL rights.
   - a migrator role: used only from a trusted machine.
3. Run migrations with the migrator role (`ConnectionStrings:RbosProdMigrator`):
   `dotnet run --project src/Rbos.Api -- --migrate prod`
   This applies 001 to 037, including `037_data_protection_keys.sql`.
4. Confirm `rbos_app` can select and insert on `identity.data_protection_key`.

## 3. Secrets and settings (Cloud Run)
Set as environment variables or secrets (see `.env.example`). Never commit real values.
- `Rbos__Database=prod`
- `Rbos__AllowOpenRegistration=false`
- `Otp__Enabled=false`
- `ConnectionStrings__RbosProdApp=<rbos_app connection string, SSL Mode=Require>`
Do not set Gemini or SMS secrets unless explicitly approved.
Do not put the migrator connection string in Cloud Run.

## 4. Build and deploy
1. Build the image: `docker build -t rbos .` (the Dockerfile has not been build-tested yet; do this first).
2. Push to Artifact Registry and deploy to Cloud Run with `--min-instances 0` and `--max-instances 1` for the pilot.
3. Allow public access to the service; the app enforces login.

## 5. First owner
1. Open the service URL and choose "Create an account".
2. Register the owner with email and a password of 12 or more characters.
3. After this, registration returns 403 "Registration is closed." for everyone else.
4. Further staff accounts are created from inside the app by the owner (if supported) or by a later approved change.

## 6. Verification checklist
- `/api/health` returns Healthy.
- `/` and a deep link such as `/sales` load the app.
- `/api/auth/config` returns `{"otpEnabled":false}`.
- Login works; refresh keeps the session; a new revision keeps the session (Data Protection keys persist in the database).
- Registering a second user is refused.

## 7. Backups and restore
- Neon's built-in history window is short on the free tier.
- Schedule an independent encrypted `pg_dump` (for example weekly) stored outside Neon.
- Test a restore into a scratch database before real users arrive.

## 8. Rollback
- Redeploy the previous Cloud Run revision.
- Migrations are forward-only; take a dump before applying a new one.

## 9. Known gaps
- Docker image not build-tested on the build machine.
- Unknown `/api/*` paths return the React page instead of 404.
- Split-payment restriction not yet reviewed.
- Playwright suite not re-run after the OTP changes.
