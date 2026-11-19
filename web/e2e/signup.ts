import { expect, type Locator, type Page } from '@playwright/test'

export const PASSWORD = 'Synthetic#Pass123'

/** A random valid Indian mobile number so each run gets a unique account. */
export const randomMobile = () => '9' + Math.floor(100_000_000 + Math.random() * 899_999_999).toString()

/**
 * Creates an account through the single signup flow: account + mobile OTP, business, address and tax, preferences, review.
 * Relies on the Development API showing the OTP on screen (no real SMS is sent).
 */
export async function signUp(page: Page, opts: { email: string; businessName: string; city?: string; state?: string; pin?: string; mobile?: string }) {
  const mobile = opts.mobile ?? randomMobile()
  await page.goto('/')
  await page.getByRole('button', { name: 'Create an account' }).click()
  await page.getByLabel('Email').fill(opts.email)
  await page.getByLabel(/Password/).fill(PASSWORD)
  await page.getByLabel('Mobile number').fill(mobile)
  const sendCode = page.getByRole('button', { name: 'Send code' })
  if (await sendCode.isVisible()) {
    await sendCode.click()
    const hint = await page.getByText(/Development code: \d{6}/).textContent()
    const code = /(\d{6})/.exec(hint ?? '')![1]
    await page.getByLabel('Enter the 6-digit code').fill(code)
    await page.getByRole('button', { name: 'Verify code' }).click()
    await expect(page.getByText('Mobile number verified.')).toBeVisible()
  }
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByLabel('Business name').fill(opts.businessName)
  await page.getByLabel('Business category').selectOption({ index: 1 })
  await page.getByLabel('Owner or contact name').fill('E2E Owner')
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByLabel('Business address').fill('1 Test Street')
  await page.getByLabel('City or town').fill(opts.city ?? 'Chennai')
  await page.getByLabel('State').selectOption(opts.state ?? 'Tamil Nadu')
  await page.getByLabel('PIN code').fill(opts.pin ?? '600001')
  await page.getByLabel('GST registration').selectOption('NotRegistered')
  await page.getByRole('button', { name: 'Next' }).click()
  await page.getByRole('button', { name: 'Review' }).click()
  await page.getByRole('button', { name: 'Create account' }).click()
  // A new browser must complete the app's local privacy-lock setup before workspace routes are available.
  await expect(page.getByRole('heading', { name: 'Set up your app PIN' })).toBeVisible()
  await page.getByLabel('Create four-digit PIN').fill('1234')
  await page.getByLabel('Confirm PIN').fill('1234')
  await page.getByRole('button', { name: 'Save PIN' }).click()
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
}

/** Opens the Add product form from the Products list (the form is not shown by default). */
export async function openAddProduct(page: Page) {
  const products = page.getByRole('region', { name: 'Products' })
  await products.getByRole('button', { name: 'Add product', exact: true }).click()
  return products.getByRole('form', { name: 'Add product' })
}

/** Submit the product, expose API rejections, then verify it through catalogue search. */
export async function createProductAndVerify(page: Page, form: Locator, name: string) {
  const products = page.getByRole('region', { name: 'Products' })
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/products'
  )
  await form.getByRole('button', { name: 'Add product', exact: true }).click()
  const response = await responsePromise
  const responseBody = await response.text()
  expect(response.ok(), `POST /api/products returned ${response.status()}: ${responseBody}`).toBe(true)

  // The catalogue is paged (10 rows by default). Verify the filtered API response first,
  // then assert the same saved record is rendered in the catalogue.
  const searchResponsePromise = page.waitForResponse(candidate => {
    if (candidate.request().method() !== 'GET') return false
    const url = new URL(candidate.url())
    return url.pathname === '/api/products' && url.searchParams.get('q') === name
  })
  await products.getByRole('search').getByLabel('Search', { exact: true }).fill(name)
  const searchResponse = await searchResponsePromise
  const searchBody = await searchResponse.text()
  expect(searchResponse.ok(), `GET /api/products?q=... returned ${searchResponse.status()}: ${searchBody}`).toBe(true)
  const matches = JSON.parse(searchBody) as { name: string }[]
  expect(matches.some(product => product.name === name), `Filtered product response did not contain ${name}: ${searchBody}`).toBe(true)
  await expect(products.getByRole('cell', { name, exact: true })).toBeVisible({ timeout: 30_000 })
}

/** Category is a select: pick an existing one, otherwise choose "Other" and type a new one. */
export async function chooseCategory(form: Locator, name: string) {
  const select = form.getByLabel('Category')
  if (await select.locator('option', { hasText: name }).count()) await select.selectOption({ label: name })
  else {
    await select.selectOption('__other')
    await form.getByLabel('New category').fill(name)
  }
}

/** Posts a sale through the Generate invoice screen (/#/sell/bill). Leaves the page on the "Saved as INV-..." status. */
export async function postInvoice(page: Page, o: { product: string; qty: string; price: string; discount?: string; mode?: 'Cash' | 'UPI' | 'Credit'; paid?: string; name?: string; phone?: string }) {
  await page.goto('/#/sell/bill')
  const bill = page.getByRole('region', { name: 'Generate invoice' })
  if (o.phone) await bill.locator('#billing-phone').fill(o.phone)
  if (o.name) await bill.locator('#billing-name').fill(o.name)
  if (o.mode) await bill.locator('#billing-mode').selectOption(o.mode)
  if (o.paid) await bill.locator('#billing-paid').fill(o.paid)
  await bill.locator('#billing-prod').fill(o.product)
  await bill.locator('#billing-qty').fill(o.qty)
  await bill.locator('#billing-price').fill(o.price)
  if (o.discount) await bill.locator('#billing-disc').fill(o.discount)
  await bill.getByRole('button', { name: 'Add product', exact: true }).click()
  await bill.getByRole('button', { name: 'Generate invoice' }).click()
  await page.getByRole('button', { name: 'Confirm & print' }).click()
}
