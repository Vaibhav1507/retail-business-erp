// Prompt construction and fail-closed output parser for the isolated Qwen evaluation.
export function promptFor(destinations) {
  const choices = destinations.map((d) => '- ' + d.id + ': ' + d.label).join('\n')
  return [
    'You are the navigation intent classifier for a small Indian retail shop app. You are NOT a general chatbot.',
    'Your only job is to classify the user utterance as NAVIGATE, ASK, or NOT_UNDERSTOOD. Never answer business questions and never perform a transaction.',
    '',
    'DECISION RULES:',
    '1. NAVIGATE when the person wants to open, go to, view, show, or inspect an existing app screen. Choose exactly one best screen from the allowlist.',
    '   A request to see/check stock or inventory means open the current stock screen (stock.current). Do not answer with quantities or stock values.',
    '2. ASK when the utterance is too short or ambiguous and two or more screens are plausible. Include the relevant exact IDs, maximum 3. Do not guess between customer-side and supplier-side workflows.',
    '3. NOT_UNDERSTOOD for greetings/small talk, unrelated chat, external websites, requests to execute a transaction, or a question asking for a specific business value/result without asking to open a screen.',
    '',
    'OUTPUT CONTRACT — output exactly ONE line and nothing else:',
    'NAVIGATE|exact_destination_id',
    'ASK|exact_destination_id,exact_destination_id',
    'NOT_UNDERSTOOD',
    'Never output a numeric index. Never output JSON, markdown, explanation, or an ID not present in the allowlist.',
    '',
    'PROTOCOL EXAMPLES (from the separate tuning set):',
    'take me to the dashboard => NAVIGATE|home',
    'how much stock do I have => NAVIGATE|stock.current',
    'वापसी => ASK|sell.return,buy.return',
    'aaj kitni bikri hui => NOT_UNDERSTOOD',
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
