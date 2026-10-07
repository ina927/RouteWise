import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { once } from 'node:events'
import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw/http'

// Supply the browser URL context; all tested API functions still use real fetch.
globalThis.location = new URL('http://localhost:5173')
const { handlers } = await import('../src/mocks/handlers/index.ts')
const { resetMockStore, setMockViewer } = await import('../src/mocks/store.ts')
const { mockPlaces, createSeed } = await import('../src/mocks/data/seed.ts')
const routes = await import('../src/api/routesApi.ts')
const journeys = await import('../src/api/journeysApi.ts')
const sharing = await import('../src/api/sharingApi.ts')
const { searchUsers } = await import('../src/api/usersApi.ts')
const { ApiError } = await import('../src/api/httpClient.ts')
const { apiUrl } = await import('../src/api/config.ts')
const server = setupServer(...handlers)

function status(expected) {
  return (error) =>
    error instanceof ApiError &&
    error.status === expected &&
    Boolean(error.message)
}

function coherent(route) {
  assert.equal(route.segments.length, route.points.length - 1)
  assert.equal(
    new Set(route.points.map((point) => point.id)).size,
    route.points.length,
  )
  assert.equal(
    new Set(route.segments.map((segment) => segment.id)).size,
    route.segments.length,
  )
  route.segments.forEach((segment, index) => {
    assert.equal(segment.fromPointId, route.points[index].id)
    assert.equal(segment.toPointId, route.points[index + 1].id)
    assert.ok(segment.path.length >= 2)
  })
  assert.equal(
    route.totalDistanceMeters,
    route.segments.reduce((sum, segment) => sum + segment.distanceMeters, 0),
  )
  assert.equal(
    route.totalDurationSeconds,
    route.segments.reduce((sum, segment) => sum + segment.durationSeconds, 0),
  )
}

function representativePlace(summary, detail) {
  assert.equal(typeof summary.representativePlaceId, 'string')
  assert.ok(summary.representativePlaceId.length > 0)
  // Assert the MOCK-ONLY selection, not a permanent product policy.
  assert.equal(
    summary.representativePlaceId,
    detail.plan.route.points[0].place.placeId,
  )
  assert.equal(Object.hasOwn(detail, 'representativePlaceId'), false)
}

try {
  resetMockStore()
  server.listen({ onUnhandledFrame: 'error' })
  setMockViewer('user-river')
  const owned = await journeys.getOwnedJourneys()
  assert.equal(owned.length, 2)
  for (const summary of owned) {
    const detail = await journeys.getJourney(summary.id)
    representativePlace(summary, detail)
    coherent(detail.plan.route)
    assert.equal(summary.name, detail.name)
    assert.equal(
      summary.totalDistanceMeters,
      detail.plan.route.totalDistanceMeters,
    )
    assert.equal(
      summary.totalDurationSeconds,
      detail.plan.route.totalDurationSeconds,
    )
    assert.equal(summary.locationCount, detail.plan.route.points.length)
    assert.equal(detail.access, 'owner')
  }

  for (const summary of await sharing.getSharedJourneys()) {
    representativePlace(summary, await journeys.getJourney(summary.id))
  }
  const seededJourneys = createSeed().journeys
  const invitations = [
    ...(await sharing.getIncomingInvitations()),
    ...(await sharing.getJourneyInvitations('journey-city')),
  ]
  for (const invitation of invitations) {
    representativePlace(
      invitation.journey,
      seededJourneys.find((journey) => journey.id === invitation.journey.id),
    )
  }

  const start = { id: 'start', place: mockPlaces.central }
  const end = { id: 'end', place: mockPlaces.airport }
  const destinations = [
    { id: 'opera', place: mockPlaces.opera },
    { id: 'museum', place: mockPlaces.museum },
    { id: 'bondi', place: mockPlaces.bondi },
  ]
  const input = {
    start,
    destinations,
    travelMode: 'driving',
    objective: 'distance',
  }
  for (const endMode of ['open', 'returnToStart', 'fixed']) {
    for (const travelMode of ['walking', 'driving']) {
      for (const objective of ['distance', 'time']) {
        const result = await routes.optimizeRoute({
          ...input,
          travelMode,
          objective,
          endMode,
          end,
          returnPointId: 'return',
        })
        coherent(result)
        assert.deepEqual(result.points[0], start)
        if (endMode === 'fixed') assert.deepEqual(result.points.at(-1), end)
        if (endMode === 'open')
          assert.ok(
            destinations.some((point) => point.id === result.points.at(-1).id),
          )
        if (endMode === 'returnToStart') {
          assert.equal(result.points.at(-1).id, 'return')
          assert.deepEqual(result.points.at(-1).place, start.place)
        }
      }
    }
    const count = endMode === 'open' ? 9 : 8
    const selected = Array.from({ length: count }, (_, index) => ({
      id: `point-${index}`,
      place: mockPlaces.opera,
    }))
    const boundary = {
      ...input,
      destinations: selected,
      endMode,
      end,
      returnPointId: 'return',
    }
    assert.equal((await routes.optimizeRoute(boundary)).points.length, 10)
    await assert.rejects(
      routes.optimizeRoute({
        ...boundary,
        destinations: [...selected, { id: 'extra', place: mockPlaces.bondi }],
      }),
      status(400),
    )
  }
  await assert.rejects(
    routes.optimizeRoute({ ...input, endMode: 'fixed' }),
    status(400),
  )
  await assert.rejects(
    routes.optimizeRoute({
      ...input,
      endMode: 'returnToStart',
      returnPointId: 'start',
    }),
    status(400),
  )
  await assert.rejects(
    routes.calculateRoute({ points: [start, start], travelMode: 'driving' }),
    status(400),
  )

  const exactPoints = [
    start,
    destinations[1],
    destinations[0],
    destinations[2],
    end,
  ]
  const exact = await routes.calculateRoute({
    points: exactPoints,
    travelMode: 'driving',
  })
  assert.deepEqual(exact.points, exactPoints)
  coherent(exact)
  await assert.rejects(
    routes.calculateRoute({
      points: Array.from({ length: 11 }, (_, i) => ({
        id: `${i}`,
        place: mockPlaces.opera,
      })),
      travelMode: 'driving',
    }),
    status(400),
  )
  const returnRoute = await routes.optimizeRoute({
    ...input,
    endMode: 'returnToStart',
    returnPointId: 'return',
  })
  const plan = {
    travelMode: 'driving',
    objective: 'distance',
    endMode: 'returnToStart',
    route: returnRoute,
  }
  await assert.rejects(journeys.createJourney({ name: ' ', plan }), status(400))
  const created = await journeys.createJourney({
    name: 'Smoke journey',
    date: '2026-10-18',
    plan,
    annotations: [
      { pointId: 'start', note: 'Departure', plannedTime: '09:00' },
      { pointId: 'return', note: 'Return', plannedTime: '17:00' },
    ],
  })
  assert.deepEqual(
    created.annotations.map((item) => item.plannedTime),
    ['09:00', '17:00'],
  )
  assert.deepEqual(created.plan.route, returnRoute)
  representativePlace(
    (await journeys.getOwnedJourneys()).find((item) => item.id === created.id),
    created,
  )
  const renamed = await journeys.updateJourney(created.id, {
    name: 'Renamed',
    date: null,
  })
  assert.equal(renamed.name, 'Renamed')
  assert.equal(renamed.date, undefined)
  await assert.rejects(
    journeys.updateJourney(created.id, {
      name: 'Must not save',
      annotations: [{ pointId: 'missing', note: 'Invalid' }],
    }),
    status(400),
  )
  assert.equal((await journeys.getJourney(created.id)).name, 'Renamed')
  await assert.rejects(
    journeys.updateJourney(created.id, {
      annotations: [{ pointId: 'start', plannedTime: '25:00' }],
    }),
    status(400),
  )
  await assert.rejects(
    journeys.updateJourney(created.id, { date: '2026-02-30' }),
    status(400),
  )
  const reorderedPoints = [
    returnRoute.points[0],
    ...returnRoute.points.slice(1, -1).reverse(),
    returnRoute.points.at(-1),
  ]
  const recalculated = await routes.calculateRoute({
    points: reorderedPoints,
    travelMode: 'driving',
  })
  const updated = await journeys.updateJourney(created.id, {
    plan: { ...plan, route: recalculated },
  })
  assert.deepEqual(updated.annotations, created.annotations)
  assert.deepEqual(updated.plan.route.points, reorderedPoints)
  representativePlace(
    (await journeys.getOwnedJourneys()).find((item) => item.id === updated.id),
    updated,
  )
  assert.deepEqual(
    (await journeys.updateJourney(created.id, { annotations: [] })).annotations,
    [],
  )

  const duplicate = await journeys.duplicateJourney('journey-city')
  assert.notEqual(duplicate.id, 'journey-city')
  assert.equal(duplicate.access, 'owner')
  representativePlace(
    (await journeys.getOwnedJourneys()).find(
      (item) => item.id === duplicate.id,
    ),
    duplicate,
  )
  assert.deepEqual(await sharing.getJourneyInvitations(duplicate.id), [])
  await journeys.updateJourney(duplicate.id, { name: 'Independent copy' })
  assert.equal((await journeys.getJourney('journey-city')).name, 'City sights')
  assert.equal(await journeys.deleteJourney(duplicate.id), undefined)
  await assert.rejects(journeys.getJourney(duplicate.id), status(404))

  const matches = await searchUsers('AVERY')
  assert.deepEqual(matches, [{ id: 'user-avery', username: 'avery' }])
  assert.deepEqual(await searchUsers('avery'), matches)
  assert.deepEqual(await searchUsers(''), [])
  await assert.rejects(
    sharing.sendInvitation('journey-harbour', { recipientId: 'missing' }),
    status(404),
  )
  const invitation = await sharing.sendInvitation('journey-harbour', {
    recipientId: 'user-avery',
  })
  assert.equal(invitation.status, 'pending')
  await assert.rejects(
    sharing.sendInvitation('journey-harbour', { recipientId: 'user-avery' }),
    status(409),
  )
  setMockViewer('user-avery')
  await assert.rejects(journeys.getJourney('journey-harbour'), status(403))
  const pendingBefore = (await sharing.getIncomingInvitations()).filter(
    (item) => item.status === 'pending',
  ).length
  assert.equal(
    (await sharing.respondToInvitation(invitation.id, { status: 'accepted' }))
      .status,
    'accepted',
  )
  assert.equal(
    (await sharing.getIncomingInvitations()).filter(
      (item) => item.status === 'pending',
    ).length,
    pendingBefore - 1,
  )
  assert.ok(
    (await sharing.getSharedJourneys()).some(
      (item) => item.id === 'journey-harbour',
    ),
  )
  assert.equal((await journeys.getJourney('journey-harbour')).access, 'shared')
  await assert.rejects(
    journeys.updateJourney('journey-harbour', { name: 'Forbidden' }),
    status(403),
  )
  await assert.rejects(journeys.deleteJourney('journey-harbour'), status(403))
  await assert.rejects(
    journeys.duplicateJourney('journey-harbour'),
    status(403),
  )
  await assert.rejects(
    sharing.sendInvitation('journey-harbour', { recipientId: 'user-casey' }),
    status(403),
  )

  setMockViewer('user-river')
  const declined = await sharing.sendInvitation('journey-city', {
    recipientId: 'user-quinn',
  })
  setMockViewer('user-quinn')
  assert.equal(
    (await sharing.respondToInvitation(declined.id, { status: 'declined' }))
      .status,
    'declined',
  )
  assert.ok(
    !(await sharing.getSharedJourneys()).some(
      (item) => item.id === 'journey-city',
    ),
  )
  setMockViewer('user-river')
  assert.equal(await sharing.removeSharedJourney('journey-museum'), undefined)
  assert.ok(
    !(await sharing.getSharedJourneys()).some(
      (item) => item.id === 'journey-museum',
    ),
  )
  await assert.rejects(journeys.getJourney('journey-museum'), status(403))
  setMockViewer('user-quinn')
  assert.equal((await journeys.getJourney('journey-museum')).access, 'owner')
  setMockViewer('user-casey')
  assert.equal((await journeys.getJourney('journey-museum')).access, 'shared')

  setMockViewer('user-avery')
  await journeys.deleteJourney('journey-coast')
  setMockViewer('user-river')
  assert.ok(
    !(await sharing.getIncomingInvitations()).some(
      (item) => item.journey.id === 'journey-coast',
    ),
  )
  const malformed = await fetch(apiUrl('/routes/calculate'), {
    method: 'POST',
    body: '{',
  })
  assert.equal(malformed.status, 400)
  server.use(
    http.get(
      apiUrl('/journeys'),
      () => new HttpResponse('Proxy failure', { status: 502 }),
    ),
  )
  await assert.rejects(journeys.getOwnedJourneys(), status(502))
  server.resetHandlers()
  resetMockStore()
  setMockViewer('user-river')
  assert.deepEqual(await journeys.getOwnedJourneys(), owned)
  const { smokeAuth } = await import('./smoke-auth.mjs')
  await smokeAuth()
  console.log(
    'PASS: HTTP/API round trips, 3 end modes, max-10 boundaries, exact order, stable identities, metadata, CRUD, sharing isolation, read-only access, errors and reset.',
  )
} finally {
  server.close()
  resetMockStore()
}

// With MSW stopped, the same API function reaches an actual local HTTP server.
const real = createServer((req, res) => {
  assert.equal(req.url, '/api/journeys')
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end('[]')
})
try {
  real.listen(0, '127.0.0.1')
  await once(real, 'listening')
  globalThis.location = new URL(`http://127.0.0.1:${real.address().port}`)
  assert.deepEqual(await journeys.getOwnedJourneys(), [])
  console.log(
    'PASS: MSW disabled; unchanged API module reaches real HTTP. Local server stopped afterward.',
  )
} finally {
  await new Promise((resolve, reject) =>
    real.close((error) => (error ? reject(error) : resolve())),
  )
}
