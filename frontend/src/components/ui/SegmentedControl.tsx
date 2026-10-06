import { useId } from 'react'
import type { LucideIcon } from 'lucide-react'
import styles from './SegmentedControl.module.css'

type SegmentedControlProps = {
  label: string
  options: readonly {
    value: string
    label: string
    icon?: LucideIcon
    disabled?: boolean
  }[]
  value: string
  onChange: (value: string) => void
  name?: string
  disabled?: boolean
}

export function SegmentedControl({
  label,
  options,
  value,
  onChange,
  name,
  disabled,
}: SegmentedControlProps) {
  const generatedName = useId()
  return (
    <fieldset className={styles.group} disabled={disabled}>
      <legend className={styles.legend}>{label}</legend>
      <div className={styles.options}>
        {options.map(
          ({
            value: optionValue,
            label: optionLabel,
            icon: Icon,
            disabled: optionDisabled,
          }) => (
            <label key={optionValue} className={styles.option}>
              <input
                type="radio"
                name={name ?? generatedName}
                value={optionValue}
                checked={value === optionValue}
                disabled={optionDisabled}
                onChange={() => onChange(optionValue)}
                className={styles.input}
              />
              <span className={styles.content}>
                {Icon && <Icon aria-hidden="true" />}
                {optionLabel}
              </span>
            </label>
          ),
        )}
      </div>
    </fieldset>
  )
}
