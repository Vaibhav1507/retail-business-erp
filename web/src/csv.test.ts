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

  it('preserves arbitrary category-specific columns as flexible product specs', () => {
    const headers = ['Product Name', 'Category', 'Brand', 'Model', 'Attribute: Energy Rating', 'Attribute: Load Type']
    const mapping = autoMap(headers)
    const rows = toImportRows([['Washer', 'Washing Machines', 'LG', 'TX-1', '5 Star', 'Top Load']], mapping, headers)
    expect(rows[0]).toMatchObject({
      attributes: {},
      specs: { energy_rating: '5 Star', load_type: 'Top Load' },
      brand: 'LG',
      model: 'TX-1',
    })
  })

})
