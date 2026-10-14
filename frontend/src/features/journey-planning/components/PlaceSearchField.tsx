import { useEffect, useId, useRef, useState } from 'react'
import { Search, X } from 'lucide-react'
import type { Place } from '../../../contracts/places'
import { TextInput } from '../../../components/ui/TextInput'
import { IconButton } from '../../../components/ui/IconButton'
import { createPlaceSearch, type PlaceSuggestion } from '../googleMaps'
import type { PlaceDraft } from '../planning'
import styles from './PlaceSearchField.module.css'

type Props = {
  label: string
  row: PlaceDraft
  available: boolean
  disabled: boolean
  canSelect: boolean
  clearable?: boolean
  onChange: (query: string, place: Place | null) => void
}

export function PlaceSearchField({
  label,
  row,
  available,
  disabled,
  canSelect,
  clearable = true,
  onChange,
}: Props) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [search] = useState(createPlaceSearch)
  const [focused, setFocused] = useState(false)
  const [suggestions, setSuggestions] = useState<PlaceSuggestion[]>([])
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>(
    'idle',
  )
  const [active, setActive] = useState(-1)
  const [message, setMessage] = useState('')
  const revision = useRef(0)
  const searching =
    focused &&
    available &&
    !disabled &&
    canSelect &&
    !row.place &&
    Boolean(row.query.trim())

  useEffect(() => {
    if (!searching) return
    let cancelled = false
    const timer = setTimeout(() => {
      setStatus('loading')
      void search
        .search(row.query.trim())
        .then((items) => {
          if (cancelled) return
          setSuggestions(items)
          setActive(-1)
          setStatus('ready')
        })
        .catch(() => {
          if (cancelled) return
          setSuggestions([])
          setStatus('error')
          setMessage('Edit your search to try loading places again.')
        })
    }, 300)
    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [row.query, searching, search])

  useEffect(
    () => () => {
      revision.current++
      search.reset()
    },
    [search],
  )

  async function select(suggestion: PlaceSuggestion) {
    if (disabled || !canSelect) return
    const version = ++revision.current
    setStatus('loading')
    setSuggestions([])
    setActive(-1)
    try {
      const place = await suggestion.select()
      if (version !== revision.current) return
      onChange(place.name, place)
      setStatus('idle')
      setFocused(false)
    } catch {
      if (version !== revision.current) return
      setStatus('error')
      setMessage('Please select the place again.')
    }
  }

  const open = searching && suggestions.length > 0 && status === 'ready'
  useEffect(() => {
    if (open && active >= 0)
      document
        .getElementById(`${id}-option-${active}`)
        ?.scrollIntoView({ block: 'nearest' })
  }, [open, active, id])

  const feedback = !available
    ? status === 'error'
      ? message
      : ''
    : !canSelect && !row.place
      ? 'Remove a location before selecting another.'
      : status === 'loading'
        ? 'Searching…'
        : status === 'error'
          ? message
          : searching && status === 'ready' && !suggestions.length
            ? 'Try another search to find a place.'
            : row.query && !row.place
              ? 'Choose a search result to select a location.'
              : ''

  return (
    <div
      className={styles.field}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) {
          setFocused(false)
          setActive(-1)
          revision.current++
          search.reset()
        }
      }}
    >
      <TextInput
        ref={inputRef}
        id={id}
        label={label}
        hideLabel
        leadingIcon={Search}
        placeholder={label}
        value={row.query}
        disabled={disabled || !available}
        autoComplete="off"
        role="combobox"
        aria-autocomplete="list"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? `${id}-results` : undefined}
        aria-activedescendant={
          open && active >= 0 ? `${id}-option-${active}` : undefined
        }
        aria-describedby={feedback ? `${id}-feedback` : undefined}
        className={clearable ? styles.input : undefined}
        onFocus={() => setFocused(true)}
        onChange={(event) => {
          revision.current++
          setSuggestions([])
          setActive(-1)
          setStatus('idle')
          setFocused(true)
          // Visible edits immediately discard the old selection and its coordinates.
          onChange(event.target.value, null)
          if (!event.target.value.trim()) search.reset()
        }}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            setFocused(false)
            setActive(-1)
            return
          }
          if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault()
            setFocused(true)
            if (suggestions.length)
              setActive((index) =>
                index < 0
                  ? event.key === 'ArrowDown'
                    ? 0
                    : suggestions.length - 1
                  : (index +
                      (event.key === 'ArrowDown' ? 1 : -1) +
                      suggestions.length) %
                    suggestions.length,
              )
          }
          if (event.key === 'Enter') {
            event.preventDefault()
            const suggestion = open ? suggestions[active] : undefined
            if (suggestion) void select(suggestion)
          }
        }}
      />
      {clearable && row.query && (
        <IconButton
          className={styles.clear}
          icon={X}
          label={`Clear ${label.toLowerCase()}`}
          disabled={disabled}
          onClick={() => {
            revision.current++
            search.reset()
            onChange('', null)
            setSuggestions([])
            setStatus('idle')
            setActive(-1)
            inputRef.current?.focus()
          }}
        />
      )}
      {open && (
        <div className={styles.results}>
          <ul
            id={`${id}-results`}
            role="listbox"
            aria-label={`${label} suggestions`}
            className={styles.list}
          >
            {suggestions.map((suggestion, index) => (
              <li
                key={`${suggestion.id}-${index}`}
                id={`${id}-option-${index}`}
                role="option"
                aria-selected={active === index}
                className={styles.option}
                onPointerDown={(event) => event.preventDefault()}
                onClick={() => {
                  void select(suggestion)
                }}
              >
                {suggestion.label}
              </li>
            ))}
          </ul>
          <p className={styles.attribution} translate="no">
            Google Maps
          </p>
        </div>
      )}
      <p id={`${id}-feedback`} className={styles.feedback} role="status">
        {feedback}
      </p>
    </div>
  )
}
