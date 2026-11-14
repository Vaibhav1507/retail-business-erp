import { navigationKeywordProfiles, navigationStopWords, navigationWordAliases } from './navigation-keywords'

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

const targetById = new Map(navigationTargets.map(target => [target.id, target]))

const ambiguousTerms: Record<string, string[]> = {
  return: ['sell.return', 'buy.return'],
  payment: ['sell.collect', 'buy.pay'],
  bill: ['sell.bill', 'sell.bills'],
  purchase: ['buy.receive', 'buy.history', 'buy.return'],
  history: ['sell.bills', 'buy.history', 'stock.history', 'parties.purchases', 'more.audit'],
  stock: ['stock.current', 'stock.adjust', 'stock.count'],
  goods: ['stock.current', 'buy.receive', 'more.transfers'],
  udhar: ['sell.collect', 'parties.dues'],
  baki: ['sell.collect', 'parties.dues'],
  money: ['money.cash', 'money.expenses', 'money.owner'],
}

function normalise(value: string): string {
  return value.normalize('NFC').toLocaleLowerCase()
    .replace(/[’']/g, '')
    .replace(/[^\p{L}\p{N}\s]/gu, ' ')
    .replace(/\s+/g, ' ').trim()
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

/**
 * Conservative romanised-Hindi/Bundeli spelling folds:
 * - repeated vowels: graahak -> grahak
 * - dropped aspiration: kharid -> karid
 * - final a/o variation: kharcha <-> kharcho
 *
 * These are alternate comparison forms only. They never rewrite the transcript
 * shown to the user and never bypass the ambiguity or safety checks.
 */
function romanForms(token: string): string[] {
  if (!/^[a-z]+$/.test(token)) return [token]
  const compact = token.replace(/([aeiou])\1+/g, '$1')
  const unaspirated = compact
    .replace(/chh/g, 'ch')
    .replace(/kh/g, 'k').replace(/gh/g, 'g').replace(/th/g, 't')
    .replace(/dh/g, 'd').replace(/ph/g, 'p').replace(/bh/g, 'b')
  const forms = new Set([token, compact, unaspirated])
  if (compact.length >= 5 && /[ao]$/.test(compact)) {
    forms.add(compact.slice(0, -1) + (compact.endsWith('a') ? 'o' : 'a'))
  }
  return [...forms]
}

function canonicalToken(token: string): string {
  const direct = navigationWordAliases[token]
  if (direct) return direct
  for (const form of romanForms(token)) {
    const alias = navigationWordAliases[form]
    if (alias) return alias
  }
  return romanForms(token).sort((a, b) => a.length - b.length)[0]
}

function tokenize(value: string): string[] {
  return normalise(value).split(' ')
    .map(canonicalToken)
    .filter(token => token && !navigationStopWords.has(token))
}

function tokenMatches(queryToken: string, keyword: string): boolean {
  if (queryToken === keyword) return true
  if (!/^[a-z]+$/.test(queryToken) || !/^[a-z]+$/.test(keyword)) return false
  const queryForms = romanForms(queryToken)
  const keywordForms = romanForms(keyword)
  for (const a of queryForms) {
    for (const b of keywordForms) {
      if (a === b) return true
      const minimum = Math.min(a.length, b.length)
      if (minimum >= 5 && editDistance(a, b) <= (Math.max(a.length, b.length) >= 9 ? 2 : 1)) return true
    }
  }
  return false
}

type RankedTarget = { id: string; score: number; matchedGroups: number }

function rankTargets(queryTokens: string[], allowPartial = false): RankedTarget[] {
  return Object.entries(navigationKeywordProfiles).map(([id, profile]) => {
    if (profile.excludes?.some(excluded => queryTokens.some(token => tokenMatches(token, excluded)))) {
      return { id, score: 0, matchedGroups: 0 }
    }
    let matchedGroups = 0
    let matchedTokens = 0
    for (const group of profile.groups) {
      const matches = queryTokens.filter(token => group.some(keyword => tokenMatches(token, keyword)))
      if (matches.length > 0) {
        matchedGroups++
        matchedTokens += Math.min(matches.length, 2)
      } else if (!allowPartial) {
        return { id, score: 0, matchedGroups: 0 }
      }
    }
    // Required concepts carry most of the score. Extra matching vocabulary is
    // a small tie-breaker, never enough to overrule a missing required concept.
    const score = matchedGroups * 4 + matchedTokens * 0.15
    return { id, score, matchedGroups }
  }).filter(item => allowPartial ? item.matchedGroups > 0 : item.matchedGroups === navigationKeywordProfiles[item.id].groups.length)
    .sort((a, b) => b.score - a.score || a.id.localeCompare(b.id))
}

function askFor(ids: string[]): NavigationResolution {
  return {
    kind: 'ask',
    targets: ids.map(id => targetById.get(id)).filter((target): target is NavigationTarget => Boolean(target)),
  }
}

const questionIntent = /\\b(how much|how many|what is|what are|who owes|whose|which customer|kitna|kitne|kitni|kiska|kiski|kiske|kisne|kaun|kya|kab|kahan|batao|bataiye)\\b|कितना|कितनी|कितने|किसका|किसकी|किसके|किसने|कौन|क्या|कब|कहाँ|बताओ|बताइए/i
const dataTerms = /\\b(balance|amount|total|revenue|profit|sales today|today sales|phone|address|number|baki|udhar|cash|stock|maal|goods|customer|supplier|sold|quantity|rate|price)\\b|बिक्री|बिकरी|पैसा|रुपया|स्टॉक|माल|ग्राहक|सप्लायर|भाव|दाम/i
const directDataRequest = /\\b(who owes|how much|how many|phone number|contact number|business address|total sales|sales today|today sales)\\b|किसका कितना|कितना बाकी|कितनी बिक्री|आज की बिक्री|कितना पैसा|कौन सा ग्राहक/i
const actionRequest = /\\b(delete|remove|cancel|save|submit|post|send|bhejo|bhej do)\\b|\\b(pay|transfer|send)\\s+\\d+|\\b(jama|bhej|bhejo)\\s+(kar|karo|do)\\b|\\b(paise|paisa|payment)\\s+(jama|bhej|send|de do)\\b|हटा दो|भेजो|जमा करो|जमा कर दो|पोस्ट करो|भुगतान कर दो/i
const namedPersonReference = /\\b[a-z][a-z'-]{2,}\\s+(?:ji|bhai|bhaiya|sahab)\\b/i

function isRejectedRequest(input: string, normalized: string): boolean {
  if (/https?:\\/\\/|www\\./i.test(input) || /\\b[a-z0-9-]+\\.(?:com|in|org|net|io)(?=$|[\\s/?#])/i.test(input)) return true
  if (/\\d/.test(normalized) || actionRequest.test(normalized)) return true
  if (directDataRequest.test(normalized)) return true
  return questionIntent.test(normalized) && dataTerms.test(normalized)
}

export function resolveNavigation(input: string): NavigationResolution {
  const q = normalise(input)
  if (!q) return { kind: 'not-understood' }
  if (isRejectedRequest(input, q)) return { kind: 'reject' }
  // A named customer/supplier reference is a lookup-like utterance. It can
  // suggest the relevant screen, but generic navigation must not discard the name.
  if (namedPersonReference.test(input)) return { kind: 'not-understood' }

  const qTokens = tokenize(q)
  if (!qTokens.length) return { kind: 'not-understood' }

  if (qTokens.length === 1 && ambiguousTerms[qTokens[0]]) {
    return askFor(ambiguousTerms[qTokens[0]])
  }

  const ranked = rankTargets(qTokens)
  if (ranked.length === 0) return { kind: 'not-understood' }

  const top = ranked[0]
  const second = ranked[1]
  if (second && top.score - second.score < 1.0) {
    return askFor(ranked.filter(item => top.score - item.score < 1.0).slice(0, 3).map(item => item.id))
  }

  const target = targetById.get(top.id)
  return target ? { kind: 'navigate', target } : { kind: 'not-understood' }
}

/**
 * Suggestions intentionally allow incomplete keywords but are never executed.
 * A suggestion only appears when at least one meaningful group has a match.
 */
export function suggestNavigation(input: string): NavigationTarget[] {
  const q = normalise(input)
  if (!q || q.length < 3 || isRejectedRequest(input, q)) return []
  const queryTokens = tokenize(q)
  if (!queryTokens.length) return []
  if (queryTokens.length === 1 && ambiguousTerms[queryTokens[0]]) {
    return ambiguousTerms[queryTokens[0]].map(id => targetById.get(id)).filter((target): target is NavigationTarget => Boolean(target))
  }
  return rankTargets(queryTokens, true).slice(0, 3)
    .map(item => targetById.get(item.id))
    .filter((target): target is NavigationTarget => Boolean(target))
}
