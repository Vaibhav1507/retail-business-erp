# Slice 1 dependencies and licences

Sources: public nuget.org and registry.npmjs.org only (project `NuGet.Config` and `.npmrc`). No private feeds or company resources.

Licence column is from general knowledge of each project and has **not** been verified against package metadata. Confirm before any distribution.

| Component | Version | Purpose | Expected licence |
|---|---|---|---|
| .NET SDK / ASP.NET Core | 10.0.x | Backend runtime | MIT |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.14 | Users, password hashing, lockout | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.14 | Identity schema SQL generation | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL / Npgsql | 10.0.3 | PostgreSQL access | PostgreSQL licence |
| Microsoft.AspNetCore.OpenApi | 10.0.14 | Template reference, unused | MIT |
| xUnit, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing | see csproj | Tests | Apache-2.0 / MIT |
| PostgreSQL (portable binaries) | 17.11 | Database, run locally | PostgreSQL licence |
| React / React DOM | ^19.3.1 | Frontend | MIT |
| Vite, @vitejs/plugin-react | ^8.3.5 / ^6.1.2 | Frontend build | MIT |
| TypeScript | ~7.0.2 | Frontend types | Apache-2.0 |
| oxlint | ^1.89.0 | Lint | MIT |

## Checks run

- `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.
- `npm audit`: 0 vulnerabilities.
- `npm audit --omit=dev`: 0 vulnerabilities (re-run in Slice 1.1).

## Licence inventory (Slice 1.1, from locally installed package metadata)

Method: read `license` from each installed npm `package.json` (113 unique name@version, `node_modules` to depth 3) and `license`/`licenseUrl` from each restored NuGet `.nuspec` (79 unique packages from `dotnet list package --include-transitive`). This reads declared metadata only; it is not a legal review and not an SBOM.

| Ecosystem | Licence (declared) | Count |
|---|---|---|
| npm | MIT | 92 |
| npm | Apache-2.0 | 9 |
| npm | ISC / BSD-2-Clause / BSD-3-Clause / BlueOak-1.0.0 | 2 / 2 / 2 / 1 |
| npm | MIT-0 | 2 |
| npm | CC0-1.0 | 1 (`mdn-data`, data) |
| npm | **MPL-2.0** | 2 (`lightningcss`, `lightningcss-win32-x64-msvc`; transitive Vite build tooling, not shipped in the bundle) |
| NuGet | MIT | 69 |
| NuGet | Apache-2.0 | 7 |
| NuGet | PostgreSQL | 2 (`Npgsql`, `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| NuGet | licenseUrl only | 1 (`xunit.abstractions` 2.0.3, test-only; URL points to the xUnit licence, Apache-2.0 expected, **unverified**) |

Open items: MPL-2.0 is file-level copyleft and acceptable for unmodified build tooling, but must be reviewed before any distribution decision. `xunit.abstractions` licence is declared only by URL. Playwright (Apache-2.0) uses the system Edge browser; no browser binaries were downloaded. Product-owner decision on licences remains pending.
- Not done: SBOM generation; licence text review of non-SPDX entries; native/binary components inside packages.
