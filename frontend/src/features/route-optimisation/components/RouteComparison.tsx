import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react'
import type { RouteResult, OptimisationObjective } from '../../../contracts/routes'
import { formatDistance, formatDuration } from '../formatters'
import styles from './RouteComparison.module.css'

type Props = {
  optimisedRoute: RouteResult
  currentRoute: RouteResult
  objective: OptimisationObjective
}

export function RouteComparison({
  optimisedRoute,
  currentRoute,
  objective,
}: Props) {
  const isDistance = objective === 'distance'

  const originalCost = isDistance
    ? optimisedRoute.totalDistanceMeters
    : optimisedRoute.totalDurationSeconds

  const modifiedCost = isDistance
    ? currentRoute.totalDistanceMeters
    : currentRoute.totalDurationSeconds

  const difference = modifiedCost - originalCost
  const percentage =
    originalCost > 0 ? (difference / originalCost) * 100 : 0

  const tolerance = 0.000001
  const status =
    Math.abs(difference) < tolerance
      ? 'equal'
      : difference < 0
        ? 'better'
        : 'worse'

  const formatCost = isDistance ? formatDistance : formatDuration

  const message =
    status === 'equal'
      ? 'Your changes have no effect on the selected objective.'
      : `Your modified route is ${Math.abs(percentage).toFixed(1)}% ${
          status === 'better' ? 'better' : 'worse'
        } than the SA route.`

  const Icon =
    status === 'better'
      ? ArrowDownRight
      : status === 'worse'
        ? ArrowUpRight
        : Minus

  return (
    <section className={styles.comparison} aria-label="Route comparison">
      <h3 className={styles.heading}>Route comparison</h3>

      <div className={styles.values}>
        <div>
          <span className={styles.label}>SA optimised</span>
          <strong>{formatCost(originalCost)}</strong>
        </div>
        <div>
          <span className={styles.label}>Your modified route</span>
          <strong>{formatCost(modifiedCost)}</strong>
        </div>
      </div>

      <div className={`${styles.message} ${styles[status]}`}>
        <Icon aria-hidden="true" />
        <span>{message}</span>
      </div>
    </section>
  )
}