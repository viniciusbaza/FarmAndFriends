import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/http'
import { getSessionScope } from '../api/sessionScope'
import {
  sendFertilizerAttempt,
} from './fertilizerApi'
import {
  fertilizerAttemptKey, parseFertilizerAttempt,
  type FertilizerAttempt, type FertilizerIntent,
} from './fertilizerState'

/** Keep the same request fingerprint after a lost response, including a remount. */
export function useFertilizerAttempt(userId: string | undefined) {
  const [pending, setPending] = useState<FertilizerAttempt | null>(() => userId
    ? parseFertilizerAttempt(localStorage.getItem(fertilizerAttemptKey(userId))) : null)
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)

  useEffect(() => {
    setPending(userId ? parseFertilizerAttempt(localStorage.getItem(fertilizerAttemptKey(userId))) : null)
  }, [userId])

  const run = useCallback(async (attempt: FertilizerAttempt) => {
    if (!userId || busyRef.current) return null
    const scope = getSessionScope()
    busyRef.current = true
    setBusy(true)
    try {
      localStorage.setItem(fertilizerAttemptKey(userId), JSON.stringify(attempt))
      setPending(attempt)
      const result = await sendFertilizerAttempt(attempt)
      if (scope !== getSessionScope()) return null
      localStorage.removeItem(fertilizerAttemptKey(userId))
      setPending(null)
      return result
    } catch (error) {
      if (scope === getSessionScope() && error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429) {
        localStorage.removeItem(fertilizerAttemptKey(userId))
        setPending(null)
      }
      throw error
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }, [userId])

  const execute = useCallback((intent: FertilizerIntent) => {
    if (!userId || busyRef.current) return Promise.resolve(null)
    const saved = parseFertilizerAttempt(localStorage.getItem(fertilizerAttemptKey(userId)))
    if (saved) { setPending(saved); return Promise.resolve(null) }
    return run({ ...intent, key: crypto.randomUUID() })
  }, [run, userId])

  const recover = useCallback(() => {
    if (!userId || busyRef.current) return Promise.resolve(null)
    const saved = parseFertilizerAttempt(localStorage.getItem(fertilizerAttemptKey(userId)))
    return saved ? run(saved) : Promise.resolve(null)
  }, [run, userId])

  return { busy, pending, execute, recover }
}
