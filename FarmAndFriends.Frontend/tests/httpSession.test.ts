import assert from 'node:assert/strict'
import test from 'node:test'
import { authFetch, ApiError } from '../src/api/http.ts'
import { advanceSessionScope } from '../src/api/sessionScope.ts'
import { sendAnimalAttempt } from '../src/animals/animalApi.ts'

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}

test('HTTP session, command confirmations and exact recovery transport', async t => {
  const storage = new Map<string, string>()
  const events = new EventTarget()
  const browser = Object.assign(events, { location: { href: '/game' } })
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) } })
  Object.defineProperty(globalThis, 'window', { value: browser, configurable: true })
  t.after(() => { Reflect.deleteProperty(globalThis, 'window'); Reflect.deleteProperty(globalThis, 'localStorage') })

  await t.test('old refresh cannot log out or overwrite a newer account', async () => {
    storage.set('access_token', 'alice')
    const refreshStarted = deferred<void>()
    const refresh = deferred<Response>()
    const fetchMock = t.mock.method(globalThis, 'fetch', async (url: string | URL | Request) => {
      if (String(url).endsWith('/auth/refresh')) { refreshStarted.resolve(); return refresh.promise }
      return new Response(null, { status: 401 })
    })
    const request = authFetch('/users/me')
    await refreshStarted.promise
    advanceSessionScope()
    storage.set('access_token', 'bob')
    refresh.resolve(Response.json({ access_token: 'alice-refreshed' }))
    await assert.rejects(request, /Sessão alterada/)
    assert.equal(storage.get('access_token'), 'bob')
    assert.equal(browser.location.href, '/game')
    fetchMock.mock.restore()
  })

  await t.test('every XP producer uses one confirmed event, and a rejection never confirms', async () => {
    let confirmed = 0
    const listener = () => { confirmed += 1 }
    events.addEventListener('economy:confirmed', listener)
    const fetchMock = t.mock.method(globalThis, 'fetch', async () => Response.json({ xpGained: 5 }))
    for (const route of ['/plots/p/plant', '/plots/p/harvest', '/farms/f/plots/p/steal', '/farms/f/plots/p/care', '/farms/f/plots/p/pest/remove', '/farms/f/animals/a/help']) {
      await authFetch(route, { method: 'POST' })
    }
    assert.equal(confirmed, 6)
    fetchMock.mock.restore()
    const rejectMock = t.mock.method(globalThis, 'fetch', async () => Response.json({ code: 'OWNER_BONUS_LIMIT_REACHED', detail: 'Bônus completo' }, { status: 409 }))
    await assert.rejects(authFetch('/farms/f/animals/a/help', { method: 'POST' }), (error: unknown) => error instanceof ApiError && error.code === 'OWNER_BONUS_LIMIT_REACHED')
    assert.equal(confirmed, 6)
    events.removeEventListener('economy:confirmed', listener)
    rejectMock.mock.restore()
  })

  await t.test('sale recovery resends identical key and quantity after response loss, even with zero inventory', async () => {
    const calls: { key: string | undefined; body: string | null | undefined }[] = []
    let first = true
    const fetchMock = t.mock.method(globalThis, 'fetch', async (_url: string | URL | Request, init?: RequestInit) => {
      calls.push({ key: (init?.headers as Record<string, string>)?.['Idempotency-Key'], body: init?.body as string | undefined })
      if (first) { first = false; throw new TypeError('network response lost') }
      return Response.json({ operationId: 'op', replayed: true, effect: { quantity: 3 }, current: null })
    })
    const attempt = { action: 'sell-animal-product' as const, farmId: 'farm', itemId: 'egg', quantity: 3, key: '11111111-1111-4111-8111-111111111111' }
    await assert.rejects(sendAnimalAttempt(attempt), /lost/)
    const result = await sendAnimalAttempt(attempt)
    assert.equal(result.replayed, true)
    assert.deepEqual(calls[0], calls[1])
    assert.deepEqual(JSON.parse(calls[1].body!), { itemId: 'egg', quantity: 3 })
    fetchMock.mock.restore()
  })

  await t.test('cow purchase and milk sale transport preserve species and product identifiers', async () => {
    const calls: { url: string; body: object; key: string | undefined }[] = []
    const fetchMock = t.mock.method(globalThis, 'fetch', async (url: string | URL | Request, init?: RequestInit) => {
      calls.push({ url: String(url), body: JSON.parse(init?.body as string),
        key: (init?.headers as Record<string, string>)?.['Idempotency-Key'] })
      return Response.json({ operationId: 'op', replayed: false, current: null })
    })
    const buyKey = '22222222-2222-4222-8222-222222222222'
    const sellKey = '33333333-3333-4333-8333-333333333333'
    await sendAnimalAttempt({ action: 'buy-animal', farmId: 'farm', animalTypeId: 'cow', key: buyKey })
    await sendAnimalAttempt({ action: 'sell-animal-product', farmId: 'farm', itemId: 'milk', quantity: 8, key: sellKey })
    assert.ok(calls[0].url.endsWith('/shop/buy-animal'))
    assert.deepEqual(calls[0].body, { animalTypeId: 'cow' })
    assert.ok(calls[1].url.endsWith('/shop/sell-animal-product'))
    assert.deepEqual(calls[1].body, { itemId: 'milk', quantity: 8 })
    assert.deepEqual(calls.map(call => call.key), [buyKey, sellKey])
    fetchMock.mock.restore()
  })
})
