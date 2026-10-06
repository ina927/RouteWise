import type { UserSummary } from '../../contracts/users'
import styles from './Sharing.module.css'

const tones = [
  styles.avatarBlue,
  styles.avatarSlate,
  styles.avatarMuted,
] as const

export function InitialAvatar({ user }: { user: UserSummary }) {
  // Stable ID keeps the colour consistent across rows and username changes.
  let hash = 0
  for (const character of user.id)
    hash = (hash * 31 + character.codePointAt(0)!) >>> 0
  const initial = Array.from(user.username.trim())[0]?.toUpperCase() ?? '?'
  return (
    <span
      aria-hidden="true"
      className={`${styles.avatar} ${tones[hash % tones.length]}`}
    >
      {initial}
    </span>
  )
}
