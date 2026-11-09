let csrf: string | null = null

async function token(): Promise<string> {
  if (!csrf) {
	const r = await fetch('/api/csrf', { credentials: 'same-origin' })
	csrf = (await r.json()).token as string
  }
  return csrf
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
	super(message)
	this.status = status
  }
}

export async function api<T>(method: string, path: string, body?: unknown, idempotencyKey?: string): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = await token()
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey
  const r = await fetch(`/api${path}`, {
	method,
	headers,
	credentials: 'same-origin',
	body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (method !== 'GET' && r.status === 400) csrf = null
  const text = await r.text()
  const data = text ? JSON.parse(text) : undefined
  if (!r.ok) {
    const d = data as { error?: string; detail?: string; title?: string } | undefined
    throw new ApiError(r.status, d?.error ?? d?.detail ?? d?.title ?? 'Request failed')
  }
  return data as T
}

export const resetCsrf = () => { csrf = null }

let otpConfig: Promise<boolean> | null = null

export function otpEnabled(): Promise<boolean> {
  otpConfig ??= fetch('/api/auth/config', { credentials: 'same-origin' })
	.then(r => (r.ok ? r.json() : { otpEnabled: false }))
	.then(d => d?.otpEnabled === true)
	.catch(() => false)
  return otpConfig
}
