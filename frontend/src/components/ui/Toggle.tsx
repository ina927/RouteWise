import type { ComponentPropsWithRef } from 'react'
import styles from './Toggle.module.css'

type ToggleProps = Omit<
  ComponentPropsWithRef<'input'>,
  'type' | 'role' | 'checked' | 'defaultChecked' | 'onChange' | 'children'
> & {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}

export function Toggle({
  label,
  checked,
  onChange,
  className = '',
  ...props
}: ToggleProps) {
  return (
    <label className={styles.label}>
      <span>{label}</span>
      <input
        {...props}
        className={`${styles.input} ${className}`}
        type="checkbox"
        role="switch"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span className={styles.track} aria-hidden="true">
        <span className={styles.thumb} />
      </span>
    </label>
  )
}
