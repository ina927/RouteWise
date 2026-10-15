import { useId, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteJourney, updateJourney } from '../../api/journeysApi'
import type { JourneySummary } from '../../contracts/journeys'
import { Modal } from '../../components/ui/Modal'
import { Button } from '../../components/ui/Button'
import { TextInput } from '../../components/ui/TextInput'
import {
  journeyNameError,
  journeyNameLimit,
} from '../../utils/journeyValidation'
import {
  cacheJourney,
  journeyKey,
  ownedJourneysKey,
} from '../journey-details/journeyData'
import styles from './SavedJourneys.module.css'

export function SavedJourneyDialog({
  kind,
  journey,
  onClose,
}: {
  kind: 'rename' | 'delete'
  journey: JourneySummary
  onClose: () => void
}) {
  const [name, setName] = useState(journey.name)
  const [attempted, setAttempted] = useState(false)
  const guard = useRef(false)
  const input = useRef<HTMLInputElement>(null)
  const cancel = useRef<HTMLButtonElement>(null)
  const form = useId()
  const client = useQueryClient()
  const mutation = useMutation({
    mutationFn: async () => {
      if (kind === 'rename')
        await cacheJourney(
          client,
          await updateJourney(journey.id, { name: name.trim() }),
        )
      else {
        await deleteJourney(journey.id)
        await Promise.all([
          client.cancelQueries({ queryKey: journeyKey(journey.id) }),
          client.cancelQueries({ queryKey: ownedJourneysKey }),
        ])
        client.removeQueries({ queryKey: journeyKey(journey.id) })
        client.setQueryData<JourneySummary[]>(ownedJourneysKey, (current) =>
          current?.filter((item) => item.id !== journey.id),
        )
        await client.invalidateQueries({ queryKey: ownedJourneysKey })
      }
    },
  })
  async function submit() {
    if (guard.current) return
    setAttempted(true)
    if (kind === 'rename' && journeyNameError(name)) return
    if (kind === 'rename' && name.trim() === journey.name) {
      onClose()
      return
    }
    guard.current = true
    try {
      await mutation.mutateAsync()
      onClose()
    } catch {
      /* Keep the dialog and input for retry. */
    } finally {
      guard.current = false
    }
  }
  const close = () => {
    if (!guard.current) onClose()
  }
  return (
    <Modal
      open
      title={kind === 'rename' ? 'Rename journey' : 'Delete journey?'}
      onClose={close}
      initialFocusRef={kind === 'rename' ? input : cancel}
      footer={
        <>
          <Button
            ref={cancel}
            variant="secondary"
            disabled={mutation.isPending}
            onClick={close}
          >
            Cancel
          </Button>
          <Button
            form={form}
            type="submit"
            variant={kind === 'delete' ? 'danger' : 'primary'}
            loading={mutation.isPending}
            loadingLabel={kind === 'delete' ? 'Deleting…' : 'Saving…'}
          >
            {kind === 'delete' ? 'Delete journey' : 'Save name'}
          </Button>
        </>
      }
    >
      <form
        id={form}
        className={styles.dialogForm}
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        {kind === 'rename' ? (
          <TextInput
            ref={input}
            label="Journey name (required)"
            required
            value={name}
            disabled={mutation.isPending}
            onChange={(event) => setName(event.target.value)}
            helperText={`${name.trim().length} / ${journeyNameLimit} characters`}
            error={
              attempted || name.trim().length > journeyNameLimit
                ? journeyNameError(name)
                : undefined
            }
          />
        ) : (
          <p>Delete “{journey.name}”? This cannot be undone.</p>
        )}
        {mutation.error && (
          <p role="alert" className={styles.error}>
            {mutation.error.message}
          </p>
        )}
      </form>
    </Modal>
  )
}
