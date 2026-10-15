import { useId, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { createJourney } from '../../api/journeysApi'
import type { JourneyPlan } from '../../contracts/journeys'
import { Modal } from '../../components/ui/Modal'
import { TextInput } from '../../components/ui/TextInput'
import { Button } from '../../components/ui/Button'
import { cacheJourney } from './journeyData'
import {
  journeyNameError,
  journeyNameLimit,
} from '../../utils/journeyValidation'
import styles from './JourneyDetails.module.css'

export function SaveJourneyModal({
  plan,
  onClose,
}: {
  plan: JourneyPlan
  onClose: () => void
}) {
  const [name, setName] = useState('')
  const [date, setDate] = useState('')
  const [attempted, setAttempted] = useState(false)
  const guard = useRef(false)
  const input = useRef<HTMLInputElement>(null)
  const formId = useId()
  const client = useQueryClient()
  const navigate = useNavigate()
  const mutation = useMutation({ mutationFn: createJourney })
  const nameError = journeyNameError(name)
  async function submit() {
    setAttempted(true)
    if (nameError || guard.current) return
    guard.current = true
    try {
      const journey = await mutation.mutateAsync({
        name: name.trim(),
        ...(date ? { date } : {}),
        plan,
        annotations: [],
      })
      await cacheJourney(client, journey)
      await navigate(`/journeys/${encodeURIComponent(journey.id)}`)
    } catch {
      /* Mutation error is rendered below; keep entered values. */
    } finally {
      guard.current = false
    }
  }
  return (
    <Modal
      open
      title="Save journey"
      initialFocusRef={input}
      onClose={() => {
        if (!guard.current) onClose()
      }}
      footer={
        <>
          <Button
            variant="secondary"
            disabled={mutation.isPending}
            onClick={onClose}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            form={formId}
            loading={mutation.isPending}
            loadingLabel="Saving…"
          >
            Save journey
          </Button>
        </>
      }
    >
      <form
        id={formId}
        className={styles.form}
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
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
              ? nameError
              : undefined
          }
        />
        <TextInput
          label="Date (optional)"
          type="date"
          value={date}
          disabled={mutation.isPending}
          onChange={(event) => setDate(event.target.value)}
        />
        {mutation.error && (
          <p className={styles.error} role="alert">
            {mutation.error.message}
          </p>
        )}
      </form>
    </Modal>
  )
}
