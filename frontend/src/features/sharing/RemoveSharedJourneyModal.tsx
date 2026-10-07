import { useRef } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { removeSharedJourney } from '../../api/sharingApi'
import type { JourneySummary } from '../../contracts/journeys'
import { Modal } from '../../components/ui/Modal'
import { Button } from '../../components/ui/Button'
import { journeyKey } from '../journey-details/journeyData'
import { sharedJourneysKey } from './sharingData'
import styles from './Sharing.module.css'

export function RemoveSharedJourneyModal({
  journey,
  onClose,
  onRemoved,
}: {
  journey: JourneySummary
  onClose: () => void
  onRemoved: () => void
}) {
  const client = useQueryClient()
  const guard = useRef(false)
  const cancel = useRef<HTMLButtonElement>(null)
  const mutation = useMutation({
    mutationFn: async () => {
      await removeSharedJourney(journey.id)
      await Promise.all([
        client.cancelQueries({ queryKey: sharedJourneysKey }),
        client.cancelQueries({ queryKey: journeyKey(journey.id) }),
      ])
      client.removeQueries({ queryKey: journeyKey(journey.id) })
      client.setQueryData<JourneySummary[]>(sharedJourneysKey, (current) =>
        current?.filter((item) => item.id !== journey.id),
      )
      await client.invalidateQueries({ queryKey: sharedJourneysKey })
    },
  })
  async function remove() {
    if (guard.current) return
    guard.current = true
    try {
      await mutation.mutateAsync()
      onRemoved()
    } catch {
      /* Leave the card/dialog in place for retry. */
    } finally {
      guard.current = false
    }
  }
  function close() {
    if (!guard.current) onClose()
  }
  return (
    <Modal
      open
      title="Remove from Shared?"
      onClose={close}
      initialFocusRef={cancel}
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
            variant="danger"
            loading={mutation.isPending}
            loadingLabel="Removing…"
            onClick={() => {
              void remove()
            }}
          >
            Remove from Shared
          </Button>
        </>
      }
    >
      <div className={styles.dialog}>
        <p>
          Remove “{journey.name}” from your Shared trips? The owner's journey
          and other recipients are not affected.
        </p>
        {mutation.error && (
          <p role="alert" className={styles.error}>
            {mutation.error.message}
          </p>
        )}
      </div>
    </Modal>
  )
}
