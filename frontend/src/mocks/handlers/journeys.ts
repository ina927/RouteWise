import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { readJson, respond } from '../http.ts'
import {
  createMockJourney,
  deleteMockJourney,
  duplicateMockJourney,
  getMockJourney,
  listOwnedJourneys,
  updateMockJourney,
} from '../store.ts'
import { text } from '../validation.ts'

export const journeyHandlers = [
  http.get(apiUrl('/journeys'), () => HttpResponse.json(listOwnedJourneys())),
  http.post(apiUrl('/journeys'), ({ request }) =>
    respond(async () =>
      HttpResponse.json(createMockJourney(await readJson(request)), {
        status: 201,
      }),
    ),
  ),
  http.get(apiUrl('/journeys/:id'), ({ params }) =>
    respond(() =>
      HttpResponse.json(getMockJourney(text(params.id, 'Journey ID'))),
    ),
  ),
  http.patch(apiUrl('/journeys/:id'), ({ params, request }) =>
    respond(async () =>
      HttpResponse.json(
        updateMockJourney(
          text(params.id, 'Journey ID'),
          await readJson(request),
        ),
      ),
    ),
  ),
  http.post(apiUrl('/journeys/:id/duplicate'), ({ params }) =>
    respond(() =>
      HttpResponse.json(duplicateMockJourney(text(params.id, 'Journey ID')), {
        status: 201,
      }),
    ),
  ),
  http.delete(apiUrl('/journeys/:id'), ({ params }) =>
    respond(() => {
      deleteMockJourney(text(params.id, 'Journey ID'))
      return new HttpResponse(null, { status: 204 })
    }),
  ),
]
