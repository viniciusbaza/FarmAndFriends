// Login/logout advance this scope; access-token refresh keeps the same identity.
let scope = 0
export const getSessionScope = () => scope
export function advanceSessionScope() { scope += 1 }
