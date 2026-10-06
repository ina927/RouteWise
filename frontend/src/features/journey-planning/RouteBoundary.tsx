import { Navigate } from 'react-router'
import { usePlanning } from './planningContext'

// Phase 5 replaces only this neutral content; route and state ownership persist.
export function RouteBoundary() {
  const { result } = usePlanning()
  if (!result) return <Navigate to="/plan" replace />
  return <p>Optimised Route — implementation pending Phase 5.</p>
}
