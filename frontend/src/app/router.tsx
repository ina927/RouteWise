import { createBrowserRouter, Navigate } from 'react-router'
import { PlanningLayout } from '../features/journey-planning/PlanningLayout'
import { PlanJourneyPage } from '../features/journey-planning/PlanJourneyPage'
import { RouteBoundary } from '../features/journey-planning/RouteBoundary'
import { JourneyDetailsPage } from '../features/journey-details/JourneyDetailsPage'
import { SavedJourneysPage } from '../features/saved-journeys/SavedJourneysPage'
import { SharedTripsPage } from '../features/sharing/SharedTripsPage'

import { AuthGuard } from '../features/auth/AuthGuard'
import { AuthPage } from '../features/auth/AuthPage'
import { AccountPage } from '../features/auth/AccountPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Navigate to="/plan" replace />,
  },
  {
    element: <AuthGuard publicOnly />,
    children: [
      { path: '/login', element: <AuthPage key="login" mode="login" /> },
      { path: '/signup', element: <AuthPage key="signup" mode="signup" /> },
    ],
  },
  {
    element: <AuthGuard />,
    children: [
      { path: '/account', element: <AccountPage /> },
      {
        path: '/plan',
        element: <PlanningLayout />,
        children: [
          { index: true, element: <PlanJourneyPage /> },
          { path: 'route', element: <RouteBoundary /> },
        ],
      },
      {
        path: '/saved',
        element: <SavedJourneysPage />,
      },
      {
        path: '/shared',
        element: <SharedTripsPage />,
      },
      {
        path: '/journeys/:journeyId',
        element: <JourneyDetailsPage />,
      },
    ],
  },
  {
    path: '*',
    element: <main>Page not found.</main>,
  },
])
