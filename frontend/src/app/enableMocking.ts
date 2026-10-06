export async function enableMocking() {
  if (!import.meta.env.DEV || import.meta.env.VITE_ENABLE_MOCKS !== 'true')
    return

  const { worker } = await import('../mocks/browser.ts')
  await worker.start({
    serviceWorker: { url: `${import.meta.env.BASE_URL}mockServiceWorker.js` },
    onUnhandledFrame: 'bypass',
  })
}
