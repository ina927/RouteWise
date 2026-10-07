import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { readJson, respond } from '../http.ts'
import {
  currentMockUser,
  signupMockUser,
  loginMockUser,
  logoutMockUser,
  deleteMockAccount,
} from '../store.ts'

export const authHandlers = [
  http.get(apiUrl('/auth/me'), () =>
    respond(() => HttpResponse.json(currentMockUser())),
  ),
  http.post(apiUrl('/auth/signup'), ({ request }) =>
    respond(async () =>
      HttpResponse.json(signupMockUser(await readJson(request)), {
        status: 201,
      }),
    ),
  ),
  http.post(apiUrl('/auth/login'), ({ request }) =>
    respond(async () =>
      HttpResponse.json(loginMockUser(await readJson(request))),
    ),
  ),
  http.post(apiUrl('/auth/logout'), () =>
    respond(() => {
      logoutMockUser()
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.delete(apiUrl('/account'), () =>
    respond(() => {
      deleteMockAccount()
      return new HttpResponse(null, { status: 204 })
    }),
  ),
]
