import { useRef, useState } from 'react'
import { deleteAccount, logout } from '../../api/authApi'
import { ProductShell } from '../../app/ProductShell'
import { PageHeader } from '../../components/layout/PageHeader'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { useAuth } from './authContext'
import styles from './Auth.module.css'

export function AccountPage() {
  const { session, setSession } = useAuth()
  const [deleting, setDeleting] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const guard = useRef(false)
  const cancel = useRef<HTMLButtonElement>(null)
  async function endSession(remove: boolean) {
    if (guard.current) return
    guard.current = true
    setBusy(true)
    setError('')
    try {
      await (remove ? deleteAccount : logout)()
      // The guard performs the one redirect together with ending this session.
      await setSession(null, remove ? '/signup' : '/login')
    } catch {
      setError(
        remove
          ? 'Unable to delete your account. Please try again.'
          : 'Unable to log out. Please try again.',
      )
    } finally {
      guard.current = false
      setBusy(false)
    }
  }
  function close() {
    if (!guard.current) {
      setDeleting(false)
      setError('')
    }
  }
  return (
    <ProductShell activeItem="account">
      <div className={styles.account}>
        <PageHeader title="Account" />
        <section className={styles.accountSection} aria-label="Your account">
          <dl>
            <dt>Username</dt>
            <dd>{session.data?.username}</dd>
          </dl>
          <Button
            loading={busy && !deleting}
            disabled={busy}
            loadingLabel="Logging out…"
            onClick={() => {
              void endSession(false)
            }}
          >
            Log out
          </Button>
          {!deleting && error && (
            <p role="alert" className={styles.error}>
              {error}
            </p>
          )}
        </section>
        <section
          className={styles.accountSection}
          aria-labelledby="danger-heading"
        >
          <h2 id="danger-heading">Danger zone</h2>
          <p className={styles.secondary}>
            Permanently delete your account, owned journeys and associated
            sharing data.
          </p>
          <Button
            variant="danger"
            disabled={busy}
            onClick={() => {
              setError('')
              setDeleting(true)
            }}
          >
            Delete account
          </Button>
        </section>
      </div>
      {deleting && (
        <Modal
          open
          title="Delete account?"
          onClose={close}
          initialFocusRef={cancel}
          description="This permanently deletes your account, all journeys you own and associated invitations and sharing relationships. This cannot be undone."
          footer={
            <>
              <Button
                ref={cancel}
                variant="secondary"
                disabled={busy}
                onClick={close}
              >
                Cancel
              </Button>
              <Button
                variant="danger"
                loading={busy}
                loadingLabel="Deleting account…"
                onClick={() => {
                  void endSession(true)
                }}
              >
                Delete account
              </Button>
            </>
          }
        >
          {busy && <p role="status">Deleting your account…</p>}
          {error && (
            <p role="alert" className={styles.error}>
              {error}
            </p>
          )}
        </Modal>
      )}
    </ProductShell>
  )
}
