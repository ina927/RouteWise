export function formatDistance(metres: number): string {
  return metres < 1000
    ? `${Math.round(metres)} m`
    : `${Number((metres / 1000).toFixed(1))} km`
}

export function formatDuration(seconds: number): string {
  if (seconds > 0 && seconds < 60) return '<1 min'
  const minutes = Math.round(seconds / 60)
  return minutes < 60
    ? `${minutes} min`
    : `${Math.floor(minutes / 60)} hr${minutes % 60 ? ` ${minutes % 60} min` : ''}`
}
