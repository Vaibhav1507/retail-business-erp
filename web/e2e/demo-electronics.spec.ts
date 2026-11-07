import { signUp, PASSWORD, randomMobile } from './signup'
import { expect, test, type Page } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// Demo Electronics end-to-end: supplier invoices (real Gemini reads) -> suppliers/products created from the draft ->
// stock -> saved invoice images -> customer sale -> bill. Runs against the TEST database with a fresh synthetic business.
const run = Date.now().toString(36)
const password = 'Synthetic#Pass123'
const dir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'Test Data Invoice')
const f = (...n: string[]) => n.map(x => path.join(dir, x))

type Case = { label: string; files: string[]; supplier: RegExp; invoice: RegExp; total: number | null; lines: number }
const invoices: Case[] = [
  { label: 'Maa Vaishno 196', files: f('Media (1).jpg'), supplier: /vaishno/i, invoice: /196/, total: 73600, lines: 2 },
  { label: 'Friends Electronics FE-8050 (page 1 only)', files: f('Media (2).jpg'), supplier: /friends electronics/i, invoice: /8050/, total: null, lines: 9 },
  { label: 'Friends Electronics FE-5884', files: f('Media (4).jpg'), supplier: /friends electronics/i, invoice: /5884/, total: 103400, lines: 4 },
  { label: 'Friends Marketing FM-9332', files: f('Media (6).jpg'), supplier: /friends marketing/i, invoice: /9332/, total: 66000, lines: 2 },
  { label: 'Friends Enterprises ENT-5507 (page 1 only)', files: f('Media (7).jpg'), supplier: /friends enterprises/i, invoice: /5507/, total: null, lines: 6 },
  { label: 'Friends Electronics FE-9263', files: f('Media (8).jpg'), supplier: /friends electronics/i, invoice: /9263/, total: 73890, lines: 4 },
  { label: 'Sonal Electronics 436', files: f('Media.jpg'), supplier: /sonal/i, invoice: /436/, total: 53475, lines: 3 },
  { label: 'Friends Electronics FE-5132 (2 pages as one)', files: f('Media (5).jpg', 'Media (3).jpg'), supplier: /friends electronics/i, invoice: /5132/, total: 178600, lines: 10 },
]

type StockRow = Record<string, unknown>
const getJson = (page: Page, url: string) => page.evaluate(async u => (await fetch(u, { credentials: 'same-origin' })).json(), url)
const qtyOf = (r: StockRow) => Number(r.quantity ?? r.onHand ?? r.qty ?? 0)
const nameOf = (r: StockRow) => String(r.product ?? r.productName ?? r.name ?? '')

let page: Page
test.describe.configure({ mode: 'serial' })
test.setTimeout(420_000)

test.beforeAll(async ({ browser }) => {
  page = await (await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })).newPage()
  await page.addInitScript(() => { window.print = () => undefined })
  await signUp(page, { email: `e2e-demo-${run}@example.test`, businessName: 'Demo Electronics ' + run, city: 'Kharagpur', state: 'West Bengal', pin: '721301' })
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
})

test('1. upload all 8 supplier invoices, create suppliers and products from the draft, post stock', async () => {
  const bought: Record<string, number> = {}
  for (const inv of invoices) {
    await page.goto('/#/buy/receive')
    const up = page.getByRole('region', { name: 'Read invoice' })
    await up.locator('input[type=file]').setInputFiles(inv.files)
    const meta = up.locator('.invoice-upload-meta')
    await expect(meta, inv.label).toBeVisible({ timeout: 90_000 })
    await expect.soft(meta, inv.label + ' supplier').toContainText(inv.supplier)
    await expect.soft(meta, inv.label + ' invoice no').toContainText(inv.invoice)
    await expect.soft(meta, inv.label + ' date year').toContainText('2026')
    if (inv.total != null) await expect.soft(meta, inv.label + ' total').toContainText(`Total ${inv.total.toFixed(2)}`)
    await expect.soft(up.locator('tbody tr'), inv.label + ' lines').toHaveCount(inv.lines)

    // Supplier: new ones are created from the invoice (no manual supplier step); known ones must already be matched.
    const sup = up.getByLabel('Supplier for this invoice')
    if (!(await sup.inputValue())) await sup.selectOption('__new__')
    await expect(sup).not.toHaveValue('')
    await expect(sup).not.toHaveValue('__new__')

    // Products: any line not matched to the catalogue is created from the invoice.
    const addAll = up.getByRole('button', { name: /Add \d+ unmatched as new products/ })
    if (await addAll.isVisible()) await addAll.click()
    const use = up.getByRole('button', { name: /^Use \d+ lines?$/ })
    await expect(use).toBeEnabled({ timeout: 30_000 })
    await use.click()
    await page.getByRole('button', { name: 'Post receipt' }).click()
    await expect(page.getByRole('status').filter({ hasText: 'Receipt posted' })).toBeVisible({ timeout: 30_000 })
    bought[inv.label] = inv.lines
    await page.waitForTimeout(3000)
  }
  expect(Object.keys(bought)).toHaveLength(8)
})

test('2. suppliers, products, stock and purchase history reflect the uploads', async () => {
  const suppliers = (await getJson(page, '/api/suppliers')) as { name: string }[]
  const names = suppliers.map(s => s.name.toLowerCase())
  for (const k of ['vaishno', 'friends electronics', 'friends marketing', 'friends enterprises', 'sonal']) expect(names.some(n => n.includes(k)), 'supplier ' + k).toBe(true)
  expect(names.filter(n => n.includes('friends electronics')), 'Friends Electronics must be one supplier, not duplicated').toHaveLength(1)

  const purchases = (await getJson(page, '/api/purchases')) as { supplierRef: string | null; total: number }[]
  expect(purchases).toHaveLength(8)
  const stock = (await getJson(page, '/api/stock')) as StockRow[]
  expect(stock.length).toBeGreaterThan(20)
  expect(stock.every(r => qtyOf(r) > 0)).toBe(true)
  await page.goto('/#/buy/history')
  await expect(page.getByRole('table').first()).toContainText(/5132|5884|9263/)
})

test('3. saved invoice images can be retrieved (single image and multi-page zip)', async () => {
  const purchases = (await getJson(page, '/api/purchases')) as { id: string; supplierRef: string | null }[]
  const one = purchases.find(p => /5884/.test(p.supplierRef ?? ''))!
  const multi = purchases.find(p => /5132/.test(p.supplierRef ?? ''))!
  const img = await page.request.get(`/api/purchases/${one.id}/invoice`)
  expect(img.status()).toBe(200)
  expect(img.headers()['content-type']).toContain('image/jpeg')
  const zip = await page.request.get(`/api/purchases/${multi.id}/invoice`)
  expect(zip.status()).toBe(200)
  expect(zip.headers()['content-type']).toContain('application/zip')
  const bytes = await zip.body()
  expect(bytes.subarray(0, 2).toString()).toBe('PK')
  const text = bytes.toString('latin1')
  expect(text).toContain('page-1.jpg')
  expect(text).toContain('page-2.jpg')
})

test('4. sell to a new customer and generate the bill', async () => {
  const before = (await getJson(page, '/api/stock')) as StockRow[]
  const item = before.find(r => qtyOf(r) >= 2)!
  const itemName = nameOf(item)
  const startQty = qtyOf(item)
  await page.goto('/#/sell/bill')
  const bill = page.getByRole('region', { name: 'Generate invoice' })
  await bill.getByLabel('Mobile number').fill('9000000001')
  await bill.getByLabel('Customer name').fill('E2E Customer ' + run)
  await bill.getByLabel('Address').fill('12 Market Road')
  await bill.getByLabel('Pincode').fill('721301')
  await bill.getByLabel('City').fill('Kharagpur')
  await bill.getByLabel('State').fill('West Bengal')
  await bill.getByLabel('Product', { exact: true }).fill(itemName)
  await bill.getByLabel('Qty').fill('2')
  await bill.getByLabel('Price (INR)').fill('500')
  await bill.getByRole('button', { name: 'Add product' }).click()
  await expect(bill.getByText(/Total\s*1,?000\.00/)).toBeVisible()
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await page.getByRole('button', { name: 'Confirm & print' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'Saved as' })).toBeVisible({ timeout: 20_000 })
  const after = (await getJson(page, '/api/stock')) as StockRow[]
  expect(qtyOf(after.find(r => nameOf(r) === itemName)!)).toBe(startQty - 2)
  const customers = (await getJson(page, '/api/customers')) as { name: string }[]
  expect(customers.some(c => c.name.includes('E2E Customer'))).toBe(true)
  await page.goto('/#/sell/bills')
  await expect(page.getByRole('table', { name: 'Sales history' })).toContainText('1000.00')
})

test('5. edge cases: oversell, bad files, too many pages, unreadable invoice', async () => {
  const before = (await getJson(page, '/api/stock')) as StockRow[]
  const itemName = nameOf(before[0])
  await page.goto('/#/sell/bill')
  const bill = page.getByRole('region', { name: 'Generate invoice' })
  await bill.getByLabel('Mobile number').fill('9000000002')
  await bill.getByLabel('Customer name').fill('E2E Oversell ' + run)
  await bill.getByLabel('Product', { exact: true }).fill(itemName)
  await bill.getByLabel('Qty').fill('99999')
  await bill.getByLabel('Price (INR)').fill('10')
  await bill.getByRole('button', { name: 'Add product' }).click()
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await page.getByRole('button', { name: 'Confirm & print' }).click()
  await expect(page.getByRole('alert')).toContainText(/stock/i)

  await page.goto('/#/buy/receive')
  const up = page.getByRole('region', { name: 'Read invoice' })
  const input = up.locator('input[type=file]')
  await input.setInputFiles({ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('not an invoice') })
  await expect(up.getByRole('alert')).toContainText(/photo or a PDF/i)
  await input.setInputFiles(Array.from({ length: 7 }, (_, i) => ({ name: `p${i}.jpg`, mimeType: 'image/jpeg', buffer: Buffer.from([0xff, 0xd8, 0xff]) })))
  await expect(up.getByRole('alert')).toContainText(/at most 6/i)

  await page.route('**/api/purchases/extract', r => r.fulfill({ status: 400, contentType: 'application/json', body: '{"error":"Could not read this invoice. Try a clearer photo or enter it by hand."}' }))
  await input.setInputFiles(f('Media (1).jpg'))
  await expect(up.getByRole('alert')).toContainText(/clearer photo/i)
  await page.unroute('**/api/purchases/extract')
})
