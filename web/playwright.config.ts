import { defineConfig } from '@playwright/test'

const API = 'http://localhost:5082'
const WEB = 'http://localhost:5173'

// Localhost only. Starts the API against the TEST database with open registration enabled for synthetic users,
// and the Vite dev server (which proxies /api to the API). Existing servers are reused.
export default defineConfig({
  testDir: './e2e',
  // CI runs only deterministic, synthetic-data specs. The invoice-extraction/demo specs
  // exercise external AI/document providers and remain manual, opt-in acceptance tests.
  ...(process.env.CI ? { testMatch: ['api-contract.spec.ts', 'collections.spec.ts', 'opening-stock.spec.ts', 'sales.spec.ts', 'ui-audit.spec.ts'] } : {}),
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: { baseURL: WEB, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'mobile-390x844', use: { browserName: 'chromium', viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/Rbos.Api --no-launch-profile --urls http://localhost:5082',
      url: `${API}/api/csrf`,
      reuseExistingServer: true,
      timeout: 120_000,
      env: { ASPNETCORE_ENVIRONMENT: 'Development', Rbos__Database: 'test', Rbos__AllowOpenRegistration: 'true' },
    },
    { command: 'npm run dev -- --host localhost', url: WEB, reuseExistingServer: true, timeout: 60_000 },
  ],
})

