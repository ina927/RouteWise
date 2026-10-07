import type { Coordinates, Place } from '../../contracts/places.ts'
import type { RouteResult } from '../../contracts/routes'

// Only the SDK members used by this adapter are described here. Vendor objects
// never enter React draft state or the HTTP contracts; no wrapper dependency.
type GoogleRuntime = { importLibrary: (name: string) => Promise<unknown> }
type GooglePlace = {
  id: string
  photos?: {
    getURI: (options: { maxWidth: number; maxHeight: number }) => string
    authorAttributions: { displayName: string; uri?: string }[]
  }[]
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
  Place: new (options: { id: string }) => GooglePlace
  AutocompleteSessionToken: new () => object
  AutocompleteSuggestion: {
    fetchAutocompleteSuggestions: (request: {
      input: string
      language: string
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
  InfoWindow: new (options: { maxWidth: number }) => {
    setContent: (content: HTMLElement) => void
    setOptions: (options: { ariaLabel: string }) => void
    open: (options: {
      map: MapInstance
      anchor: Marker
      shouldFocus: boolean
    }) => void
    close: () => void
    addListener: (name: string, callback: () => void) => { remove: () => void }
  }
  Polyline: new (options: LineOptions) => Polyline
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
type LineOptions = {
  map?: MapInstance
  path?: Coordinates[]
  strokeColor?: string
  strokeOpacity?: number
  strokeWeight?: number
  clickable?: boolean
  zIndex?: number
  icons?: {
    icon: {
      path: string
      strokeColor: string
      strokeOpacity: number
      strokeWeight: number
      scale: number
    }
    offset: string
    repeat: string
  }[]
}
type Polyline = {
  setMap: (map: MapInstance | null) => void
  setOptions: (options: LineOptions) => void
}
type Marker = HTMLElement & {
  map: MapInstance | null
  position: Coordinates
  title: string
  gmpClickable: boolean
}
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
    glyphText?: string
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
    script.src = `https://maps.googleapis.com/maps/api/js?${new URLSearchParams({ key, v: 'weekly', language: 'en', loading: 'async', callback: 'routeWiseGoogleReady' })}`
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
          language: 'en',
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

// Transient presentation data only; never part of a Journey or persisted cache.
export type PlacePhotoData = {
  src: string
  authors: { name: string; url?: string }[]
}

export async function getPlacePhoto(
  placeId: string,
  signal: AbortSignal,
): Promise<PlacePhotoData | null> {
  const runtime = await loadGoogle()
  signal.throwIfAborted()
  const library = (await bounded(
    runtime.importLibrary('places'),
  )) as PlacesLibrary
  signal.throwIfAborted()
  const place = new library.Place({ id: placeId })
  await bounded(place.fetchFields({ fields: ['photos'] }))
  signal.throwIfAborted()
  const photo = place.photos?.[0]
  if (!photo) return null
  return {
    src: photo.getURI({ maxWidth: 400, maxHeight: 300 }),
    authors: photo.authorAttributions.map((author) => ({
      name: author.displayName,
      ...(author.uri ? { url: author.uri } : {}),
    })),
  }
}

export type PlanMarker = {
  id: string
  place: Place
  role: 'start' | 'intermediate' | 'end'
  label: string
}
export type PlanMap = {
  update: (points: PlanMarker[]) => void
  dispose: () => void
}

async function createMapScene(
  element: HTMLElement,
  onFailure: () => void,
  signal: AbortSignal,
) {
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
  let coordinates: Coordinates[] = []
  function fit() {
    const first = coordinates[0]
    if (!first) {
      map.setCenter(neutral)
      map.setZoom(2)
      return
    }
    if (
      coordinates.every(
        (point) => point.lat === first.lat && point.lng === first.lng,
      )
    ) {
      map.setCenter(first)
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
  const css = getComputedStyle(element)
  const colours = {
    solid: css.getPropertyValue('--color-text-on-solid').trim(),
    primary: css.getPropertyValue('--color-primary').trim(),
    route: css.getPropertyValue('--color-route').trim(),
  }
  return {
    map,
    maps,
    colours,
    fit(points: Coordinates[]) {
      coordinates = points
      fit()
    },
    marker(
      position: Coordinates,
      title: string,
      intermediate: boolean,
      number?: number,
    ) {
      const marker = new markersLibrary.AdvancedMarkerElement({
        map,
        position,
        title,
      })
      marker.append(
        new markersLibrary.PinElement({
          background: intermediate ? colours.primary : colours.route,
          borderColor: colours.solid,
          glyphColor: colours.solid,
          ...(number === undefined ? {} : { glyphText: String(number) }),
        }),
      )
      return marker
    },
    dispose() {
      observer.disconnect()
      capabilities.remove()
      element.replaceChildren()
    },
  }
}

export async function createPlanMap(
  element: HTMLElement,
  onFailure: () => void,
  signal: AbortSignal,
): Promise<PlanMap> {
  const scene = await createMapScene(element, onFailure, signal)
  const info = new scene.maps.InfoWindow({ maxWidth: 280 })
  const markers = new Map<
    string,
    {
      marker: Marker
      point: PlanMarker
      onClick: () => void
    }
  >()
  let selectedId: string | null = null
  const closed = info.addListener('close', () => {
    selectedId = null
  })

  function show(id: string, shouldFocus: boolean) {
    const entry = markers.get(id)
    if (!entry) return
    const { point, marker } = entry
    const content = document.createElement('div')
    content.style.cssText =
      'font: var(--font-body-secondary); color: var(--color-text); overflow-wrap: anywhere;'
    const role = document.createElement('small')
    role.textContent = point.label
    role.style.cssText =
      'font: var(--font-caption); color: var(--color-text-secondary);'
    const name = document.createElement('h3')
    name.textContent = point.place.name
    name.style.cssText =
      'font: var(--font-label); margin: var(--space-1) 0 var(--space-2);'
    content.append(role, name)
    if (point.place.address) {
      const address = document.createElement('p')
      address.textContent = point.place.address
      address.style.marginBlock = '0 var(--space-2)'
      content.append(address)
    }
    const link = document.createElement('a')
    const url = new URL('https://www.google.com/maps/search/')
    url.search = new URLSearchParams({
      api: '1',
      query: point.place.name,
      query_place_id: point.place.placeId,
    }).toString()
    link.href = url.href
    link.textContent = 'View on Google Maps'
    link.target = '_blank'
    link.rel = 'noopener noreferrer'
    content.append(link)
    selectedId = id
    info.setContent(content)
    info.setOptions({ ariaLabel: `${point.label}: ${point.place.name}` })
    info.open({ map: scene.map, anchor: marker, shouldFocus })
  }
  return {
    update(points) {
      const ids = new Set(points.map((point) => point.id))
      if (selectedId && !ids.has(selectedId)) {
        selectedId = null
        info.close()
      }
      for (const [id, entry] of markers) {
        if (!ids.has(id)) {
          entry.marker.removeEventListener('gmp-click', entry.onClick)
          entry.marker.map = null
          markers.delete(id)
        }
      }
      for (const point of points) {
        const title = `${point.label}: ${point.place.name}`
        const existing = markers.get(point.id)
        if (existing) {
          existing.point = point
          existing.marker.position = point.place.coordinates
          existing.marker.title = title
        } else {
          const marker = scene.marker(
            point.place.coordinates,
            title,
            point.role === 'intermediate',
          )
          marker.gmpClickable = true
          const onClick = () => show(point.id, true)
          marker.addEventListener('gmp-click', onClick)
          markers.set(point.id, { marker, point, onClick })
        }
      }
      scene.fit(points.map((point) => point.place.coordinates))
      if (selectedId) show(selectedId, false)
    },
    dispose() {
      selectedId = null
      closed.remove()
      info.close()
      markers.forEach(({ marker, onClick }) => {
        marker.removeEventListener('gmp-click', onClick)
        marker.map = null
      })
      markers.clear()
      scene.dispose()
    },
  }
}

export type RouteMap = {
  update: (route: RouteResult) => void
  highlight: (hovered: string | null, selected: string | null) => void
  dispose: () => void
}

export async function createRouteMap(
  element: HTMLElement,
  onFailure: () => void,
  signal: AbortSignal,
): Promise<RouteMap> {
  const scene = await createMapScene(element, onFailure, signal)
  let markers: Marker[] = []
  let lines: { id: string; halo: Polyline; line: Polyline }[] = []
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)')
  let frame: number | undefined
  let animated: {
    line: Polyline
    icons: NonNullable<LineOptions['icons']>
  } | null = null
  function stopAnimation() {
    if (frame !== undefined) cancelAnimationFrame(frame)
    frame = undefined
  }
  function syncAnimation() {
    stopAnimation()
    const target = animated
    if (!target) return
    target.icons[0]!.offset = '0px'
    target.line.setOptions({ icons: target.icons })
    if (reducedMotion.matches || document.hidden) return
    const started = performance.now()
    function tick(now: number) {
      if (!target) return
      // One 16px dash interval per second, following the supplied segment path.
      target.icons[0]!.offset = `${((now - started) * 0.016) % 16}px`
      target.line.setOptions({ icons: target.icons })
      frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
  }
  reducedMotion.addEventListener('change', syncAnimation)
  document.addEventListener('visibilitychange', syncAnimation)
  function clear() {
    stopAnimation()
    animated = null
    markers.forEach((marker) => {
      marker.map = null
    })
    lines.forEach(({ halo, line }) => {
      halo.setMap(null)
      line.setMap(null)
    })
    markers = []
    lines = []
  }
  return {
    update(route) {
      clear()
      const first = route.points[0]
      const last = route.points.at(-1)
      const sharedEndpoint =
        first &&
        last &&
        first.place.coordinates.lat === last.place.coordinates.lat &&
        first.place.coordinates.lng === last.place.coordinates.lng
      markers = route.points.flatMap((point, index) => {
        // The list retains both occurrences; co-located endpoints share one visible pin.
        if (sharedEndpoint && index === route.points.length - 1) return []
        const intermediate = index > 0 && index < route.points.length - 1
        const label = intermediate
          ? `Stop ${index}`
          : index === 0
            ? sharedEndpoint
              ? 'Start / End'
              : 'Start'
            : 'End'
        return [
          scene.marker(
            point.place.coordinates,
            `${label}: ${point.place.name}`,
            intermediate,
            intermediate ? index : undefined,
          ),
        ]
      })
      // Draw only supplied segment geometry, never derive road paths from places.
      lines = route.segments.map((segment) => ({
        id: segment.id,
        halo: new scene.maps.Polyline({
          map: scene.map,
          path: segment.path,
          clickable: false,
          strokeColor: scene.colours.solid,
          strokeOpacity: 1,
          strokeWeight: 8,
          zIndex: 1,
        }),
        line: new scene.maps.Polyline({
          map: scene.map,
          path: segment.path,
          clickable: false,
          strokeColor: scene.colours.route,
          strokeOpacity: 1,
          strokeWeight: 4,
          zIndex: 2,
        }),
      }))
      scene.fit([
        ...route.points.map((point) => point.place.coordinates),
        ...route.segments.flatMap((segment) => segment.path),
      ])
    },
    highlight(hovered, selected) {
      stopAnimation()
      animated = null
      lines.forEach(({ id, line, halo }) => {
        const chosen = id === selected
        const preview = id === hovered
        halo.setOptions({ strokeWeight: chosen || preview ? 10 : 8 })
        const icons: NonNullable<LineOptions['icons']> = chosen
          ? [
              {
                icon: {
                  path: 'M 0,-1 0,1',
                  strokeColor: scene.colours.route,
                  strokeOpacity: 1,
                  strokeWeight: 5,
                  scale: 3,
                },
                offset: '0',
                repeat: '16px',
              },
            ]
          : []
        line.setOptions({
          strokeOpacity: chosen ? 0 : 1,
          strokeWeight: preview ? 6 : 4,
          zIndex: chosen || preview ? 3 : 2,
          icons,
        })
        if (chosen) animated = { line, icons }
      })
      // Highlight changes never fit bounds or zoom.
      syncAnimation()
    },
    dispose() {
      clear()
      reducedMotion.removeEventListener('change', syncAnimation)
      document.removeEventListener('visibilitychange', syncAnimation)
      scene.dispose()
    },
  }
}
