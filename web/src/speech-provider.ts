/**
 * Speech runtime selection is deliberately separate from the assistant UI.
 *
 * Shruti runs locally in a Web Worker and remains the primary STT provider.
 * Hosted Whisper is a separate API adapter in speech-fallback.ts and is called
 * only after Shruti fails or returns no usable transcript. Its server-side key
 * and explicit enable switch are managed by the API.
 */
export type SpeechProviderId = 'shruti-local'

export interface SpeechProviderDescriptor {
  readonly id: SpeechProviderId
  readonly label: string
  readonly execution: 'on-device'
  readonly workerUrl: string
  readonly modelDownloadBytes: number
}

const providers: Record<SpeechProviderId, SpeechProviderDescriptor> = {
  'shruti-local': {
    id: 'shruti-local',
    label: 'Shruti (on-device)',
    execution: 'on-device',
    workerUrl: '/shruti-worker.js',
    modelDownloadBytes: 29_546_527,
  },
}

export const DEFAULT_SPEECH_PROVIDER: SpeechProviderId = 'shruti-local'

export function getSpeechProvider(
  id: SpeechProviderId = DEFAULT_SPEECH_PROVIDER,
): SpeechProviderDescriptor {
  return providers[id]
}

/** Create the worker for the selected provider without coupling UI to its URL. */
export function createSpeechWorker(
  id: SpeechProviderId = DEFAULT_SPEECH_PROVIDER,
): Worker {
  const provider = getSpeechProvider(id)
  return new Worker(provider.workerUrl)
}
