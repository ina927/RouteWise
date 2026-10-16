import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { LayoutGrid, List, Plus } from 'lucide-react'
import { duplicateJourney, getOwnedJourneys } from '../../api/journeysApi'
import type { JourneySummary } from '../../contracts/journeys'
import { ProductShell } from '../../app/ProductShell'
import { PageHeader } from '../../components/layout/PageHeader'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/SearchInput'
import { Select } from '../../components/ui/Select'
import { cacheJourney, ownedJourneysKey } from '../journey-details/journeyData'
import { JourneyCard } from './JourneyCard'
import { JourneyListRow } from './JourneyListRow'
import { SavedJourneyDialog } from './SavedJourneyDialog'
import type { CardAction } from './CardActions'
import styles from './SavedJourneys.module.css'
import cardStyles from '../journeys/JourneyCards.module.css'

const names = new Intl.Collator(undefined, {
  sensitivity: 'base',
  numeric: true,
})

type Sort = 'name' | 'newest' | 'oldest'
type View = 'cards' | 'list'

function compare(a: JourneySummary, b: JourneySummary, sort: Sort) {
  if (sort !== 'name') {
    if (!a.date && b.date) return 1
    if (a.date && !b.date) return -1
    if (a.date && b.date && a.date !== b.date)
      return (sort === 'newest' ? -1 : 1) * a.date.localeCompare(b.date)
  }
  return names.compare(a.name, b.name) || a.id.localeCompare(b.id)
}

export function SavedJourneysPage() {
  const navigate = useNavigate()
  const client = useQueryClient()
  const query = useQuery({
    queryKey: ownedJourneysKey,
    queryFn: ({ signal }) => getOwnedJourneys(signal),
    retry: false,
  })
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<Sort>('name')

  const [view, setView] = useState<View>(() =>
    localStorage.getItem('routewise-saved-view') === 'list' ? 'list' : 'cards',
  )

  function changeView(next: View) {
    setView(next)
    localStorage.setItem('routewise-saved-view', next)
  }

  const [dialog, setDialog] = useState<{
    kind: 'rename' | 'delete'
    journey: JourneySummary
  } | null>(null)
  const [busyIds, setBusyIds] = useState<string[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [feedback, setFeedback] = useState<{ message: string } | null>(null)
  const inFlight = useRef(new Set<string>())
  const duplicate = useMutation({ mutationFn: duplicateJourney })
  useEffect(() => {
    if (!feedback) return
    const timer = setTimeout(() => setFeedback(null), 5000)
    return () => clearTimeout(timer)
  }, [feedback])
  const term = search.trim().toLocaleLowerCase()
  const journeys = (query.data ?? [])
    .filter((journey) =>
      [journey.name, journey.start.name, journey.end.name].some((value) =>
        value.toLocaleLowerCase().includes(term),
      ),
    )
    .sort((a, b) => compare(a, b, sort))
  const newJourney = () => {
    void navigate('/plan')
  }
  async function action(journey: JourneySummary, action: CardAction) {
    if (inFlight.current.has(journey.id)) return
    if (action === 'edit') {
      await navigate('/plan', { state: { editJourneyId: journey.id } })
      return
    }
    if (action === 'rename' || action === 'delete') {
      setDialog({ kind: action, journey })
      return
    }
    inFlight.current.add(journey.id)
    setBusyIds((current) => [...current, journey.id])
    setErrors((current) => ({ ...current, [journey.id]: '' }))
    try {
      await cacheJourney(client, await duplicate.mutateAsync(journey.id))
      setFeedback({ message: 'Journey duplicated' })
    } catch (error) {
      setErrors((current) => ({
        ...current,
        [journey.id]:
          error instanceof Error
            ? error.message
            : 'Unable to duplicate journey.',
      }))
    } finally {
      inFlight.current.delete(journey.id)
      setBusyIds((current) => current.filter((id) => id !== journey.id))
    }
  }
  return (
    <ProductShell activeItem="saved">
      <PageHeader
        title="Saved journeys"
        subtitle="Your trips, all in one place."
        actions={
          <Button leadingIcon={Plus} onClick={newJourney}>
            New journey
          </Button>
        }
      />
      <div className={styles.toolbar}>
        <SearchInput
          label="Search journeys"
          hideLabel
          placeholder="Search journeys…"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Select
          label="Sort journeys"
          hideLabel
          value={sort}
          onChange={(event) => {
            const value = event.target.value
            if (value === 'name' || value === 'newest' || value === 'oldest')
              setSort(value)
          }}
        >
          <option value="name">Name A–Z</option>
          <option value="newest">Date newest</option>
          <option value="oldest">Date oldest</option>
        </Select>

        <div className={styles.viewToggle} role="group" aria-label="Journey view">
          <button
            type="button"
            className={`${styles.viewButton} ${view === 'cards' ? styles.activeView : ''}`}
            aria-label="Card view"
            aria-pressed={view === 'cards'}
            onClick={() => changeView('cards')}
          >
            <LayoutGrid aria-hidden="true" />
          </button>
          <button
            type="button"
            className={`${styles.viewButton} ${view === 'list' ? styles.activeView : ''}`}
            aria-label="List view"
            aria-pressed={view === 'list'}
            onClick={() => changeView('list')}
          >
            <List aria-hidden="true" />
          </button>
        </div>
      </div>
      <p role="status" className={styles.feedback}>
        {feedback?.message ?? ''}
      </p>
      {query.isPending ? (
        <p role="status">Loading saved journeys…</p>
      ) : query.isError ? (
        <div className={styles.empty}>
          <p role="alert">{query.error.message}</p>
          <Button
            variant="secondary"
            onClick={() => {
              void query.refetch()
            }}
          >
            Retry
          </Button>
        </div>
      ) : !query.data.length ? (
        <section className={styles.empty}>
          <h2>No saved journeys yet</h2>
          <p>Plan a route and save it to keep your itinerary here.</p>
          <Button leadingIcon={Plus} onClick={newJourney}>
            New journey
          </Button>
        </section>
      ) : !journeys.length ? (
        <section className={styles.empty}>
          <h2>No matching journeys</h2>
          <p>Try another journey or place name.</p>
          <Button variant="secondary" onClick={() => setSearch('')}>
            Clear search
          </Button>
        </section>
      ) : (
        <div className={view === 'cards' ? cardStyles.grid : styles.list}>
          {journeys.map((journey) => {
            const props = {
              journey,
              busy: busyIds.includes(journey.id),
              error: errors[journey.id],
              onOpen: () => {
                void navigate(`/journeys/${encodeURIComponent(journey.id)}`)
              },
              onAction: (choice: CardAction) => {
                void action(journey, choice)
              },
            }

            return view === 'cards' ? (
              <JourneyCard key={journey.id} {...props} />
            ) : (
              <JourneyListRow key={journey.id} {...props} />
            )
          })}
        </div>
      )}
      {dialog && (
        <SavedJourneyDialog
          key={`${dialog.kind}-${dialog.journey.id}`}
          {...dialog}
          onClose={() => setDialog(null)}
        />
      )}
    </ProductShell>
  )
}
