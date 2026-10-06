import {
  createContext,
  useContext,
  type Dispatch,
  type SetStateAction,
} from 'react'
import type { PlanningDraft, PlanningResult } from './planning'
import type { Journey } from '../../contracts/journeys'

export const PlanningContext = createContext<{
  draft: PlanningDraft
  editingJourney: Journey | null
  setDraft: Dispatch<SetStateAction<PlanningDraft>>
  result: PlanningResult | null
  setResult: Dispatch<SetStateAction<PlanningResult | null>>
} | null>(null)

export function usePlanning() {
  const value = useContext(PlanningContext)
  if (!value) throw new Error('Planning components require PlanningLayout.')
  return value
}
