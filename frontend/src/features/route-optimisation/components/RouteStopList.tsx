import { Fragment, type ReactNode } from 'react'
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Car, Footprints, GripHorizontal } from 'lucide-react'
import { EndpointPin } from '../../../components/ui/EndpointPin'
import { IconButton } from '../../../components/ui/IconButton'
import type { RoutePoint } from '../../../contracts/places'
import type {
  EndMode,
  RouteResult,
  TravelMode,
} from '../../../contracts/routes'
import { formatDistance, formatDuration } from '../formatters'
import styles from './RouteStopList.module.css'

type Props = {
  route: RouteResult
  travelMode: TravelMode
  endMode: EndMode
  disabled: boolean
  hoveredSegmentId: string | null
  selectedSegmentId: string | null
  onHover: (id: string | null) => void
  onSelect: (id: string) => void
  onReorder: (points: RoutePoint[]) => void
}

function StopContent({
  point,
  number,
  endpoint,
  handle,
}: {
  point: RoutePoint
  number?: number
  endpoint?: 'Start' | 'End' | 'Return to start'
  handle?: ReactNode
}) {
  return (
    <div className={styles.stop}>
      {endpoint ? (
        <EndpointPin
          role={endpoint === 'End' ? 'end' : 'start'}
          className={styles.endpoint}
        />
      ) : (
        <span className={styles.number} aria-label={`Stop ${number}`}>
          {number}
        </span>
      )}
      <div className={styles.place}>
        <div>
          {endpoint && <span className={styles.label}>{endpoint}</span>}
          <p>{point.place.name}</p>
        </div>
        {handle}
      </div>
    </div>
  )
}

function SortableStop({
  point,
  number,
  disabled,
}: {
  point: RoutePoint
  number: number
  disabled: boolean
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    isDragging,
  } = useSortable({ id: point.id, disabled })
  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform) }}
      className={isDragging ? styles.dragging : undefined}
    >
      <StopContent
        point={point}
        number={number}
        handle={
          <IconButton
            variant="ghost"
            icon={GripHorizontal}
            label={`Move stop ${number}: ${point.place.name}`}
            ref={setActivatorNodeRef}
            className={styles.handle}
            {...attributes}
            {...listeners}
            disabled={disabled}
          />
        }
      />
    </div>
  )
}

export function RouteStopList({
  route,
  travelMode,
  endMode,
  disabled,
  hoveredSegmentId,
  selectedSegmentId,
  onHover,
  onSelect,
  onReorder,
}: Props) {
  const intermediates = route.points.slice(1, -1)
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  )
  const ModeIcon = travelMode === 'walking' ? Footprints : Car
  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={({ active, over }) => {
        if (disabled || !over || active.id === over.id) return
        const from = intermediates.findIndex((point) => point.id === active.id)
        const to = intermediates.findIndex((point) => point.id === over.id)
        const first = route.points[0]
        const last = route.points.at(-1)
        if (from < 0 || to < 0 || !first || !last) return
        onReorder([first, ...arrayMove(intermediates, from, to), last])
      }}
      accessibility={{
        screenReaderInstructions: {
          draggable:
            'To move an intermediate stop, press Space, use the arrow keys, then press Space to drop or Escape to cancel. Start and End cannot be moved.',
        },
      }}
    >
      <SortableContext
        items={intermediates.map((point) => point.id)}
        strategy={verticalListSortingStrategy}
      >
        <ol className={styles.list} aria-label="Route stops">
          {route.points.map((point, index) => {
            const next = route.points[index + 1]
            const segment = next
              ? route.segments.find(
                  (item) =>
                    item.fromPointId === point.id && item.toPointId === next.id,
                )
              : undefined
            const selected = segment?.id === selectedSegmentId
            return (
              <Fragment key={point.id}>
                <li>
                  {index === 0 || index === route.points.length - 1 ? (
                    <StopContent
                      point={point}
                      endpoint={
                        index === 0
                          ? 'Start'
                          : endMode === 'returnToStart'
                            ? 'Return to start'
                            : 'End'
                      }
                    />
                  ) : (
                    <SortableStop
                      point={point}
                      number={index}
                      disabled={disabled || intermediates.length < 2}
                    />
                  )}
                  {segment && (
                    <div className={styles.segmentConnector}>
                      <button
                        type="button"
                        className={`${styles.segment} ${selected ? styles.selected : ''} ${segment.id === hoveredSegmentId ? styles.hovered : ''}`}
                        aria-label={`${point.place.name} to ${next?.place.name}: ${formatDuration(segment.durationSeconds)}, ${formatDistance(segment.distanceMeters)}`}
                        aria-pressed={selected}
                        onMouseEnter={() => onHover(segment.id)}
                        onMouseLeave={() => onHover(null)}
                        onFocus={() => onHover(segment.id)}
                        onBlur={() => onHover(null)}
                        onClick={() => onSelect(segment.id)}
                      >
                        <ModeIcon aria-hidden="true" />
                        <span>
                          {formatDuration(segment.durationSeconds)} ·{' '}
                          {formatDistance(segment.distanceMeters)}
                        </span>
                        {selected && (
                          <span className={styles.selectedLabel}>Selected</span>
                        )}
                      </button>
                    </div>
                  )}
                </li>
              </Fragment>
            )
          })}
        </ol>
      </SortableContext>
    </DndContext>
  )
}
