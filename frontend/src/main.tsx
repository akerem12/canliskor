import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { FavoritesProvider } from './favorites/FavoritesProvider'
import { I18nProvider } from './i18n/I18nProvider'
import { PlayerProfileProvider } from './players/PlayerProfileProvider'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <FavoritesProvider>
        <PlayerProfileProvider>
          <App />
        </PlayerProfileProvider>
      </FavoritesProvider>
    </I18nProvider>
  </StrictMode>,
)
