import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Outlet, useLocation, useNavigate } from 'react-router'
import { ProductShell } from '../../app/ProductShell'
import { Button } from '../../components/ui/Button'
import { getJourney } from '../../api/journeysApi'
import type { Journey } from '../../contracts/journeys'
import { journeyKey } from '../journey-details/journeyData'
import { initialDraft, type PlanningResult } from './planning'
import { PlanningContext } from './planningContext'
import { savedJourneyDraft } from './savedJourneyDraft'

function editIdFromState(state: unknown): string | null {
  return typeof state === 'object' &&
    state !== null &&
    'editJourneyId' in state &&
    typeof state.editJourneyId === 'string'
    ? state.editJourneyId
    : null
}

export function PlanningLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const [editId] = useState(() => editIdFromState(location.state))
  // Consume the navigation intent: history/refresh must not resurrect an old edit.
  useEffect(() => {
    if (editId && location.state)
      void navigate(location.pathname, { replace: true, state: null })
  }, [editId, location.state, location.pathname, navigate])
  const query = useQuery({
    queryKey: journeyKey(editId ?? ''),
    queryFn: ({ signal }) => getJourney(editId ?? '', signal),
    enabled: Boolean(editId),
    retry: false,
    refetchOnWindowFocus: false,
  })
  const journey = editId ? query.data : null
  const usable =
    !editId ||
    (journey?.access === 'owner' && journey.plan.route.points.length >= 2)
  return (
    <ProductShell activeItem="plan">
      {editId && query.isPending ? (
        <p role="status">Loading journey for editing…</p>
      ) : !usable ? (
        <section>
          <h1>Unable to edit journey</h1>
          <p role="alert">
            {query.error?.message ??
              'Only an owned journey with a valid route can be edited.'}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              void navigate('/saved')
            }}
          >
            Back to saved journeys
          </Button>
        </section>
      ) : (
        <PlanningSession journey={journey ?? null} />
      )}
    </ProductShell>
  )
}

function PlanningSession({ journey }: { journey: Journey | null }) {
  const [editingJourney] = useState(() =>
    journey ? structuredClone(journey) : null,
  )
  const [draft, setDraft] = useState(() =>
    journey ? savedJourneyDraft(journey) : initialDraft(),
  )
  const [result, setResult] = useState<PlanningResult | null>(null)
  return (
    <PlanningContext
      value={{ draft, setDraft, result, setResult, editingJourney }}
    >
      <Outlet />
    </PlanningContext>
  )
}
