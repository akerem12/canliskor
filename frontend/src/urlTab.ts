// A page's open tab, kept in the URL so a link (and Back) leads to the same view.

/** "?tab=squads" → that tab. Null if there is none or it isn't one of the page's tabs. */
export function tabFromUrl<T extends string>(tabs: readonly T[]): T | null {
  const tab = new URLSearchParams(window.location.search).get('tab')
  return tabs.find(known => known === tab) ?? null
}

/** Replaces the current history entry: switching tabs is not a step Back should undo. */
export function writeTabToUrl(tab: string) {
  const url = new URL(window.location.href)
  url.searchParams.set('tab', tab)
  window.history.replaceState(window.history.state, '', url)
}
