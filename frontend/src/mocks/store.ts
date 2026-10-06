import type { Journey, JourneySummary } from '../contracts/journeys.ts'
import type { Invitation } from '../contracts/sharing.ts'
import type { UserSummary } from '../contracts/users.ts'
import { createSeed, mockUsers, type MockInvitation } from './data/seed.ts'
import { MockError } from './http.ts'
import {
  object,
  parseAnnotations,
  parseDate,
  parsePlan,
  requireValid,
  text,
} from './validation.ts'

let state = createSeed()
let viewerId = 'user-river'
let sequence = 0
const removedShares = new Set<string>()

// Dev tools / smoke checks only. There is no session or authentication contract.
export function resetMockStore() {
  state = createSeed()
  viewerId = 'user-river'
  sequence = 0
  removedShares.clear()
}

export function setMockViewer(id: string) {
  user(id)
  viewerId = id
}

function user(id: string): UserSummary {
  const found = mockUsers.find((item) => item.id === id)
  if (!found) throw new MockError(404, 'User not found.')
  return { id: found.id, username: found.username }
}

function findJourney(id: string) {
  const journey = state.journeys.find((item) => item.id === id)
  if (!journey) throw new MockError(404, 'Journey not found.')
  return journey
}

function ownedJourney(id: string) {
  const journey = findJourney(id)
  if (journey.owner.id !== viewerId)
    throw new MockError(403, 'Only the owner can change or share this journey.')
  return journey
}

function canViewShared(id: string) {
  return state.invitations.some(
    (item) =>
      item.journeyId === id &&
      item.recipientId === viewerId &&
      item.status === 'accepted' &&
      !removedShares.has(item.id),
  )
}

function summary(journey: Omit<Journey, 'access'>): JourneySummary {
  const route = journey.plan.route
  const start = route.points[0]?.place
  const end = route.points.at(-1)?.place
  if (!start || !end) throw new Error('Invalid mock journey route.')
  return structuredClone({
    id: journey.id,
    owner: journey.owner,
    name: journey.name,
    ...(journey.date === undefined ? {} : { date: journey.date }),
    // MOCK-ONLY: choose the start place; final photo selection policy is undecided.
    representativePlaceId: start.placeId,
    travelMode: journey.plan.travelMode,
    start: { placeId: start.placeId, name: start.name },
    end: { placeId: end.placeId, name: end.name },
    locationCount: route.points.length,
    totalDistanceMeters: route.totalDistanceMeters,
    totalDurationSeconds: route.totalDurationSeconds,
  })
}

function invitation(item: MockInvitation): Invitation {
  return {
    id: item.id,
    journey: summary(findJourney(item.journeyId)),
    sender: user(item.senderId),
    recipient: user(item.recipientId),
    status: item.status,
  }
}

export function getMockJourney(id: string): Journey {
  const journey = findJourney(id)
  const access = journey.owner.id === viewerId ? 'owner' : 'shared'
  if (access === 'shared' && !canViewShared(id))
    throw new MockError(403, 'This journey is not shared with you.')
  return structuredClone({ ...journey, access })
}

export function listOwnedJourneys() {
  return state.journeys
    .filter((item) => item.owner.id === viewerId)
    .map(summary)
}

export function listSharedJourneys() {
  return state.journeys
    .filter((item) => item.owner.id !== viewerId && canViewShared(item.id))
    .map(summary)
}

export function createMockJourney(value: unknown) {
  const body = object(value)
  const name = text(body.name, 'Journey name')
  const plan = parsePlan(body.plan)
  const annotations = parseAnnotations(
    body.annotations ?? [],
    plan.route.points,
  )
  const date = body.date === undefined ? undefined : parseDate(body.date)
  const journey: Omit<Journey, 'access'> = {
    id: `journey-created-${++sequence}`,
    owner: user(viewerId),
    name,
    plan,
    annotations,
    ...(date === undefined ? {} : { date }),
  }
  state.journeys.push(journey)
  return getMockJourney(journey.id)
}

export function updateMockJourney(id: string, value: unknown) {
  const current = ownedJourney(id)
  const body = object(value)
  requireValid(
    ['name', 'date', 'plan', 'annotations'].some((key) =>
      Object.hasOwn(body, key),
    ),
    'Provide a field to update.',
  )
  // Validate the complete next state before mutation, including annotations.
  const next = structuredClone(current)
  if (body.name !== undefined) next.name = text(body.name, 'Journey name')
  if (body.date === null) delete next.date
  else if (body.date !== undefined) next.date = parseDate(body.date)
  if (body.plan !== undefined) next.plan = parsePlan(body.plan)
  next.annotations =
    body.annotations === undefined
      ? next.annotations.filter((annotation) =>
          next.plan.route.points.some(
            (point) => point.id === annotation.pointId,
          ),
        )
      : parseAnnotations(body.annotations, next.plan.route.points)
  state.journeys = state.journeys.map((item) => (item.id === id ? next : item))
  return getMockJourney(id)
}

export function duplicateMockJourney(id: string) {
  const copy = structuredClone(ownedJourney(id))
  copy.id = `journey-created-${++sequence}`
  copy.name = `${copy.name} (copy)` // Mock-only; final duplicate naming is undecided.
  state.journeys.push(copy)
  // Invitations live separately and are deliberately not copied.
  return getMockJourney(copy.id)
}

export function deleteMockJourney(id: string) {
  ownedJourney(id)
  state.journeys = state.journeys.filter((item) => item.id !== id)
  for (const item of state.invitations.filter((item) => item.journeyId === id))
    removedShares.delete(item.id)
  state.invitations = state.invitations.filter((item) => item.journeyId !== id)
}

export function searchMockUsers(query: string) {
  const term = query.trim().toLowerCase()
  if (!term) return []
  return mockUsers
    .filter(
      (item) =>
        item.id !== viewerId &&
        (item.username.toLowerCase().includes(term) ||
          item.email.toLowerCase().includes(term)),
    )
    .map((item) => user(item.id))
}

export function listJourneyInvitations(id: string) {
  ownedJourney(id)
  return state.invitations
    .filter((item) => item.journeyId === id)
    .map(invitation)
}

export function listIncomingInvitations() {
  return state.invitations
    .filter((item) => item.recipientId === viewerId)
    .map(invitation)
}

export function sendMockInvitation(journeyId: string, recipientId: string) {
  ownedJourney(journeyId)
  user(recipientId)
  requireValid(recipientId !== viewerId, 'Choose another user to invite.')
  // Mock-only repeat-invitation policy; backend/product handling is not agreed.
  if (
    state.invitations.some(
      (item) =>
        item.journeyId === journeyId && item.recipientId === recipientId,
    )
  ) {
    throw new MockError(
      409,
      'Mock-only: an invitation already exists for this recipient and journey.',
    )
  }
  const item: MockInvitation = {
    id: `invite-created-${++sequence}`,
    journeyId,
    senderId: viewerId,
    recipientId,
    status: 'pending',
  }
  state.invitations.push(item)
  return invitation(item)
}

export function respondMockInvitation(
  id: string,
  status: 'accepted' | 'declined',
) {
  const item = state.invitations.find((item) => item.id === id)
  if (!item) throw new MockError(404, 'Invitation not found.')
  if (item.recipientId !== viewerId)
    throw new MockError(403, 'Only the recipient can respond.')
  if (item.status !== 'pending')
    throw new MockError(
      409,
      'Mock-only: this invitation has already been answered.',
    )
  item.status = status
  return invitation(item)
}

export function removeMockSharedJourney(journeyId: string) {
  findJourney(journeyId)
  const relationships = state.invitations.filter(
    (item) =>
      item.journeyId === journeyId &&
      item.recipientId === viewerId &&
      item.status === 'accepted' &&
      !removedShares.has(item.id),
  )
  if (!relationships.length)
    throw new MockError(404, 'Accepted shared journey not found.')
  // Retain invitation history, but remove this viewer's listing and read access.
  relationships.forEach((item) => removedShares.add(item.id))
}
