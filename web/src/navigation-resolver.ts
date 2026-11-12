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

const stopWords = new Set(['please','plz','the','a','an','to','me','my','for','show','open','go','take','navigate','karo','kar','kijiye','kholo','dikhao','dikha','jao','par','pe','mujhe','zara','do','hai','hain','ka','ki','ke'])
const dataQuestion = /\b(how much|how many|what is|what are|who owes|whose|balance|total|today|yesterday|kitna|kitne|kiska|kiski|kiske|batao|bataiye|amount|value|number|phone|address|sales|sold|revenue|profit|paisa kitna|stock kitna|कितना|कितने|किसका|किसकी|किसके|बताओ|बताइए|आज की बिक्री|कितनी बिक्री)\b/i
const transaction = /\b(delete|remove|cancel|make payment|pay \d|send|transfer \d|post|save|submit|bhejo|bhej do|jama karo|darj karo|हटा दो|भेजो|जमा करो|पोस्ट करो)\b/i
const ambiguous = new Map<string, string[]>([
  ['return', ['sell.return','buy.return']], ['returns', ['sell.return','buy.return']], ['wapasi', ['sell.return','buy.return']], ['वापसी', ['sell.return','buy.return']],
  ['payment', ['sell.collect','buy.pay']], ['pay', ['sell.collect','buy.pay']], ['history', ['sell.bills','buy.history','stock.history','parties.purchases','more.audit']],
  ['stock', ['stock.current','stock.adjust','stock.count']], ['udhaar', ['sell.collect','parties.dues']], ['order', ['more.orders','sell.bill']],
])
function normalise(value: string): string {
  return value.normalize('NFC').toLocaleLowerCase().replace(/[’']/g, '').replace(/[^\p{L}\p{N}\s]/gu, ' ').replace(/\s+/g, ' ').trim()
    .replace(/उधार/g, 'udhar').replace(/बिल/g, 'bill').replace(/माल/g, 'maal').replace(/सामान/g, 'saman').replace(/खरीद/g, 'kharid').replace(/वापस/g, 'wapas').replace(/खर्च/g, 'kharch').replace(/स्टॉक/g, 'stock')
}
function tokens(value: string): string[] { return normalise(value).split(' ').filter(t => t && !stopWords.has(t)) }
function similarity(query: string[], phrase: string[]): number {
  if (!query.length || !phrase.length) return 0
  const q = new Set(query), p = new Set(phrase)
  const overlap = [...q].filter(x => p.has(x)).length
  const queryCoverage = overlap / q.size
  const phraseCoverage = overlap / p.size
  return queryCoverage * 0.65 + phraseCoverage * 0.35
}
export function resolveNavigation(input: string): NavigationResolution {
  const q = normalise(input)
  if (!q) return { kind: 'not-understood' }
  const hindiDataTerms = ['कितना','कितने','किसका','किसकी','किसके','बताओ','बताइए','आज की बिक्री','कितनी बिक्री','मेरे पास','मेरी दुकान','मेरा बकाया']
  if (/(https?:\\/\\/|www\\.|[a-z0-9-]+\\.(com|in|org|net|io)\\b)/i.test(input) || dataQuestion.test(q) || hindiDataTerms.some(term => input.includes(term)) || transaction.test(q) || /\\d/.test(q)) return { kind: 'reject' }
  const qTokens = tokens(q)
  if (qTokens.length === 1 && ambiguous.has(qTokens[0])) {
    const ids = ambiguous.get(qTokens[0])!
    return { kind: 'ask', targets: ids.map(id => navigationTargets.find(t => t.id === id)!).filter(Boolean) }
  }
  const ranked = Object.entries(navigationAnchors).map(([id, phrases]) => ({
    id, score: Math.max(...phrases.map(p => similarity(qTokens, tokens(p)))),
  })).sort((a,b) => b.score - a.score || a.id.localeCompare(b.id))
  const top = ranked[0], second = ranked[1]
  if (!top || top.score < 0.58) return { kind: 'not-understood' }
  const candidates = ranked.filter(x => top.score - x.score < 0.12 && x.score >= 0.48).slice(0,3)
  if (candidates.length > 1 || (second && top.score - second.score < 0.12)) {
    return { kind: 'ask', targets: candidates.map(x => navigationTargets.find(t => t.id === x.id)!).filter(Boolean) }
  }
  const target = navigationTargets.find(t => t.id === top.id)
  return target ? { kind: 'navigate', target } : { kind: 'not-understood' }
}
