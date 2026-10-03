import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { FavoritesProvider } from './favorites/FavoritesProvider'
import { PlayerProfileProvider } from './players/PlayerProfileProvider'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <FavoritesProvider>
      <PlayerProfileProvider>
        <App />
      </PlayerProfileProvider>
    </FavoritesProvider>
  </StrictMode>,
)
