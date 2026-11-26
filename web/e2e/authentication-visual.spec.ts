import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { randomMobile } from './signup'

const run = Date.now().toString(36)
const email = `e2e-auth-${run}@example.test`
const businessName = `E2E Auth Shop ${run}`
const mobile = randomMobile()
const outDir = 'test-results/authentication'
const viewports = [
  { name: 'phone-360', width: 360, height: 800 },
  { name: 'reference-390', width: 390, height: 844 },
  { name: 'phone-412', width: 412, height: 915 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'desktop-1280', width: 1280, height: 900 },
]

type AuditEntry = {
  state: string
  viewport: string
  overflowX: boolean
  unnamedControls: string[]
  smallTargets: string[]
  axeViolations?: { id: string; impact: string | null; nodes: number; sample: string }[]
}

async function captureState(page: Page, state: string, report: AuditEntry[]) {
  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    await page.waitForTimeout(80)
    const metrics = await page.evaluate(() => {
      const visible = (element: Element) => {
        const rect = element.getBoundingClientRect()
        const style = getComputedStyle(element)
        return rect.width > 0 && rect.height > 0 && style.visibility !== 'hidden' && style.display !== 'none'
      }
      const controls = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea')].filter(visible)
      const accessibleName = (element: Element) => {
        const html = element as HTMLElement & { labels?: NodeListOf<HTMLLabelElement> }
        return element.getAttribute('aria-label')
          || element.getAttribute('aria-labelledby')
          || (html.labels?.length ? 'labelled' : '')
          || (element.textContent || '').trim()
          || element.getAttribute('title')
          || ''
      }
      const unnamedControls = controls.filter(element => !accessibleName(element)).map(element => element.outerHTML.slice(0, 120))
      const smallTargets = controls.filter(element => {
        const target = element.closest('label') ?? element
        const rect = target.getBoundingClientRect()
        return rect.width < 44 || rect.height < 44
      }).map(element => {
        const rect = (element.closest('label') ?? element).getBoundingClientRect()
        return `${element.tagName.toLowerCase()} ${Math.round(rect.width)}x${Math.round(rect.height)}`
      })
      return {
        overflowX: document.documentElement.scrollWidth > window.innerWidth,
        unnamedControls,
        smallTargets,
      }
    })

    const entry: AuditEntry = { state, viewport: viewport.name, ...metrics }
    if (viewport.name === 'reference-390') {
      const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
      entry.axeViolations = axe.violations.map(violation => ({
        id: violation.id,
        impact: violation.impact,
        nodes: violation.nodes.length,
        sample: violation.nodes[0]?.html.slice(0, 120) ?? '',
      }))
    }
    report.push(entry)
    await page.screenshot({ path: `${outDir}/${state}-${viewport.name}-full.png`, fullPage: true })
    if (viewport.name === 'reference-390') {
      await page.screenshot({ path: `${outDir}/${state}-reference-390-viewport.png` })
    }

    expect(metrics.overflowX, `${state} has horizontal overflow at ${viewport.name}`).toBe(false)
    expect(metrics.unnamedControls, `${state} has unnamed controls at ${viewport.name}`).toEqual([])
  }
}

test('authentication and five-step signup visual audit', async ({ page }) => {
  test.setTimeout(240_000)
  mkdirSync(outDir, { recursive: true })
  const runtimeErrors: string[] = []
  const failedRequests: string[] = []
  page.on('pageerror', error => runtimeErrors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') runtimeErrors.push(message.text()) })
  page.on('requestfailed', request => failedRequests.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText ?? 'failed'}`))

  const report: AuditEntry[] = []
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
  await captureState(page, 'sign-in', report)

  await page.getByLabel('Email address').fill(email)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('Enter your password.')
  await captureState(page, 'sign-in-validation', report)

  await page.getByRole('button', { name: 'Create your shop account' }).click()
  await expect(page.getByText('STEP 1 OF 5')).toBeVisible()
  await page.getByLabel('Email *').fill(email)
  await page.getByLabel(/Password \*/).fill('Synthetic#Pass123')
  await page.getByLabel('Mobile number *').fill(mobile)
  await captureState(page, 'signup-account', report)

  const sendCode = page.getByRole('button', { name: 'Send code', exact: true })
  if (await sendCode.isVisible().catch(() => false)) {
    await sendCode.click()
    const developmentCode = await page.getByText(/Development code: \d{6}/).textContent()
    const code = /(\d{6})/.exec(developmentCode ?? '')?.[1]
    expect(code, 'Development OTP was not shown for the synthetic signup').toBeTruthy()
    await page.getByLabel('Enter the 6-digit code').fill(code!)
    await page.getByRole('button', { name: 'Verify code' }).click()
    await expect(page.getByText('Mobile number verified.')).toBeVisible()
  }

  await page.getByRole('button', { name: 'Continue' }).click()
  await expect(page.getByText('STEP 2 OF 5')).toBeVisible()
  await page.getByLabel('Business name *').fill(businessName)
  await page.getByLabel('Business category *').selectOption('electronics_mobile')
  await page.getByLabel('Owner or contact name *').fill('Synthetic Owner')
  await captureState(page, 'signup-shop', report)

  await page.getByRole('button', { name: 'Continue' }).click()
  await expect(page.getByText('STEP 3 OF 5')).toBeVisible()
  await page.getByLabel('Business address *').fill('10 Test Road')
  await page.getByLabel('City or town *').fill('Bengaluru')
  await page.getByLabel('State *').selectOption('Karnataka')
  await page.getByLabel('PIN code *').fill('560001')
  await page.getByLabel('GST registration *').selectOption('NotRegistered')
  await captureState(page, 'signup-address-tax', report)

  await page.getByRole('button', { name: 'Continue' }).click()
  await expect(page.getByText('STEP 4 OF 5')).toBeVisible()
  await expect(page.getByLabel('Financial year starts in')).toHaveValue('4')
  await expect(page.getByLabel('Language')).toHaveValue('en')
  await captureState(page, 'signup-preferences', report)

  await page.getByRole('button', { name: 'Review' }).click()
  await expect(page.getByText('STEP 5 OF 5')).toBeVisible()
  await expect(page.locator('.setup-invoice-preview')).toBeVisible()
  await expect(page.getByText('Not registered selected. Do not show GST as a registered-business tax invoice.')).toBeVisible()
  await captureState(page, 'signup-review', report)

  await page.getByRole('button', { name: 'Create account', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Set up your app PIN' })).toBeVisible()
  const createPin = page.getByLabel('Create four-digit PIN')
  await createPin.click()
  for (const digit of ['1', '2', '3', '4']) await page.getByRole('button', { name: `Digit ${digit}` }).click()
  const confirmPin = page.getByLabel('Confirm PIN')
  await confirmPin.click()
  for (const digit of ['1', '2', '3', '4']) await page.getByRole('button', { name: `Digit ${digit}` }).click()
  await captureState(page, 'pin-setup', report)
  await page.getByRole('button', { name: 'Save PIN' }).click()
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()

  await page.evaluate(() => window.dispatchEvent(new Event('rbos:lock')))
  await expect(page.getByRole('heading', { name: 'Unlock your shop' })).toBeVisible()
  await captureState(page, 'pin-unlock', report)
  const pin = page.getByLabel('PIN')
  for (const digit of ['1', '2', '3']) await page.getByRole('button', { name: `Digit ${digit}` }).click()
  await page.getByRole('button', { name: 'Backspace' }).click()
  await expect(pin).toHaveValue('12')
  for (const digit of ['3', '4']) await page.getByRole('button', { name: `Digit ${digit}` }).click()
  await expect(pin).toHaveValue('1234')
  await page.getByRole('button', { name: 'Unlock', exact: true }).click()
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()

  writeFileSync(`${outDir}/report.json`, JSON.stringify({ screens: report, runtimeErrors, failedRequests }, null, 2))
  expect(runtimeErrors, 'Browser console or page runtime errors were recorded').toEqual([])
})
