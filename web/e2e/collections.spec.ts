import { signUp, PASSWORD, randomMobile, openAddProduct, chooseCategory, postInvoice } from './signup'
import { expect, test, type Page } from '@playwright/test'

const run = Date.now().toString(36)
const password = 'Synthetic#Pass123'
const owner = 'e2e-coll-' + run + '@example.test'
const item = 'E2E Coll Cable ' + run
const customer = 'E2E Collect Customer ' + run

async function pick(page: Page, region: string, label: string, optionText: string) {
  const s = page.getByRole('region', { name: region, exact: true }).locator('label', { hasText: new RegExp('^' + label) }).locator('select').first()
  const value = await s.locator('option', { hasText: optionText }).first().getAttribute('value')
  await s.selectOption(value!)
}

test('credit sale, outstanding balance, later collection, balance update', async ({ page }) => {
  await signUp(page, { email: owner, businessName: 'E2E Coll ' + run })
  await page.goto('/#/stock/products')
  await expect(page.getByRole('region', { name: 'Products' })).toBeVisible()

  const form = await openAddProduct(page)
  await form.getByLabel('Name', { exact: true }).fill(item)
  await chooseCategory(form, 'Cables')
  await form.getByLabel('Generic / unbranded').setChecked(true)
  await form.getByRole('button', { name: 'Add product' }).click()
  await expect(page.getByRole('region', { name: 'Products' }).getByRole('cell', { name: item, exact: true })).toBeVisible({ timeout: 30_000 })

  await page.goto('/#/stock/opening')
  const stockForm = page.getByRole('region', { name: 'Opening stock' })
  await pick(page, 'Opening stock', 'Product', item)
  await stockForm.getByLabel('Quantity').fill('10')
  await stockForm.getByLabel('Cost for one item (₹)').fill('20')
  await stockForm.getByRole('button', { name: 'Add item' }).click()
  await stockForm.getByRole('button', { name: 'Review starting stock' }).click()
  await stockForm.getByRole('button', { name: 'Confirm & post opening stock' }).click()
  await expect(stockForm.getByRole('status')).toContainText('200.00 INR')

  const sales = page.getByRole('region', { name: 'Sales', exact: true })
  await postInvoice(page, { product: item, qty: '3', price: '100', mode: 'Credit', paid: '100', name: customer, phone: randomMobile() })
  await expect(page.getByRole('status')).toContainText('Saved as INV-')
  await page.goto('/#/parties/customers')
  const custRow = page.getByRole('table', { name: 'Customers' }).getByRole('row', { name: new RegExp(customer) })
  await expect(custRow).toContainText('200.00')

  await page.goto('/#/sell/collect')
  const coll = page.getByRole('region', { name: 'Collections and returns' })
  await coll.getByRole('button', { name: 'Refresh', exact: true }).click()
  await pick(page, 'Collections and returns', 'Collect from', customer)
  await expect(coll.getByTestId('collect-owed')).toContainText('200.00')
  await coll.getByLabel('Collection amount (INR)').fill('75')
  await coll.getByLabel('Collection method').selectOption('UPI')
  await coll.getByRole('button', { name: 'Record payment' }).click()
  await expect(coll.getByRole('status')).toContainText('now owes 125.00')
  await page.reload()
  await page.goto('/#/parties/customers')
  await expect(custRow).toContainText('125.00')
  await page.goto('/#/sell/collect')
  await expect(coll.getByRole('table', { name: 'Collection history' })).toContainText('75.00')
})

