import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { TextInput } from '../../components/ui/TextInput'
import {
  journeyNameError,
  journeyNameLimit,
  journeyNoteLimit,
} from '../../utils/journeyValidation'
import styles from './JourneyDetails.module.css'

export function DetailFieldEditor({
  label,
  value,
  type = 'text',
  required = false,
  pending,
  error,
  onSave,
  onCancel,
}: {
  label: string
  value: string
  type?: 'text' | 'date' | 'time' | 'note'
  required?: boolean
  pending: boolean
  error: Error | null
  onSave: (value: string) => Promise<boolean>
  onCancel: () => void
}) {
  const [draft, setDraft] = useState(value)
  const [attempted, setAttempted] = useState(false)
  const id = useId()
  const formRef = useRef<HTMLFormElement>(null)
  useEffect(() => {
    formRef.current?.querySelector<HTMLElement>('input, textarea')?.focus()
  }, [])
  const validationError =
    type === 'text' && required
      ? journeyNameError(draft)
      : type === 'note' && draft.trim().length > journeyNoteLimit
        ? `Note must be ${journeyNoteLimit} characters or fewer.`
        : undefined
  const showError = attempted || Boolean(draft.trim())
  return (
    <form
      ref={formRef}
      className={styles.editor}
      onSubmit={(event) => {
        event.preventDefault()
        setAttempted(true)
        const next = draft.trim()
        if (pending || validationError || (required && !next)) return
        if (next === value) {
          onCancel()
          return
        }
        void onSave(next).then((saved) => {
          if (saved) onCancel()
        })
      }}
    >
      {type === 'note' ? (
        <label className={styles.noteLabel} htmlFor={id}>
          {label}
          <textarea
            id={id}
            value={draft}
            disabled={pending}
            onChange={(event) => setDraft(event.target.value)}
            rows={2}
            aria-invalid={Boolean(validationError)}
            aria-describedby={`${id}-feedback`}
          />
          <span
            id={`${id}-feedback`}
            className={validationError ? styles.error : undefined}
            role={validationError ? 'alert' : undefined}
          >
            {validationError ??
              `${draft.trim().length} / ${journeyNoteLimit} characters`}
          </span>
        </label>
      ) : (
        <TextInput
          label={label}
          type={type}
          required={required}
          value={draft}
          disabled={pending}
          onChange={(event) => setDraft(event.target.value)}
          helperText={
            type === 'text'
              ? `${draft.trim().length} / ${journeyNameLimit} characters`
              : undefined
          }
          error={showError ? validationError : undefined}
        />
      )}
      <div className={styles.actions}>
        <Button type="submit" loading={pending} loadingLabel="Saving…">
          Save
        </Button>
        <Button variant="ghost" disabled={pending} onClick={onCancel}>
          Cancel
        </Button>
        {!required && draft && (
          <Button
            variant="ghost"
            disabled={pending}
            onClick={() => setDraft('')}
          >
            Clear
          </Button>
        )}
      </div>
      {error && (
        <p role="alert" className={styles.error}>
          {error.message}
        </p>
      )}
    </form>
  )
}
