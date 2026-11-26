import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { signUp } from './signup'

const run = Date.now().toString(36)
const outDir = 'test-results/ui-audit/assistant'
const viewports = [
  { name: 'mobile-360x800', width: 360, height: 800 },
  { name: 'mobile-390x844', width: 390, height: 844 },
  { name: 'mobile-412x915', width: 412, height: 915 },
  { name: 'tablet-768x1024', width: 768, height: 1024 },
  { name: 'landscape-844x390', width: 844, height: 390 },
  { name: 'desktop-1280x900', width: 1280, height: 900 },
]
const captures: Array<{ route: string; viewport: string; screenshot: string; overflowX: boolean; panelInBounds: boolean; touchTargets: string[]; axe: string[] }> = []
const pageErrors: string[] = []
const consoleErrors: string[] = []
const failedRequests: string[] = []

test('Business assistant navigation, Home/Add Purchase layouts and WCAG 2 A/AA', async ({ page }) => {
  test.setTimeout(12 * 60 * 1000)
  mkdirSync(outDir, { recursive: true })
  page.on('pageerror', error => pageErrors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') consoleErrors.push(message.text()) })
  page.on('requestfailed', request => failedRequests.push(request.method() + ' ' + request.url() + ' ' + (request.failure()?.errorText ?? '')))

  await signUp(page, { email: 'e2e-assistant-' + run + '@example.test', businessName: 'E2E Assistant ' + run })

  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    await page.goto('/#/home')
    await page.getByRole('button', { name: 'Open assistant' }).click()
    const panel = page.getByRole('region', { name: 'Navigation assistant' })
    await expect(panel).toBeVisible()
    await expect(page.getByRole('button', { name: 'New bill' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Add purchase' })).toBeVisible()
    const geometry = await page.evaluate(() => {
      const panel = document.querySelector<HTMLElement>('.navigation-assistant')!
      const launcher = document.querySelector<HTMLElement>('.assistant-launcher')!
      const p = panel.getBoundingClientRect(), l = launcher.getBoundingClientRect()
      const visible = (el: Element) => {
        const rect = el.getBoundingClientRect(), style = getComputedStyle(el)
        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden'
      }
      const controls = Array.from(panel.querySelectorAll('button,input')).filter(visible)
      const touchTargets = controls.filter(el => {
        const rect = el.getBoundingClientRect()
        return rect.width < 44 || rect.height < 44
      }).map(el => el.tagName.toLowerCase() + ' ' + Math.round(el.getBoundingClientRect().width) + 'x' + Math.round(el.getBoundingClientRect().height))
      return {
        overflowX: document.documentElement.scrollWidth > window.innerWidth,
        panelInBounds: p.left >= 0 && p.right <= window.innerWidth && p.top >= 0 && p.bottom <= window.innerHeight,
        launcherInBounds: l.left >= 0 && l.right <= window.innerWidth && l.bottom <= window.innerHeight,
        touchTargets,
      }
    })
    await page.screenshot({ path: outDir + '/' + viewport.name + '-home-assistant.png', fullPage: true, animations: 'disabled' })
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
    captures.push({
      route: 'home', viewport: viewport.name, screenshot: viewport.name + '-home-assistant.png',
      overflowX: geometry.overflowX, panelInBounds: geometry.panelInBounds && geometry.launcherInBounds,
      touchTargets: geometry.touchTargets,
      axe: axe.violations.map(v => v.id + ':' + (v.impact ?? 'unknown') + ':' + v.nodes.length),
    })
    await page.getByRole('button', { name: 'Close assistant' }).click()
  }

  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/#/home')
  await page.getByRole('button', { name: 'Open assistant' }).click()
  await page.getByLabel('Ask your business assistant').fill('make a new bill')
  await page.getByRole('button', { name: 'Go' }).click()
  await expect(page).toHaveURL(/#\/sell\/bill$/)
  await expect(page.getByRole('button', { name: 'Open assistant' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Navigation assistant' })).toHaveCount(0)

  await page.goto('/#/home')
  await page.getByRole('button', { name: 'Open assistant' }).click()
  await page.getByLabel('Ask your business assistant').fill('return')
  await page.getByRole('button', { name: 'Go' }).click()
  await expect(page.getByRole('button', { name: 'Customer return' })).toBeVisible()
  await page.getByRole('button', { name: 'Customer return' }).click()
  await expect(page).toHaveURL(/#\/sell\/return$/)
  await expect(page.getByRole('region', { name: 'Navigation assistant' })).toHaveCount(0)

  await page.goto('/#/buy/receive')
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.locator('.buy-review-bar')).toBeVisible()
  await page.getByRole('button', { name: 'Open assistant' }).click()
  const purchaseLayout = await page.evaluate(() => {
    const panel = document.querySelector<HTMLElement>('.navigation-assistant')!
    const launcher = document.querySelector<HTMLElement>('.assistant-launcher')!
    const review = document.querySelector<HTMLElement>('.buy-review-bar')!
    const p = panel.getBoundingClientRect(), l = launcher.getBoundingClientRect(), r = review.getBoundingClientRect()
    return {
      panelAboveReview: p.bottom <= r.top - 4,
      launcherAboveReview: l.bottom <= r.top - 4,
      centred: Math.abs((p.left + p.right) / 2 - window.innerWidth / 2) < 2,
      panelInBounds: p.left >= 0 && p.right <= window.innerWidth && p.top >= 0,
    }
  })
  await expect(page.getByRole('region', { name: 'Navigation assistant' })).toHaveClass(/is-purchase-context/)
  await page.screenshot({ path: outDir + '/mobile-390x844-add-purchase-assistant.png', fullPage: true, animations: 'disabled' })
  const purchaseAxe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
  captures.push({
    route: 'buy/receive', viewport: 'mobile-390x844', screenshot: 'mobile-390x844-add-purchase-assistant.png',
    overflowX: await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth),
    panelInBounds: purchaseLayout.panelInBounds && purchaseLayout.panelAboveReview && purchaseLayout.launcherAboveReview && purchaseLayout.centred,
    touchTargets: [],
    axe: purchaseAxe.violations.map(v => v.id + ':' + (v.impact ?? 'unknown') + ':' + v.nodes.length),
  })
  await page.getByRole('button', { name: 'Close assistant' }).click()

  writeFileSync(outDir + '/report.json', JSON.stringify({ scope: 'assistant', generatedAt: new Date().toISOString(), captures, pageErrors, consoleErrors, failedRequests }, null, 2))
  expect(captures.filter(x => x.overflowX)).toEqual([])
  expect(captures.filter(x => !x.panelInBounds)).toEqual([])
  expect(captures.flatMap(x => x.touchTargets)).toEqual([])
  expect(captures.flatMap(x => x.axe)).toEqual([])
  expect(pageErrors).toEqual([])
  expect(consoleErrors).toEqual([])
  expect(failedRequests).toEqual([])
})
