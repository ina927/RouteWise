import { useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { Car, Clock, Footprints, Plus, Route, X } from 'lucide-react'
import { EndpointPin } from '../../components/ui/EndpointPin'
import { PageHeader } from '../../components/layout/PageHeader'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { SegmentedControl } from '../../components/ui/SegmentedControl'
import { optimizeRoute } from '../../api/routesApi'
import { ApiError } from '../../api/httpClient'
import type { OptimizeRouteRequest } from '../../contracts/routes'
import type { Place } from '../../contracts/places'
import { loadGoogle, onGoogleFailure, type PlanMarker } from './googleMaps'
import {
  buildRequest,
  canAddRow,
  canSelectPlace,
  emptyPlace,
  locationCount,
  planningError,
  replacePlace,
  type PlaceDraft,
} from './planning'
import { usePlanning } from './planningContext'
import { EndModeSelector } from './components/EndModeSelector'
import { PlaceSearchField } from './components/PlaceSearchField'
import { JourneyPlanMap } from './components/JourneyPlanMap'
import styles from './PlanJourneyPage.module.css'

export function PlanJourneyPage() {
  const { draft, setDraft, setResult, editingJourney } = usePlanning()
  const navigate = useNavigate()
  const [googleReady, setGoogleReady] = useState(false)
  const [googleError, setGoogleError] = useState<string | null>(null)
  const requestInFlight = useRef<AbortController | null>(null)
  const addButton = useRef<HTMLButtonElement>(null)
  const formRef = useRef<HTMLFormElement>(null)
  const mutation = useMutation({
    mutationFn: (request: OptimizeRouteRequest) =>
      optimizeRoute(request, requestInFlight.current?.signal),
    retry: false,
  })

  useEffect(() => {
    let active = true
    const unsubscribe = onGoogleFailure((message) => {
      if (active) {
        setGoogleError(message)
        setGoogleReady(false)
      }
    })
    void loadGoogle()
      .then(() => {
        if (active) setGoogleReady(true)
      })
      .catch(() => {
        if (active)
          setGoogleError('Map and place search are temporarily unavailable.')
      })
    return () => {
      active = false
      unsubscribe()
      requestInFlight.current?.abort()
    }
  }, [])

  const count = locationCount(draft)
  const validationError = planningError(draft)
  const request = buildRequest(draft)
  const busy = mutation.isPending
  const settingsReady = Boolean(draft.travelMode && draft.objective)
  const points: PlanMarker[] = [
    ...(draft.start.place
      ? [
          {
            id: draft.start.id,
            place: draft.start.place,
            role: 'start' as const,
            label: draft.endMode === 'returnToStart' ? 'Start / End' : 'Start',
          },
        ]
      : []),
    ...draft.destinations.flatMap((row, index) =>
      row.place
        ? [
            {
              id: row.id,
              place: row.place,
              role: 'intermediate' as const,
              label: `Stop ${index + 1}`,
            },
          ]
        : [],
    ),
    ...(draft.endMode === 'fixed' && draft.end.place
      ? [
          {
            id: draft.end.id,
            place: draft.end.place,
            role: 'end' as const,
            label: 'End',
          },
        ]
      : []),
  ]

  function changePlace(id: string, query: string, place: Place | null) {
    if (requestInFlight.current) return
    setDraft((current) => replacePlace(current, id, query, place))
    mutation.reset()
  }

  function field(row: PlaceDraft, label: string, clearable = true) {
    return (
      <PlaceSearchField
        key={row.id}
        label={label}
        row={row}
        available={googleReady && !googleError}
        disabled={busy || !settingsReady}
        canSelect={canSelectPlace(draft, row.id)}
        clearable={clearable}
        onChange={(query, place) => changePlace(row.id, query, place)}
      />
    )
  }

  async function submit() {
    if (!request || requestInFlight.current) return
    const controller = new AbortController()
    requestInFlight.current = controller
    const snapshot = structuredClone(request)
    try {
      const result = await mutation.mutateAsync(snapshot)
      if (controller.signal.aborted) return
      setResult({
        request: snapshot,
        optimisedRoute: structuredClone(result),
        currentRoute: structuredClone(result),
        modified: false,
      })
      await navigate('/plan/route')
    } catch {
      // TanStack exposes the failure inline. The editable draft remains intact.
    } finally {
      requestInFlight.current = null
    }
  }

  return (
    <>
      <PageHeader
        title="Plan a journey"
        subtitle={
          editingJourney ? `Editing “${editingJourney.name}”` : undefined
        }
      />
      <form
        ref={formRef}
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <div className={styles.controls}>
          <SegmentedControl
            label="Travel mode"
            value={draft.travelMode ?? ''}
            disabled={busy}
            options={[
              { value: 'walking', label: 'Walking', icon: Footprints },
              { value: 'driving', label: 'Driving', icon: Car },
            ]}
            onChange={(value) => {
              if (value === 'walking' || value === 'driving') {
                setDraft((current) => ({ ...current, travelMode: value }))
                mutation.reset()
              }
            }}
          />
          <div className={styles.objective}>
            <SegmentedControl
              label="Optimise for"
              value={draft.objective ?? ''}
              disabled={busy}
              options={[
                { value: 'distance', label: 'Distance', icon: Route },
                { value: 'time', label: 'Travel time', icon: Clock },
              ]}
              onChange={(value) => {
                if (value === 'distance' || value === 'time') {
                  setDraft((current) => ({ ...current, objective: value }))
                  mutation.reset()
                }
              }}
            />
          </div>
        </div>
        {googleError && (
          <p className={styles.notice} role="status">
            {googleError}
          </p>
        )}
        {!settingsReady && (
          <p id="plan-settings-hint" className={styles.notice}>
            Choose a travel mode and optimisation objective to add your places.
          </p>
        )}
        <div className={styles.workspace}>
          <fieldset
            className={styles.builder}
            disabled={!settingsReady}
            aria-label="Journey places"
            aria-describedby={!settingsReady ? 'plan-settings-hint' : undefined}
          >
            <section aria-labelledby="plan-start-heading">
              <h2 id="plan-start-heading" className={styles.label}>
                Start
              </h2>
              <div className={styles.startRow}>
                <EndpointPin role="start" className={styles.endpoint} />
                {field(draft.start, 'Start place')}
              </div>
            </section>
            <section aria-labelledby="plan-places-heading">
              <div className={styles.sectionHeading}>
                <h2 id="plan-places-heading" className={styles.label}>
                  Places to visit
                </h2>
                <span
                  className={count > 10 ? styles.error : styles.counter}
                  aria-live="polite"
                >
                  {count} / 10 total locations
                </span>
              </div>
              <p className={styles.help}>
                Add places in any order. RouteWise will optimise your route.
              </p>
              <div className={styles.places} data-places>
                {draft.destinations.map((row, index) => (
                  <div className={styles.placeRow} key={row.id}>
                    <span className={styles.dot} aria-hidden="true" />
                    {field(row, `Place to visit ${index + 1}`, false)}
                    <IconButton
                      icon={X}
                      label={`Remove place to visit ${index + 1}`}
                      disabled={busy}
                      onClick={() => {
                        setDraft((current) => ({
                          ...current,
                          destinations: current.destinations.filter(
                            (item) => item.id !== row.id,
                          ),
                        }))
                        mutation.reset()
                        // Return focus to the remaining row or the add control after removal.
                        requestAnimationFrame(() => {
                          const remaining =
                            formRef.current?.querySelectorAll<HTMLInputElement>(
                              '[data-places] input',
                            )
                          ;(
                            remaining?.[
                              Math.min(index, (remaining?.length ?? 1) - 1)
                            ] ?? addButton.current
                          )?.focus()
                        })
                      }}
                    />
                  </div>
                ))}
              </div>
              <Button
                ref={addButton}
                variant="secondary"
                leadingIcon={Plus}
                disabled={busy || !canAddRow(draft)}
                onClick={() => {
                  if (!canAddRow(draft)) return
                  setDraft((current) =>
                    canAddRow(current)
                      ? {
                          ...current,
                          destinations: [...current.destinations, emptyPlace()],
                        }
                      : current,
                  )
                  requestAnimationFrame(() => {
                    const inputs =
                      formRef.current?.querySelectorAll<HTMLInputElement>(
                        '[data-places] input',
                      )
                    inputs?.[inputs.length - 1]?.focus()
                  })
                }}
              >
                Add another place
              </Button>
            </section>
            <EndModeSelector
              value={draft.endMode}
              disabled={busy}
              fixedEndDisabled={
                draft.endMode !== 'fixed' && 1 + draft.destinations.length >= 10
              }
              onChange={(endMode) => {
                // prevent selecting fixed end when all 10 slots are occupied
                if (
                  endMode === 'fixed' &&
                  draft.endMode !== 'fixed' &&
                  1 + draft.destinations.length >= 10
                ) {
                  return
                }

                setDraft((current) => ({ ...current, endMode }))
                mutation.reset()
              }}
            >
              {field(draft.end, 'End place')}
            </EndModeSelector>
            {count > 10 && (
              <p className={styles.error} role="alert">
                Remove {count - 10} location{count - 10 === 1 ? '' : 's'} to
                stay within the 10-location limit.
              </p>
            )}
            {validationError && (
              <p className={styles.error} role="alert">
                {validationError}
              </p>
            )}
            {mutation.isError && (
              <p className={styles.error} role="alert">
                {mutation.error instanceof ApiError &&
                mutation.error.status === 404
                  ? 'Route planning is currently unavailable.'
                  : mutation.error.message ||
                    'Please try finding a route again.'}
              </p>
            )}
            <div>
              <Button
                type="submit"
                leadingIcon={Route}
                className={styles.submit}
                disabled={!request}
                loading={busy}
                loadingLabel="Finding route…"
              >
                Find best route
              </Button>
            </div>
          </fieldset>
          <JourneyPlanMap
            points={points}
            ready={googleReady}
            error={googleError}
          />
        </div>
      </form>
    </>
  )
}
