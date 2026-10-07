import { Car, Clock, Footprints, MapPin, Plus, StickyNote } from 'lucide-react'
import type { Journey } from '../../contracts/journeys'
import { Button } from '../../components/ui/Button'
import { DetailFieldEditor } from './DetailFieldEditor'
import { PlacePhoto } from './PlacePhoto'
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import type { useJourneyUpdate } from './journeyData'
import { replaceAnnotationField } from './journeyData'
import styles from './JourneyDetails.module.css'

export type EditingField = {
  field: 'name' | 'date' | 'note' | 'plannedTime'
  pointId?: string
} | null

export function JourneyItinerary({
  journey,
  compact = false,
  showTimes,
  editing,
  onEdit,
  updates,
}: {
  journey: Journey
  compact?: boolean
  showTimes: boolean
  editing: EditingField
  onEdit: (field: EditingField) => void
  updates: ReturnType<typeof useJourneyUpdate>
}) {
  const editable = journey.access === 'owner' && !compact
  const Transport = journey.plan.travelMode === 'walking' ? Footprints : Car
  const points = journey.plan.route.points
  return (
    <ol className={`${styles.itinerary} ${compact ? styles.compact : ''}`}>
      {points.map((point, index) => {
        const endpoint = index === 0 || index === points.length - 1
        const role =
          index === 0
            ? 'Start'
            : index === points.length - 1
              ? 'End'
              : `Stop ${index}`
        const annotation = journey.annotations.find(
          (item) => item.pointId === point.id,
        )
        const next = points[index + 1]
        const segment = journey.plan.route.segments.find(
          (item) =>
            item.fromPointId === point.id && item.toPointId === next?.id,
        )
        const fieldEditing =
          editing?.pointId === point.id ? editing.field : undefined
        function editField(field: 'note' | 'plannedTime') {
          const value = annotation?.[field] ?? ''
          return (
            <DetailFieldEditor
              label={`${field === 'note' ? 'Note' : 'Planned time'} for ${role}: ${point.place.name}`}
              type={field === 'note' ? 'note' : 'time'}
              value={value}
              pending={updates.pending}
              error={updates.error}
              onCancel={() => onEdit(null)}
              onSave={(value) =>
                updates.save((current) => ({
                  annotations: replaceAnnotationField(
                    current.annotations,
                    point.id,
                    field,
                    value,
                  ),
                }))
              }
            />
          )
        }
        return (
          <li key={point.id}>
            <div
              className={`${styles.stop} ${endpoint ? styles.endpoint : ''}`}
            >
              <span className={styles.marker} aria-label={role}>
                {endpoint ? <MapPin aria-hidden="true" /> : index}
              </span>
              {!endpoint && !compact && (
                <PlacePhoto key={point.place.placeId} place={point.place} />
              )}
              <div className={styles.place}>
                {endpoint && <span className={styles.role}>{role}</span>}
                <h3>{point.place.name}</h3>
                {point.place.address && (
                  <p className={styles.address}>{point.place.address}</p>
                )}
                {editable && fieldEditing === 'note' ? (
                  editField('note')
                ) : editable ? (
                  <Button
                    variant="ghost"
                    className={styles.noteButton}
                    leadingIcon={annotation?.note ? StickyNote : Plus}
                    disabled={Boolean(editing) || updates.pending}
                    onClick={() => onEdit({ field: 'note', pointId: point.id })}
                    aria-label={`${annotation?.note ? 'Edit' : 'Add'} note for ${role}: ${point.place.name}`}
                  >
                    {annotation?.note ?? 'Add note'}
                  </Button>
                ) : (
                  annotation?.note && (
                    <p className={styles.note}>
                      <StickyNote aria-hidden="true" />
                      {annotation.note}
                    </p>
                  )
                )}
              </div>
              {showTimes && (
                <div className={styles.time}>
                  {editable && fieldEditing === 'plannedTime' ? (
                    editField('plannedTime')
                  ) : editable ? (
                    <Button
                      variant="secondary"
                      leadingIcon={Clock}
                      disabled={Boolean(editing) || updates.pending}
                      onClick={() =>
                        onEdit({ field: 'plannedTime', pointId: point.id })
                      }
                      aria-label={`Edit planned time for ${role}: ${point.place.name}`}
                    >
                      {annotation?.plannedTime ?? 'Select time'}
                    </Button>
                  ) : (
                    annotation?.plannedTime && (
                      <span className={styles.savedTime}>
                        <Clock aria-hidden="true" />
                        <time>{annotation.plannedTime}</time>
                      </span>
                    )
                  )}
                </div>
              )}
            </div>
            {segment && (
              <div className={styles.segment}>
                <Transport aria-hidden="true" />
                <span>
                  {formatDuration(segment.durationSeconds)} ·{' '}
                  {formatDistance(segment.distanceMeters)}
                </span>
              </div>
            )}
          </li>
        )
      })}
    </ol>
  )
}
