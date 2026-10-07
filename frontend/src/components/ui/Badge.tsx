import type { ComponentPropsWithoutRef } from 'react'
import styles from './Badge.module.css'

type BadgeProps = ComponentPropsWithoutRef<'span'> & {
  tone?: 'neutral' | 'info' | 'success' | 'notification'
}

export function Badge({
  tone = 'neutral',
  className = '',
  ...props
}: BadgeProps) {
  return (
    <span
      {...props}
      className={`${styles.badge} ${styles[tone]} ${className}`}
    />
  )
}
