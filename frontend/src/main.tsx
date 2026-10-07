import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { enableMocking } from './app/enableMocking'
import './styles/global.css'

const root = document.getElementById('root')

if (!root) {
  throw new Error('The application root element is missing.')
}

async function bootstrap(container: HTMLElement) {
  await enableMocking()
  // Do not evaluate the router/providers (or future route loaders) until MSW
  // has acknowledged that interception is ready for this document.
  const { App } = await import('./app/App')
  createRoot(container).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

void bootstrap(root).catch((error: unknown) => {
  console.error('RouteWise bootstrap failed.', error)
  root.textContent =
    'Unable to start RouteWise. Check the development configuration.'
})
