import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { AppShell } from '../components/layout/AppShell'
import type { SidebarProps } from '../components/layout/Sidebar'
import { incomingInvitationsOptions } from '../features/sharing/sharingData'

// Application data/navigation belongs here; the shared AppShell remains presentational.
export function ProductShell({
  activeItem,
  children,
}: {
  activeItem: SidebarProps['activeItem']
  children: ReactNode
}) {
  const navigate = useNavigate()
  const invitations = useQuery(incomingInvitationsOptions)
  return (
    <AppShell
      activeItem={activeItem}
      pendingSharedCount={
        invitations.data?.filter((item) => item.status === 'pending').length ??
        0
      }
      onNavigate={(destination) => {
        void navigate(`/${destination}`)
      }}
    >
      {children}
    </AppShell>
  )
}
