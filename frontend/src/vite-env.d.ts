interface ImportMetaEnv {
  /** The backend's address when it isn't the site's own (the Android app); see src/api/base.ts. */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
