import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SearchableSelect, type SearchableSelectOption } from './SearchableSelect'

const options: SearchableSelectOption[] = [
  { value: 'c1', label: 'Asha Stores', description: 'Owes ₹1,200', searchText: '9876543210' },
  { value: 'c2', label: 'Ravi Kumar', description: 'Owes ₹300', searchText: '9123456780' }
]

function ControlledSelect({ id, label, options, placeholder, onChange }: {
  id: string; label: string; options: SearchableSelectOption[]; placeholder: string; onChange: (value: string) => void
}) {
  const [value, setValue] = useState('')
  return <SearchableSelect id={id} label={label} value={value} options={options}
    onChange={next => { onChange(next); setValue(next) }} placeholder={placeholder} />
}

describe('SearchableSelect', () => {
  it('filters by customer name and selects with the keyboard', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<ControlledSelect id="customer" label="Customer" options={options}
      onChange={onChange} placeholder="Search by customer name" />)

    const input = screen.getByRole('combobox', { name: 'Customer' })
    await user.type(input, 'Ravi')
    expect(screen.getByRole('listbox', { name: 'Customer options' })).toBeVisible()
    expect(screen.getByRole('option', { name: /Ravi Kumar/ })).toBeVisible()
    expect(screen.queryByRole('option', { name: /Asha Stores/ })).not.toBeInTheDocument()

    await user.keyboard('{ArrowDown}{Enter}')
    expect(onChange).toHaveBeenLastCalledWith('c2')
    expect(input).toHaveValue('Ravi Kumar')
    expect(input).toHaveAttribute('aria-expanded', 'false')
    expect(screen.getByRole('listbox', { hidden: true, name: 'Customer options' })).not.toBeVisible()
  })

  it('starts keyboard navigation on the first result and does not wrap past the last', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<ControlledSelect id="customer" label="Customer" options={options}
      onChange={onChange} placeholder="Search by customer name" />)

    const input = screen.getByRole('combobox', { name: 'Customer' })
    await user.type(input, 'a')
    await user.keyboard('{ArrowDown}')
    expect(input).toHaveAttribute('aria-activedescendant', 'customer-option-0')
    await user.keyboard('{ArrowDown}{ArrowDown}')
    expect(input).toHaveAttribute('aria-activedescendant', 'customer-option-1')
    await user.keyboard('{Enter}')
    expect(onChange).toHaveBeenLastCalledWith('c2')
    expect(input).toHaveValue('Ravi Kumar')
  })

  it('supports matching by additional reference text and selecting an explicit empty option', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<ControlledSelect id="bill" label="Apply to a bill (optional)"
      options={[{ value: '', label: 'On account (no specific bill)' }, ...options]}
      onChange={onChange} placeholder="Search bill or choose on account" />)

    const input = screen.getByRole('combobox', { name: 'Apply to a bill (optional)' })
    await user.click(input)
    expect(input).toHaveValue('')
    await user.type(input, '9876543210')
    await user.click(screen.getByRole('option', { name: /Asha Stores/ }))
    expect(onChange).toHaveBeenLastCalledWith('c1')
    expect(input).toHaveValue('Asha Stores')
  })
})
