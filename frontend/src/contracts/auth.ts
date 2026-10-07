import type { UserSummary } from './users.ts'

// Session identity; public sharing identity remains UserSummary.
export type CurrentUser = UserSummary
export type AuthRequest = { username: string; password: string }
