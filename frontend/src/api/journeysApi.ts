import type {
  CreateJourneyRequest,
  Journey,
  JourneySummary,
  UpdateJourneyRequest,
} from '../contracts/journeys.ts'
import { request } from './httpClient.ts'

export function getOwnedJourneys(signal?: AbortSignal) {
  return request<JourneySummary[]>('/journeys', { signal })
}

export function getJourney(id: string, signal?: AbortSignal) {
  return request<Journey>(`/journeys/${encodeURIComponent(id)}`, { signal })
}

export function createJourney(body: CreateJourneyRequest) {
  return request<Journey>('/journeys', { method: 'POST', body })
}

// This also covers rename and per-occurrence notes/planned-time edits.
export function updateJourney(id: string, body: UpdateJourneyRequest) {
  return request<Journey>(`/journeys/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body,
  })
}

export function duplicateJourney(id: string) {
  return request<Journey>(`/journeys/${encodeURIComponent(id)}/duplicate`, {
    method: 'POST',
  })
}

export function deleteJourney(id: string) {
  return request(`/journeys/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    noContent: true,
  })
}
