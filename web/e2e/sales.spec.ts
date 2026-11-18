import { signUp, randomMobile, openAddProduct, chooseCategory, postInvoice } from './signup'
import { expect, test, type Page } from '@playwright/test'

const run = Date.now().toString(36)
const owner = 'e2e-sale-' + run + '@example.test'
const item = 'E2E Sale Cable ' + run
const customer = 'E2E Customer ' + run

async function pick(page: Page, region: string, label: string, optionText: string) {
  const s = page.getByRole('region', { name: region }).locator('label', { hasText: new RegExp('^' + label) }).locator('select').first()
  const value = await s.locator('option', { hasText: optionText }).first().getAttribute('value')
  await s.selectOption(value!)
}

test('shopkeeper sells for cash, on credit with part payment, and reviews history', async ({ page }) => {
  await signUp(page, { email: owner, businessName: 'E2E Sales ' + run })
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

  await postInvoice(page, { product: item, qty: '2', price: '50', discount: '10' })
  await expect(page.getByRole('status')).toContainText('Saved as INV-')
  await page.goto('/#/stock/current')
  const stock = page.getByRole('region', { name: 'Stock on hand' })
  await expect(stock.getByRole('row', { name: new RegExp(item) })).toContainText('8')

  await postInvoice(page, { product: item, qty: '3', price: '100', mode: 'Credit', paid: '120', name: customer, phone: randomMobile() })
  await expect(page.getByRole('status')).toContainText('Saved as INV-')
  await page.goto('/#/parties/customers')
  await expect(page.getByRole('region', { name: 'Customers', exact: true }).getByRole('table', { name: 'Customers' }).getByRole('row', { name: new RegExp(customer) })).toContainText('180.00')
  await page.goto('/#/stock/current')
  await expect(stock.getByRole('row', { name: new RegExp(item) })).toContainText('5')

  await page.goto('/#/sell/bills')
  const sales = page.getByRole('region', { name: 'Sales', exact: true })
  await sales.getByLabel('Search sales').fill(customer)
  const history = sales.getByRole('table', { name: 'Sales history' })
  await expect(history.getByRole('row')).toHaveCount(2)
  await history.getByRole('button', { name: 'View' }).dispatchEvent('click')
  await expect(page.getByRole('dialog', { name: 'Sale detail' })).toContainText('Balance due')

  await postInvoice(page, { product: item, qty: '99', price: '10' })
  await expect(page.getByRole('alert')).toContainText(/stock/i)
})
