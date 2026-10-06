import { createBrowserRouter, Navigate } from 'react-router'
import { PlanningLayout } from '../features/journey-planning/PlanningLayout'
import { PlanJourneyPage } from '../features/journey-planning/PlanJourneyPage'
import { RouteBoundary } from '../features/journey-planning/RouteBoundary'
import { JourneyDetailsPage } from '../features/journey-details/JourneyDetailsPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Navigate to="/plan" replace />,
  },
  {
    path: '/plan',
    element: <PlanningLayout />,
    children: [
      { index: true, element: <PlanJourneyPage /> },
      { path: 'route', element: <RouteBoundary /> },
    ],
  },
  {
    path: '/journeys/:journeyId',
    element: <JourneyDetailsPage />,
  },
  {
    path: '*',
    element: <main>Page not found.</main>,
  },
])
