export type ImportRow = {
  name: string
  categoryName: string
  isGeneric: boolean
  baseUnit: string
  brand: string | null
  model: string | null
  attributes: Record<string, string>
  specs: Record<string, string>
}

export const FIELDS = [
  { key: 'name', label: 'Name', required: true },
  { key: 'category', label: 'Category', required: true },
  { key: 'unit', label: 'Unit (pcs/set/pack/m)', required: false },
  { key: 'brand', label: 'Brand', required: false },
  { key: 'model', label: 'Model', required: false },
  { key: 'colour', label: 'Colour', required: false },
  { key: 'capacity', label: 'Capacity', required: false },
  { key: 'connector_type', label: 'Connector type', required: false },
  { key: 'compatible_with', label: 'Compatible with', required: false },
] as const

export type FieldKey = (typeof FIELDS)[number]['key']
export type Mapping = Partial<Record<FieldKey, number>>

export const MAX_ROWS = 200

/** Minimal RFC 4180 parser: quoted fields, escaped quotes, CRLF/LF, commas inside quotes. */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let cell = ''
  let quoted = false
  const src = text.replace(/^\uFEFF/, '')
  for (let i = 0; i < src.length; i++) {
    const ch = src[i]
    if (quoted) {
      if (ch === '"') {
        if (src[i + 1] === '"') { cell += '"'; i++ } else quoted = false
      } else cell += ch
    } else if (ch === '"') quoted = true
    else if (ch === ',') { row.push(cell); cell = '' }
    else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && src[i + 1] === '\n') i++
      row.push(cell); cell = ''
      rows.push(row); row = []
    } else cell += ch
  }
  if (cell !== '' || row.length > 0) { row.push(cell); rows.push(row) }
  return rows.filter(r => r.some(c => c.trim() !== ''))
}

const norm = (s: string) => s.toLowerCase().replace(/[^a-z0-9]/g, '')
const ALIASES: Record<FieldKey, string[]> = {
  name: ['name', 'productname', 'product', 'item', 'itemname', 'description'],
  category: ['category', 'group', 'type'],
  unit: ['unit', 'uom', 'baseunit'],
  brand: ['brand', 'make', 'manufacturer'],
  model: ['model', 'modelno', 'modelnumber'],
  colour: ['colour', 'color'],
  capacity: ['capacity', 'size'],
  connector_type: ['connectortype', 'connector', 'port'],
  compatible_with: ['compatiblewith', 'compatible', 'fits'],
}

/** Guess a column for each field from header names. */
export function autoMap(headers: string[]): Mapping {
  const m: Mapping = {}
  const used = new Set<number>()
  for (const f of FIELDS) {
    const idx = headers.findIndex((h, i) => !used.has(i) && ALIASES[f.key].includes(norm(h)))
    if (idx >= 0) { m[f.key] = idx; used.add(idx) }
  }
  return m
}

export function missingRequired(m: Mapping): string[] {
  return FIELDS.filter(f => f.required && m[f.key] === undefined).map(f => f.label)
}

const UNIT_WORDS: Record<string, string> = { pc: 'pcs', pcs: 'pcs', piece: 'pcs', pieces: 'pcs', nos: 'pcs', each: 'pcs', set: 'set', sets: 'set', pack: 'pack', packs: 'pack', m: 'm', mtr: 'm', meter: 'm', metre: 'm', meters: 'm', metres: 'm' }

/** Converts data rows (header excluded) to API rows. A product with no brand and no model is generic. */
export function toImportRows(data: string[][], m: Mapping, headers: string[] = []): ImportRow[] {
  const get = (r: string[], k: FieldKey) => (m[k] === undefined ? '' : (r[m[k]!] ?? '').trim())
  const mapped = new Set(Object.values(m).filter((v): v is number => v !== undefined))
  return data.map(r => {
    const brand = get(r, 'brand')
    const model = get(r, 'model')
    const unitRaw = get(r, 'unit')
    const attributes: Record<string, string> = {}
    const specs: Record<string, string> = {}
    for (const k of ['colour', 'capacity', 'connector_type', 'compatible_with'] as const) {
      const v = get(r, k)
      if (v) attributes[k] = v
    }
    // Preserve additional AI-extracted or user-supplied specification columns.
    r.forEach((raw, index) => {
      if (mapped.has(index) || !raw?.trim()) return
      const label = (headers[index] ?? '').replace(/^attribute:\s*/i, '').trim()
      const key = label.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '')
      if (key && !['name', 'category', 'unit', 'brand', 'model'].includes(key)) specs[key] = raw.trim()
    })
    return {
      name: get(r, 'name'),
      categoryName: get(r, 'category'),
      isGeneric: !brand && !model,
      baseUnit: unitRaw === '' ? 'pcs' : (UNIT_WORDS[unitRaw.toLowerCase()] ?? unitRaw),
      brand: brand || null,
      model: model || null,
      attributes,
      specs,
    }
  })
}
export const SAMPLE_CSV = 'Product Name,Category,Unit,Brand,Model,Colour,Capacity,Connector Type,Compatible With\nUSB-C Cable 1m,Cables,pcs,,,Black,1m,USB-C,\nFast Charger 20W,Chargers,pcs,Volt,VC-20,White,20W,USB-C,Phones\nHDMI Cable 2m,Cables,pcs,,,,2m,HDMI,\n'
