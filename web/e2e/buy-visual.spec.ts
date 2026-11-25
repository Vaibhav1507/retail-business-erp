import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { signUp, unlockApp } from './signup'

const run = Date.now().toString(36)
const outDir = 'test-results/ui-audit/buy'
const viewports = [
  { name: 'mobile-360x800', width: 360, height: 800 },
  { name: 'mobile-390x844', width: 390, height: 844 },
  { name: 'mobile-412x915', width: 412, height: 915 },
  { name: 'tablet-768x1024', width: 768, height: 1024 },
  { name: 'landscape-844x390', width: 844, height: 390 },
]
const desktop = { name: 'desktop-1280x900', width: 1280, height: 900 }
const routes = [
  { path: 'buy/receive', heading: 'Add purchase' },
  { path: 'buy/pay', heading: 'Pay supplier' },
  { path: 'buy/history', heading: 'Past purchases' },
  { path: 'buy/return', heading: 'Return to supplier' },
]
type Capture = { route: string; viewport: string; screenshot: string; overflowX: boolean; unnamed: string[]; undersized: string[]; axe: string[] }
const captures: Capture[] = []
const pageErrors: string[] = []
const consoleErrors: string[] = []
const failedRequests: string[] = []

async function capture(page: Page, route: string, viewport: typeof viewports[number] | typeof desktop) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height })
  await page.waitForLoadState('networkidle')
  const screenshot = `${viewport.name}-${route.replace('/', '-')}.png`
  await page.screenshot({ path: `${outDir}/${screenshot}`, fullPage: true, animations: 'disabled' })
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
    }).map(el => el.tagName.toLowerCase() + ' ' + Math.round(el.getBoundingClientRect().width) + 'x' + Math.round(el.getBoundingClientRect().height))
    return { overflowX: document.documentElement.scrollWidth > window.innerWidth, unnamed, undersized }
  })
  const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
  captures.push({ route, viewport: viewport.name, screenshot, ...metrics, axe: axe.violations.map(v => `${v.id}:${v.impact ?? 'unknown'}:${v.nodes.length}`) })
  writeFileSync(`${outDir}/report.json`, JSON.stringify({ captures, pageErrors, consoleErrors, failedRequests }, null, 2))
}

test('Buy: responsive routes, Add Purchase interactions and WCAG 2 A/AA audit', async ({ page, context, browser }) => {
  test.setTimeout(15 * 60 * 1000)
  mkdirSync(outDir, { recursive: true })
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()) })
  page.on('requestfailed', request => failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`))

  await signUp(page, { email: `e2e-buy-${run}@example.test`, businessName: 'E2E Buy ' + run })
  await page.goto('/#/buy/receive')
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Add purchase', exact: true })).toBeVisible()
  await expect(page.locator('.buy-review-bar')).toBeVisible()
  await expect(page.locator('.buy-review-bar')).toContainText('0 items')
  await expect(page.getByRole('button', { name: /Review purchase/ })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Camera' })).toBeEnabled()
  await expect(page.getByRole('button', { name: 'Upload', exact: true })).toBeEnabled()
  await expect(page.getByLabel('Take supplier bill photo')).toHaveAttribute('capture', 'environment')
  await expect(page.getByLabel('Upload supplier bill files')).toHaveAttribute('multiple', '')

  const buyTabs = page.locator('.buy-tabs a')
  await expect(buyTabs).toHaveCount(4)
  await expect(page.locator('.buy-purchase-card')).toHaveCount(1)
  await expect(page.locator('.buy-optional-badge')).toBeVisible()
  await expect(page.locator('.buy-item-entry-help')).toBeVisible()

  // MagicPath composition checks at the reference Android viewport.
  await page.setViewportSize({ width: 390, height: 844 })
  const layout = await page.evaluate(() => {
    const rect = (selector: string) => {
      const element = document.querySelector(selector)
      if (!element) throw new Error(`Missing layout element: ${selector}`)
      const r = element.getBoundingClientRect()
      return { x: r.x, y: r.y, width: r.width, height: r.height, right: r.right, bottom: r.bottom }
    }
    const labels = [...document.querySelectorAll('.buy-receipt-section .buy-fields label')].map(el => {
      const r = el.getBoundingClientRect()
      return { x: r.x, y: r.y, width: r.width, height: r.height }
    })
    const itemLabels = [...document.querySelectorAll('.buy-item-entry label')].map(el => {
      const r = el.getBoundingClientRect()
      return { x: r.x, y: r.y, width: r.width, height: r.height }
    })
    const itemActions = [...document.querySelectorAll('.buy-item-entry button')].map(el => {
      const r = el.getBoundingClientRect()
      return { x: r.x, y: r.y, width: r.width, height: r.height }
    })
    const bar = rect('.buy-review-bar'), nav = rect('.nav')
    return {
      tabsDisplay: getComputedStyle(document.querySelector('.buy-tabs')!).display,
      tabsWrap: getComputedStyle(document.querySelector('.buy-tabs')!).flexWrap,
      shellBorder: getComputedStyle(document.querySelector('.buy-receive-shell')!).borderTopWidth,
      shellPadding: getComputedStyle(document.querySelector('.buy-receive-shell')!).paddingLeft,
      uploadBorder: getComputedStyle(document.querySelector('.invoice-upload-dropzone')!).borderTopStyle,
      uploadRegionBorder: getComputedStyle(document.querySelector('.invoice-upload[role=region]')!).borderTopWidth,
      uploadRegionPadding: getComputedStyle(document.querySelector('.invoice-upload[role=region]')!).paddingTop,
      uploadInnerBorder: getComputedStyle(document.querySelector('.invoice-upload-inner')!).borderTopWidth,
      uploadIntroDisplay: getComputedStyle(document.querySelector('.invoice-upload-intro')!).display,
      uploadIntroDirection: getComputedStyle(document.querySelector('.invoice-upload-intro')!).flexDirection,
      supplierFields: labels,
      supplierGridColumn: getComputedStyle(document.querySelector('.buy-fields label:first-child')!).gridColumn,
      invoiceGridColumn: getComputedStyle(document.querySelector('.buy-fields label:last-child')!).gridColumn,
      receiptGridColumns: getComputedStyle(document.querySelector('.buy-fields')!).gridTemplateColumns,
      itemLabels,
      itemActions,
      footerPosition: getComputedStyle(document.querySelector('.buy-review-bar')!).position,
      footer: bar,
      nav,
      scrollWidth: document.documentElement.scrollWidth,
    }
  })
  expect(layout.tabsDisplay).toBe('flex')
  expect(layout.tabsWrap).toBe('nowrap')
  expect(layout.shellBorder).toBe('0px')
  expect(layout.shellPadding).toBe('0px')
  expect(layout.uploadBorder).toBe('dashed')
  expect(layout.uploadRegionBorder).toBe('0px')
  expect(layout.uploadRegionPadding).toBe('0px')
  expect(layout.uploadInnerBorder).toBe('0px')
  expect(layout.uploadIntroDisplay).toBe('flex')
  expect(layout.uploadIntroDirection).toBe('row')
  expect(layout.supplierGridColumn).toBe('1 / -1')
  expect(layout.invoiceGridColumn).toBe('1 / -1')
  expect(layout.supplierFields[0]!.width).toBeGreaterThan(layout.supplierFields[1]!.width)
  expect(Math.abs(layout.supplierFields[1]!.y - layout.supplierFields[2]!.y)).toBeLessThan(2)
  expect(layout.itemLabels[0]!.width).toBeGreaterThan(layout.itemLabels[1]!.width * 1.7)
  expect(Math.abs(layout.itemLabels[1]!.y - layout.itemLabels[2]!.y)).toBeLessThan(2)
  expect(Math.abs(layout.itemActions[0]!.y - layout.itemActions[1]!.y)).toBeLessThan(2)
  expect(layout.footerPosition).toBe('fixed')
  expect(layout.footer.x).toBeGreaterThanOrEqual(0)
  expect(layout.footer.right).toBeLessThanOrEqual(391)
  expect(layout.footer.bottom).toBeLessThanOrEqual(layout.nav.y + 1)
  expect(layout.scrollWidth).toBeLessThanOrEqual(390)
  const initialFooterTop = layout.footer.y
  await page.getByRole('button', { name: 'Camera' }).focus()
  await expect(page.getByRole('button', { name: 'Camera' })).toBeFocused()
  await capture(page, 'add-purchase-default', viewports[1]!)
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  const overlay = await page.evaluate((footerTop) => {
    const bar = document.querySelector('.buy-review-bar')!.getBoundingClientRect()
    const nav = document.querySelector('.nav')!.getBoundingClientRect()
    const guidance = document.querySelector('.buy-receive .buy-guidance')!.getBoundingClientRect()
    return { barNavOverlap: bar.bottom > nav.top + 1, contentHiddenByBar: guidance.bottom > bar.top + 1, barStayedFixed: Math.abs(bar.top - footerTop) < 2 }
  }, initialFooterTop)
  expect(overlay.barNavOverlap, 'Review bar must sit above the fixed bottom navigation').toBe(false)
  expect(overlay.contentHiddenByBar, 'The final form guidance must remain visible above the review bar').toBe(false)
  expect(overlay.barStayedFixed, 'Review bar must not scroll with the form').toBe(true)

  // Exercise the real OCR loading/review path with a deterministic synthetic PDF response.
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.route('**/api/purchases/extract', async route => {
    await new Promise(resolve => setTimeout(resolve, 350))
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      fileId: 'audit-invoice-file', supplierName: null, supplierId: null, invoiceNumber: 'AUDIT-1',
      date: '2026-11-24', total: 0, lines: [], warnings: [],
    }) })
  })
  await page.getByLabel('Upload supplier bill files').setInputFiles({
    name: 'supplier-invoice.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 synthetic test invoice'),
  })
  await expect(page.getByRole('status')).toContainText('Reading invoice')
  await expect(page.getByText('supplier-invoice.pdf')).toBeVisible()
  await capture(page, 'ocr-loading', viewports[1]!)
  await expect(page.getByRole('button', { name: 'Discard' })).toBeVisible()
  await page.getByRole('button', { name: 'Discard' }).click()
  await page.unroute('**/api/purchases/extract')
  await expect(page.getByRole('button', { name: 'Camera' })).toBeEnabled()

  // Smoke-test each Buy destination and capture the full responsive matrix.
  for (const route of routes) {
    await page.goto('/#/' + route.path)
    await expect(page.getByRole('heading', { name: route.heading, exact: true })).toBeVisible()
    for (const viewport of viewports) await capture(page, route.path, viewport)
  }

  // Use a genuine desktop browser context rather than relabelling a mobile context.
  const desktopContext = await browser.newContext({
    baseURL: 'http://localhost:5173', storageState: await context.storageState(),
    viewport: { width: desktop.width, height: desktop.height },
  })
  const desktopPage = await desktopContext.newPage()
  desktopPage.on('pageerror', error => pageErrors.push('desktop: ' + error.message))
  await desktopPage.goto('/')
  await unlockApp(desktopPage)
  for (const route of routes) {
    await desktopPage.goto('/#/' + route.path)
    await expect(desktopPage.getByRole('heading', { name: route.heading, exact: true })).toBeVisible()
    await capture(desktopPage, route.path, desktop)
  }
  await desktopContext.close()

  writeFileSync(`${outDir}/report.json`, JSON.stringify({
    scope: 'buy', generatedAt: new Date().toISOString(), captures, pageErrors, consoleErrors, failedRequests,
  }, null, 2))
  expect(captures.filter(x => x.overflowX), 'No Buy screen may horizontally overflow').toEqual([])
  expect(captures.flatMap(x => x.unnamed), 'All visible controls must have accessible names').toEqual([])
  expect(captures.flatMap(x => x.undersized), 'All visible controls must meet the 44px touch target threshold').toEqual([])
  expect(captures.flatMap(x => x.axe), 'Axe WCAG 2 A/AA violations must be fixed').toEqual([])
  expect(pageErrors, 'No uncaught browser errors').toEqual([])
  expect(consoleErrors, 'No browser console errors').toEqual([])
  expect(failedRequests, 'No failed network requests').toEqual([])
})
