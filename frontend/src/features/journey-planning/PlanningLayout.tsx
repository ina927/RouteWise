import { useState } from 'react'
import { Outlet, useNavigate } from 'react-router'
import { AppShell } from '../../components/layout/AppShell'
import { initialDraft, type PlanningResult } from './planning'
import { PlanningContext } from './planningContext'

export function PlanningLayout() {
  const [draft, setDraft] = useState(initialDraft)
  const [result, setResult] = useState<PlanningResult | null>(null)
  const navigate = useNavigate()
  return (
    <PlanningContext value={{ draft, setDraft, result, setResult }}>
      <AppShell
        activeItem="plan"
        disabledItems={['saved', 'shared', 'account']}
        onNavigate={(destination) => {
          if (destination === 'plan') void navigate('/plan')
        }}
      >
        <Outlet />
      </AppShell>
    </PlanningContext>
  )
}
