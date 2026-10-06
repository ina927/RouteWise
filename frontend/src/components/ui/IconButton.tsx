import type { ComponentPropsWithRef } from 'react'
import type { LucideIcon } from 'lucide-react'
import buttonStyles from './Button.module.css'
import styles from './IconButton.module.css'

type IconButtonProps = Omit<
  ComponentPropsWithRef<'button'>,
  'children' | 'aria-label' | 'aria-labelledby'
> & {
  label: string
  icon: LucideIcon
  variant?: 'secondary' | 'ghost' | 'danger'
}

export function IconButton({
  label,
  icon: Icon,
  variant = 'ghost',
  type = 'button',
  className = '',
  ...props
}: IconButtonProps) {
  return (
    <button
      {...props}
      type={type}
      aria-label={label}
      className={`${buttonStyles.button} ${buttonStyles[variant]} ${styles.button} ${className}`}
    >
      <Icon aria-hidden="true" />
    </button>
  )
}
