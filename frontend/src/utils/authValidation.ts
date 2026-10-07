export function usernameError(value: string): string | undefined {
  const username = value.trim()
  if (!username) return 'Enter a username.'
  if (!/^[A-Za-z0-9_]{3,20}$/.test(username))
    return 'Use 3–20 letters, numbers or underscores.'
}

export function passwordError(value: string): string | undefined {
  if (!value) return 'Enter a password.'
  if (value.length < 8) return 'Use at least 8 characters.'
}
