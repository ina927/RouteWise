import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { MapPinned } from 'lucide-react'
import { login, signup } from '../../api/authApi'
import { ApiError } from '../../api/httpClient'
import { Button } from '../../components/ui/Button'
import { TextInput } from '../../components/ui/TextInput'
import { usernameError, passwordError } from '../../utils/authValidation'
import { useAuth } from './authContext'
import styles from './Auth.module.css'

export function AuthPage({ mode }: { mode: 'login' | 'signup' }) {
  const creating = mode === 'signup'
  const { setSession } = useAuth()
  const navigate = useNavigate()
  const form = useRef<HTMLFormElement>(null)
  const alert = useRef<HTMLParagraphElement>(null)
  const guard = useRef(false)
  const [busy, setBusy] = useState(false)
  const [username, setUsername] = useState('')
  const [errors, setErrors] = useState<{
    username?: string
    password?: string
    confirm?: string
    form?: string
  }>({})
  async function submit() {
    if (guard.current || !form.current) return
    const fields = new FormData(form.current)
    const passwordValue = fields.get('password')
    const confirmValue = fields.get('confirm')
    const password = typeof passwordValue === 'string' ? passwordValue : ''
    const confirm = typeof confirmValue === 'string' ? confirmValue : ''
    const next = {
      username: creating
        ? usernameError(username)
        : !username.trim()
          ? 'Enter a username.'
          : undefined,
      password: creating
        ? passwordError(password)
        : !password
          ? 'Enter a password.'
          : undefined,
      confirm:
        creating && confirm !== password ? 'Passwords must match.' : undefined,
    }
    setErrors(next)
    if (Object.values(next).some(Boolean)) {
      requestAnimationFrame(() =>
        form.current
          ?.querySelector<HTMLInputElement>('[aria-invalid=true]')
          ?.focus(),
      )
      return
    }
    guard.current = true
    setBusy(true)
    try {
      // Credentials stay in native inputs/request scope, never Query mutation variables.
      const user = await (creating ? signup : login)({
        username: username.trim(),
        password,
      })
      await setSession(user)
      await navigate('/plan', { replace: true })
    } catch (error) {
      if (creating && error instanceof ApiError && error.status === 409) {
        setErrors({ username: 'This username is already taken.' })
        requestAnimationFrame(() =>
          form.current
            ?.querySelector<HTMLInputElement>('[name=username]')
            ?.focus(),
        )
      } else {
        setErrors({
          form:
            !creating && error instanceof ApiError && error.status === 401
              ? 'Invalid username or password.'
              : 'Unable to complete your request. Please try again.',
        })
        requestAnimationFrame(() => alert.current?.focus())
      }
    } finally {
      form.current
        ?.querySelectorAll<HTMLInputElement>('input[type=password]')
        .forEach((input) => {
          input.value = ''
        })
      guard.current = false
      setBusy(false)
    }
  }
  return (
    <main className={styles.standalone}>
      <section className={styles.panel} aria-labelledby="auth-heading">
        <div className={styles.brand}>
          <MapPinned aria-hidden="true" />
          RouteWise
        </div>
        <h1 id="auth-heading" className={styles.heading}>
          {creating ? 'Create your account' : 'Log in'}
        </h1>
        <form
          ref={form}
          className={styles.form}
          noValidate
          onSubmit={(event) => {
            event.preventDefault()
            void submit()
          }}
          aria-busy={busy}
        >
          <TextInput
            name="username"
            label="Username"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            required
            value={username}
            disabled={busy}
            onChange={(event) => setUsername(event.target.value)}
            error={errors.username}
            placeholder={
              creating
                ? '3–20 letters, numbers or underscores'
                : 'Enter username'
            }
          />
          <TextInput
            name="password"
            label="Password"
            type="password"
            autoComplete={creating ? 'new-password' : 'current-password'}
            required
            disabled={busy}
            error={errors.password}
            placeholder={creating ? 'At least 8 characters' : 'Enter password'}
          />
          {creating && (
            <TextInput
              name="confirm"
              label="Confirm password"
              placeholder="Re-enter password"
              type="password"
              autoComplete="new-password"
              required
              disabled={busy}
              error={errors.confirm}
            />
          )}
          {errors.form && (
            <p ref={alert} tabIndex={-1} role="alert" className={styles.error}>
              {errors.form}
            </p>
          )}
          <Button
            type="submit"
            loading={busy}
            loadingLabel={creating ? 'Creating account…' : 'Logging in…'}
          >
            {creating ? 'Sign up' : 'Log in'}
          </Button>
          {busy && (
            <p role="status" className={styles.secondary}>
              {creating ? 'Creating your account…' : 'Logging in…'}
            </p>
          )}
        </form>
        <p className={styles.secondary}>
          {creating ? 'Already have an account? ' : 'Don’t have an account? '}
          {busy ? (
            <span>{creating ? 'Log in' : 'Sign up'}</span>
          ) : (
            <Link to={creating ? '/login' : '/signup'}>
              {creating ? 'Log in' : 'Sign up'}
            </Link>
          )}
        </p>
      </section>
    </main>
  )
}
