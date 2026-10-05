import { createBrowserRouter } from 'react-router'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <main>RouteWise scaffold.</main>,
  },
  {
    path: '*',
    element: <main>Page not found.</main>,
  },
])
