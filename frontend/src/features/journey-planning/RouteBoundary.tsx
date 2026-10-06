import { Navigate } from 'react-router'
import { usePlanning } from './planningContext'
import { OptimisedRoutePage } from '../route-optimisation/OptimisedRoutePage'

export function RouteBoundary() {
  const { result } = usePlanning()
  if (!result) return <Navigate to="/plan" replace />
  return <OptimisedRoutePage result={result} />
}
