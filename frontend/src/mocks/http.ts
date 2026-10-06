import { HttpResponse } from 'msw/http'
import type { ApiErrorBody } from '../contracts/errors.ts'

export class MockError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export async function readJson(request: Request): Promise<unknown> {
  try {
    return (await request.json()) as unknown
  } catch {
    throw new MockError(400, 'A valid JSON request body is required.')
  }
}

export async function respond(action: () => Response | Promise<Response>) {
  try {
    return await action()
  } catch (error) {
    if (!(error instanceof MockError)) throw error
    return HttpResponse.json<ApiErrorBody>(
      { message: error.message },
      { status: error.status },
    )
  }
}
