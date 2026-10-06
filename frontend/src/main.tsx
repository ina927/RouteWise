import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { enableMocking } from './app/enableMocking'
import './styles/global.css'

const root = document.getElementById('root')

if (!root) {
  throw new Error('The application root element is missing.')
}

enableMocking()
  .then(() => {
    createRoot(root).render(
      <StrictMode>
        <App />
      </StrictMode>,
    )
  })
  .catch((error: unknown) => {
    console.error('RouteWise bootstrap failed.', error)
    root.textContent =
      'Unable to start RouteWise. Check the development configuration.'
  })
