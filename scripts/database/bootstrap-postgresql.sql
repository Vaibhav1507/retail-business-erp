-- Retail Business OS: local PostgreSQL bootstrap (personal native PostgreSQL 17 only).
-- Run as the local postgres superuser against database postgres:
--   psql -h localhost -p 5432 -U postgres -d postgres -f bootstrap-postgresql.sql
-- Contains NO passwords; role passwords are set by scripts/local/set-development-secrets.ps1.
-- Idempotent: roles are guarded by pg_roles. CREATE DATABASE cannot run inside a DO block,
-- so databases are created through \gexec only when missing.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rbos_migrator') THEN
	CREATE ROLE rbos_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rbos_app') THEN
	CREATE ROLE rbos_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
  END IF;
END
$$;

ALTER ROLE rbos_migrator NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
ALTER ROLE rbos_app      NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;

SELECT format('CREATE DATABASE %I OWNER rbos_migrator ENCODING ''UTF8'' TEMPLATE template0', d)
FROM (VALUES ('rbos_dev'), ('rbos_test')) AS v(d)
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = v.d)
\gexec

ALTER DATABASE rbos_dev  OWNER TO rbos_migrator;
ALTER DATABASE rbos_test OWNER TO rbos_migrator;

REVOKE ALL ON DATABASE rbos_dev  FROM PUBLIC;
REVOKE ALL ON DATABASE rbos_test FROM PUBLIC;
GRANT CONNECT ON DATABASE rbos_dev  TO rbos_migrator, rbos_app;
GRANT CONNECT ON DATABASE rbos_test TO rbos_migrator, rbos_app;
