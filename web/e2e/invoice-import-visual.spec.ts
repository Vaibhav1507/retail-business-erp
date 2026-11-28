import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { randomMobile, setDialogHandler, signUp } from './signup'

const stamp = Date.now().toString(36)
const outDir = 'test-results/ui-audit/invoice-import'
const viewports = [
  { name: 'mobile-360x800', width: 360, height: 800 },
  { name: 'mobile-390x844', width: 390, height: 844 },
  { name: 'mobile-412x915', width: 412, height: 915 },
]
const pageErrors: string[] = []
const audit: { state: string; viewport: string; overflowX: boolean; screenshot: string; axeViolations: string[] }[] = []

async function captureState(page: import('@playwright/test').Page, state: string) {
  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    const screenshot = `${viewport.name}-${state}.png`
    await page.screenshot({ path: `${outDir}/${screenshot}`, fullPage: true, animations: 'disabled' })
    const overflowX = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
    audit.push({ state, viewport: viewport.name, overflowX, screenshot, axeViolations: axe.violations.map(v => v.id) })
    expect(overflowX, `Unexpected horizontal overflow in ${state} at ${viewport.width}px`).toBe(false)
    expect(axe.violations.filter(v => v.impact === 'critical').map(v => v.id)).toEqual([])
  }
  writeFileSync(`${outDir}/report.json`, JSON.stringify({ scope: 'invoice-import', pageErrors, captures: audit }, null, 2))
}

test('Create Invoice: handwritten import review and tag feedback fit mobile screens', async ({ page }) => {
  test.setTimeout(5 * 60 * 1000)
  mkdirSync(outDir, { recursive: true })
  page.on('pageerror', error => pageErrors.push(error.message))
  await signUp(page, { email: `invoice-import-${stamp}@example.test`, businessName: `Invoice Import ${stamp}`, mobile: randomMobile() })
  setDialogHandler(page, async dialog => { if (dialog.type() === 'confirm') await dialog.accept(); else await dialog.dismiss() })
  await page.goto('/#/sell/bill')
  const bill = page.getByRole('region', { name: 'Generate invoice', exact: true })
  await expect(bill).toBeVisible()

  const description = bill.locator('#billing-prod')
  const chatLauncher = page.locator('.assistant-launcher')
  await expect(chatLauncher).toBeVisible()
  await expect(description).toHaveCSS('resize', 'none')
  await expect(description).toHaveCSS('height', '76px')
  await expect(description).toHaveCSS('overflow-y', 'hidden')
  await description.fill('Long handwritten-style product description with model and package details\n'.repeat(5))
  await expect.poll(async () => Number((await description.evaluate(element => getComputedStyle(element).height)).replace('px', ''))).toBeGreaterThan(76)
  await description.fill('')
  await expect(description).toHaveCSS('height', '76px')

  const sourceDocumentId = '11111111-2222-4333-8444-555555555555'
  await page.route('**/api/documents/' + sourceDocumentId + '/pages/1', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/pdf',
      body: Buffer.from('%PDF-1.4\\n% synthetic retained handwritten bill\\n%%EOF'),
    })
  })
  await page.route('**/api/documents/' + sourceDocumentId + '/pages/1?preview=true', async route => {
    await route.fulfill({
      status: 200, contentType: 'image/png',
      body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/8X8AAAAASUVORK5CYII=', 'base64'),
    })
  })
  await page.route('**/api/sales/invoice-image-extract/' + sourceDocumentId + '/discard', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ documentId: sourceDocumentId, status: 'Cancelled' }) })
  })
  const invoiceUploadHeaders: Record<string, string | undefined>[] = []
  await page.route('**/api/sales/invoice-image-extract', async route => {
    invoiceUploadHeaders.push(route.request().headers())
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      status: 'NeedsAttention',
      sourceDocumentId,
      extraction: {
        invoiceNumber: 'HAND-468', date: '2026-11-26', customerName: 'Handwritten Customer',
        customerPhone: '9876543210', subtotal: 10000, taxAmount: 1800, total: 11800,
        fieldStates: { invoiceNumber: 'ambiguous', date: 'source_read', customerName: 'source_read', customerPhone: 'source_read', subtotal: 'source_read', taxAmount: 'source_read', total: 'ambiguous' },
        ocrEvidence: {
          invoiceNumber: { status: 'ambiguous', haikuOrPrimaryReading: 'HAND-468', ocrCandidate: 'Bill No. 4006', ocrAgrees: false, pageNumber: 1, region: { left: 0.12, top: 0.1, width: 0.22, height: 0.04 }, verificationCandidate: 'HAND-468', verificationStatus: 'source_read' },
          total: { status: 'ambiguous', haikuOrPrimaryReading: '11800', ocrCandidate: 'TOTAL 11800', ocrAgrees: false, pageNumber: 1, region: { left: 0.75, top: 0.82, width: 0.16, height: 0.05 }, verificationCandidate: '11800', verificationStatus: 'source_read' },
        },
        lines: [{
          description: 'Samsung LED Television model UA43\\nSerial: SN12345678\\nFull handwritten model and package text preserved on this line',
          brand: 'Samsung', model: 'UA43', serialNumbers: [],
          identifierCandidates: [{ value: 'SN12345678', classification: 'serial', confirmed: false }, { value: 'UA43', classification: 'model', confirmed: false }],
          fieldStates: { description: 'source_read', brand: 'source_read', model: 'source_read', hsn: 'source_read', quantity: 'source_read', unitPrice: 'source_read', discount: 'source_read', amount: 'source_read', gstRate: 'source_read', gstAmount: 'source_read' },
          hsn: '8528', quantity: 1, unitPrice: 10000, discount: 0, amount: 10000,
          gstRate: 18, gstAmount: 1800, confidence: 0.92,
        }],
      },
      warnings: ['Synthetic visual audit extraction: verify the handwritten total.'],
    }) })
  })

  async function importBill() {
    await bill.getByRole('button', { name: /Import bill/ }).click()
    const picker = page.getByRole('dialog', { name: 'Add handwritten invoice' })
    await expect(picker).toBeVisible()
    const chooser = page.waitForEvent('filechooser')
    await picker.getByRole('button', { name: 'Choose from device' }).click()
    await (await chooser).setFiles({
      name: 'synthetic-handwritten-bill.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4\n% synthetic visual audit document\n%%EOF'),
    })
  }

  await importBill()
  const review = bill.getByRole('region', { name: 'Review extracted sales invoice', exact: true })
  await expect(review).toBeVisible()
  const extractedDescription = review.getByLabel('Extracted line 1 description')
  await expect(extractedDescription).toHaveValue('Samsung LED Television model UA43\nSerial: SN12345678\nFull handwritten model and package text preserved on this line')
  await expect(extractedDescription).toHaveCSS('resize', 'none')
  await expect(extractedDescription).toHaveCSS('overflow-y', 'hidden')
  await expect.poll(async () => Number((await extractedDescription.evaluate(element => getComputedStyle(element).height)).replace('px', ''))).toBeGreaterThan(48)
  await expect(review.getByLabel('Extracted line 1 serial numbers')).toHaveValue('')
  await expect(review.getByRole('img', { name: 'Original handwritten customer sales bill, page 1' })).toBeVisible()
  await expect(review.getByRole('button', { name: 'Inspect invoice number' })).toBeVisible()
  await expect(review.getByText(/OCR text near the labelled field: “Bill No. 4006”/)).toBeVisible()
  await expect(review.getByText(/Hydrogen’s separate reading: “HAND-468”/)).toBeVisible()
  await expect(review.locator('.invoice-identifier-candidates')).toContainText('SN12345678')
  await expect(review.getByRole('checkbox', { name: 'Confirm this reading' }).first()).not.toBeChecked()
  expect(invoiceUploadHeaders[0]?.['x-source-file-name']).toBe('synthetic-handwritten-bill.pdf')
  await expect(review.getByLabel('Extracted line 1 quantity')).toHaveValue('1')
  await expect(review.getByLabel('Extracted line 1 unit price')).toHaveValue('10000')
  await expect(bill.locator('#billing-prod')).toBeHidden()
  await expect(bill.locator('#billing-qty')).toBeHidden()
  await expect(bill.locator('#billing-price')).toBeHidden()
  await expect(bill.locator('.invoice-product-more-details')).toBeHidden()
  await expect(bill.locator('.invoice-add-item-action')).toBeHidden()
  await expect(bill.locator('#billing-name')).toHaveValue('Handwritten Customer')
  await expect(bill.locator('#billing-phone')).toHaveValue('9876543210')

  const separation = await review.evaluate(element => {
    const cards = element.querySelectorAll('.invoice-extracted-line')
    const action = element.querySelector('.invoice-extraction-actions')
    if (!cards.length || !action) return -1
    return action.getBoundingClientRect().top - cards[cards.length - 1]!.getBoundingClientRect().bottom
  })
  expect(separation).toBeGreaterThanOrEqual(10)
  await captureState(page, 'extraction-review')
  await expect(chatLauncher).toBeVisible()

  await review.getByRole('button', { name: 'Discard extraction' }).click()
  await expect(review).toHaveCount(0)
  await expect(bill.getByText(/Invoice details extracted/)).toHaveCount(0)
  await expect(description).toBeVisible()
  await expect(bill.locator('#billing-qty')).toBeVisible()

  await importBill()
  const acceptedReview = bill.getByRole('region', { name: 'Review extracted sales invoice', exact: true })
  await expect(acceptedReview).toBeVisible()
  await acceptedReview.getByRole('checkbox', { name: 'Confirm this reading' }).first().check()
  await acceptedReview.getByRole('button', { name: 'Add reviewed lines to bill' }).click()
  await expect(bill.locator('.invoice-added-item')).toContainText('Samsung LED Television model UA43')
  await expect(bill.locator('.invoice-added-item')).toContainText('Serial: SN12345678')
  await expect(bill.getByText(/Invoice details extracted/)).toHaveCount(0)
  await expect(description).toBeVisible()
  await page.unroute('**/api/sales/invoice-image-extract')

  await page.route('**/api/sales/item-tag-scan', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      status: 'ReadyForReview',
      extraction: { productName: 'LG Washing Machine', brand: 'LG', model: 'WM-AUDIT-900',
        serial: 'AUDIT-SERIAL-9001', barcode: null, imei1: null, imei2: null, mrp: 18000, hsn: '8450' },
      suggestedProduct: null, warnings: ['Synthetic audit extraction: verify the serial against the tag.'],
    }) })
  })
  await bill.getByRole('button', { name: 'Scan tag' }).click()
  const tagPicker = page.getByRole('dialog', { name: 'Add product tag photo' })
  const tagChooser = page.waitForEvent('filechooser')
  await tagPicker.getByRole('button', { name: 'Choose from device' }).click()
  await (await tagChooser).setFiles({
    name: 'synthetic-tag.jpg',
    mimeType: 'image/jpeg',
    buffer: Buffer.from('/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAoHBwgHBgoICAgLCgoLDhgQDg0NDh0VFhEYIx8lJCIfIiEmKzcvJik0KSEiMEExNDk7Pj4+JS5ESUM8SDc9Pjv/2wBDAQoLCw4NDhwQEBw7KCIoOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozv/wAARCAAgACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWW5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD2aiiigAooooAKKKKACiiigD//2Q==', 'base64'),
  })
  await expect(page.getByText(/Product details detected/)).toBeVisible()
  const feedback = bill.locator('.invoice-product-entry .item-tag-feedback').first()
  await expect(feedback).toBeVisible()
  const bounds = await feedback.evaluate(element => {
    const rect = element.getBoundingClientRect()
    return { left: rect.left, right: rect.right, width: rect.width, viewport: window.innerWidth }
  })
  expect(bounds.left).toBeGreaterThanOrEqual(0)
  expect(bounds.right).toBeLessThanOrEqual(bounds.viewport)
  await captureState(page, 'tag-extraction-feedback')
  await expect(chatLauncher).toBeVisible()
  expect(pageErrors).toEqual([])
  writeFileSync(`${outDir}/report.json`, JSON.stringify({ scope: 'invoice-import', pageErrors, captures: audit }, null, 2))
})
