import { signUp, PASSWORD, randomMobile, openAddProduct, chooseCategory, createProductAndVerify } from './signup'
import { expect, test, type APIRequestContext, type Page } from '@playwright/test'

// Synthetic data only. Unique suffix per run so the test is repeatable against the same database.
const run = Date.now().toString(36)
const ownerA = `e2e-a-${run}@example.test`
const ownerB = `e2e-b-${run}@example.test`
const cableName = `E2E USB-C Cable ${run}`
const genericName = `E2E Generic Adapter ${run}`

async function api(ctx: APIRequestContext, method: 'GET' | 'POST', path: string, body?: unknown, key?: string) {
  const headers: Record<string, string> = {}
  if (method === 'POST') {
    const t = await (await ctx.get('/api/csrf')).json()
    headers['X-CSRF-TOKEN'] = t.token
    headers['Content-Type'] = 'application/json'
    if (key) headers['Idempotency-Key'] = key
  }
  return ctx.fetch(`/api${path}`, { method, headers, data: body === undefined ? undefined : JSON.stringify(body) })
}

async function addProduct(page: Page, name: string, generic: boolean, brand = '', model = '') {
  await page.goto('/#/stock/products')
  const s = await openAddProduct(page)
  await s.getByLabel('Name', { exact: true }).fill(name)
  await chooseCategory(s, 'Cables')
  const box = s.getByLabel('Generic / unbranded')
  if ((await box.isChecked()) !== generic) await box.setChecked(generic)
  if (!generic) { await s.getByLabel('Brand', { exact: true }).fill(brand); await s.getByLabel('Model number', { exact: true }).fill(model) }
  await createProductAndVerify(page, s, name)
}

async function addLine(page: Page, product: string, qty: string, cost: string) {
  await page.goto('/#/stock/opening')
  const s = page.getByRole('region', { name: 'Opening stock' })
  const value = await s.getByLabel('Product').locator('option', { hasText: product }).getAttribute('value')
  await s.getByLabel('Product').selectOption(value!)
  await s.getByLabel('Quantity').fill(qty)
  await s.getByLabel('Cost for one item (₹)').fill(cost)
  await s.getByRole('button', { name: 'Add item' }).click()
}

test('owner sets up business, posts opening stock once, and is isolated from another tenant', async ({ page, browser, baseURL }) => {
  // 1-3. Register + sign in, bootstrap tenant, default branch and stock location
  await signUp(page, { email: ownerA, businessName: `E2E Traders ${run}` })
  await page.goto('/#/stock/products')
  await expect(page.getByRole('region', { name: 'Products' })).toBeVisible()

  // 4-5. Electronics-lite (branded) product and a separate generic product
  await addProduct(page, cableName, false, 'SyntheticBrand', 'SB-100')
  await addProduct(page, genericName, true)

  // 6-7. Costed opening stock with a deterministic, reviewable total: 10 x 25.50 + 3 x 33.3333 = 255.00 + 100.00
  await addLine(page, cableName, '10', '25.5')
  await addLine(page, genericName, '3', '33.3333')
  await expect(page.getByTestId('estimated-total')).toContainText('355.00 INR')

  // 8. Post
  const stockForm = page.getByRole('region', { name: 'Opening stock' })
  await stockForm.getByRole('button', { name: 'Review starting stock' }).click()
  await stockForm.getByRole('button', { name: 'Confirm & post opening stock' }).click()
  await expect(stockForm.getByRole('status')).toContainText('355.00 INR')

  // 9. Current quantity and value
  await page.goto('/#/stock/current')
  const stock = page.getByRole('region', { name: 'Stock on hand' })
  const cableRow = stock.getByRole('row', { name: new RegExp(cableName) })
  await expect(cableRow).toContainText('10')
  await expect(cableRow).toContainText('255.00')
  const genericRow = stock.getByRole('row', { name: new RegExp(genericName) })
  await expect(genericRow).toContainText('100.00')

  // 10. Movement history is not part of the Slice 1 UI (stock-on-hand only); recorded as a known gap in the docs.

  // 11-12. Repeat the posting: must be blocked, and nothing changes
  await addLine(page, cableName, '1', '1')
  await stockForm.getByRole('button', { name: 'Review starting stock' }).click()
  await stockForm.getByRole('button', { name: 'Confirm & post opening stock' }).click()
  await expect(stockForm.getByRole('alert')).toContainText('already posted')
  await page.goto('/#/stock/current')
  await expect(cableRow).toContainText('255.00')

  // 13. Tenant B (separate browser context, no shared cookies) cannot reach Tenant A's resources
  const aCtx = page.context().request
  const products = await (await api(aCtx, 'GET', '/products')).json()
  const aProduct = products.find((p: { name: string }) => p.name === cableName).id as string
  const aLocation = (await (await api(aCtx, 'GET', '/locations')).json())[0].id as string

  const bContext = await browser.newContext()
  const bPage = await bContext.newPage()
  try {
    // Exercise the same verified mobile signup flow as a real second tenant. The old
    // direct /auth/register call skipped OTP verification and now correctly returns 400.
    await signUp(bPage, { email: ownerB, businessName: `E2E Other ${run}` })
    const b = bPage.context().request

    expect(await (await api(b, 'GET', '/products')).json()).toEqual([])
    expect(await (await api(b, 'GET', '/stock')).json()).toEqual([])

    const attack = await api(b, 'POST', '/opening-stock', {
      locationId: aLocation, effectiveDate: new Date().toISOString().slice(0, 10), lines: [{ productId: aProduct, quantity: 1, unitCost: 1 }],
    }, crypto.randomUUID())
    expect(attack.status()).toBeGreaterThanOrEqual(400)
    expect(attack.status()).toBeLessThan(500)
    expect(JSON.stringify(await attack.json())).not.toContain(cableName)
  } finally {
    await bContext.close()
  }

  // Tenant A is unchanged after the attempted cross-tenant write
  await page.goto('/#/stock/current')
  await page.reload()
  await expect(page.getByRole('region', { name: 'Stock on hand' }).getByRole('row', { name: new RegExp(cableName) })).toContainText('255.00')
})

