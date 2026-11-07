# Project guardrails (permanent)

These rules apply to every conversation, slice, tool, script and contributor on Retail Business OS. They override any skill, prompt or convenience. When in doubt, stop and ask the product owner.

## 1. Nature of the project

- Personal learning project; not a commercial SaaS commitment.
- Priorities in order: learning sound engineering → solving real small-retail problems → correct accounting and inventory → strong tenant isolation → simple workflows → free/local-first → complete vertical slices over feature count.
- The product owner holds final authority over product, architecture, scope, dependency, cost, security, accounting, tax and implementation decisions.

## 2. Company (H&M) separation — non-negotiable

Never use, access, inspect, query, connect to, modify, copy from or depend on any H&M or other company:

- Azure tenant, subscription, service, resource, managed identity or service connection
- GitHub organisation or repository; Azure DevOps organisation or project
- Source code, internal documentation, wiki, SharePoint, OneDrive or Teams content
- Internal API, database, data, telemetry or test environment
- Credential, password, token, secret or certificate
- Private NuGet, npm, container or artifact feed
- Atlassian, SonarQube or other company integration
- Company-connected MCP server, plugin, agent, search source, Context7, Microsoft Learn connector, Playwright MCP or NuGet MCP
- H&M-specific Copilot skills (for example `%USERPROFILE%\.copilot\skills\hm-*`)

Additionally:

- Never log in with a company account.
- Never upload company code or documents to personal tools.
- Never adapt company code or architecture unless it is independently public under a compatible licence.
- Company connectors that appear globally in Visual Studio or Copilot must not be used in this project, even if available.

### Ambiguous ownership rule

If ownership of a tool, connector, account, feed, skill or server is unclear: do not use it → record the concern → continue without it → ask the product owner only if it blocks work.

Recorded concerns (Phase 0): NuGet MCP, Context7, Microsoft Learn connector and Playwright MCP are visible in sessions with unclear account paths. **Not used.**

## 3. Free-first and local-first

- Prefer free and open-source frameworks, libraries, tools and databases.
- No paid subscriptions, paid APIs, paid cloud, mandatory hosted services, paid AI models or token consumption.
- Record licence, purpose, maintenance status and alternatives for every dependency (`quality/dependency-licence-review.md`).
- The product must work fully with all AI disabled.
- Any paid option requires: use case, why free alternatives fall short, free alternatives, verifiable cost estimate, and explicit product-owner approval **before** use.

## 4. Data rules

- Synthetic data by default for documentation, development and tests.
- No real customer, supplier, transaction, GST, financial, inventory or personal data from the father's shop (or anyone else) without separate, explicit, specific authorisation.
- Validation observations may record workflow patterns and non-sensitive terminology only.
- Never commit, log or print secrets. Use placeholders; local secrets in an ignored file or user-secrets store.

## 5. Package-feed isolation

Before any future package restore, the implementation task must:

1. Create a project-level `NuGet.Config` in `projects/retail-business-os/`.
2. Use `<clear />` in `<packageSources>` to drop inherited sources.
3. Add only `https://api.nuget.org/v3/index.json`.
4. Store no credentials.
5. Report the effective sources (`dotnet nuget list source --configfile <project NuGet.Config>`) before restore.
6. For npm, use a project-level `.npmrc` with `registry=https://registry.npmjs.org/` and no auth tokens.

Do not change global NuGet or npm configuration. Do not authenticate to any feed.

## 6. Workspace rules (from `Personal-Lab/.github/copilot-instructions.md`)

- Personal-Lab is **not** a Git repository. Do not run `git init`, add remotes, commit or push.
- Read-only by default. Every file change, package action, database action or container action needs explicit approval for that action.
- Implement one approved vertical slice at a time, then stop for review.
- Bundled skill scripts (e.g. `poka-yoke/scripts/detect_hazards.py`) are not run without explicit approval.
- Do not modify other projects (e.g. Grocery Intelligence) or global Copilot/MCP configuration.

## 7. Product-correctness guardrails

- Never trade accounting, inventory or tenant-isolation correctness for speed.
- AI has no posting authority, and never owns totals, tax, quantities, valuation, journals, allocations, balances, ITC eligibility, filing status or legal classification.
- Never fabricate invoices, GSTIN, HSN, tax amounts or quantities. Never encourage hiding sales or tax evasion.
- Tax and accounting policies are not professionally approved until a qualified CA reviews them. Statutory thresholds and rates are never taken from model memory.
- No claim that a build, test, filing or payment succeeded unless it actually did.
