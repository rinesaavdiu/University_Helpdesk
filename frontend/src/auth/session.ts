import { useSyncExternalStore } from 'react'
import type { Role } from '../routes'

export type SessionUser = {
  id: number
  email: string
  displayName: string
  role: Role
}

export type Session = {
  accessToken: string
  /** When the token expires, in milliseconds since 1970 (`Date.now()` time). */
  expiresAt: number
  user: SessionUser
}

/** Why the last session ended; the sign-in page explains it. */
export type SessionEnd = 'expired' | 'signedOut'

type SessionState = {
  session: Session | null
  ended: SessionEnd | null
}

// Session storage keeps the sign-in across page refreshes in the same tab only. It is cleared when
// the tab closes, and each tab signs in separately.
const storageKey = 'helpdesk-session'

let state: SessionState = readStoredState()
let expiryTimer: ReturnType<typeof setTimeout> | undefined
const listeners = new Set<() => void>()

scheduleExpiry()

function readStoredState(): SessionState {
  let stored: Session | null = null
  try {
    stored = JSON.parse(sessionStorage.getItem(storageKey) ?? 'null') as Session | null
  } catch {
    // An unreadable value is treated as signed out.
  }
  if (stored && typeof stored.accessToken === 'string' && typeof stored.expiresAt === 'number') {
    if (stored.expiresAt > Date.now()) {
      return { session: stored, ended: null }
    }
    sessionStorage.removeItem(storageKey)
    return { session: null, ended: 'expired' }
  }
  sessionStorage.removeItem(storageKey)
  return { session: null, ended: null }
}

function setState(next: SessionState) {
  state = next
  if (next.session) {
    sessionStorage.setItem(storageKey, JSON.stringify(next.session))
  } else {
    sessionStorage.removeItem(storageKey)
  }
  scheduleExpiry()
  listeners.forEach((listener) => listener())
}

function scheduleExpiry() {
  clearTimeout(expiryTimer)
  if (state.session) {
    expiryTimer = setTimeout(() => endSession('expired'), Math.max(0, state.session.expiresAt - Date.now()))
  }
}

/** The current session, or null when signed out or when the token has expired. */
export function getSession(): Session | null {
  if (state.session && state.session.expiresAt <= Date.now()) {
    endSession('expired')
  }
  return state.session
}

export function startSession(session: Session) {
  setState({ session, ended: null })
}

export function endSession(reason: SessionEnd) {
  if (state.session) {
    setState({ session: null, ended: reason })
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

function getState() {
  return state
}

export function useSessionState(): SessionState {
  return useSyncExternalStore(subscribe, getState)
}

export function useSession(): Session | null {
  return useSessionState().session
}
