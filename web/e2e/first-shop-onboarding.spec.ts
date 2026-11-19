import { expect, test } from '@playwright/test'
import { signUp, openAddProduct, chooseCategory, createProductAndVerify, postInvoice, expectInvoiceSaved } from './signup'

const run = Date.now().toString(36)
const owner = `e2e-first-shop-${run}@example.test`
const businessName = `E2E Electronics ${run}`
const product = `E2E LED TV ${run}`
const supplier = `Synthetic Supplier ${run}`

test('first-time electronics retailer completes and resumes the saved first-shop journey on mobile', async ({ page }) => {
  test.setTimeout(180_000)
  await signUp(page, { email: owner, businessName, city: 'Bengaluru', state: 'Karnataka', pin: '560001' })

  const guide = page.getByRole('region', { name: 'First shop setup' })
  await expect(guide.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '1')
  await expect(guide.getByRole('link', { name: 'Continue setup' })).toHaveAttribute('href', '#/stock/products')

  // Catalogue: product creation remains separate from quantity and financial stock entry.
  await page.goto('/#/stock/products')
  const form = await openAddProduct(page)
  await form.getByLabel('Name', { exact: true }).fill(product)
  await chooseCategory(form, 'LED TVs')
  await form.getByLabel('Generic / unbranded').setChecked(false)
  await form.getByLabel('Brand', { exact: true }).fill('SyntheticBrand')
  await form.getByLabel('Model number').fill('TV-001')
  await createProductAndVerify(page, form, product)

  // Opening stock: entry and review must not post; the owner explicitly confirms afterward.
  await page.goto('/#/stock/opening')
  const stock = page.getByRole('region', { name: 'Opening stock' })
  const productOption = await stock.getByLabel('Product').locator('option', { hasText: product }).getAttribute('value')
  await stock.getByLabel('Product').selectOption(productOption!)
  await stock.getByLabel('Quantity').fill('3')
  await stock.getByLabel('Cost for one item (₹)').fill('12000')
  await stock.getByRole('button', { name: 'Add item' }).click()
  await expect(stock.getByTestId('estimated-total')).toContainText('36000.00 INR')
  await stock.getByRole('button', { name: 'Review starting stock' }).click()
  const stockReview = stock.getByRole('region', { name: 'Review opening stock' })
  await expect(stockReview).toContainText('36000.00')
  await expect(stockReview.getByRole('button', { name: 'Confirm & post opening stock' })).toBeVisible()
  await stockReview.getByRole('button', { name: 'Confirm & post opening stock' }).click()
  await expect(stock.getByRole('status')).toContainText('36000.00 INR')

  // Supplier and first purchase reuse the existing supplier master and purchase receipt workflow.
  await page.goto('/#/parties/suppliers')
  const supplierPage = page.getByRole('region', { name: 'Purchasing' })
  await supplierPage.getByLabel('Supplier name').fill(supplier)
  await supplierPage.getByRole('button', { name: 'Add supplier' }).click()
  await expect(supplierPage.getByRole('status')).toContainText('Supplier added')

  await page.goto('/#/buy/receive')
  const purchase = page.getByRole('region', { name: 'Purchasing' })
  await purchase.getByLabel('Supplier', { exact: true }).selectOption({ label: supplier })
  await purchase.getByLabel('Product').selectOption(productOption!)
  await purchase.getByLabel('Quantity').fill('1')
  await purchase.getByLabel('Unit cost (INR, before GST)').fill('11500')
  await purchase.getByRole('button', { name: 'Add line' }).click()
  await purchase.getByRole('button', { name: 'Review purchase' }).click()
  const purchaseReview = purchase.getByRole('region', { name: 'Review purchase' })
  await expect(purchaseReview).toContainText(supplier)
  await expect(purchaseReview).toContainText('11500.00')
  await purchaseReview.getByRole('button', { name: 'Post receipt' }).click()
  await expect(purchase.getByRole('status').filter({ hasText: 'Receipt posted' })).toBeVisible()

  // First sale continues through the existing invoice review, explicit confirmation and print action.
  await postInvoice(page, { product, qty: '1', price: '14500', mode: 'UPI' })
  await expectInvoiceSaved(page)

  // A fresh Home load derives completion from saved tenant records, not browser memory or a page visit.
  await page.goto('/#/')
  await expect(page.getByRole('region', { name: 'First shop setup' }).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '5')
  await page.reload()
  await expect(page.getByRole('region', { name: 'First shop setup' }).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '5')
})
