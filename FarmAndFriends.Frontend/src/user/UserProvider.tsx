import { useCallback, useEffect, useRef, useState } from 'react'
import { UserContext } from './UserContext'
import type { SaveProfileInput, User } from './UserContext'
import { getCurrentUser, updateProfile, type UpdateProfileResponse } from '../api/users'
import { useAuth } from '../auth/useAuth'
import { AuthoritativeReadGate } from './authoritativeRead'
import { getSessionScope } from '../api/sessionScope'
import { advanceAsyncSession, isAsyncSessionCurrent, type AsyncSession } from '../social/asyncSession'
import { applySavedProfile, mergeUserRefresh, type UserRevisions } from './userUpdates'

export function UserProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [economyPending, setEconomyPending] = useState(false)
  const readGate = useRef(new AuthoritativeReadGate())
  const activeSession = useRef<AsyncSession>({ token, generation: 0 })
  activeSession.current = advanceAsyncSession(activeSession.current, token)
  const [dataSession, setDataSession] = useState(activeSession.current)
  const mounted = useRef(true)
  const revisions = useRef<UserRevisions>({ profile: 0, xp: 0 })
  const latestLoad = useRef(0)
  const pendingSave = useRef<{
    session: AsyncSession
    promise: Promise<UpdateProfileResponse>
  } | null>(null)

  const loadUser = useCallback(async (showInitialLoading: boolean) => {
    const session = activeSession.current
    const requestId = ++latestLoad.current
    const ticket = readGate.current.begin(getSessionScope())
    const capturedRevisions = { ...revisions.current }
    const isCurrent = () => mounted.current &&
      isAsyncSessionCurrent(activeSession.current, session) && requestId === latestLoad.current &&
      readGate.current.accepts(ticket, getSessionScope())
    if (!token) {
      setUser(null)
      setLoading(false)
      return
    }

    if (showInitialLoading) {
      setLoading(true)
    }

    try {
      const data = await getCurrentUser()
      if (!isCurrent()) return
      setUser(current => mergeUserRefresh(current, data, capturedRevisions, revisions.current))
      setDataSession(session)
      setEconomyPending(false)
    } catch (err) {
      if (!isCurrent()) return
      setEconomyPending(true)
      if (showInitialLoading) {
        setUser(null)
      }
      if (showInitialLoading) throw err
    } finally {
      if (isCurrent()) {
        setLoading(false)
      }
    }
  }, [token])

  const refreshUser = useCallback(() => {
    return loadUser(false)
  }, [loadUser])

  useEffect(() => {
    const confirmed = () => {
      readGate.current.invalidate()
      setEconomyPending(true)
      void loadUser(false)
    }
    const refresh = () => { void loadUser(false) }
    window.addEventListener('economy:confirmed', confirmed)
    window.addEventListener('focus', refresh)
    const timer = window.setInterval(refresh, economyPending ? 5_000 : 30_000)
    return () => {
      window.removeEventListener('economy:confirmed', confirmed)
      window.removeEventListener('focus', refresh)
      window.clearInterval(timer)
    }
  }, [loadUser, economyPending])

  const saveProfile = useCallback((profile: SaveProfileInput): Promise<UpdateProfileResponse> => {
    const session = activeSession.current
    if (!session.token || !isAsyncSessionCurrent(dataSession, session) || !user) {
      return Promise.reject(new Error('Seu perfil ainda não está disponível. Tente novamente.'))
    }
    if (pendingSave.current?.session === session) return pendingSave.current.promise

    const promise = (async () => {
      const result = await updateProfile(profile)
      if (mounted.current && isAsyncSessionCurrent(activeSession.current, session)) {
        revisions.current.profile += 1
        setUser(current => current ? applySavedProfile(current, result) : current)
      }
      return result
    })().finally(() => {
      if (pendingSave.current?.promise === promise) pendingSave.current = null
    })
    pendingSave.current = { session, promise }
    return promise
  }, [dataSession, user])

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      latestLoad.current += 1
    }
  }, [])

  useEffect(() => {
    void loadUser(true).catch(() => undefined)
  }, [loadUser])

  return (
    <UserContext.Provider value={{
      user: token && isAsyncSessionCurrent(dataSession, activeSession.current) ? user : null,
      loading,
      refreshUser,
      economyPending,
      saveProfile,
    }}>
      {children}
    </UserContext.Provider>
  )
}
