import { useId, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router'
import {
  ArrowLeft,
  CalendarDays,
  Car,
  Clock,
  Footprints,
  List,
  Map,
  Pencil,
  Route,
  Share2,
} from 'lucide-react'
import { getJourney } from '../../api/journeysApi'
import { ApiError } from '../../api/httpClient'
import type { Journey } from '../../contracts/journeys'
import { ProductShell } from '../../app/ProductShell'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { Toggle } from '../../components/ui/Toggle'
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import { OptimisedRouteMap } from '../route-optimisation/components/OptimisedRouteMap'
import { DetailFieldEditor } from './DetailFieldEditor'
import { JourneyItinerary, type EditingField } from './JourneyItinerary'
import { journeyKey, useJourneyUpdate } from './journeyData'
import { ShareJourneyModal } from '../sharing/ShareJourneyModal'
import { JourneySharingStatus } from '../sharing/JourneySharingStatus'
import styles from './JourneyDetails.module.css'

export function JourneyDetailsPage() {
  const { journeyId = '' } = useParams()
  const navigate = useNavigate()
  const query = useQuery({
    queryKey: journeyKey(journeyId),
    queryFn: ({ signal }) => getJourney(journeyId, signal),
    retry: false,
  })
  return (
    <ProductShell
      activeItem={query.data?.access === 'shared' ? 'shared' : 'saved'}
    >
      <Button
        variant="ghost"
        leadingIcon={ArrowLeft}
        className={styles.back}
        onClick={() => {
          void navigate(query.data?.access === 'shared' ? '/shared' : '/saved')
        }}
      >
        {query.data?.access === 'shared'
          ? 'Back to shared trips'
          : 'Back to saved journeys'}
      </Button>
      {query.isPending ? (
        <p role="status">Loading journey…</p>
      ) : query.data ? (
        <JourneyDetails key={query.data.id} journey={query.data} />
      ) : (
        <section className={styles.failure}>
          <h1>
            {query.error instanceof ApiError && query.error.status === 404
              ? 'Journey not found'
              : 'Journey unavailable'}
          </h1>
          <p role="alert">
            {query.error?.message ?? 'This journey could not be loaded.'}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              void query.refetch()
            }}
          >
            Try again
          </Button>
        </section>
      )}
    </ProductShell>
  )
}

function JourneyDetails({ journey }: { journey: Journey }) {
  const [sharing, setSharing] = useState(false)
  const [tab, setTab] = useState<'itinerary' | 'map'>('itinerary')
  const [showTimes, setShowTimes] = useState(() =>
    journey.annotations.some((item) => item.plannedTime),
  )
  const [editing, setEditing] = useState<EditingField>(null)
  const updates = useJourneyUpdate(journey)
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const id = useId()
  const owner = journey.access === 'owner'
  const Transport = journey.plan.travelMode === 'walking' ? Footprints : Car
  const route = journey.plan.route
  function edit(field: EditingField) {
    updates.reset()
    setEditing(field)
  }
  return (
    <div className={styles.details}>
      <header className={styles.header}>
        {owner && editing?.field === 'name' ? (
          <DetailFieldEditor
            label="Journey name"
            required
            value={journey.name}
            pending={updates.pending}
            error={updates.error}
            onCancel={() => edit(null)}
            onSave={(name) => updates.save(() => ({ name }))}
          />
        ) : (
          <div className={styles.titleRow}>
            <h1>{journey.name}</h1>
            {owner && (
              <IconButton
                icon={Pencil}
                label="Edit journey name"
                disabled={Boolean(editing) || updates.pending}
                onClick={() => edit({ field: 'name' })}
              />
            )}
            {owner && (
              <div className={styles.share}>
                <Button
                  leadingIcon={Share2}
                  disabled={Boolean(editing) || updates.pending}
                  onClick={() => setSharing(true)}
                >
                  Share
                </Button>
                <JourneySharingStatus journeyId={journey.id} />
              </div>
            )}
          </div>
        )}
        <div className={styles.metadata}>
          {owner && editing?.field === 'date' ? (
            <DetailFieldEditor
              label="Journey date (optional)"
              type="date"
              value={journey.date ?? ''}
              pending={updates.pending}
              error={updates.error}
              onCancel={() => edit(null)}
              onSave={(date) => updates.save(() => ({ date: date || null }))}
            />
          ) : (
            <span className={styles.date}>
              <CalendarDays aria-hidden="true" />
              {journey.date ? (
                <time dateTime={journey.date}>{journey.date}</time>
              ) : (
                <span>No date set</span>
              )}
              {owner && (
                <IconButton
                  icon={Pencil}
                  label="Edit journey date"
                  disabled={Boolean(editing) || updates.pending}
                  onClick={() => edit({ field: 'date' })}
                />
              )}
            </span>
          )}
          <span>
            <Transport aria-hidden="true" />
            {journey.plan.travelMode === 'walking' ? 'Walking' : 'Driving'}
          </span>
          <span>
            <Route aria-hidden="true" />
            {formatDistance(route.totalDistanceMeters)}
          </span>
          <span>
            <Clock aria-hidden="true" />
            {formatDuration(route.totalDurationSeconds)}
          </span>
          <span>{route.points.length} locations</span>
        </div>
        {!owner && (
          <p className={styles.readOnly}>
            Shared by {journey.owner.username} · Read-only journey
          </p>
        )}
      </header>
      <div role="tablist" aria-label="Journey view" className={styles.tabs}>
        {(['itinerary', 'map'] as const).map((value, index) => {
          const Icon = value === 'itinerary' ? List : Map
          return (
            <button
              key={value}
              ref={(element) => {
                tabRefs.current[index] = element
              }}
              type="button"
              role="tab"
              id={`${id}-${value}`}
              aria-controls={`${id}-panel`}
              aria-selected={tab === value}
              tabIndex={tab === value ? 0 : -1}
              disabled={Boolean(editing) || updates.pending}
              onClick={() => setTab(value)}
              onKeyDown={(event) => {
                if (
                  !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(
                    event.key,
                  )
                )
                  return
                event.preventDefault()
                const target =
                  event.key === 'Home' ? 0 : event.key === 'End' ? 1 : 1 - index
                setTab(target === 0 ? 'itinerary' : 'map')
                tabRefs.current[target]?.focus()
              }}
            >
              <Icon aria-hidden="true" />
              {value === 'itinerary' ? 'Itinerary' : 'Map view'}
            </button>
          )
        })}
      </div>
      <section
        role="tabpanel"
        id={`${id}-panel`}
        aria-labelledby={`${id}-${tab}`}
        tabIndex={0}
        className={tab === 'map' ? styles.mapView : styles.sheet}
      >
        <div className={tab === 'map' ? styles.compactSheet : undefined}>
          <div className={styles.sheetHeading}>
            <h2>Itinerary</h2>
            {owner && tab === 'itinerary' && (
              <Toggle
                label="Plan times"
                checked={showTimes}
                disabled={Boolean(editing) || updates.pending}
                onChange={setShowTimes}
              />
            )}
          </div>
          {showTimes && tab === 'itinerary' && (
            <p className={styles.timeHeading}>Planned time</p>
          )}
          <JourneyItinerary
            journey={journey}
            compact={tab === 'map'}
            showTimes={tab === 'map' || !owner ? true : showTimes}
            editing={owner ? editing : null}
            onEdit={edit}
            updates={updates}
          />
        </div>
        {tab === 'map' && (
          <OptimisedRouteMap
            route={route}
            hoveredSegmentId={null}
            selectedSegmentId={null}
            unavailableDescription="Maps cannot load right now. You can still review the saved itinerary."
          />
        )}
      </section>
      {owner && sharing && (
        <ShareJourneyModal
          journeyId={journey.id}
          onClose={() => setSharing(false)}
        />
      )}
    </div>
  )
}
