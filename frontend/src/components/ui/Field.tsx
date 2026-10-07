import type { ReactNode } from 'react'
import styles from './Field.module.css'

// Internal shared label/message structure for input and select controls.
export type FieldProps = {
  label: string
  hideLabel?: boolean
  helperText?: string
  error?: string
}

export function Field({
  id,
  label,
  hideLabel,
  helperText,
  error,
  children,
}: FieldProps & {
  id: string
  children: ReactNode
}) {
  return (
    <div className={styles.field}>
      <label htmlFor={id} className={hideLabel ? styles.hidden : styles.label}>
        {label}
      </label>
      {children}
      {helperText && (
        <p id={`${id}-help`} className={styles.help}>
          {helperText}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className={styles.error}>
          {error}
        </p>
      )}
    </div>
  )
}
