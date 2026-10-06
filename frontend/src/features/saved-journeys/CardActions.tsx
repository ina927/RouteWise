import { Copy, Pencil, Route, Trash2 } from 'lucide-react'
import { JourneyCardMenu } from '../journeys/JourneyCardMenu'
export type CardAction = 'edit' | 'rename' | 'duplicate' | 'delete'
const actions = [
  { id: 'edit', label: 'Edit route', icon: Route },
  { id: 'rename', label: 'Rename', icon: Pencil },
  { id: 'duplicate', label: 'Duplicate', icon: Copy },
  { id: 'delete', label: 'Delete', icon: Trash2, destructive: true },
] as const
export function CardActions(props: {
  name: string
  disabled: boolean
  onAction: (action: CardAction) => void
}) {
  return <JourneyCardMenu<CardAction> {...props} actions={actions} />
}
