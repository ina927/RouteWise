import { useId, type ReactNode } from 'react'
import { MapPin, RotateCcw, Route } from 'lucide-react'
import type { EndMode } from '../../../contracts/routes'
import styles from './EndModeSelector.module.css'

const options = [
  {
    value: 'open',
    label: 'Open end',
    description: 'Let RouteWise choose the most efficient final stop.',
    icon: Route,
  },
  {
    value: 'returnToStart',
    label: 'Return to start',
    description: 'Finish where you started.',
    icon: RotateCcw,
  },
  {
    value: 'fixed',
    label: 'Choose an end',
    description: 'Set a specific final destination.',
    icon: MapPin,
  },
] as const

export function EndModeSelector({
  value,
  onChange,
  disabled,
  fixedEndDisabled = false,
  children,
}: {
  value: EndMode | null
  onChange: (value: EndMode) => void
  disabled: boolean
  fixedEndDisabled?: boolean
  children: ReactNode
}) {
  const name = useId()

  return (
    <fieldset className={styles.group} disabled={disabled}>
      <legend className={styles.legend}>End your journey</legend>

      <div className={styles.options}>
        {options.map(({ value: option, label, description, icon: Icon }) => {
          // disable fixed end if it would exceed the location limit
          const optionDisabled =
            option === 'fixed' && fixedEndDisabled && value !== 'fixed'

          return (
            <div
              key={option}
              className={`${styles.card} ${value === option ? styles.selected : ''}`}
              style={
                optionDisabled
                  ? { opacity: 0.5, cursor: 'not-allowed' }
                  : undefined
              }
            >
              <label className={styles.label}>
                <input
                  type="radio"
                  name={name}
                  value={option}
                  checked={value === option}
                  disabled={optionDisabled}
                  onChange={() => {
                    if (!optionDisabled) onChange(option)
                  }}
                />

                <Icon
                  aria-hidden="true"
                  className={option === 'fixed' ? styles.endpoint : undefined}
                />

                <span>
                  <span className={styles.title}>{label}</span>
                  <span className={styles.description}>
                    {optionDisabled
                      ? 'Maximum 10 locations. Remove a place to choose a separate end.'
                      : description}
                  </span>
                </span>
              </label>

              {option === 'fixed' && value === 'fixed' && (
                <div className={styles.end}>{children}</div>
              )}
            </div>
          )
        })}
      </div>
    </fieldset>
  )
}