export type Theme = 'dark' | 'light'

/** Also read by the script in index.html, which applies the theme before the first paint. */
export const ThemeStorageKey = 'canliskor.theme.v1'

/** The browser's own bars (mobile address bar) take the page's colour. */
export const themeColors: Record<Theme, string> = { dark: '#0a0e17', light: '#f1f5f9' }

/** The theme picked earlier; dark, the site's own look, if none was. */
export const initialTheme = (stored: string | null): Theme => (stored === 'light' ? 'light' : 'dark')

export const otherTheme = (theme: Theme): Theme => (theme === 'dark' ? 'light' : 'dark')
