import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import './index.css'
import { AuthProvider } from './auth/AuthProvider'
import { UserProvider } from './user/UserProvider.tsx'
import { SocialProvider } from './social/SocialProvider.tsx'
import { InventoryProvider } from './inventory/InventoryProvider.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <UserProvider>
        <InventoryProvider>
        <SocialProvider>
          <App />
        </SocialProvider>
        </InventoryProvider>
      </UserProvider>
    </AuthProvider>
  </StrictMode>,
)
