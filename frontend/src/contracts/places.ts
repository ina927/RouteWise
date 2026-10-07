export type Coordinates = { lat: number; lng: number }

export type Place = {
  placeId: string
  name: string
  address?: string
  coordinates: Coordinates
}

// An occurrence ID is independent of Google placeId, including on a return leg.
export type RoutePoint = { id: string; place: Place }
