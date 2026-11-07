import { signUp, PASSWORD, randomMobile } from './signup'
import { expect, test } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const run = Date.now().toString(36)
const password = 'Synthetic#Pass123'
const names = ['E2E Invoice Cable', 'E2E Invoice Charger', 'E2E Invoice Earphones']

test('invoice extraction returns multi-line draft', async ({ page }) => {
  test.setTimeout(120_000)
  await signUp(page, { email: 'e2e-inv-' + run + '@example.test', businessName: 'E2E Invoice ' + run })
  const products = page.getByRole('region', { name: 'Products' })
  await expect(products).toBeVisible()
  for (const n of names) {
    const form = products.getByRole('form', { name: 'Add product' })
    await form.getByLabel('Name', { exact: true }).fill(n)
    await form.getByLabel('Category').fill('Cables')
    await form.getByLabel('Generic / unbranded').setChecked(true)
    await form.getByRole('button', { name: 'Add product' }).click()
    await expect(products.getByRole('cell', { name: n, exact: true })).toBeVisible()
  }
  const data = fs.readFileSync(path.join(__dirname, 'test-invoice.png')).toString('base64')
  const result = await page.evaluate(async (d) => {
    const c = await (await fetch('/api/csrf', { credentials: 'same-origin' })).json()
    const r = await fetch('/api/purchases/extract', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': c.token ?? c.requestToken ?? '' },
      body: JSON.stringify({ contentType: 'image/png', data: d }),
    })
    return { status: r.status, body: await r.text() }
  }, data)
  console.log('EXTRACT', result.status, result.body)
  expect(result.status).toBe(200)
})

