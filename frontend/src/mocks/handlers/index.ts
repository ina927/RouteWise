import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { journeyHandlers } from './journeys.ts'
import { routeHandlers } from './routes.ts'
import { sharingHandlers } from './sharing.ts'

export const handlers = [
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
