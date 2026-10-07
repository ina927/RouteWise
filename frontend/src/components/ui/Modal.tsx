import { useEffect, useId, useRef, type ReactNode, type RefObject } from 'react'
import { X } from 'lucide-react'
import { IconButton } from './IconButton'
import styles from './Modal.module.css'

type ModalProps = {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
  footer?: ReactNode
  initialFocusRef?: RefObject<HTMLElement | null>
}

export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  initialFocusRef,
}: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const titleRef = useRef<HTMLHeadingElement>(null)
  const id = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    if (!open || !dialog) return
    const opener = document.activeElement
    const previousOverflow = document.body.style.overflow
    // showModal supplies top-layer rendering, inert background and focus containment.
    dialog.showModal()
    document.body.style.overflow = 'hidden'
    const target = initialFocusRef?.current
    if (target && dialog.contains(target)) target.focus()
    else titleRef.current?.focus()
    return () => {
      dialog.close()
      document.body.style.overflow = previousOverflow
      if (opener instanceof HTMLElement && opener.isConnected) opener.focus()
    }
  }, [open, initialFocusRef])

  return (
    <dialog
      ref={dialogRef}
      className={styles.dialog}
      aria-modal="true"
      aria-labelledby={`${id}-title`}
      aria-describedby={description ? `${id}-description` : undefined}
      onCancel={(event) => {
        event.preventDefault()
        onClose()
      }}
      onClose={() => {
        if (open && !dialogRef.current?.open) onClose()
      }}
    >
      <header className={styles.header}>
        <h2
          ref={titleRef}
          id={`${id}-title`}
          tabIndex={-1}
          className={styles.title}
        >
          {title}
        </h2>
        <IconButton icon={X} label="Close dialog" onClick={onClose} />
      </header>
      {description && (
        <p id={`${id}-description`} className={styles.description}>
          {description}
        </p>
      )}
      <div className={styles.content}>{children}</div>
      {footer && <footer className={styles.footer}>{footer}</footer>}
    </dialog>
  )
}
