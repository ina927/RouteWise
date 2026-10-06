import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { readJson, respond } from '../http.ts'
import { fixtureOptimisation, fixtureRoute } from '../routeFixtures.ts'
import { parseCalculate, parseOptimize } from '../validation.ts'

export const routeHandlers = [
  http.post(apiUrl('/routes/optimize'), ({ request }) =>
    respond(async () => {
      return HttpResponse.json(
        fixtureOptimisation(parseOptimize(await readJson(request))),
      )
    }),
  ),
  http.post(apiUrl('/routes/calculate'), ({ request }) =>
    respond(async () => {
      const body = parseCalculate(await readJson(request))
      return HttpResponse.json(fixtureRoute(body.points, body.travelMode))
    }),
  ),
]
