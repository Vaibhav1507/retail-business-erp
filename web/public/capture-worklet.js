/* Push-to-talk microphone capture. Samples stay in this browser tab. */
class RbosCapture extends AudioWorkletProcessor {
 constructor() { super(); this.block = new Float32Array(4096); this.count = 0; this.running = true; this.port.onmessage = e => { if (e.data === 'stop') { this.flush(); this.running = false } } }
 flush() { if (!this.count) return; const output = this.block.slice(0, this.count); this.port.postMessage(output, [output.buffer]); this.count = 0 }
 process(inputs) {
  const channels = inputs[0]
  if (this.running && channels?.length) {
   for (let i = 0; i < channels[0].length; i++) {
    let sample = 0; for (const channel of channels) sample += channel[i] / channels.length
    this.block[this.count++] = sample
    if (this.count === this.block.length) { const output = this.block; this.port.postMessage(output, [output.buffer]); this.block = new Float32Array(4096); this.count = 0 }
   }
  }
  return this.running
 }
}
registerProcessor('rbos-capture', RbosCapture)
