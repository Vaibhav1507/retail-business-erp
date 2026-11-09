import { mkdirSync, writeFileSync } from 'node:fs'
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { signUp } from './signup'

const run = Date.now().toString(36)
const routes = [
  'home', 'sell/menu', 'sell/bill', 'sell/bills', 'sell/collect', 'buy',
  'stock/current', 'stock/products', 'stock/opening', 'parties/customers', 'money', 'more',
]
const outDir = 'test-results/ui-audit'
const viewports = [{ name: 'mobile', w: 390, h: 844 }, { name: 'tablet', w: 768, h: 1024 }]

test('UI audit: overflow, control names, touch targets, headings (synthetic data, report only)', async ({ page }) => {
  mkdirSync(outDir, { recursive: true })
  await signUp(page, { email: `e2e-audit-${run}@example.test`, businessName: 'E2E Audit ' + run })
  await page.goto('/#/stock/products')
  await expect(page.getByRole('region', { name: 'Products' })).toBeVisible()

  const report: Record<string, unknown>[] = []
  for (const vp of viewports) for (const route of routes) {
    await page.setViewportSize({ width: vp.w, height: vp.h })
    await page.goto('/#/' + route)
    await page.waitForLoadState('networkidle')
    await page.screenshot({ path: `${outDir}/${vp.name}-${route.replace('/', '-')}.png`, fullPage: true })
    const found = await page.evaluate(() => {
      const vis = (el: Element) => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' }
      const label = (el: Element) => {
        const h = el as HTMLElement & { labels?: NodeListOf<HTMLLabelElement> }
        return (el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || h.labels?.length || (el.textContent || '').trim() || el.getAttribute('title') || '')
      }
      const controls = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea')].filter(vis)
      const unnamed = controls.filter((el) => !label(el)).map((el) => el.outerHTML.slice(0, 100))
      const smallEls = controls.filter((el) => { const r = (el.closest('label') ?? el).getBoundingClientRect(); return r.width < 44 || r.height < 44 })
      const small = smallEls.length
      const smallList = smallEls.map((el) => el.outerHTML.slice(0, 110) + ' ' + Math.round(el.getBoundingClientRect().width) + 'x' + Math.round(el.getBoundingClientRect().height))
      return {
        overflowX: document.documentElement.scrollWidth > window.innerWidth,
        h1: document.querySelectorAll('h1').length,
        h2: document.querySelectorAll('h2').length,
        controls: controls.length,
        unnamed,
        smallTouchTargets: small,
        smallList,
      }
    })
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze()
    const violations = axe.violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.length, sample: v.nodes[0]?.html.slice(0, 110) }))
    report.push({ viewport: vp.name, route, ...found, violations })
  }
  writeFileSync(`${outDir}/report.json`, JSON.stringify(report, null, 2))
  expect(report).toHaveLength(routes.length * viewports.length)
})
