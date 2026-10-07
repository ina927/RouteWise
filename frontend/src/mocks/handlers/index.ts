import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { journeyHandlers } from './journeys.ts'
import { routeHandlers } from './routes.ts'
import { sharingHandlers } from './sharing.ts'
import { authHandlers } from './auth.ts'
import { requireMockViewer } from '../store.ts'
import { respond } from '../http.ts'

export const handlers = [
  ...authHandlers,
  // No session cookie is simulated: this guard uses the same in-memory viewer
  // as ownership, invitations and sharing. Undefined continues to the next handler.
  http.all(apiUrl('/*'), ({ request }) =>
    respond(() => {
      if (new URL(request.url).pathname !== new URL(apiUrl('/health')).pathname)
        requireMockViewer()
      return undefined
    }),
  ),
  ...routeHandlers,
  ...journeyHandlers,
  ...sharingHandlers,
  // Preserve the actual backend health probe; unknown product APIs fail locally.
  http.all(apiUrl('/*'), ({ request }) => {
    if (new URL(request.url).pathname === new URL(apiUrl('/health')).pathname)
      return
    return HttpResponse.json(
      { message: 'No mock handler exists for this API request.' },
      { status: 501 },
    )
  }),
]
