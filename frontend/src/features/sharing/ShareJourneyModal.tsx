import { useEffect, useId, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { searchUsers } from '../../api/usersApi'
import { sendInvitation } from '../../api/sharingApi'
import type { UserSummary } from '../../contracts/users'
import { InitialAvatar } from './InitialAvatar'
import type { Invitation } from '../../contracts/sharing'
import { Modal } from '../../components/ui/Modal'
import { SearchInput } from '../../components/ui/SearchInput'
import { Button } from '../../components/ui/Button'
import { journeyInvitationsKey, journeyInvitationsOptions } from './sharingData'
import { InvitationBadge } from './InvitationBadge'
import styles from './Sharing.module.css'

export function ShareJourneyModal({
  journeyId,
  onClose,
}: {
  journeyId: string
  onClose: () => void
}) {
  const client = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const guard = useRef(false)
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<UserSummary | null>(null)
  const [expanded, setExpanded] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const listId = useId()
  const term = query.trim()
  const [debounced, setDebounced] = useState('')
  const invitations = useQuery(journeyInvitationsOptions(journeyId))
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(term), 300)
    return () => clearTimeout(timer)
  }, [term])
  const searching = Boolean(term) && term !== debounced
  const users = useQuery({
    queryKey: ['user-search', debounced],
    queryFn: ({ signal }) => searchUsers(debounced, signal),
    enabled: Boolean(debounced) && !searching && Boolean(term),
    retry: false,
  })
  const blockingInvitations = (invitations.data ?? []).filter(
    (item) => item.status !== 'removed',
  )
  // The legacy boundary may match other fields; only username matches can be selected.
  const suggestions =
    !searching && invitations.isSuccess && !invitations.isFetching
      ? (users.data ?? []).filter(
          (user) =>
            user.username
              .toLocaleLowerCase()
              .includes(term.toLocaleLowerCase()) &&
            !blockingInvitations.some((item) => item.recipient.id === user.id),
        )
      : []
  const showSuggestions =
    expanded && !selected && Boolean(term) && suggestions.length > 0
  const alreadyInvited =
    blockingInvitations.some(
      (item) =>
        item.recipient.username.toLocaleLowerCase() ===
        term.toLocaleLowerCase(),
    ) ||
    (users.isSuccess &&
      !users.isFetching &&
      !suggestions.length &&
      blockingInvitations.some((item) =>
        item.recipient.username
          .toLocaleLowerCase()
          .includes(term.toLocaleLowerCase()),
      ))
  function select(user: UserSummary) {
    setSelected(user)
    setExpanded(false)
    setActiveIndex(-1)
    invite.reset()
  }
  const invite = useMutation({
    mutationFn: async (recipientId: string) => {
      const invitation = await sendInvitation(journeyId, { recipientId })
      const key = journeyInvitationsKey(journeyId)
      await client.cancelQueries({ queryKey: key })
      client.setQueryData<Invitation[]>(key, (current) => [
        ...(current ?? []).filter((item) => item.id !== invitation.id),
        invitation,
      ])
      await client.invalidateQueries({ queryKey: key })
    },
  })
  async function send() {
    const recipientId = selected?.id
    if (!recipientId) return
    if (
      guard.current ||
      !invitations.isSuccess ||
      invitations.isFetching ||
      blockingInvitations.some((item) => item.recipient.id === recipientId)
    )
      return
    guard.current = true
    try {
      await invite.mutateAsync(recipientId)
      setSelected(null)
      setQuery('')
      setExpanded(false)
      setActiveIndex(-1)
      requestAnimationFrame(() => input.current?.focus())
    } catch {
      /* Preserve input/results and allow retry. */
    } finally {
      guard.current = false
    }
  }
  return (
    <Modal
      open
      title="Share journey"
      onClose={() => {
        if (!guard.current) onClose()
      }}
      description="Invite another user to view this journey. They can view the itinerary and map, but cannot edit it."
      initialFocusRef={input}
      footer={
        <Button
          variant="secondary"
          disabled={invite.isPending}
          onClick={() => {
            if (!guard.current) onClose()
          }}
        >
          Done
        </Button>
      }
    >
      <div className={styles.dialog}>
        <section aria-label="Invite someone">
          <div
            className={styles.recipientArea}
            onBlur={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget))
                setExpanded(false)
            }}
          >
            <div className={styles.recipientControls}>
              <div className={styles.lookup}>
                {selected ? (
                  <div className={styles.selected}>
                    <span className={styles.identity}>
                      <InitialAvatar user={selected} />
                      <span className={styles.username}>
                        {selected.username}
                      </span>
                    </span>
                    <Button
                      variant="ghost"
                      disabled={invite.isPending}
                      onClick={() => {
                        setSelected(null)
                        setQuery('')
                        setExpanded(false)
                        invite.reset()
                        requestAnimationFrame(() => input.current?.focus())
                      }}
                    >
                      Change
                    </Button>
                  </div>
                ) : (
                  <SearchInput
                    ref={input}
                    label="Search by username"
                    placeholder="Search users..."
                    value={query}
                    disabled={invite.isPending}
                    autoComplete="off"
                    role="combobox"
                    aria-autocomplete="list"
                    aria-expanded={showSuggestions}
                    aria-controls={showSuggestions ? listId : undefined}
                    aria-activedescendant={
                      showSuggestions && suggestions[activeIndex]
                        ? listId + '-' + activeIndex
                        : undefined
                    }
                    onFocus={() => setExpanded(true)}
                    onKeyDown={(event) => {
                      if (event.key === 'Escape' && expanded) {
                        event.preventDefault()
                        event.stopPropagation()
                        setExpanded(false)
                        setActiveIndex(-1)
                      } else if (
                        (event.key === 'ArrowDown' ||
                          event.key === 'ArrowUp') &&
                        suggestions.length
                      ) {
                        event.preventDefault()
                        setExpanded(true)
                        setActiveIndex((current) =>
                          event.key === 'ArrowDown'
                            ? (current + 1) % suggestions.length
                            : current <= 0
                              ? suggestions.length - 1
                              : current - 1,
                        )
                      } else if (
                        event.key === 'Enter' &&
                        showSuggestions &&
                        suggestions[activeIndex]
                      ) {
                        event.preventDefault()
                        select(suggestions[activeIndex])
                      }
                    }}
                    onChange={(event) => {
                      setQuery(event.target.value)
                      setExpanded(true)
                      setActiveIndex(-1)
                      if (!guard.current) invite.reset()
                    }}
                  />
                )}
                {showSuggestions && (
                  <ul
                    id={listId}
                    role="listbox"
                    aria-label="Matching usernames"
                    className={styles.suggestions}
                  >
                    {suggestions.map((user, index) => (
                      <li
                        key={user.id}
                        id={listId + '-' + index}
                        role="option"
                        aria-selected={activeIndex === index}
                        className={styles.suggestion}
                        onMouseDown={(event) => event.preventDefault()}
                        onClick={() => select(user)}
                      >
                        <InitialAvatar user={user} />
                        <span className={styles.username}>{user.username}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <Button
                disabled={
                  !selected ||
                  !invitations.isSuccess ||
                  invitations.isFetching ||
                  blockingInvitations.some(
                    (item) => item.recipient.id === selected.id,
                  )
                }
                loading={invite.isPending}
                loadingLabel="Inviting?"
                onClick={() => {
                  void send()
                }}
              >
                Invite
              </Button>
            </div>
          </div>
          {!selected &&
          term &&
          invitations.isSuccess &&
          !invitations.isFetching &&
          !searching &&
          alreadyInvited ? (
            <p role="alert" className={styles.error}>
              This user has already been invited to this journey.
            </p>
          ) : !selected && term && (searching || users.isPending) ? (
            <p role="status" className={styles.secondary}>
              Searching users?
            </p>
          ) : !selected && term && users.isError ? (
            <div>
              <p role="alert" className={styles.error}>
                {users.error.message}
              </p>
              <Button
                variant="ghost"
                onClick={() => {
                  void users.refetch()
                }}
              >
                Retry search
              </Button>
            </div>
          ) : (
            !selected &&
            term &&
            invitations.isSuccess &&
            !invitations.isFetching &&
            !suggestions.length && (
              <p role="status" className={styles.secondary}>
                No available usernames found.
              </p>
            )
          )}
          {invite.isPending && (
            <p role="status" className={styles.secondary}>
              Sending invitation…
            </p>
          )}
          {invite.isSuccess && (
            <p role="status" className={styles.feedback}>
              Invitation sent.
            </p>
          )}
          {invite.error && (
            <p role="alert" className={styles.error}>
              {invite.error.message}
            </p>
          )}
        </section>
        <section aria-labelledby="existing-invitations-heading">
          <h3 id="existing-invitations-heading">Existing invitations</h3>
          {invitations.isPending ? (
            <p role="status">Loading invitations…</p>
          ) : invitations.isError ? (
            <div>
              <p role="alert" className={styles.error}>
                {invitations.error.message}
              </p>
              <Button
                variant="ghost"
                onClick={() => {
                  void invitations.refetch()
                }}
              >
                Retry invitations
              </Button>
            </div>
          ) : invitations.data.length ? (
            <ul className={styles.list}>
              {invitations.data.map((item) => (
                <li key={item.id} className={styles.invitationRow}>
                  <span className={styles.identity}>
                    <InitialAvatar user={item.recipient} />
                    <span className={styles.username}>
                      {item.recipient.username}
                    </span>
                  </span>
                  <InvitationBadge status={item.status} />
                </li>
              ))}
            </ul>
          ) : (
            <p className={styles.secondary}>No invitations yet.</p>
          )}
        </section>
      </div>
    </Modal>
  )
}
