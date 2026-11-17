import { api, resetCsrf } from './api'

type JsonDescriptor = { type: string; id: string; transports?: string[] }
type JsonCreationOptions = Record<string, unknown> & {
  challenge: string
  user: Record<string, unknown> & { id: string }
  excludeCredentials?: JsonDescriptor[]
}
type JsonRequestOptions = Record<string, unknown> & {
  challenge: string
  allowCredentials?: JsonDescriptor[]
}

function decodeBase64Url(value: string): Uint8Array {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64 + '='.repeat((4 - base64.length % 4) % 4)
  return Uint8Array.from(atob(padded), char => char.charCodeAt(0))
}

function encodeBase64Url(value: ArrayBuffer): string {
  const bytes = new Uint8Array(value)
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function decodeCreationOptions(raw: JsonCreationOptions): PublicKeyCredentialCreationOptions {
  return {
    ...raw,
    challenge: decodeBase64Url(raw.challenge),
    user: { ...raw.user, id: decodeBase64Url(raw.user.id) },
    excludeCredentials: raw.excludeCredentials?.map(item => ({ ...item, id: decodeBase64Url(item.id) }))
  } as unknown as PublicKeyCredentialCreationOptions
}

function decodeRequestOptions(raw: JsonRequestOptions): PublicKeyCredentialRequestOptions {
  return {
    ...raw,
    challenge: decodeBase64Url(raw.challenge),
    allowCredentials: raw.allowCredentials?.map(item => ({ ...item, id: decodeBase64Url(item.id) }))
  } as unknown as PublicKeyCredentialRequestOptions
}

function credentialJson(credential: PublicKeyCredential): string {
  const response = credential.response
  let encodedResponse: Record<string, unknown>
  if (response instanceof AuthenticatorAttestationResponse) {
    encodedResponse = {
      clientDataJSON: encodeBase64Url(response.clientDataJSON),
      attestationObject: encodeBase64Url(response.attestationObject),
      transports: response.getTransports?.() ?? []
    }
  } else if (response instanceof AuthenticatorAssertionResponse) {
    encodedResponse = {
      clientDataJSON: encodeBase64Url(response.clientDataJSON),
      authenticatorData: encodeBase64Url(response.authenticatorData),
      signature: encodeBase64Url(response.signature),
      userHandle: response.userHandle ? encodeBase64Url(response.userHandle) : null
    }
  } else {
    throw new Error('The browser returned an unsupported passkey response.')
  }
  return JSON.stringify({
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment,
    clientExtensionResults: credential.getClientExtensionResults(),
    response: encodedResponse
  })
}

function ensureSupported() {
  if (!window.isSecureContext || !navigator.credentials || !window.PublicKeyCredential) {
    throw new Error('Passkeys require a supported browser and a secure HTTPS connection.')
  }
}

export async function passkeyEnabled(): Promise<boolean> {
  try {
    const result = await api<{ enabled: boolean }>('GET', '/auth/passkeys/config')
    return result.enabled && !!window.isSecureContext && !!navigator.credentials && !!window.PublicKeyCredential
  } catch { return false }
}

export async function signInWithPasskey(): Promise<void> {
  ensureSupported()
  const raw = await api<JsonRequestOptions>('POST', '/auth/passkeys/request-options', {})
  const credential = await navigator.credentials.get({ publicKey: decodeRequestOptions(raw) })
  if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey sign-in was cancelled or not completed.')
  await api('POST', '/auth/passkeys/sign-in', { credentialJson: credentialJson(credential) })
  resetCsrf()
}

export async function registerPasskey(name: string, password: string): Promise<void> {
  ensureSupported()
  const raw = await api<JsonCreationOptions>('POST', '/auth/passkeys/creation-options', { password })
  const credential = await navigator.credentials.create({ publicKey: decodeCreationOptions(raw) })
  if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey registration was cancelled or not completed.')
  await api('POST', '/auth/passkeys/register', { credentialJson: credentialJson(credential), name })
}

export type PasskeySummary = {
  credentialId: string
  name: string | null
  createdAt: string
  isBackedUp: boolean
  isBackupEligible: boolean
  transports: string[]
}

export const listPasskeys = () => api<PasskeySummary[]>('GET', '/auth/passkeys')
export const removePasskey = (credentialId: string, password: string) => api('DELETE', '/auth/passkeys/' + encodeURIComponent(credentialId), { password })
