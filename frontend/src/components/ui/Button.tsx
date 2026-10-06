import type { ComponentPropsWithRef, ReactNode } from 'react'
import { LoaderCircle, type LucideIcon } from 'lucide-react'
import styles from './Button.module.css'

export type ButtonProps = ComponentPropsWithRef<'button'> & {
  children: ReactNode
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  leadingIcon?: LucideIcon
  trailingIcon?: LucideIcon
  loading?: boolean
  loadingLabel?: string
}

export function Button({
  children,
  variant = 'primary',
  leadingIcon: LeadingIcon,
  trailingIcon: TrailingIcon,
  loading = false,
  loadingLabel = 'Loading…',
  disabled,
  type = 'button',
  className = '',
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`${styles.button} ${styles[variant]} ${className}`}
    >
      {loading ? (
        <LoaderCircle className={styles.spinner} aria-hidden="true" />
      ) : (
        LeadingIcon && <LeadingIcon aria-hidden="true" />
      )}
      <span>{loading ? loadingLabel : children}</span>
      {!loading && TrailingIcon && <TrailingIcon aria-hidden="true" />}
    </button>
  )
}
