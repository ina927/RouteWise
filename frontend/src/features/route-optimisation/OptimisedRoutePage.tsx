import { useState } from 'react'
import { useNavigate } from 'react-router'
import {
  ArrowLeft,
  Bookmark,
  Car,
  Clock,
  Footprints,
  RotateCcw,
  Route,
} from 'lucide-react'
import { PageHeader } from '../../components/layout/PageHeader'
import { Button } from '../../components/ui/Button'
import { Badge } from '../../components/ui/Badge'
import { SegmentedControl } from '../../components/ui/SegmentedControl'
import type { PlanningResult } from '../journey-planning/planning'
import type { JourneyPlan } from '../../contracts/journeys'
import { SaveJourneyModal } from '../journey-details/SaveJourneyModal'
import { useRouteUpdates } from './useRouteUpdates'
import { formatDistance, formatDuration } from './formatters'
import { RouteStopList } from './components/RouteStopList'
import { OptimisedRouteMap } from './components/OptimisedRouteMap'
import styles from './OptimisedRoutePage.module.css'

export function OptimisedRoutePage({ result }: { result: PlanningResult }) {
  const navigate = useNavigate()
  const updates = useRouteUpdates(result)
  const route = result.currentRoute
  const [savePlan, setSavePlan] = useState<JourneyPlan | null>(null)
  const [interaction, setInteraction] = useState<{
    route: typeof route
    hovered: string | null
    selected: string | null
  }>({ route, hovered: null, selected: null })
  // Preserve surviving segment IDs, but never let removed highlights reappear later.
  if (interaction.route !== route) {
    const exists = (id: string | null) =>
      route.segments.some((segment) => segment.id === id) ? id : null
    setInteraction({
      route,
      hovered: exists(interaction.hovered),
      selected: exists(interaction.selected),
    })
  }
  return (
    <>
      <Button
        variant="ghost"
        leadingIcon={ArrowLeft}
        className={styles.back}
        onClick={() => {
          void navigate('/plan')
        }}
      >
        Edit places
      </Button>
      <PageHeader title="Optimised route" />
      <div className={styles.controls}>
        <SegmentedControl
          label="Travel mode"
          value={updates.settings.travelMode}
          disabled={updates.pending === 'calculate'}
          options={[
            { value: 'walking', label: 'Walking', icon: Footprints },
            { value: 'driving', label: 'Driving', icon: Car },
          ]}
          onChange={(value) => {
            if (value === 'walking' || value === 'driving')
              void updates.changeSettings({ travelMode: value })
          }}
        />
        <div className={styles.objective}>
          <SegmentedControl
            label="Optimize for"
            value={updates.settings.objective}
            disabled={updates.pending === 'calculate'}
            options={[
              { value: 'distance', label: 'Distance', icon: Route },
              { value: 'time', label: 'Travel time', icon: Clock },
            ]}
            onChange={(value) => {
              if (value === 'distance' || value === 'time')
                void updates.changeSettings({ objective: value })
            }}
          />
        </div>
      </div>
      <div className={styles.update} role="status">
        {updates.pending ? 'Updating route…' : ''}
      </div>
      {updates.error && (
        <p className={styles.error} role="alert">
          {updates.error}
        </p>
      )}
      <div className={styles.workspace}>
        <section
          className={styles.panel}
          aria-label="Route itinerary"
          aria-busy={Boolean(updates.pending)}
        >
          <dl className={styles.summary}>
            <div
              className={
                result.request.objective === 'distance'
                  ? styles.emphasisedSummary
                  : undefined
              }
            >
              <Route aria-hidden="true" />
              <div>
                <dt>Total distance</dt>
                <dd>{formatDistance(route.totalDistanceMeters)}</dd>
              </div>
            </div>
            <div
              className={
                result.request.objective === 'time'
                  ? styles.emphasisedSummary
                  : undefined
              }
            >
              <Clock aria-hidden="true" />
              <div>
                <dt>Total travel time</dt>
                <dd>{formatDuration(route.totalDurationSeconds)}</dd>
              </div>
            </div>
          </dl>
          <RouteStopList
            route={route}
            travelMode={result.request.travelMode}
            disabled={Boolean(updates.pending)}
            hoveredSegmentId={interaction.hovered}
            selectedSegmentId={interaction.selected}
            onHover={(hovered) =>
              setInteraction((current) => ({ ...current, hovered }))
            }
            onSelect={(selected) =>
              setInteraction((current) => ({
                ...current,
                selected: current.selected === selected ? null : selected,
              }))
            }
            onReorder={(points) => {
              void updates.reorder(points)
            }}
          />
          {result.modified && (
            <div className={styles.modified}>
              <Badge tone="info">Modified route</Badge>
              <Button
                variant="ghost"
                leadingIcon={RotateCcw}
                disabled={Boolean(updates.pending)}
                onClick={updates.reset}
              >
                Reset to optimised route
              </Button>
            </div>
          )}
          <Button
            leadingIcon={Bookmark}
            className={styles.save}
            onClick={() =>
              setSavePlan(
                structuredClone({
                  travelMode: result.request.travelMode,
                  objective: result.request.objective,
                  endMode: result.request.endMode,
                  route,
                }),
              )
            }
          >
            Save journey
          </Button>
        </section>
        <OptimisedRouteMap
          route={route}
          hoveredSegmentId={interaction.hovered}
          selectedSegmentId={interaction.selected}
        />
      </div>
      {savePlan && (
        <SaveJourneyModal plan={savePlan} onClose={() => setSavePlan(null)} />
      )}
    </>
  )
}
