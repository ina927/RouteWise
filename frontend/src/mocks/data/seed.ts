import type { Journey } from '../../contracts/journeys.ts'
import type { Place, RoutePoint } from '../../contracts/places.ts'
import type { InvitationStatus } from '../../contracts/sharing.ts'
import { fixtureRoute } from '../routeFixtures.ts'

// These are fictional users and synthetic place IDs, not Google credentials/data.
export const mockUsers = [
  { id: 'user-river', username: 'river' },
  { id: 'user-avery', username: 'avery' },
  { id: 'user-quinn', username: 'quinn' },
  { id: 'user-casey', username: 'casey' },
]

export const mockPlaces = {
  central: {
    placeId: 'mock-place-central',
    name: 'Sydney Central Station',
    address: 'Eddy Avenue, Sydney',
    coordinates: { lat: -33.883, lng: 151.207 },
  },
  opera: {
    placeId: 'mock-place-opera',
    name: 'Sydney Opera House',
    address: 'Bennelong Point, Sydney',
    coordinates: { lat: -33.857, lng: 151.215 },
  },
  museum: {
    placeId: 'mock-place-museum',
    name: 'Australian Museum',
    address: '1 William Street, Sydney',
    coordinates: { lat: -33.874, lng: 151.213 },
  },
  bondi: {
    placeId: 'mock-place-bondi',
    name: 'Bondi Beach',
    address: 'Queen Elizabeth Drive, Bondi Beach',
    coordinates: { lat: -33.891, lng: 151.277 },
  },
  airport: {
    placeId: 'mock-place-airport',
    name: 'Sydney Airport',
    address: 'Mascot, Sydney',
    coordinates: { lat: -33.939, lng: 151.175 },
  },
} satisfies Record<string, Place>

export type MockInvitation = {
  id: string
  journeyId: string
  senderId: string
  recipientId: string
  status: InvitationStatus
}

export function createSeed() {
  const definitions = [
    {
      id: 'journey-harbour',
      name: 'Harbour loop',
      ownerIndex: 0,
      places: [mockPlaces.central, mockPlaces.opera, mockPlaces.central],
      endMode: 'returnToStart' as const,
    },
    {
      id: 'journey-city',
      name: 'City sights',
      ownerIndex: 0,
      places: [mockPlaces.central, mockPlaces.museum, mockPlaces.opera],
      endMode: 'open' as const,
    },
    {
      id: 'journey-coast',
      name: 'Coastal afternoon',
      ownerIndex: 1,
      places: [mockPlaces.central, mockPlaces.bondi, mockPlaces.airport],
      endMode: 'fixed' as const,
    },
    {
      id: 'journey-museum',
      name: 'Museum walk',
      ownerIndex: 2,
      places: [mockPlaces.central, mockPlaces.museum, mockPlaces.opera],
      endMode: 'open' as const,
    },
  ]
  const journeys: Omit<Journey, 'access'>[] = definitions.map((definition) => {
    const user = mockUsers[definition.ownerIndex]
    if (!user) throw new Error('Missing fixture owner.')
    const points: RoutePoint[] = definition.places.map((place, index) => ({
      id: `${definition.id}-point-${index}`,
      place,
    }))
    const travelMode =
      definition.id === 'journey-museum' ? 'walking' : 'driving'
    return {
      id: definition.id,
      name: definition.name,
      owner: { id: user.id, username: user.username },
      date: '2026-10-18',
      plan: {
        travelMode,
        objective: 'distance',
        endMode: definition.endMode,
        route: fixtureRoute(points, travelMode),
      },
      annotations: [
        { pointId: `${definition.id}-point-0`, plannedTime: '09:00' },
        { pointId: `${definition.id}-point-1`, note: 'Bring a camera.' },
        { pointId: `${definition.id}-point-2`, plannedTime: '16:30' },
      ],
    }
  })
  const invitations: MockInvitation[] = [
    {
      id: 'invite-incoming-pending',
      journeyId: 'journey-coast',
      senderId: 'user-avery',
      recipientId: 'user-river',
      status: 'pending',
    },
    {
      id: 'invite-incoming-accepted',
      journeyId: 'journey-museum',
      senderId: 'user-quinn',
      recipientId: 'user-river',
      status: 'accepted',
    },
    {
      id: 'invite-other-recipient',
      journeyId: 'journey-museum',
      senderId: 'user-quinn',
      recipientId: 'user-casey',
      status: 'accepted',
    },
    {
      id: 'invite-outgoing-pending',
      journeyId: 'journey-city',
      senderId: 'user-river',
      recipientId: 'user-casey',
      status: 'pending',
    },
    {
      id: 'invite-outgoing-accepted',
      journeyId: 'journey-city',
      senderId: 'user-river',
      recipientId: 'user-avery',
      status: 'accepted',
    },
    {
      id: 'invite-outgoing-declined',
      journeyId: 'journey-harbour',
      senderId: 'user-river',
      recipientId: 'user-quinn',
      status: 'declined',
    },
  ]
  return structuredClone({ journeys, invitations })
}
