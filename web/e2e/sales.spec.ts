import { signUp, PASSWORD, randomMobile } from './signup'
import { expect, test, type Page } from '@playwright/test'

const run = Date.now().toString(36)
const password = 'Synthetic#Pass123'
const owner = 'e2e-sale-' + run + '@example.test'
const item = 'E2E Sale Cable ' + run
const customer = 'E2E Customer ' + run

async function pick(page: Page, region: string, label: string, optionText: string) {
  const s = page.getByRole('region', { name: region }).locator('label', { hasText: new RegExp('^' + label) }).locator('select').first()
  const value = await s.locator('option', { hasText: optionText }).first().getAttribute('value')
  await s.selectOption(value!)
}
async function saleLine(page: Page, qty: string, price: string, discount = '') {
  const r = page.getByRole('region', { name: 'Sales' })
  await pick(page, 'Sales', 'Product', item)
  await r.getByLabel('Quantity').fill(qty)
  await r.getByLabel('Unit price (INR)').fill(price)
  if (discount) await r.getByLabel('Discount (INR)').fill(discount)
  await r.getByRole('button', { name: 'Add line' }).click()
}

test('shopkeeper sells for cash, on credit with part payment, and reviews history', async ({ page }) => {
  await signUp(page, { email: owner, businessName: 'E2E Sales ' + run })
  await expect(page.getByRole('region', { name: 'Products' })).toBeVisible()

  const form = page.getByRole('region', { name: 'Products' }).getByRole('form', { name: 'Add product' })
  await form.getByLabel('Name', { exact: true }).fill(item)
  await form.getByLabel('Category').fill('Cables')
  await form.getByLabel('Generic / unbranded').setChecked(true)
  await form.getByRole('button', { name: 'Add product' }).click()
  await expect(page.getByRole('region', { name: 'Products' }).getByRole('cell', { name: item, exact: true })).toBeVisible()

  const stockForm = page.getByRole('region', { name: 'Opening stock' })
  await pick(page, 'Opening stock', 'Product', item)
  await stockForm.getByLabel('Quantity').fill('10')
  await stockForm.getByLabel('Unit cost (INR)').fill('20')
  await stockForm.getByRole('button', { name: 'Add line' }).click()
  await stockForm.getByRole('button', { name: 'Post opening stock' }).click()
  await expect(stockForm.getByRole('status')).toContainText('200.00 INR')

  const sales = page.getByRole('region', { name: 'Sales' })
  await sales.getByLabel('Customer name').fill(customer)
  await sales.getByRole('button', { name: 'Add customer' }).click()
  await expect(sales.getByRole('table', { name: 'Customers' }).getByRole('cell', { name: customer })).toBeVisible()

  await saleLine(page, '2', '50', '10')
  await expect(sales.getByTestId('sale-total')).toContainText('90.00')
  await sales.getByRole('button', { name: 'Post sale' }).click()
  await expect(sales.getByRole('status')).toContainText('paid in full')
  const stock = page.getByRole('region', { name: 'Stock on hand' })
  await expect(stock.getByRole('row', { name: new RegExp(item) })).toContainText('8')

  await pick(page, 'Sales', 'Customer', customer)
  await sales.getByLabel('Sale type').selectOption('Credit')
  await saleLine(page, '3', '100')
  await sales.getByLabel('Paid now (INR)').fill('120')
  await sales.getByRole('button', { name: 'Post sale' }).click()
  await expect(sales.getByRole('status')).toContainText('balance due 180.00')
  await expect(sales.getByRole('table', { name: 'Customers' }).getByRole('row', { name: new RegExp(customer) })).toContainText('180.00')
  await expect(stock.getByRole('row', { name: new RegExp(item) })).toContainText('5')

  await sales.getByLabel('Search sales').fill(customer)
  const history = sales.getByRole('table', { name: 'Sales history' })
  await expect(history.getByRole('row')).toHaveCount(2)
  await history.getByRole('button', { name: 'View' }).dispatchEvent('click')
  await expect(sales.getByRole('region', { name: 'Sale detail' })).toContainText('Balance 180.00')

  await sales.getByLabel('Sale type').selectOption('Cash')
  await saleLine(page, '99', '10')
  await sales.getByRole('button', { name: 'Post sale' }).click()
  await expect(sales.getByRole('alert')).toContainText(/stock/i)
})
