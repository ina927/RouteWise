import { useId, type ComponentPropsWithRef } from 'react'
import type { LucideIcon } from 'lucide-react'
import { Field, type FieldProps } from './Field'
import styles from './Field.module.css'

export type TextInputProps = ComponentPropsWithRef<'input'> &
  FieldProps & {
    leadingIcon?: LucideIcon
  }

export function TextInput({
  id,
  label,
  hideLabel,
  helperText,
  error,
  leadingIcon: Icon,
  className = '',
  type = 'text',
  'aria-describedby': describedBy,
  'aria-invalid': invalid,
  ...props
}: TextInputProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const description =
    [describedBy, helperText && `${inputId}-help`, error && `${inputId}-error`]
      .filter(Boolean)
      .join(' ') || undefined
  return (
    <Field
      id={inputId}
      label={label}
      hideLabel={hideLabel}
      helperText={helperText}
      error={error}
    >
      <div className={styles.controlWrap}>
        {Icon && <Icon className={styles.icon} aria-hidden="true" />}
        <input
          {...props}
          id={inputId}
          type={type}
          aria-describedby={description}
          aria-invalid={error ? true : invalid}
          className={`${styles.control} ${Icon ? styles.withIcon : ''} ${className}`}
        />
      </div>
    </Field>
  )
}
