import assert from 'node:assert/strict'
import {
  buildRequest,
  initialDraft,
  planningError,
} from '../src/features/journey-planning/planning.ts'
import { journeyNameError } from '../src/utils/journeyValidation.ts'
import { mockPlaces } from '../src/mocks/data/seed.ts'
import {
  createMockJourney,
  duplicateMockJourney,
  getMockJourney,
  resetMockStore,
  setMockViewer,
  updateMockJourney,
} from '../src/mocks/store.ts'

const row = (place) => ({ id: crypto.randomUUID(), query: place.name, place })
const draft = {
  ...initialDraft(),
  travelMode: 'walking',
  objective: 'distance',
  endMode: 'fixed',
  start: row(mockPlaces.central),
  destinations: [row(mockPlaces.museum)],
  end: row(mockPlaces.opera),
}
assert.ok(buildRequest(draft))

// A conflicting fixed end is retained for correction, and cannot be submitted.
const duplicateEnd = { ...draft, end: row(mockPlaces.museum) }
assert.match(
  planningError(duplicateEnd),
  /Remove this place from Places to visit/,
)
assert.equal(buildRequest(duplicateEnd), null)
assert.ok(buildRequest({ ...duplicateEnd, destinations: [] }))
assert.equal(buildRequest({ ...draft, end: row(mockPlaces.central) }), null)
assert.equal(
  buildRequest({ ...draft, destinations: [row(mockPlaces.central)] }),
  null,
)
assert.equal(
  buildRequest({
    ...draft,
    destinations: [row(mockPlaces.museum), row(mockPlaces.museum)],
  }),
  null,
)

// Typed but unselected places must never disappear silently from the request.
const unselected = { id: 'unselected', query: 'Opera House', place: null }
assert.equal(
  buildRequest({ ...draft, destinations: [...draft.destinations, unselected] }),
  null,
)
assert.ok(
  buildRequest({
    ...draft,
    destinations: [...draft.destinations, { ...unselected, query: '' }],
  }),
)
assert.equal(
  buildRequest({ ...draft, endMode: 'returnToStart', destinations: [] }),
  null,
)
assert.equal(
  buildRequest({ ...draft, endMode: 'open', destinations: [] }),
  null,
)
assert.ok(
  buildRequest({
    ...draft,
    endMode: 'returnToStart',
    end: row(mockPlaces.museum),
  }),
)
assert.ok(
  buildRequest({ ...draft, endMode: 'open', end: row(mockPlaces.museum) }),
)
const maximum = {
  ...draft,
  destinations: Array.from({ length: 8 }, (_, index) =>
    row({ ...mockPlaces.museum, placeId: `unique-${index}` }),
  ),
}
assert.ok(buildRequest(maximum))
assert.equal(
  buildRequest({
    ...maximum,
    destinations: [...maximum.destinations, row(mockPlaces.bondi)],
  }),
  null,
)

assert.ok(journeyNameError('   '))
assert.equal(journeyNameError(` ${'a'.repeat(100)} `), undefined)
assert.ok(journeyNameError('a'.repeat(101)))
resetMockStore()
setMockViewer('user-river')
const source = getMockJourney('journey-city')
assert.throws(
  () => createMockJourney({ ...source, name: 'a'.repeat(101) }),
  /100 characters/,
)
assert.throws(
  () => updateMockJourney(source.id, { name: 'a'.repeat(101) }),
  /100 characters/,
)
assert.throws(
  () =>
    updateMockJourney(source.id, {
      name: 'Changed',
      annotations: [
        { pointId: source.plan.route.points[0].id, note: 'a'.repeat(2001) },
      ],
    }),
  /2000 characters/,
)
assert.equal(getMockJourney(source.id).name, source.name)
updateMockJourney(source.id, {
  name: 'a'.repeat(100),
  annotations: [
    { pointId: source.plan.route.points[0].id, note: 'a'.repeat(2000) },
  ],
})
const copy = duplicateMockJourney(source.id)
assert.equal(copy.name.length, 100)
assert.ok(copy.name.endsWith(' (copy)'))
assert.ok(updateMockJourney(copy.id, { date: '2026-10-20' }))
console.log('Planning validation and journey text limits passed.')
