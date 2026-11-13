import { navigationAnchors } from './navigation-anchors'

export type NavigationTarget = { id: string; hash: string; label: string }
export type NavigationResolution =
  | { kind: 'navigate'; target: NavigationTarget }
  | { kind: 'ask'; targets: NavigationTarget[] }
  | { kind: 'reject' }
  | { kind: 'not-understood' }

export const navigationTargets: NavigationTarget[] = [
  { id: 'home', hash: '#/home', label: 'Home' },
  { id: 'sell.menu', hash: '#/sell/menu', label: 'Sell menu' },
  { id: 'sell.bill', hash: '#/sell/bill', label: 'New sale bill' },
  { id: 'sell.bills', hash: '#/sell/bills', label: 'Old sale bills' },
  { id: 'sell.collect', hash: '#/sell/collect', label: 'Collect customer payment' },
  { id: 'sell.return', hash: '#/sell/return', label: 'Customer return' },
  { id: 'buy.receive', hash: '#/buy/receive', label: 'Add purchase' },
  { id: 'buy.pay', hash: '#/buy/pay', label: 'Pay supplier' },
  { id: 'buy.history', hash: '#/buy/history', label: 'Purchase history' },
  { id: 'buy.return', hash: '#/buy/return', label: 'Return to supplier' },
  { id: 'stock.current', hash: '#/stock/current', label: 'Current stock' },
  { id: 'stock.products', hash: '#/stock/products', label: 'Products' },
  { id: 'stock.opening', hash: '#/stock/opening', label: 'Opening stock' },
  { id: 'stock.import', hash: '#/stock/import', label: 'Import products' },
  { id: 'stock.adjust', hash: '#/stock/adjust', label: 'Adjust stock' },
  { id: 'stock.count', hash: '#/stock/count', label: 'Stock count' },
  { id: 'stock.history', hash: '#/stock/history', label: 'Stock adjustment history' },
  { id: 'stock.alerts', hash: '#/stock/alerts', label: 'Stock alerts' },
  { id: 'stock.units', hash: '#/stock/units', label: 'Units' },
  { id: 'parties.customers', hash: '#/parties/customers', label: 'Customers' },
  { id: 'parties.suppliers', hash: '#/parties/suppliers', label: 'Suppliers' },
  { id: 'parties.purchases', hash: '#/parties/purchases', label: 'Customer purchase history' },
  { id: 'parties.dues', hash: '#/parties/dues', label: 'Customer dues' },
  { id: 'money.cash', hash: '#/money/cash', label: 'Cash and bank' },
  { id: 'money.expenses', hash: '#/money/expenses', label: 'Expenses' },
  { id: 'money.owner', hash: '#/money/owner', label: 'Owner money' },
  { id: 'money.closing', hash: '#/money/closing', label: 'Day closing' },
  { id: 'more.menu', hash: '#/more', label: 'More menu' },
  { id: 'more.reports', hash: '#/more/reports', label: 'Reports' },
  { id: 'more.print', hash: '#/more/print', label: 'Print documents' },
  { id: 'more.lock', hash: '#/more/lock', label: 'Lock period' },
  { id: 'more.audit', hash: '#/more/audit', label: 'Audit history' },
  { id: 'more.gst', hash: '#/more/gst', label: 'GST' },
  { id: 'more.staff', hash: '#/more/staff', label: 'Staff access' },
  { id: 'more.branches', hash: '#/more/branches', label: 'Branches' },
  { id: 'more.transfers', hash: '#/more/transfers', label: 'Stock transfers' },
  { id: 'more.orders', hash: '#/more/orders', label: 'Purchase orders' },
  { id: 'more.reconcile', hash: '#/more/reconcile', label: 'Bank reconciliation' },
  { id: 'more.loans', hash: '#/more/loans', label: 'Loans' },
  { id: 'settings', hash: '#/settings', label: 'Business settings' },
]

const stopWords = new Set([
  'please','plz','the','a','an','to','me','my','for','show','open','go','take','navigate','screen','page',
  'karo','kar','kijiye','kholo','dikhao','dikha','jao','par','pe','mujhe','zara','do','hai','hain','ka','ki','ke','ko',
  'mera','meri','mere','banao','banana','banani','banaye','banaiye','karna','chahiye','wala','wali','wale',
  'please','ji','ab','ek','aur','se','ko','करनी','करना','करते','करती','हुआ','हुई'
])

const dataQuestion = /\b(how much|how many|what is|what are|who owes|whose|balance|total|today|yesterday|kitna|kitne|kiska|kiski|kiske|batao|bataiye|amount|value|number|phone|address|sales|sold|revenue|profit|paisa kitna|stock kitna|कितना|कितने|किसका|किसकी|किसके|बताओ|बताइए|आज की बिक्री|कितनी बिक्री)\b/i
const transaction = /\b(delete|remove|cancel|make payment|pay \d|send|transfer \d|post|save|submit|bhejo|bhej do|jama karo|darj karo|हटा दो|भेजो|जमा करो|पोस्ट करो)\b/i

const canonicalWords: Record<string, string> = {
  'ग्राहक':'customer','ग्राहकों':'customer','ग्रहक':'customer','कस्टमर':'customer','grahak':'customer','graahak':'customer','custmer':'customer','costumer':'customer',
  'सप्लायर':'supplier','विक्रेता':'supplier','supplier':'supplier','suplier':'supplier','saplayer':'supplier',
  'बिल':'bill','बिलिंग':'bill','चालान':'bill','इनवॉइस':'bill','invoice':'bill','bll':'bill',
  'स्टॉक':'stock','माल':'stock','सामान':'stock','इन्वेंटरी':'stock','inventory':'stock','stok':'stock','stokh':'stock',
  'खरीद':'purchase','खरीदारी':'purchase','खरीदी':'purchase','परचेज':'purchase','purchase':'purchase','purches':'purchase',
  'खर्च':'expense','खर्चा':'expense','व्यय':'expense','expense':'expense','expence':'expense',
  'वापसी':'return','रिटर्न':'return','वापस':'return','return':'return',
  'पुराना':'past','पुराने':'past','पिछला':'past','पिछली':'past','history':'history','इतिहास':'history',
  'पैसा':'payment','पैसे':'payment','भुगतान':'payment','पेमेंट':'payment','payment':'payment',
  'जोड़':'add','जोड़ो':'add','जोड़ना':'add','बनाओ':'create','बनाना':'create','बनानी':'create','बनाइए':'create','बनाए':'create','create':'create','add':'add',
  'देखो':'view','दिखाओ':'view','दिखाना':'view','दिखाइए':'view','खोलो':'view','खोलना':'view','देखना':'view','show':'view','check':'view','view':'view',
  'सूची':'list','लिस्ट':'list','list':'list','खाता':'ledger','बही':'ledger','उधार':'dues','बकाया':'dues',
  'एंट्री':'entry','प्रविष्टि':'entry','entry':'entry','नया':'new','नई':'new','new':'new','मौजूदा':'current','current':'current',
  'से':'','का':'','की':'','के':'','को':'','है':'','हैं':'','मुझे':'','मेरे':'','मेरी':'','मेरा':''
}

function normalise(value: string): string {
  return value.normalize('NFC').toLocaleLowerCase()
    .replace(/[’']/g, '')
    .replace(/[^\p{L}\p{N}\s]/gu, ' ')
    .replace(/\s+/g, ' ').trim()
}

function canonicalToken(token: string): string {
  const direct = canonicalWords[token]
  if (direct !== undefined) return direct
  // Common ASR spellings: normalize elongated vowels without changing short words.
  return token.length > 5 ? token.replace(/([aeiou])\1+/g, '$1') : token
}

function tokens(value: string): string[] {
  return normalise(value).split(' ')
    .map(canonicalToken)
    .filter(token => token && !stopWords.has(token))
}

function editDistance(a: string, b: string): number {
  if (a === b) return 0
  if (Math.abs(a.length - b.length) > 2) return 3
  let previous = Array.from({ length: b.length + 1 }, (_, i) => i)
  for (let i = 1; i <= a.length; i++) {
    const current = [i]
    for (let j = 1; j <= b.length; j++) {
      current[j] = Math.min(current[j - 1] + 1, previous[j] + 1, previous[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1))
    }
    previous = current
  }
  return previous[b.length]
}

function tokenMatches(a: string, b: string): boolean {
  if (a === b) return true
  if (a.length < 4 || b.length < 4) return false
  const limit = Math.max(a.length, b.length) >= 8 ? 2 : 1
  return editDistance(a, b) <= limit
}

function phraseScore(query: string[], phrase: string[]): number {
  if (!query.length || !phrase.length) return 0
  const matchedQuery = new Set<number>()
  const matchedPhrase = new Set<number>()
  query.forEach((word, qi) => {
    const candidates = phrase.map((candidate, pi) => ({ pi, distance: editDistance(word, candidate) }))
      .filter(({ pi, distance }) => !matchedPhrase.has(pi) && tokenMatches(word, phrase[pi]) && distance <= 2)
      .sort((a, b) => a.distance - b.distance)
    if (candidates[0]) {
      matchedQuery.add(qi)
      matchedPhrase.add(candidates[0].pi)
    }
  })
  const queryCoverage = matchedQuery.size / query.length
  const phraseCoverage = matchedPhrase.size / phrase.length
  const extraPenalty = Math.max(0, query.length - matchedQuery.size) / query.length
  return queryCoverage * 0.72 + phraseCoverage * 0.28 - extraPenalty * 0.12
}

const ambiguous = new Map<string, string[]>([
  ['return', ['sell.return','buy.return']],
  ['payment', ['sell.collect','buy.pay']],
  ['history', ['sell.bills','buy.history','stock.history','parties.purchases','more.audit']],
  ['stock', ['stock.current','stock.adjust','stock.count']],
  ['dues', ['sell.collect','parties.dues']],
  ['order', ['more.orders','sell.bill']],
])

export function resolveNavigation(input: string): NavigationResolution {
  const q = normalise(input)
  if (!q) return { kind: 'not-understood' }
  if (input.toLowerCase().includes('http://') || input.toLowerCase().includes('https://') ||
      input.toLowerCase().includes('www.') || /\b[a-z0-9-]+\.(com|in|org|net|io)\b/i.test(input) ||
      dataQuestion.test(q) || ['कितना','कितनी','कितने','किसका','किसकी','किसके','बताओ','बताइए','आज की बिक्री','कितनी बिक्री','मेरे पास','मेरी दुकान','मेरा बकाया','खाता कितना','कितना हुआ','कितना बाकी'].some(term => q.includes(term)) || transaction.test(q) || ['हटा दो','भेजो','जमा करो','पोस्ट करो','भुगतान करो'].some(term => q.includes(term)) || /\d/.test(q)) return { kind: 'reject' }

  const qTokens = tokens(q)
  if (!qTokens.length) return { kind: 'not-understood' }
  if (qTokens.length === 1 && ambiguous.has(qTokens[0])) {
    return { kind: 'ask', targets: ambiguous.get(qTokens[0])!.map(id => navigationTargets.find(t => t.id === id)!).filter(Boolean) }
  }

  const ranked = Object.entries(navigationAnchors).map(([id, phrases]) => ({
    id,
    score: Math.max(...phrases.map(phrase => phraseScore(qTokens, tokens(phrase)))),
  })).sort((a, b) => b.score - a.score || a.id.localeCompare(b.id))

  const top = ranked[0]
  const second = ranked[1]
  if (!top || top.score < 0.54) return { kind: 'not-understood' }

  // Require both a useful match and a margin. If the words point to multiple
  // screens, ask rather than silently sending the shopkeeper to the wrong place.
  const candidates = ranked.filter(item => top.score - item.score < 0.09 && item.score >= 0.48).slice(0, 3)
  if (candidates.length > 1 || (second && top.score - second.score < 0.09)) {
    return { kind: 'ask', targets: candidates.map(item => navigationTargets.find(t => t.id === item.id)!).filter(Boolean) }
  }
  const target = navigationTargets.find(item => item.id === top.id)
  return target ? { kind: 'navigate', target } : { kind: 'not-understood' }
}
