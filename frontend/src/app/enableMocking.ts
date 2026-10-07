export async function enableMocking() {
  if (!import.meta.env.DEV || import.meta.env.VITE_ENABLE_MOCKS !== 'true')
    return

  const { startMocking } = await import('../mocks/browser.ts')
  await startMocking()
}
