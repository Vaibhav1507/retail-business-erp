import { test, expect } from '@playwright/test'
import { signUp } from './signup'

test('D8/A8/A12: OpenAPI document, cash-flow statement and WAC verification', async ({ page }) => {
  const stamp = Date.now()
  await signUp(page, { email: `e2e-contract-${stamp}@example.test`, businessName: `Contract Test ${stamp}` })
  await page.goto('/#/stock/products')
  await expect(page.getByRole('region', { name: 'Products' })).toBeVisible()
  const openapi = await page.request.get('/api/openapi/v1.json')
  expect(openapi.ok()).toBeTruthy()
  const doc = await openapi.json()
  for (const path of ['/api/reports/cash-flow', '/api/reports/wac-verification', '/api/reports/profit-loss', '/api/reports/balance-sheet']) expect(Object.keys(doc.paths)).toContain(path)
  const today = new Date().toISOString().slice(0, 10)
  const cf = await page.request.get(`/api/reports/cash-flow?from=2000-01-01&to=${today}`)
  expect(cf.status(), await cf.text()).toBe(200)
  const cash = await cf.json()
  expect(cash.closingCash).toBeCloseTo(cash.openingCash + cash.netOperating + cash.netFinancing, 2)
  expect((await page.request.get(`/api/reports/cash-flow?from=${today}&to=2000-01-01`)).status()).toBe(400)
  const wac = await page.request.get('/api/reports/wac-verification')
  expect(wac.status(), await wac.text()).toBe(200)
  expect((await wac.json()).ok).toBe(true)
})
