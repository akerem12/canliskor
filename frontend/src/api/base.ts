// Where the backend is. On the website this is empty: the backend serves the site, so "/api/..." is enough.
// The Android app is loaded from the phone itself, so its build sets VITE_API_BASE_URL (see .env.android).
const base = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/** @param path Starts with a slash: "/api/matches", "/hubs/live-scores". */
export const apiUrl = (path: string) => base + path
