import { useEffect, useId, useRef, useState } from 'react'
import { Copy, Ellipsis, Pencil, Route, Trash2 } from 'lucide-react'
import { IconButton } from '../../components/ui/IconButton'
import styles from './SavedJourneys.module.css'

export type CardAction = 'edit' | 'rename' | 'duplicate' | 'delete'
const actions = [
  { id: 'edit', label: 'Edit route', icon: Route },
  { id: 'rename', label: 'Rename', icon: Pencil },
  { id: 'duplicate', label: 'Duplicate', icon: Copy },
  { id: 'delete', label: 'Delete', icon: Trash2 },
] as const

export function CardActions({
  name,
  disabled,
  onAction,
}: {
  name: string
  disabled: boolean
  onAction: (action: CardAction) => void
}) {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const firstIndex = useRef(0)
  const id = useId()
  useEffect(() => {
    if (!open) return
    root.current
      ?.querySelectorAll<HTMLButtonElement>('[role=menuitem]')
      [firstIndex.current]?.focus()
    const outside = (event: PointerEvent) => {
      if (event.target instanceof Node && !root.current?.contains(event.target))
        setOpen(false)
    }
    document.addEventListener('pointerdown', outside)
    return () => document.removeEventListener('pointerdown', outside)
  }, [open])
  return (
    <div
      ref={root}
      className={styles.menuWrap}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false)
      }}
    >
      <IconButton
        ref={trigger}
        icon={Ellipsis}
        label={`Actions for ${name}`}
        disabled={disabled}
        className={styles.menuTrigger}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        onClick={() => {
          firstIndex.current = 0
          setOpen(!open)
        }}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault()
            firstIndex.current =
              event.key === 'ArrowUp' ? actions.length - 1 : 0
            setOpen(true)
          }
        }}
      />
      {open && (
        <div
          id={id}
          role="menu"
          aria-label={`Actions for ${name}`}
          className={styles.menu}
          onKeyDown={(event) => {
            const items = Array.from(
              event.currentTarget.querySelectorAll<HTMLButtonElement>(
                '[role=menuitem]',
              ),
            )
            const index = items.indexOf(
              document.activeElement as HTMLButtonElement,
            )
            if (event.key === 'Escape' || event.key === 'Tab') {
              if (event.key === 'Escape') event.preventDefault()
              setOpen(false)
              trigger.current?.focus()
            } else if (
              ['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)
            ) {
              event.preventDefault()
              const next =
                event.key === 'Home'
                  ? 0
                  : event.key === 'End'
                    ? items.length - 1
                    : (index +
                        (event.key === 'ArrowDown' ? 1 : -1) +
                        items.length) %
                      items.length
              items[next]?.focus()
            }
          }}
        >
          {actions.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              type="button"
              role="menuitem"
              tabIndex={-1}
              className={id === 'delete' ? styles.danger : undefined}
              onClick={() => {
                setOpen(false)
                trigger.current?.focus()
                onAction(id)
              }}
            >
              <Icon aria-hidden="true" />
              {label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
