import type { ConnectionStatus } from '../live/useLiveScores'

const labels: Record<ConnectionStatus, string> = {
  connecting: 'Connecting…',
  live: 'Live updates on',
  reconnecting: 'Reconnecting…',
  offline: 'Offline — retrying',
}

export function ConnectionBadge({ status }: { status: ConnectionStatus }) {
  return (
    <span className={`connection connection--${status}`} role="status">
      <span className="connection__dot" aria-hidden />
      {labels[status]}
    </span>
  )
}
