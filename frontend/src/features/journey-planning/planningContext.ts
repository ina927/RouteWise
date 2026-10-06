import {
  createContext,
  useContext,
  type Dispatch,
  type SetStateAction,
} from 'react'
import type { PlanningDraft, PlanningResult } from './planning'

export const PlanningContext = createContext<{
  draft: PlanningDraft
  setDraft: Dispatch<SetStateAction<PlanningDraft>>
  result: PlanningResult | null
  setResult: Dispatch<SetStateAction<PlanningResult | null>>
} | null>(null)

export function usePlanning() {
  const value = useContext(PlanningContext)
  if (!value) throw new Error('Planning components require PlanningLayout.')
  return value
}
