// Creates a fresh local test account and journey through the real API.
// Run with Node 24: node --experimental-strip-types scripts/prepare-integration.mjs
import { fixtureRoute } from '../src/mocks/routeFixtures.ts'

const base = 'http://localhost:5173/api'
const username = `nam_${Date.now().toString(36)}`
const password = 'RouteWise-test-123!'
let cookie = ''

async function request(path, body) {
  const response = await fetch(`${base}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: {
      Origin: 'http://localhost:5173',
      'Content-Type': 'application/json',
      ...(cookie ? { Cookie: cookie } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!response.ok)
    throw new Error(
      `${path}: HTTP ${response.status}: ${await response.text()}`,
    )
  const cookies = response.headers.getSetCookie()
  if (cookies.length)
    cookie = cookies.map((item) => item.split(';')[0]).join('; ')
  return response.json()
}

try {
  await request('/auth/signup', { username, password })
  const points = [
    ['start', 'Test park', -33.86, 151.2],
    ['gallery', 'Test gallery', -33.87, 151.21],
    ['cafe', 'Test cafe', -33.88, 151.22],
  ].map(([id, name, lat, lng]) => ({
    id,
    place: { placeId: `integration-${id}`, name, coordinates: { lat, lng } },
  }))
  const journey = await request('/journeys', {
    name: 'Nam integration test',
    plan: {
      travelMode: 'walking',
      objective: 'distance',
      endMode: 'open',
      route: fixtureRoute(points, 'walking'),
    },
    annotations: [
      { pointId: 'start', note: 'Keep this note', plannedTime: '09:00' },
    ],
  })
  const saved = await request(`/journeys/${encodeURIComponent(journey.id)}`)
  if (saved.id !== journey.id || saved.plan.route.points.length !== 3)
    throw new Error('Saved journey verification failed.')
  console.log(
    `Test account and saved journey ready.\nUsername: ${username}\nPassword: ${password}\nJourney: http://localhost:5173/journeys/${journey.id}`,
  )
} catch (error) {
  console.error(
    'Preparation failed. Start the API and Vite first.',
    error.message,
  )
  process.exitCode = 1
}
