import { apiBinary } from './api'

const SAMPLE_RATE = 16000
const MAX_SAMPLES = SAMPLE_RATE * 20

/** Encode mono float PCM as a 16 kHz, 16-bit PCM WAV without retaining audio bytes longer than needed. */
export function encodeNavigationWav(samples: Float32Array): Blob {
  if (samples.length === 0 || samples.length > MAX_SAMPLES) throw new Error('Voice command must be between 0 and 20 seconds.')
  const bytes = new ArrayBuffer(44 + samples.length * 2)
  const view = new DataView(bytes)
  const write = (offset: number, value: string) => {
    for (let i = 0; i < value.length; i++) view.setUint8(offset + i, value.charCodeAt(i))
  }
  write(0, 'RIFF')
  view.setUint32(4, 36 + samples.length * 2, true)
  write(8, 'WAVE')
  write(12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, SAMPLE_RATE, true)
  view.setUint32(28, SAMPLE_RATE * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  write(36, 'data')
  view.setUint32(40, samples.length * 2, true)
  let offset = 44
  for (const sample of samples) {
    const clamped = Math.max(-1, Math.min(1, sample))
    view.setInt16(offset, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true)
    offset += 2
  }
  return new Blob([bytes], { type: 'audio/wav' })
}

export async function transcribeNavigationWithWhisper(samples: Float32Array, signal?: AbortSignal): Promise<string> {
  const wav = encodeNavigationWav(samples)
  const result = await apiBinary<{ text: string }>('/voice/transcribe', wav, signal)
  return (result.text || '').trim()
}
