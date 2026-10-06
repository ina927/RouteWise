import { useId, type ComponentPropsWithRef } from 'react'
import { ChevronDown } from 'lucide-react'
import { Field, type FieldProps } from './Field'
import styles from './Field.module.css'

type SelectProps = Omit<ComponentPropsWithRef<'select'>, 'multiple' | 'size'> &
  FieldProps

export function Select({
  id,
  label,
  hideLabel,
  helperText,
  error,
  children,
  className = '',
  'aria-describedby': describedBy,
  'aria-invalid': invalid,
  ...props
}: SelectProps) {
  const generatedId = useId()
  const selectId = id ?? generatedId
  const description =
    [
      describedBy,
      helperText && `${selectId}-help`,
      error && `${selectId}-error`,
    ]
      .filter(Boolean)
      .join(' ') || undefined
  return (
    <Field
      id={selectId}
      label={label}
      hideLabel={hideLabel}
      helperText={helperText}
      error={error}
    >
      <div className={styles.controlWrap}>
        <select
          {...props}
          id={selectId}
          aria-describedby={description}
          aria-invalid={error ? true : invalid}
          className={`${styles.control} ${styles.select} ${className}`}
        >
          {children}
        </select>
        <ChevronDown
          className={`${styles.icon} ${styles.chevron}`}
          aria-hidden="true"
        />
      </div>
    </Field>
  )
}
