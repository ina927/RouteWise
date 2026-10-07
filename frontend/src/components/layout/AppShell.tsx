import { useId, type ReactNode } from 'react'
import { Sidebar, type SidebarProps } from './Sidebar'
import styles from './AppShell.module.css'

type AppShellProps = SidebarProps & { children: ReactNode }

export function AppShell({ children, ...sidebarProps }: AppShellProps) {
  const mainId = useId()
  return (
    <div className={styles.shell}>
      <a href={`#${mainId}`} className={styles.skip}>
        Skip to main content
      </a>
      <Sidebar {...sidebarProps} />
      <main id={mainId} tabIndex={-1} className={styles.main}>
        {children}
      </main>
    </div>
  )
}
