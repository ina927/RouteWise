import assert from 'node:assert/strict'
import * as auth from '../src/api/authApi.ts'
import * as journeys from '../src/api/journeysApi.ts'
import * as sharing from '../src/api/sharingApi.ts'
import { searchUsers } from '../src/api/usersApi.ts'
import { ApiError } from '../src/api/httpClient.ts'
import { apiUrl } from '../src/api/config.ts'
import { resetMockStore } from '../src/mocks/store.ts'

export async function smokeAuth() {
  const status = (code) => (error) =>
    error instanceof ApiError && error.status === code
  const signIn = (username) =>
    auth.login({ username, password: 'routewise123' })
  resetMockStore()
  await assert.rejects(auth.getCurrentUser(), status(401))
  for (const path of [
    '/journeys',
    '/journeys/journey-city',
    '/invitations',
    '/shared-journeys',
    '/users?query=avery',
  ])
    assert.equal((await fetch(apiUrl(path))).status, 401)
  for (const [path, method] of [
    ['/routes/optimize', 'POST'],
    ['/routes/calculate', 'POST'],
    ['/journeys', 'POST'],
    ['/account', 'DELETE'],
  ])
    assert.equal((await fetch(apiUrl(path), { method })).status, 401)
  for (const username of [
    '',
    'ab',
    'a'.repeat(21),
    'has space',
    'user@example.test',
  ])
    await assert.rejects(
      auth.signup({ username, password: 'routewise123' }),
      status(400),
    )
  await assert.rejects(
    auth.signup({ username: 'valid_user', password: 'short' }),
    status(400),
  )
  for (const username of ['river', 'unknown_user'])
    await assert.rejects(
      auth.login({ username, password: 'incorrect' }),
      (error) =>
        status(401)(error) && error.message === 'Invalid username or password.',
    )
  const newUser = await auth.signup({
    username: '  New_User  ',
    password: ' spaced password ',
  })
  assert.deepEqual(Object.keys(newUser).sort(), ['id', 'username'])
  assert.equal(newUser.username, 'New_User')
  assert.deepEqual(await auth.getCurrentUser(), newUser)
  assert.deepEqual(await journeys.getOwnedJourneys(), [])
  assert.deepEqual(await searchUsers('new_user'), [])
  await assert.rejects(
    auth.signup({ username: 'NEW_USER', password: 'routewise123' }),
    status(409),
  )
  await auth.logout()
  await assert.rejects(auth.getCurrentUser(), status(401))
  await assert.rejects(
    auth.login({ username: 'new_user', password: 'spaced password' }),
    status(401),
  )
  assert.deepEqual(
    await auth.login({ username: 'nEw_UsEr', password: ' spaced password ' }),
    newUser,
  )
  await auth.logout()
  assert.equal((await signIn(' RIVER ')).id, 'user-river')
  assert.equal((await journeys.getOwnedJourneys()).length, 2)
  assert.equal(
    (await sharing.getIncomingInvitations()).filter(
      (item) => item.status === 'pending',
    ).length,
    1,
  )
  assert.equal((await sharing.getSharedJourneys())[0].id, 'journey-museum')
  assert.deepEqual(await searchUsers('AVERY'), [
    { id: 'user-avery', username: 'avery' },
  ])
  assert.deepEqual(await searchUsers('avery@example.test'), [])
  await sharing.removeSharedJourney('journey-museum')
  await auth.deleteAccount()
  await assert.rejects(auth.getCurrentUser(), status(401))
  await assert.rejects(signIn('river'), status(401))
  await signIn('avery')
  assert.deepEqual(await searchUsers('river'), [])
  assert.deepEqual(await sharing.getJourneyInvitations('journey-coast'), [])
  assert.deepEqual(await sharing.getIncomingInvitations(), [])
  assert.deepEqual(await sharing.getSharedJourneys(), [])
  await assert.rejects(journeys.getJourney('journey-city'), status(404))
  await assert.rejects(journeys.getJourney('journey-harbour'), status(404))
  await auth.logout()
  await signIn('quinn')
  assert.equal((await journeys.getJourney('journey-museum')).access, 'owner')
  assert.deepEqual(
    (await sharing.getJourneyInvitations('journey-museum')).map(
      (item) => item.recipient.id,
    ),
    ['user-casey'],
  )
  await auth.logout()
  await signIn('casey')
  assert.equal((await journeys.getJourney('journey-museum')).access, 'shared')
  await auth.logout()
  const reused = await auth.signup({
    username: 'RIVER',
    password: 'routewise123',
  })
  assert.notEqual(reused.id, 'user-river')
  assert.deepEqual(await journeys.getOwnedJourneys(), [])
  assert.deepEqual(await sharing.getIncomingInvitations(), [])
  assert.deepEqual(await sharing.getSharedJourneys(), [])
  resetMockStore()
  await assert.rejects(auth.getCurrentUser(), status(401))
  console.log(
    'PASS: auth protection, signup/login validation, case-insensitive identity, exact passwords, logout, username-only search, hard-delete cascade, other-recipient isolation, username reuse and anonymous reset.',
  )
}
