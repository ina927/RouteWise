export const journeyNameLimit = 100
export const journeyNoteLimit = 2000

export function journeyNameError(value: string): string | undefined {
  if (!value.trim()) return 'Enter a journey name.'
  if (value.trim().length > journeyNameLimit)
    return `Journey name must be ${journeyNameLimit} characters or fewer.`
  return undefined
}
