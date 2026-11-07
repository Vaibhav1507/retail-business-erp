const ones = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen']
const tens = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety']

function below100(n: number) {
  return n < 20 ? ones[n] : `${tens[Math.floor(n / 10)]}${n % 10 ? ' ' + ones[n % 10] : ''}`
}

function below1000(n: number) {
  const h = Math.floor(n / 100)
  const r = n % 100
  return [h ? `${ones[h]} Hundred` : '', r ? below100(r) : ''].filter(Boolean).join(' ')
}

export function amountInWords(amount: number): string {
  const rupees = Math.floor(Math.abs(amount))
  const paise = Math.round((Math.abs(amount) - rupees) * 100)
  if (rupees === 0 && paise === 0) return 'Zero Rupees Only'
  const parts: [number, string][] = [
    [Math.floor(rupees / 10000000), 'Crore'],
    [Math.floor(rupees / 100000) % 100, 'Lakh'],
    [Math.floor(rupees / 1000) % 100, 'Thousand'],
  ]
  const words = parts.filter(([n]) => n > 0).map(([n, label]) => `${below100(n)} ${label}`)
  if (rupees % 1000) words.push(below1000(rupees % 1000))
  const rupeeText = rupees ? `${words.join(' ')} Rupees` : ''
  const paiseText = paise ? `${below100(paise)} Paise` : ''
  return `${[rupeeText, paiseText].filter(Boolean).join(' and ')} Only`
}
