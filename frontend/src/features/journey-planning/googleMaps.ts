import type { Coordinates, Place } from '../../contracts/places.ts'

// Only the SDK members used by this adapter are described here. Vendor objects
// never enter React draft state or the HTTP contracts; no wrapper dependency.
type GoogleRuntime = { importLibrary: (name: string) => Promise<unknown> }
type GooglePlace = {
  id: string
  displayName?: string | null
  formattedAddress?: string | null
  location?: { lat: () => number; lng: () => number } | null
  fetchFields: (options: { fields: string[] }) => Promise<unknown>
}
type Prediction = {
  placeId: string
  text: { toString: () => string }
  toPlace: () => GooglePlace
}
type PlacesLibrary = {
  AutocompleteSessionToken: new () => object
  AutocompleteSuggestion: {
    fetchAutocompleteSuggestions: (request: {
      input: string
      sessionToken: object
    }) => Promise<{ suggestions: { placePrediction?: Prediction }[] }>
  }
}
type MapInstance = {
  setCenter: (coordinates: Coordinates) => void
  setZoom: (zoom: number) => void
  fitBounds: (
    bounds: { north: number; south: number; east: number; west: number },
    padding: number,
  ) => void
  addListener: (name: string, callback: () => void) => { remove: () => void }
  getMapCapabilities: () => { isAdvancedMarkersAvailable?: boolean }
}
type MapsLibrary = {
  Map: new (
    element: HTMLElement,
    options: {
      center: Coordinates
      zoom: number
      mapId: string
      mapTypeControl: boolean
      streetViewControl: boolean
      fullscreenControl: boolean
    },
  ) => MapInstance
}
type Marker = HTMLElement & { map: MapInstance | null }
type MarkerLibrary = {
  AdvancedMarkerElement: new (options: {
    map: MapInstance
    position: Coordinates
    title: string
  }) => Marker
  PinElement: new (options: {
    background: string
    borderColor: string
    glyphColor: string
  }) => HTMLElement
}
type GoogleWindow = Window &
  typeof globalThis & {
    google?: { maps: GoogleRuntime }
    routeWiseGoogleReady?: () => void
    gm_authFailure?: () => void
  }

const unavailable =
  'Map and place search are unavailable. Please try again later.'
let loading: Promise<GoogleRuntime> | undefined
let failure: Error | undefined
const failureListeners = new Set<(message: string) => void>()

export function onGoogleFailure(listener: (message: string) => void) {
  failureListeners.add(listener)
  if (failure) listener(failure.message)
  return () => {
    failureListeners.delete(listener)
  }
}

function fail() {
  failure = new Error(unavailable)
  failureListeners.forEach((listener) => listener(unavailable))
  return failure
}

function mapId() {
  const configured = import.meta.env.VITE_GOOGLE_MAP_ID?.trim()
  // The public demo ID is explicitly development-only, even if set in an env file.
  if (!import.meta.env.DEV && (!configured || configured === 'DEMO_MAP_ID'))
    throw new Error(unavailable)
  return configured || 'DEMO_MAP_ID'
}

async function bounded<T>(operation: Promise<T>): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined
  try {
    return await Promise.race([
      operation,
      new Promise<never>((_, reject) => {
        timer = setTimeout(() => reject(new Error(unavailable)), 15000)
      }),
    ])
  } finally {
    clearTimeout(timer)
  }
}

export function loadGoogle(): Promise<GoogleRuntime> {
  if (failure) return Promise.reject(failure)
  if (loading) return loading
  loading = new Promise<GoogleRuntime>((resolve, reject) => {
    const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim()
    if (!key) {
      reject(new Error(unavailable))
      return
    }
    mapId()
    const host = window as GoogleWindow
    const script = document.createElement('script')
    const timer = setTimeout(() => {
      script.remove()
      reject(fail())
    }, 15000)
    host.gm_authFailure = () => {
      clearTimeout(timer)
      reject(fail())
    }
    host.routeWiseGoogleReady = () => {
      clearTimeout(timer)
      if (failure) {
        reject(failure)
        return
      }
      if (host.google?.maps.importLibrary) resolve(host.google.maps)
      else reject(fail())
    }
    script.async = true
    script.src = `https://maps.googleapis.com/maps/api/js?${new URLSearchParams({ key, v: 'weekly', loading: 'async', callback: 'routeWiseGoogleReady' })}`
    script.onerror = () => {
      clearTimeout(timer)
      reject(fail())
    }
    document.head.append(script)
  })
  return loading
}

export type PlaceSuggestion = {
  id: string
  label: string
  select: () => Promise<Place>
}

export function createPlaceSearch() {
  let token: object | undefined
  let generation = 0
  return {
    reset() {
      token = undefined
      generation++
    },
    async search(input: string): Promise<PlaceSuggestion[]> {
      const current = generation
      const runtime = await loadGoogle()
      const library = (await bounded(
        runtime.importLibrary('places'),
      )) as PlacesLibrary
      if (current !== generation) return []
      token ??= new library.AutocompleteSessionToken()
      const { suggestions } = await bounded(
        library.AutocompleteSuggestion.fetchAutocompleteSuggestions({
          input,
          sessionToken: token,
        }),
      )
      return suggestions.flatMap(({ placePrediction }) => {
        if (!placePrediction) return []
        const prediction = placePrediction
        return [
          {
            id: prediction.placeId,
            label: prediction.text.toString(),
            select: async () => {
              const place = prediction.toPlace()
              token = undefined
              generation++
              await bounded(
                place.fetchFields({
                  fields: ['id', 'displayName', 'formattedAddress', 'location'],
                }),
              )
              const lat = place.location?.lat()
              const lng = place.location?.lng()
              if (
                !place.id ||
                !place.displayName ||
                lat === undefined ||
                lng === undefined ||
                !Number.isFinite(lat) ||
                !Number.isFinite(lng)
              )
                throw new Error(
                  'This place has no usable location. Choose another result.',
                )
              return {
                placeId: place.id,
                name: place.displayName,
                ...(place.formattedAddress
                  ? { address: place.formattedAddress }
                  : {}),
                coordinates: { lat, lng },
              }
            },
          },
        ]
      })
    },
  }
}

export type PlanMarker = {
  id: string
  place: Place
  role: 'start' | 'intermediate' | 'end'
}
export type PlanMap = {
  update: (points: PlanMarker[]) => void
  dispose: () => void
}

export async function createPlanMap(
  element: HTMLElement,
  onFailure: () => void,
  signal: AbortSignal,
): Promise<PlanMap> {
  const runtime = await loadGoogle()
  const maps = (await bounded(runtime.importLibrary('maps'))) as MapsLibrary
  const markersLibrary = (await bounded(
    runtime.importLibrary('marker'),
  )) as MarkerLibrary
  signal.throwIfAborted()
  const neutral = { lat: 20, lng: 0 }
  const map = new maps.Map(element, {
    center: neutral,
    zoom: 2,
    mapId: mapId(),
    mapTypeControl: false,
    streetViewControl: false,
    fullscreenControl: false,
  })
  const capabilities = map.addListener('mapcapabilities_changed', () => {
    if (map.getMapCapabilities().isAdvancedMarkersAvailable === false)
      onFailure()
  })
  let markers: Marker[] = []
  let current: PlanMarker[] = []
  function clear() {
    markers.forEach((marker) => {
      marker.map = null
    })
    markers = []
  }
  function fit() {
    const first = current[0]
    if (!first) {
      map.setCenter(neutral)
      map.setZoom(2)
      return
    }
    const coordinates = current.map((point) => point.place.coordinates)
    if (
      coordinates.every(
        (point) =>
          point.lat === first.place.coordinates.lat &&
          point.lng === first.place.coordinates.lng,
      )
    ) {
      map.setCenter(first.place.coordinates)
      map.setZoom(14)
      return
    }
    const latitudes = coordinates.map((point) => point.lat)
    // Select the smallest circular longitude span, including trips across the date line.
    const longitudes = coordinates
      .map((point) => (point.lng + 360) % 360)
      .sort((a, b) => a - b)
    let gap = -1
    let west = 0
    let east = 0
    longitudes.forEach((longitude, index) => {
      const next = longitudes[(index + 1) % longitudes.length] ?? longitude
      const difference =
        (index === longitudes.length - 1 ? next + 360 : next) - longitude
      if (difference > gap) {
        gap = difference
        west = next
        east = longitude
      }
    })
    map.fitBounds(
      {
        north: Math.max(...latitudes),
        south: Math.min(...latitudes),
        west: west > 180 ? west - 360 : west,
        east: east > 180 ? east - 360 : east,
      },
      48,
    )
  }
  const observer = new ResizeObserver(() => fit())
  observer.observe(element)
  return {
    update(points) {
      current = points
      clear()
      const css = getComputedStyle(element)
      const solid = css.getPropertyValue('--color-text-on-solid').trim()
      markers = points.map((point) => {
        const background = css
          .getPropertyValue(
            point.role === 'intermediate' ? '--color-primary' : '--color-route',
          )
          .trim()
        const marker = new markersLibrary.AdvancedMarkerElement({
          map,
          position: point.place.coordinates,
          title: `${point.role === 'intermediate' ? 'Place to visit' : point.role === 'start' ? 'Start' : 'End'}: ${point.place.name}`,
        })
        marker.append(
          new markersLibrary.PinElement({
            background,
            borderColor: solid,
            glyphColor: solid,
          }),
        )
        return marker
      })
      fit()
    },
    dispose() {
      observer.disconnect()
      capabilities.remove()
      clear()
      element.replaceChildren()
    },
  }
}
