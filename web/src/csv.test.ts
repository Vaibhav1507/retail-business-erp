import { describe, expect, it } from 'vitest'
import { autoMap, missingRequired, parseCsv, toImportRows } from './csv'

describe('csv import helpers', () => {
  it('parses quotes, escaped quotes, embedded commas/newlines and CRLF', () => {
    const r = parseCsv('a,b\r\n"x, y","he said ""hi"""\n"multi\nline",z\n')
    expect(r).toEqual([['a', 'b'], ['x, y', 'he said "hi"'], ['multi\nline', 'z']])
  })

  it('ignores blank lines and a BOM', () => {
    expect(parseCsv('\uFEFFa,b\n\n,\n1,2')).toEqual([['a', 'b'], ['1', '2']])
  })

  it('auto-maps common header names and reports missing required fields', () => {
    const m = autoMap(['Product Name', 'Group', 'Color', 'Junk'])
    expect(m).toMatchObject({ name: 0, category: 1, colour: 2 })
    expect(missingRequired(autoMap(['Foo', 'Bar']))).toEqual(['Name', 'Category'])
  })

  it('maps rows: generic when no brand/model, unit words normalised, attributes kept', () => {
    const m = autoMap(['Name', 'Category', 'Unit', 'Brand', 'Colour'])
    const rows = toImportRows([['Cable', 'Cables', 'Pieces', '', 'Black'], ['Charger', 'Chargers', '', 'Volt', '']], m)
    expect(rows[0]).toMatchObject({ isGeneric: true, baseUnit: 'pcs', brand: null, attributes: { colour: 'Black' } })
    expect(rows[1]).toMatchObject({ isGeneric: false, baseUnit: 'pcs', brand: 'Volt', attributes: {} })
  })
})


describe('automatic supplier-sheet detection', () => {
  it('recognises common supplier aliases and maps MRP without asking for manual mapping', () => {
    const mapping = autoMap(['Particulars', 'Product Category', 'Make', 'Model No', 'UOM', 'MRP'])
    expect(mapping).toMatchObject({ name: 0, category: 1, brand: 2, model: 3, unit: 4, mrp: 5 })
    expect(missingRequired(mapping)).toEqual([])
    const rows = toImportRows([['LG TV', 'Television', 'LG', '43UR7500', 'Nos', '32990']], mapping)
    expect(rows[0]).toMatchObject({ name: 'LG TV', categoryName: 'Television', brand: 'LG', model: '43UR7500', baseUnit: 'pcs', mrp: 32990 })
  })

  it('uses a safe General category and first column fallback for unfamiliar supplier headers', () => {
    const mapping = autoMap(['Stock Details', 'Selling Price'])
    expect(mapping.name).toBe(0)
    expect(missingRequired(mapping)).toEqual([])
    expect(toImportRows([['Mixer Grinder', '2499']], mapping)[0]).toMatchObject({
      name: 'Mixer Grinder', categoryName: 'General', baseUnit: 'pcs', mrp: null,
    })
  })
})
