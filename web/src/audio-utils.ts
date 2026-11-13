const DEFAULT_TARGET_RATE = 16000
const SINC_RADIUS = 32

function sinc(value: number): number {
 if (Math.abs(value) < 1e-8) return 1
 const angle = Math.PI * value
 return Math.sin(angle) / angle
}

/**
 * Convert mono Float32 PCM to the target sample rate.
 * Downsampling uses a windowed-sinc low-pass filter to reduce aliasing.
 */
export function resampleAudio(input: Float32Array, sourceRate: number, targetRate = DEFAULT_TARGET_RATE): Float32Array {
 if (!Number.isFinite(sourceRate) || sourceRate <= 0 || !Number.isFinite(targetRate) || targetRate <= 0) {
  throw new RangeError('Sample rates must be positive finite numbers.')
 }
 if (input.length === 0) return new Float32Array(0)
 if (sourceRate === targetRate) return input.slice()

 const outputLength = Math.floor(input.length * targetRate / sourceRate)
 const output = new Float32Array(outputLength)
 const ratio = sourceRate / targetRate

 if (sourceRate < targetRate) {
  for (let i = 0; i < outputLength; i++) {
   const position = i * ratio
   const left = Math.floor(position)
   const fraction = position - left
   const right = Math.min(left + 1, input.length - 1)
   output[i] = input[left] * (1 - fraction) + input[right] * fraction
  }
  return output
 }

 const cutoff = (targetRate / sourceRate) * 0.94
 for (let i = 0; i < outputLength; i++) {
  const centre = i * ratio
  const first = Math.max(0, Math.ceil(centre - SINC_RADIUS))
  const last = Math.min(input.length - 1, Math.floor(centre + SINC_RADIUS))
  let weighted = 0
  let weightTotal = 0

  for (let sourceIndex = first; sourceIndex <= last; sourceIndex++) {
   const distance = sourceIndex - centre
   const window = 0.5 + 0.5 * Math.cos(Math.PI * distance / SINC_RADIUS)
   const weight = cutoff * sinc(cutoff * distance) * window
   weighted += input[sourceIndex] * weight
   weightTotal += weight
  }

  output[i] = weightTotal === 0 ? 0 : weighted / weightTotal
 }
 return output
}
