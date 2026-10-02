import { advanceSessionScope, getSessionScope } from './sessionScope.ts'

const API_URL = import.meta.env?.VITE_API_URL ?? 'http://localhost:5204'

export class ApiError extends Error {
  status: number
  code?: string

  constructor(status: number, message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

let pendingRefresh: { scope: number; promise: Promise<boolean> } | null = null

function forceLogout() {
  advanceSessionScope()
  localStorage.removeItem('access_token')
  window.location.href = '/login'
}

async function refreshAccessToken(): Promise<boolean> {
  const session = getSessionScope()
  if (pendingRefresh?.scope === session) return pendingRefresh.promise
  const promise = (async () => {
    const response = await fetch(`${API_URL}/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      throw new Error('Refresh failed')
    }

    const data = await response.json()
    if (session !== getSessionScope()) throw new Error('Sessão alterada.')
    localStorage.setItem('access_token', data.access_token)
    return true
  })().catch(() => false).finally(() => {
    if (pendingRefresh?.promise === promise) pendingRefresh = null
  })
  pendingRefresh = { scope: session, promise }
  return promise
}

async function request<T>(
  input: string,
  init: RequestInit = {},
  auth = false,
  retry = true
): Promise<T> {
  const session = getSessionScope()
  const token = localStorage.getItem('access_token')

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(auth && token ? { Authorization: `Bearer ${token}` } : {}),
    ...init.headers,
  }

  console.log('FETCH →', `${API_URL}${input}`)

  const response = await fetch(`${API_URL}${input}`, {
    ...init,
    headers,
    credentials: 'include',
  })
  if (auth && session !== getSessionScope()) throw new Error('Sessão alterada. Atualize os dados da conta atual.')

  // 🔁 TOKEN EXPIRADO
  if (response.status === 401 && auth && retry) {
    const refreshed = await refreshAccessToken()
    if (session !== getSessionScope()) throw new Error('Sessão alterada.')

    if (!refreshed){
      forceLogout()
      throw new Error('Sessão expirada.')
    }

    return request<T>(input, init, auth, false)
  }

  if (!response.ok) {
    const raw = await response.text()
    let message = raw
    let code: string | undefined

    if (raw) {
      try {
        const parsed: unknown = JSON.parse(raw)

        if (typeof parsed === 'string') {
          message = parsed
        } else if (parsed && typeof parsed === 'object') {
          const problem = parsed as { detail?: string; title?: string; code?: string }
          message = problem.detail || problem.title || raw
          code = problem.code
        }
      } catch {
        // Plain-text responses are already suitable for display.
      }
    }

    if (!message) {
      message = response.status === 403
        ? 'Voc\u00ea n\u00e3o tem permiss\u00e3o para realizar esta a\u00e7\u00e3o.'
        : `Erro na requisi\u00e7\u00e3o (${response.status}).`
    }

    throw new ApiError(response.status, message, code)
  }

  const data: T = response.status === 204 ? null as T : await response.json()
  if (auth && session !== getSessionScope()) throw new Error('Sessão alterada.')
  // One confirmation path for old and new producers. Effects are never wallet/XP deltas.
  if (auth && init.method?.toUpperCase() === 'POST' && /^\/(plots|farms|shop)(\/|$)/.test(input)) {
    window.dispatchEvent(new Event('economy:confirmed'))
  }
  return data
}

// ===== Públicos =====
export function post<T>(url: string, body: unknown) {
  return request<T>(url, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

// ===== Autenticados =====
export function authFetch<T>(
  url: string,
  init: RequestInit = {}
) {
  return request<T>(url, init, true)
}
