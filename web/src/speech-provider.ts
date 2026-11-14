/**
 * Speech runtime selection is deliberately separate from the assistant UI.
 *
 * H2 currently ships one provider: Shruti running locally in a Web Worker.
 * Cloud providers (for example Whisper API or Gemini) are not registered here:
 * they need explicit owner approval, credential handling, cost limits and a
 * privacy review before they can receive microphone audio.
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
