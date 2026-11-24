import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import './searchable-select.css'

export type SearchableSelectOption = {
  value: string
  label: string
  description?: string
  searchText?: string
}

type Props = {
  id: string
  label: string
  value: string
  options: SearchableSelectOption[]
  onChange: (value: string) => void
  placeholder: string
  emptyMessage?: string
  disabled?: boolean
}

export function SearchableSelect({
  id, label, value, options, onChange, placeholder, emptyMessage = 'No matching options.', disabled = false
}: Props) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const inputRef = useRef<HTMLInputElement>(null)
  const selected = options.find(option => option.value === value)

  useEffect(() => {
    if (!open) setQuery(selected?.label ?? '')
  }, [value, options, open])

  const filtered = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase()
    if (!needle) return options
    return options.filter(option =>
      [option.label, option.description, option.searchText]
        .filter(Boolean).join(' ').toLocaleLowerCase().includes(needle)
    )
  }, [options, query])

  const choose = (option: SearchableSelectOption) => {
    setQuery(option.label)
    setOpen(false)
    setActiveIndex(-1)
    onChange(option.value)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      if (!open) {
        setOpen(true)
        setActiveIndex(filtered.length ? 0 : -1)
      } else {
        setActiveIndex(index => filtered.length ? Math.min(index < 0 ? 0 : index + 1, filtered.length - 1) : -1)
      }
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      if (!open) {
        setOpen(true)
        setActiveIndex(filtered.length - 1)
      } else {
        setActiveIndex(index => filtered.length ? Math.max(index < 0 ? filtered.length - 1 : index - 1, 0) : -1)
      }
    } else if (event.key === 'Enter' && open && activeIndex >= 0 && filtered[activeIndex]) {
      event.preventDefault()
      choose(filtered[activeIndex])
    } else if (event.key === 'Escape' && open) {
      event.preventDefault()
      setOpen(false)
      setActiveIndex(-1)
      setQuery(selected?.label ?? '')
    }
  }

  const listId = id + '-options'
  const activeId = open && activeIndex >= 0 && filtered[activeIndex] ? id + '-option-' + activeIndex : undefined

  return (
    <div className="billing-field searchable-select">
      <label htmlFor={id}>{label}</label>
      <div className="searchable-select-control">
        <input
          ref={inputRef}
          id={id}
          type="search"
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls={listId}
          aria-activedescendant={activeId}
          aria-haspopup="listbox"
          autoComplete="off"
          disabled={disabled}
          value={query}
          placeholder={placeholder}
          onFocus={() => {
            setOpen(true)
            setActiveIndex(-1)
            if (selected) setQuery('')
          }}
          onBlur={() => window.setTimeout(() => setOpen(false), 120)}
          onKeyDown={onKeyDown}
          onChange={event => {
            setQuery(event.target.value)
            setOpen(true)
            setActiveIndex(-1)
            if (value) onChange('')
          }}
        />
        {query && !disabled && <button
          type="button"
          className="searchable-select-clear"
          aria-label={'Clear ' + label.toLocaleLowerCase()}
          onMouseDown={event => event.preventDefault()}
          onClick={() => {
            setQuery('')
            setOpen(true)
            setActiveIndex(-1)
            onChange('')
            inputRef.current?.focus()
          }}
        >×</button>}
      </div>
      <ul id={listId} className="searchable-select-options" role="listbox" aria-label={label + ' options'} hidden={!open}>
        {filtered.length === 0
          ? <li className="searchable-select-empty" role="presentation">{emptyMessage}</li>
          : filtered.map((option, index) => (
            <li
              key={option.value || 'empty-option'}
              id={id + '-option-' + index}
              role="option"
              aria-selected={option.value === value}
              className={index === activeIndex ? 'is-active' : undefined}
              onPointerDown={event => event.preventDefault()}
              onClick={() => choose(option)}
            >
              <strong>{option.label}</strong>
              {option.description && <span>{option.description}</span>}
            </li>
          ))}
      </ul>
    </div>
  )
}
