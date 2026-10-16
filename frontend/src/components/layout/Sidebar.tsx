import { Bookmark, MapPin, UserCircle, Users } from 'lucide-react'
import { Badge } from '../ui/Badge'
import styles from './Sidebar.module.css'
import routeWiseIcon from '../../assets/RouteWise_Logo.png';

export type SidebarDestination = 'plan' | 'saved' | 'shared' | 'account'
export type SidebarProps = {
  activeItem?: SidebarDestination
  pendingSharedCount?: number
  disabledItems?: readonly SidebarDestination[]
  onNavigate: (destination: SidebarDestination) => void
}

const destinations = [
  { id: 'plan', label: 'Plan', icon: MapPin },
  { id: 'saved', label: 'Saved', icon: Bookmark },
  { id: 'shared', label: 'Shared', icon: Users },
  { id: 'account', label: 'Account', icon: UserCircle },
] as const

export function Sidebar({
  activeItem,
  pendingSharedCount = 0,
  disabledItems = [],
  onNavigate,
}: SidebarProps) {
  return (
    <aside className={styles.sidebar}>
      <div className={styles.brand}>
        <span className={styles.brandIconBox}>
          <img
            src={routeWiseIcon}
            alt=""
            className={styles.brandIcon}
          />
        </span>
        <span className={styles.brandText}>RouteWise</span>
      </div>
      <nav aria-label="Main navigation" className={styles.navigation}>
        <ul className={styles.list}>
          {destinations.map(({ id, label, icon: Icon }) => (
            <li
              key={id}
              className={id === 'account' ? styles.account : undefined}
            >
              <button
                type="button"
                disabled={disabledItems.includes(id)}
                className={styles.item}
                aria-current={activeItem === id ? 'page' : undefined}
                aria-label={
                  id === 'shared' && pendingSharedCount > 0
                    ? `Shared, ${pendingSharedCount} pending invitations`
                    : undefined
                }
                onClick={() => onNavigate(id)}
              >
                <span className={styles.icon}>
                  <Icon aria-hidden="true" />
                  {id === 'shared' && pendingSharedCount > 0 && (
                    <Badge
                      tone="notification"
                      className={styles.count}
                      aria-hidden="true"
                    >
                      {pendingSharedCount > 99 ? '99+' : pendingSharedCount}
                    </Badge>
                  )}
                </span>
                <span>{label}</span>
              </button>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  )
}
