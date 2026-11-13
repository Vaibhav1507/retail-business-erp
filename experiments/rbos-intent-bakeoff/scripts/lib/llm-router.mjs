// Prompt construction and fail-closed output parser for the isolated Qwen evaluation.
export function promptFor(destinations) {
  const choices = destinations.map((d) => '- ' + d.id + ': ' + d.label).join('\n')
  return [
    'You are the navigation intent classifier for a small Indian retail shop app. You are NOT a general chatbot.',
    'Your only job is to classify the user utterance as NAVIGATE, ASK, or NOT_UNDERSTOOD. Never answer business questions and never perform a transaction.',
    '',
    'DECISION RULES:',
    'DECISION ORDER (apply in this order; do not jump straight to the closest screen):',
    'A. NOT_UNDERSTOOD: if the user asks for an actual business result/value such as today sales total, revenue, profit, cash amount, or a customer balance. They are asking you to retrieve data, not open a screen. Also reject chat, unrelated topics, external websites, and transaction execution.',
    'B. ASK: if the wording is too short or multiple workflows are plausible. A generic return/return-entry request can mean a customer return OR a supplier return; ask and include both exact IDs. Never choose the first matching route just because it appears first in the list.',
    'C. NAVIGATE: only when one specific existing app screen is clearly requested. Choose exactly one exact ID from the allowlist.',
    '   Inventory exception: a request to see/check stock or inventory means open stock.current; do not answer with quantities or stock values.',
    '',
    'OUTPUT CONTRACT — output exactly ONE line and nothing else:',
    'NAVIGATE|exact_destination_id',
    'ASK|exact_destination_id,exact_destination_id',
    'NOT_UNDERSTOOD',
    'Never output a numeric index. Never output JSON, markdown, explanation, or an ID not present in the allowlist.',
    'If the utterance is a bare generic workflow word and multiple allowlisted screens fit, ASK rather than NAVIGATE. Never use the first allowlist entry as a default.',
    '',
    'PROTOCOL EXAMPLES (from the separate tuning set):',
    'take me to the dashboard => NAVIGATE|home',
    'how much stock do I have => NAVIGATE|stock.current',
    'वापसी => ASK|sell.return,buy.return',
    'payment => ASK|sell.collect,buy.pay',
    'aaj kitni bikri hui => NOT_UNDERSTOOD',
    'how much stock do I have => NAVIGATE|stock.current',
    'tell me a joke => NOT_UNDERSTOOD',
    '',
    'VERIFIED DESTINATION ALLOWLIST:',
    choices
  ].join('\n')
}

export function parseDecision(output, allowed) {
  const lines = (typeof output === 'string' ? output : '')
    .replace(/<\|im_end\|>\s*$/u, '')
    .split(/\r?\n/u)
    .map((line) => line.trim())
    .filter(Boolean)
  const line = lines.at(-1) ?? ''
  const navigate = line.match(/^NAVIGATE\|([a-z][a-z0-9._-]*)$/iu)
  if (navigate && allowed.has(navigate[1])) {
    return { kind: 'navigate', ids: [navigate[1]], reason: 'model_navigate' }
  }
  const ask = line.match(/^ASK\|([a-z][a-z0-9._-]*(?:\s*,\s*[a-z][a-z0-9._-]*){1,2})$/iu)
  if (ask) {
    const ids = [...new Set(ask[1].split(',').map((id) => id.trim()))]
    if (ids.length >= 2 && ids.every((id) => allowed.has(id))) {
      return { kind: 'ask', ids, reason: 'model_ask' }
    }
  }
  if (line === 'NOT_UNDERSTOOD') return { kind: 'not_understood', ids: [], reason: 'model_reject' }
  return { kind: 'not_understood', ids: [], reason: 'invalid_model_protocol' }
}

export function chatMessages(text, destinations) {
  return [
    { role: 'system', content: promptFor(destinations) },
    { role: 'user', content: text }
  ]
}
