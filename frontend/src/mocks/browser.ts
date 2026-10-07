import { setupWorker } from 'msw/browser'
import { handlers } from './handlers/index.ts'

export const worker = setupWorker(...handlers)
let starting: Promise<void> | undefined

// Keep the start promise beside the worker, so re-evaluating the bootstrap
// module during development cannot start the same worker twice.
export function startMocking(): Promise<void> {
  starting ??= startWorker()
  return starting
}

async function startWorker() {
  const registration = await worker.start({
    serviceWorker: { url: `${import.meta.env.BASE_URL}mockServiceWorker.js` },
    onUnhandledFrame: 'bypass',
  })

  if (registration) {
    // An updated SW claims existing tabs, but its per-client mocking state is
    // new. Re-enter bootstrap instead of leaving an already-rendered app using
    // the previous worker's channel. Initial activation is handled by start().
    const startedWorker = registration.active
    const onControllerChange = () => {
      if (navigator.serviceWorker.controller !== startedWorker)
        window.location.reload()
    }
    navigator.serviceWorker.addEventListener(
      'controllerchange',
      onControllerChange,
    )
    import.meta.hot?.dispose(() => {
      navigator.serviceWorker.removeEventListener(
        'controllerchange',
        onControllerChange,
      )
    })
    if (navigator.serviceWorker.controller !== startedWorker) {
      // Navigation owns the next bootstrap; never render this stale document.
      return new Promise<void>(() => window.location.reload())
    }
  }
}
