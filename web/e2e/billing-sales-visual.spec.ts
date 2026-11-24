import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { signUp, unlockApp, setDialogHandler, randomMobile, openAddProduct, chooseCategory, createProductAndVerify, postInvoice, expectInvoiceSaved } from './signup'

const stamp = Date.now().toString(36)
const owner = 'e2e-audit-' + stamp + '@example.test'
const business = 'E2E Audit ' + stamp
const product = 'E2E Audit Cable ' + stamp
const customer = 'E2E Audit Customer ' + stamp
const phone = randomMobile()
const offlineCustomer = 'Audit Offline Customer ' + stamp
const offlinePhone = randomMobile()
const outDir = 'test-results/ui-audit/billing-sales'
const routeViewports = [
  { name: 'mobile-360x800', width: 360, height: 800 },
  { name: 'mobile-390x844', width: 390, height: 844 },
  { name: 'mobile-412x915', width: 412, height: 915 },
  { name: 'tablet-768x1024', width: 768, height: 1024 },
  { name: 'landscape-844x390', width: 844, height: 390 },
]
const stateViewports = [routeViewports[0]!, routeViewports[1]!, routeViewports[2]!, routeViewports[3]!]
const desktopViewport = { name: 'desktop-1280x900', width: 1280, height: 900 }
type RecordRow = {
  state: string; viewport: string; screenshot: string; overflowX: boolean
  unnamed: string[]; undersized: string[]
  axe: { id: string; impact: string | null; nodes: number; sample: string }[]
}
const records: RecordRow[] = []
const pageErrors: string[] = []
const dialogs: { type: string; message: string; dismissed: boolean }[] = []

async function capture(page: Page, state: string, viewports = stateViewports, fullPage = true) {
  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height })
    await page.waitForTimeout(60)
    const filename = vp.name + '-' + state + '.png'
    await page.screenshot({ path: outDir + '/' + filename, fullPage, animations: 'disabled' })
    const metrics = await page.evaluate(() => {
      const visible = (el: Element) => {
        const r = el.getBoundingClientRect(), s = getComputedStyle(el)
        return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden'
      }
      const controls = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea')].filter(visible)
      const unnamed = controls.filter(el => {
        const h = el as HTMLElement & { labels?: NodeListOf<HTMLLabelElement> }
        return !(el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || h.labels?.length ||
          (el.textContent || '').trim() || el.getAttribute('title'))
      }).map(el => el.outerHTML.slice(0, 120))
      const undersized = controls.filter(el => {
        const r = (el.closest('label') ?? el).getBoundingClientRect()
        return r.width < 44 || r.height < 44
      }).map(el => {
        const r = (el.closest('label') ?? el).getBoundingClientRect()
        return el.tagName.toLowerCase() + ' ' + Math.round(r.width) + 'x' + Math.round(r.height)
      })
      return { overflowX: document.documentElement.scrollWidth > window.innerWidth, unnamed, undersized }
    })
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
    records.push({
      state, viewport: vp.name, screenshot: filename, ...metrics,
      axe: axe.violations.map(v => ({
        id: v.id, impact: v.impact ?? null, nodes: v.nodes.length,
        sample: v.nodes[0]?.html.slice(0, 120) ?? '',
      })),
    })
    writeFileSync(outDir + '/report.json', JSON.stringify({
      scope: 'billing-sales', screenshotCount: records.length,
      capturedStateCount: new Set(records.map(r => r.state)).size, pageErrors, dialogs, captures: records,
    }, null, 2))
  }
}

async function openSellRoute(page: Page, route: string) {
  // Start from the live Sell landing page and use the app's own navigation links.
  // Direct hash navigation from the posted-invoice state can be blocked by the
  // unsaved-work confirmation and leave the previous screen mounted.
  const mainSell = page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Sell' })
  if (route === 'sell/menu') {
    if (await page.locator('.areas .area').count() !== 4) await mainSell.click()
  } else {
    if (await page.locator('.areas .area').count() !== 4) {
      const back = page.getByRole('link', { name: /Billing \/ Sales/ })
      if (await back.isVisible().catch(() => false)) await back.click()
      else await mainSell.click()
    }
    const index: Record<string, number> = { 'sell/bill': 0, 'sell/bills': 1, 'sell/return': 2, 'sell/collect': 3 }
    await page.locator('.areas .area').nth(index[route]!).click()
  }
  await ready(page, route)
}

async function ready(page: Page, route: string) {
  if (route === 'sell/menu') {
    await expect(page.locator('.areas .area')).toHaveCount(4)
    await expect(page.locator('.areas .area').first()).toBeVisible()
  }
  if (route === 'sell/bill') await expect(page.getByRole('region', { name: 'Generate invoice' })).toBeVisible()
  if (route === 'sell/bills') await expect(page.getByRole('table', { name: 'Sales history' }).locator('tbody tr').first()).toBeVisible()
  if (route === 'sell/collect') await expect(page.getByLabel('Collect from').locator('option', { hasText: customer })).toHaveCount(1)
  if (route === 'sell/return') await expect(page.getByLabel('Return against sale').locator('option', { hasText: customer })).toHaveCount(1)
}

test('Billing & Sales: exhaustive route and interaction-state visual audit', async ({ page, context, browser }) => {
  test.setTimeout(15 * 60 * 1000)
  mkdirSync(outDir, { recursive: true })
  writeFileSync(outDir + '/report.json', JSON.stringify({ scope: 'billing-sales', status: 'IN_PROGRESS', captures: [] }, null, 2))
  page.on('pageerror', e => pageErrors.push(e.message))

  // signUp installs its own navigation-confirm handler. Replace it after signup so only one
  // listener handles each dialog during the rest of this audit.
  await signUp(page, { email: owner, businessName: business })
  setDialogHandler(page, async d => {
    dialogs.push({ type: d.type(), message: d.message(), dismissed: false })
    await d.accept()
  })
  await page.goto('/#/stock/products')
  const form = await openAddProduct(page)
  await form.getByLabel('Name', { exact: true }).fill(product)
  await chooseCategory(form, 'Cables')
  await form.getByLabel('Generic / unbranded').setChecked(true)
  await createProductAndVerify(page, form, product)
  await page.goto('/#/stock/opening')
  const opening = page.getByRole('region', { name: 'Opening stock' })
  const select = opening.getByLabel('Product')
  await select.selectOption(await select.locator('option', { hasText: product }).getAttribute('value')!)
  await opening.getByLabel('Quantity').fill('20')
  await opening.getByLabel('Cost for one item (₹)').fill('100')
  await opening.getByRole('button', { name: 'Add item' }).click()
  await opening.getByRole('button', { name: 'Review starting stock' }).click()
  await opening.getByRole('button', { name: 'Confirm & post opening stock' }).click()
  await expect(opening.getByRole('status')).toContainText('2000.00 INR')
  await postInvoice(page, { product, qty: '3', price: '100', mode: 'Credit', paid: '100', name: customer, phone })
  await expectInvoiceSaved(page)

  // More than ten rows makes the real Sales History pagination visible.
  for (let i = 1; i <= 10; i++) {
    await postInvoice(page, { product, qty: '1', price: '10', name: i === 1 ? offlineCustomer : 'Audit Customer ' + i + ' ' + stamp,
      phone: i === 1 ? offlinePhone : randomMobile() })
    await expectInvoiceSaved(page)
  }

  const routes = ['sell/menu', 'sell/bill', 'sell/bills', 'sell/collect', 'sell/return']
  // Capture desktop in a true non-mobile browser context; changing viewport width in the mobile project
  // does not turn off isMobile/hasTouch and must not be labelled as desktop evidence.
  const desktopContext = await browser.newContext({
    baseURL: 'http://localhost:5173',
    storageState: await context.storageState(),
    viewport: { width: desktopViewport.width, height: desktopViewport.height },
  })
  const desktopPage = await desktopContext.newPage()
  desktopPage.on('pageerror', e => pageErrors.push('desktop: ' + e.message))
  desktopPage.on('dialog', async d => { dialogs.push({ type: d.type(), message: d.message(), dismissed: false }); await d.accept() })
  // Load the app shell before routing. Use real in-app links so the route guard and
  // confirmation behaviour are exercised deterministically rather than bypassed by goto().
  await desktopPage.goto('/')
  await unlockApp(desktopPage)
  await expect(desktopPage.getByRole('navigation', { name: 'Main' })).toBeVisible()
  for (const route of routes) {
    await openSellRoute(desktopPage, route)
    await capture(desktopPage, 'route-' + route.replace('/', '-'), [desktopViewport])
  }
  await desktopContext.close()

  await page.goto('/')
  await unlockApp(page)
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
  for (const route of routes) {
    await openSellRoute(page, route)
    await capture(page, 'route-' + route.replace('/', '-'), routeViewports)
  }

  // New bill: default, camera/gallery choices, validation, lookups and extraction states.
  await page.goto('/#/sell/bill')
  const bill = page.getByRole('region', { name: 'Generate invoice' })
  await expect(bill).toBeVisible()
  await capture(page, 'bill-empty-form', routeViewports)

  await bill.getByRole('button', { name: 'Add tag photo' }).click()
  const tagPicker = page.getByRole('dialog', { name: 'Add product tag photo' })
  await expect(tagPicker).toBeVisible()
  await capture(page, 'bill-tag-source-picker')
  await tagPicker.getByRole('button', { name: 'Cancel' }).click()

  await bill.getByRole('button', { name: 'Upload handwritten bill' }).click()
  const invoicePicker = page.getByRole('dialog', { name: 'Add handwritten invoice' })
  await expect(invoicePicker).toBeVisible()
  await capture(page, 'bill-invoice-source-picker')
  await invoicePicker.getByRole('button', { name: 'Cancel' }).click()

  await bill.locator('summary').filter({ hasText: 'More tag details' }).click()
  await capture(page, 'bill-advanced-tag-details')
  await bill.locator('summary').filter({ hasText: 'More tag details' }).click()
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await expect(bill.getByText('Customer name is required.')).toBeVisible()
  await expect(bill.getByText('Enter a valid mobile number (10–15 digits).')).toBeVisible()
  await capture(page, 'bill-customer-validation')

  await bill.locator('#billing-phone').fill(phone.slice(-4))
  const customerOptions = page.getByRole('listbox', { name: 'Matching customers' })
  await expect(customerOptions.getByRole('option').first()).toBeVisible()
  await capture(page, 'bill-customer-suggestions')
  await customerOptions.getByRole('option').first().click()
  await expect(bill.locator('#billing-name')).toHaveValue(customer)

  await bill.locator('#billing-prod').fill(product.slice(0, 8))
  const productOptions = page.getByRole('listbox', { name: 'Products' })
  await expect(productOptions.getByRole('option').first()).toBeVisible()
  await capture(page, 'bill-product-suggestions')
  await productOptions.getByRole('option').first().click()

  // Mock the extraction endpoints: visual CI must never depend on a paid AI provider.
  await page.route('**/api/sales/item-tag-scan', async route => {
    await new Promise(resolve => setTimeout(resolve, 400))
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      status: 'ReadyForReview',
      extraction: { productName: 'LG Washing Machine', brand: 'LG', model: 'WM-AUDIT-900',
        serial: 'AUDIT-SERIAL-9001', barcode: '8901234567890', barcodeSymbology: 'EAN_13',
        imei1: null, imei2: null, mrp: 18000, hsn: '8450' },
      suggestedProduct: null, warnings: ['Synthetic audit extraction: verify the serial against the tag.'],
    }) })
  })
  await bill.getByRole('button', { name: 'Add tag photo' }).click()
  const tagFile = page.waitForEvent('filechooser')
  await page.getByRole('dialog', { name: 'Add product tag photo' }).getByRole('button', { name: 'Take a photo' }).click()
  await (await tagFile).setFiles({ name: 'synthetic-tag.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAoHBwgHBgoICAgLCgoLDhgQDg0NDh0VFhEYIx8lJCIfIiEmKzcvJik0KSEiMEExNDk7Pj4+JS5ESUM8SDc9Pjv/2wBDAQoLCw4NDhwQEBw7KCIoOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozv/wAARCAAgACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD2aiiigAooooAKKKKACiiigD//2Q==', 'base64') })
  await expect(page.getByText('Reading image and extracting product details…')).toBeVisible()
  await capture(page, 'bill-tag-extraction-loading', [routeViewports[1]!])
  await expect(page.getByText(/Product details detected/)).toBeVisible()
  await expect(bill.locator('#billing-model')).toHaveValue('WM-AUDIT-900')
  await expect(page.getByText(/Synthetic audit extraction/)).toBeVisible()
  await capture(page, 'bill-tag-extraction-result')
  await bill.locator('#billing-price').fill('15000')
  await bill.getByRole('button', { name: 'Add product', exact: true }).click()
  await page.unroute('**/api/sales/item-tag-scan')

  await page.route('**/api/sales/invoice-image-extract', async route => {
    await new Promise(resolve => setTimeout(resolve, 400))
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      status: 'NeedsAttention',
      extraction: { invoiceNumber: 'SUPPLIER-AUDIT-42', date: '2026-11-24', customerName: customer,
        customerPhone: phone, subtotal: 1000, taxAmount: 180, total: 1180,
        lines: [{ description: 'Audit refrigerator', brand: 'Whirlpool', model: 'REF-AUDIT-42',
          hsn: '8418', quantity: 1, unitPrice: 1000, discount: 0, amount: 1000,
          gstRate: 18, gstAmount: 180, confidence: 0.92 }] },
      warnings: ['Synthetic audit extraction: confirm the handwritten total.'],
    }) })
  })
  await bill.getByRole('button', { name: 'Upload handwritten bill' }).click()
  const invoiceFile = page.waitForEvent('filechooser')
  await page.getByRole('dialog', { name: 'Add handwritten invoice' }).getByRole('button', { name: 'Choose from device' }).click()
  await (await invoiceFile).setFiles({ name: 'synthetic-invoice.pdf', mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4\\n% synthetic audit document\\n%%EOF') })
  await expect(page.getByText('Reading handwritten invoice…')).toBeVisible()
  await capture(page, 'bill-invoice-extraction-loading', [routeViewports[1]!])
  const extracted = page.getByRole('region', { name: 'Review extracted sales invoice' })
  await expect(extracted).toBeVisible()
  await capture(page, 'bill-invoice-extraction-review')
  await extracted.getByRole('button', { name: 'Add reviewed lines to bill' }).click()
  await expect(bill.getByText('Audit refrigerator')).toBeVisible()
  await page.unroute('**/api/sales/invoice-image-extract')

  await bill.locator('#billing-mode').selectOption('Credit')
  await bill.locator('#billing-paid').fill('100')
  await bill.locator('#billing-paid-method').selectOption('UPI')
  await bill.locator('#billing-paid2').fill('50')
  await bill.locator('#billing-paid2-method').selectOption('Bank')
  await bill.getByLabel('GST invoice').check()
  await bill.getByLabel('GST rate').selectOption('18')
  await bill.locator('#billing-bill-disc').fill('10')
  // Use a distinct customer for this second bill so the seeded credit sale remains a unique return target.
  await bill.locator('#billing-phone').fill(randomMobile())
  await bill.locator('#billing-name').fill('Audit Review Customer ' + stamp)
  await capture(page, 'bill-cart-gst-credit-split-payment')
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await capture(page, 'bill-form-bottom-actions', [routeViewports[1]!], false)
  await page.evaluate(() => window.scrollTo(0, 0))
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await expect(page.getByRole('region', { name: 'Invoice overview' })).toBeVisible()
  await capture(page, 'bill-review')
  await page.getByRole('button', { name: 'Back to edit' }).click()
  await capture(page, 'bill-review-back-to-edit', [routeViewports[1]!, routeViewports[3]!])
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await page.getByRole('button', { name: 'Confirm & print' }).click()
  await expect(page.getByText(/^Saved as INV-/)).toBeVisible({ timeout: 30_000 })
  await capture(page, 'bill-posted')
  await page.getByLabel('Paper size').selectOption('thermal')
  await capture(page, 'bill-posted-thermal')
  await page.emulateMedia({ media: 'print' })
  await capture(page, 'bill-print-media', [routeViewports[1]!])
  await page.emulateMedia({ media: 'screen' })

  // Held bill and offline pending states; pending is explicitly not a posted invoice.
  await page.getByRole('button', { name: 'New invoice' }).click()
  await bill.locator('#billing-phone').fill(offlinePhone)
  await bill.locator('#billing-name').fill(offlineCustomer)
  await bill.locator('#billing-prod').fill('Manual held item')
  await bill.locator('#billing-qty').fill('1')
  await bill.locator('#billing-price').fill('99')
  await bill.getByRole('button', { name: 'Add product', exact: true }).click()
  await bill.getByRole('button', { name: /Hold bill/i }).click()
  await expect(bill.getByText('Held bills')).toBeVisible()
  await capture(page, 'bill-held-list')
  await bill.getByRole('button', { name: 'Resume' }).click()
  // Resume restores saved lines into the invoice cart; the product input is intentionally cleared for the next item.
  // Assert the persisted line and amount before continuing into the offline-held-bill journey.
  await expect(bill.locator('#billing-prod')).toHaveValue('')
  const resumedLine = bill.locator('.billing-table tbody tr').filter({ hasText: 'Manual held item' })
  await expect(resumedLine).toBeVisible()
  await expect(resumedLine).toContainText('99.00')
  await capture(page, 'bill-held-resumed')
  await bill.locator('#billing-phone').fill(offlinePhone.slice(-4))
  const offlineOptions = page.getByRole('listbox', { name: 'Matching customers' })
  await expect(offlineOptions.getByRole('option').first()).toBeVisible()
  await offlineOptions.getByRole('option').first().click()
  await context.setOffline(true)
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await page.getByRole('button', { name: 'Confirm & print' }).click()
  const pending = page.getByRole('region', { name: 'Pending offline invoice' })
  await expect(pending.getByText(/Pending sync/)).toBeVisible()
  await capture(page, 'bill-offline-pending')
  await pending.getByRole('button', { name: 'Back to billing' }).click()
  await expect(bill.getByRole('region', { name: 'Offline bill queue' })).toBeVisible()
  await capture(page, 'bill-offline-queue')
  await context.setOffline(false)

  // Sales History: loaded rows, pagination, search, unpaid filter, detail, thermal and empty state.
  await page.goto('/#/sell/bills')
  const table = page.getByRole('table', { name: 'Sales history' })
  await expect(table.locator('tbody tr').first()).toBeVisible()
  const pager = page.getByRole('navigation', { name: 'Pagination' })
  await expect(pager).toBeVisible()
  await capture(page, 'history-first-page')
  await pager.getByRole('button', { name: 'Next' }).click()
  await capture(page, 'history-second-page')
  await pager.getByRole('button', { name: 'Previous' }).click()
  const search = page.getByLabel('Search sales')
  await search.fill(customer)
  await expect(table.locator('tbody tr')).toHaveCount(1)
  await page.getByLabel(/Unpaid only/).check()
  await capture(page, 'history-search-unpaid')
  await table.getByRole('button', { name: 'View' }).click()
  const detail = page.getByRole('dialog', { name: 'Sale detail' })
  await expect(detail).toBeVisible()
  await capture(page, 'history-invoice-detail-a4')
  await detail.getByLabel('Paper size').selectOption('thermal')
  await capture(page, 'history-invoice-detail-thermal')
  await page.emulateMedia({ media: 'print' })
  await capture(page, 'history-invoice-detail-print', [routeViewports[1]!])
  await page.emulateMedia({ media: 'screen' })
  await page.keyboard.press('Escape')
  await expect(detail).toBeHidden()
  await search.fill('no-such-audit-customer')
  await expect(table.locator('tbody tr')).toHaveCount(0)
  await capture(page, 'history-empty-search')

  // Collections: customer selected, statement, payment entry, posted confirmation and history.
  await page.goto('/#/sell/collect')
  const collection = page.getByRole('region', { name: 'Collections and returns' })
  const collectSelect = collection.getByLabel('Collect from')
  await collectSelect.selectOption(await collectSelect.locator('option', { hasText: customer }).getAttribute('value')!)
  await expect(collection.getByTestId('collect-owed')).toBeVisible()
  await capture(page, 'collection-customer-selected')
  await collection.getByRole('button', { name: 'Show statement' }).click()
  await expect(page.getByRole('region', { name: 'Customer statement' })).toBeVisible()
  await capture(page, 'collection-statement')
  await collection.getByLabel('Collection amount (INR)').fill('25')
  await collection.getByLabel('Collection method').selectOption('UPI')
  await collection.getByLabel('Collection reference').fill('AUDIT-UPI-REF')
  await capture(page, 'collection-entry')
  await collection.getByRole('button', { name: 'Record payment' }).click()
  await expect(collection.getByRole('status')).toContainText('Payment recorded')
  await capture(page, 'collection-success')
  await expect(collection.getByRole('table', { name: 'Collection history' })).toContainText('25.00')
  await capture(page, 'collection-history')

  // Customer returns: sale selection, partial/full quantities, posting and detail.
  await page.goto('/#/sell/return')
  const returns = page.getByRole('region', { name: 'Collections and returns' })
  const saleSelect = returns.getByLabel('Return against sale')
  await saleSelect.selectOption(await saleSelect.locator('option', { hasText: customer }).first().getAttribute('value')!)
  const returnQty = returns.locator('label').filter({ hasText: 'Return qty:' }).locator('input')
  await expect(returnQty).toBeVisible()
  await returnQty.fill('1')
  await returns.getByLabel('Customer return reason').fill('Synthetic visual audit')
  await returns.getByLabel('Refund method').selectOption('UPI')
  await capture(page, 'return-selected-partial')
  await returns.getByRole('button', { name: 'Return everything remaining' }).click()
  await capture(page, 'return-all-remaining')
  await returnQty.fill('1')
  await returns.getByRole('button', { name: 'Post customer return' }).click()
  await expect(returns.getByRole('status')).toContainText('Return posted')
  await capture(page, 'return-success')
  const returnTable = returns.getByRole('table', { name: 'Customer returns' })
  await expect(returnTable.locator('tbody tr').first()).toBeVisible()
  await returnTable.getByRole('button', { name: 'View return' }).first().click()
  await expect(page.getByRole('region', { name: 'Customer return detail' })).toBeVisible()
  await capture(page, 'return-history-detail')
  await page.getByRole('button', { name: 'Close return' }).click()

  // Browser-native confirm cannot be screenshotted; verify its exact wording and dismiss it.
  let cancelMessage = ''
  setDialogHandler(page, async d => { cancelMessage = d.message(); dialogs.push({ type: d.type(), message: d.message(), dismissed: true }); await d.dismiss() })
  await returns.getByRole('button', { name: 'Cancel entire sale' }).click()
  expect(cancelMessage).toContain('Cancel this whole sale?')

  // Capture representative nested states in a true desktop browser context as well.
  await desktopPage.goto('/#/sell/bill')
  await expect(desktopPage.getByRole('region', { name: 'Generate invoice' })).toBeVisible()
  await capture(desktopPage, 'bill-empty-form', [desktopViewport])
  await desktopPage.getByRole('button', { name: 'Add tag photo' }).click()
  await expect(desktopPage.getByRole('dialog', { name: 'Add product tag photo' })).toBeVisible()
  await capture(desktopPage, 'bill-tag-source-picker', [desktopViewport])
  await desktopPage.getByRole('button', { name: 'Cancel' }).click()
  await desktopPage.goto('/#/sell/bills')
  const desktopSalesTable = desktopPage.getByRole('table', { name: 'Sales history' })
  await expect(desktopSalesTable.locator('tbody tr').first()).toBeVisible()
  await desktopSalesTable.getByRole('button', { name: 'View' }).first().click()
  await expect(desktopPage.getByRole('dialog', { name: 'Sale detail' })).toBeVisible()
  await capture(desktopPage, 'history-invoice-detail', [desktopViewport])
  await desktopPage.keyboard.press('Escape')
  await desktopPage.goto('/#/sell/collect')
  const desktopCollection = desktopPage.getByRole('region', { name: 'Collections and returns' })
  const desktopCollectSelect = desktopCollection.getByLabel('Collect from')
  await desktopCollectSelect.selectOption(await desktopCollectSelect.locator('option', { hasText: customer }).getAttribute('value')!)
  await desktopCollection.getByRole('button', { name: 'Show statement' }).click()
  await expect(desktopPage.getByRole('region', { name: 'Customer statement' })).toBeVisible()
  await capture(desktopPage, 'collection-statement', [desktopViewport])
  await desktopPage.goto('/#/sell/return')
  const desktopReturns = desktopPage.getByRole('region', { name: 'Collections and returns' })
  const desktopSaleSelect = desktopReturns.getByLabel('Return against sale')
  await desktopSaleSelect.selectOption(await desktopSaleSelect.locator('option', { hasText: customer }).first().getAttribute('value')!)
  await expect(desktopReturns.locator('label').filter({ hasText: 'Return qty:' }).locator('input')).toBeVisible()
  await capture(desktopPage, 'return-sale-selected', [desktopViewport])

  // Standalone print route is a separate document surface outside the app shell.
  const response = await page.request.get('/api/sales')
  expect(response.ok()).toBeTruthy()
  const sales = await response.json() as { id: string; customer: string }[]
  const sale = sales.find(row => row.customer === customer)
  expect(sale).toBeTruthy()
  await page.goto('/#/print/sale/' + sale!.id)
  await expect(page.getByRole('main', { name: 'Printable document' })).toBeVisible()
  await expect(page.getByRole('heading', { name: /Sale invoice/ })).toBeVisible()
  await capture(page, 'standalone-sale-print')
  await page.emulateMedia({ media: 'print' })
  await capture(page, 'standalone-sale-print-media', [routeViewports[1]!])
  await page.emulateMedia({ media: 'screen' })
  await desktopPage.goto('/#/print/sale/' + sale!.id)
  await expect(desktopPage.getByRole('main', { name: 'Printable document' })).toBeVisible()
  await capture(desktopPage, 'standalone-sale-print', [desktopViewport])
  await desktopPage.emulateMedia({ media: 'print' })
  await capture(desktopPage, 'standalone-sale-print-media', [desktopViewport])
  await desktopContext.close()

  writeFileSync(outDir + '/report.json', JSON.stringify({
    scope: 'billing-sales', generatedAt: new Date().toISOString(), expectedRoutes: routes,
    routeViewports: routeViewports.map(v => ({ name: v.name, width: v.width, height: v.height })),
    stateViewports: stateViewports.map(v => ({ name: v.name, width: v.width, height: v.height })),
    capturedStateCount: new Set(records.map(r => r.state)).size, screenshotCount: records.length,
    pageErrors, dialogs, captures: records,
  }, null, 2))

  const overflow = records.filter(r => r.overflowX)
  const unnamed = records.flatMap(r => r.unnamed.map(x => r.state + '/' + r.viewport + ': ' + x))
  const undersized = records.flatMap(r => r.undersized.map(x => r.state + '/' + r.viewport + ': ' + x))
  const axe = records.flatMap(r => r.axe.map(x => r.state + '/' + r.viewport + ': ' + x.id + ' (' + (x.impact ?? 'unknown') + ')'))
  expect(overflow, 'No captured state may have horizontal overflow').toEqual([])
  expect(unnamed, 'Every visible interactive control must have an accessible name').toEqual([])
  expect(undersized, 'Visible controls must meet the 44px audit hit-area threshold').toEqual([])
  expect(axe, 'Axe WCAG 2 A/AA violations must fail the audit rather than remain report-only').toEqual([])
  expect(pageErrors, 'No uncaught browser errors should occur in audited journeys').toEqual([])
})
