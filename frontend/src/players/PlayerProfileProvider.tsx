import { useCallback, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { getPlayer } from '../api/http'
import type { PlayerProfile } from '../api/types'
import { useFetch } from '../api/useFetch'
import { EmptyState, Skeleton, TeamLogo } from '../components/common'
import { competitionLabel, statLines } from './playerStats'
import type { PlayerRef } from './usePlayerProfile'
import { PlayerProfileContext } from './usePlayerProfile'

/** Lets any player's name on any page open that player's profile, shown as a dialog over the page. */
export function PlayerProfileProvider({ children }: { children: ReactNode }) {
  const [player, setPlayer] = useState<PlayerRef | null>(null)
  const open = useCallback((next: PlayerRef) => setPlayer(next), [])

  return (
    <PlayerProfileContext.Provider value={open}>
      {children}
      {player && <PlayerProfileDialog key={player.playerId} player={player} onClose={() => setPlayer(null)} />}
    </PlayerProfileContext.Provider>
  )
}

/** A modal dialog: Esc or a click outside closes it. */
function PlayerProfileDialog({ player, onClose }: { player: PlayerRef; onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const profile = useFetch(`player/${player.playerId}`, () => getPlayer(player.leagueCode, player.playerId))

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  const data = profile.data
  const subtitle = data && [data.jersey && `#${data.jersey}`, data.position].filter(Boolean).join(' · ')

  return (
    <dialog
      ref={dialogRef}
      className="sheet profile"
      aria-label={player.name}
      onClose={onClose}
      // Clicks on the backdrop target the dialog element itself; clicks on the content target its children.
      onClick={e => e.target === e.currentTarget && dialogRef.current?.close()}
    >
      <div className="sheet__body">
        <header className="profile__top">
          <PlayerPhoto url={data?.photoUrl ?? null} />
          <div className="sheet__who">
            <strong className="profile__name">{data?.name ?? player.name}</strong>
            {subtitle && <span>{subtitle}</span>}
            {data?.team && (
              <span className="profile__club">
                <TeamLogo team={data.team} size={18} />
                {data.team.name}
              </span>
            )}
          </div>
          <button className="sheet__close" onClick={() => dialogRef.current?.close()} aria-label="Close">×</button>
        </header>

        {profile.loading && <Skeleton rows={7} />}
        {profile.error && (
          <EmptyState icon="👤" title="This player's profile can't be loaded right now." onRetry={profile.retry} />
        )}
        {data && <ProfileBody profile={data} />}
      </div>
    </dialog>
  )
}

function ProfileBody({ profile }: { profile: PlayerProfile }) {
  const [picked, setPicked] = useState(0)
  const competition = profile.competitions[picked]

  const nationality = profile.nationality && (
    <span className="profile__nation">
      {profile.flagUrl && <Flag url={profile.flagUrl} />}
      {profile.nationality}
    </span>
  )
  const facts: [string, ReactNode][] = []
  if (nationality) facts.push(['Nationality', nationality])
  if (profile.age !== null) facts.push(['Age', profile.age])
  if (profile.heightCm !== null) facts.push(['Height', `${profile.heightCm} cm`])
  if (profile.position) facts.push(['Position', profile.position])

  return (
    <>
      {facts.length > 0 && (
        <dl className="profile__facts">
          {facts.map(([label, value]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      )}

      <h3 className="detail__heading">This season</h3>
      {competition ? (
        <>
          <div className="quickbar profile__tabs" role="tablist" aria-label="Competitions">
            {profile.competitions.map((c, i) => (
              <button
                key={c.name}
                role="tab"
                aria-selected={i === picked}
                className={i === picked ? 'chip chip--active' : 'chip'}
                onClick={() => setPicked(i)}
              >
                {competitionLabel(c.name)}
              </button>
            ))}
          </div>

          <p className="profile__for">{[competition.name, competition.teamName].filter(Boolean).join(' · ')}</p>
          <dl className="profile__stats" role="tabpanel">
            {statLines(competition).map(line => (
              <div key={line.label} className="profile__stat">
                <dd>{line.value}</dd>
                <dt>{line.label}</dt>
              </div>
            ))}
          </dl>
          <p className="profile__note">Minutes played and passing figures aren't published by the data source.</p>
        </>
      ) : (
        <p className="sheet__empty">No statistics recorded for this season yet.</p>
      )}
    </>
  )
}

/** The player's portrait, or a silhouette where there is none or it fails to load. */
function PlayerPhoto({ url }: { url: string | null }) {
  const [broken, setBroken] = useState(false)

  return (
    <span className="profile__photo">
      {url && !broken ? (
        <img src={url} alt="" loading="lazy" onError={() => setBroken(true)} />
      ) : (
        <svg viewBox="0 0 64 64" aria-hidden>
          <circle cx="32" cy="24" r="12" />
          <path d="M8 64c0-14 10.7-24 24-24s24 10 24 24z" />
        </svg>
      )}
    </span>
  )
}

function Flag({ url }: { url: string }) {
  const [broken, setBroken] = useState(false)
  return broken ? null : <img className="profile__flag" src={url} alt="" loading="lazy" onError={() => setBroken(true)} />
}
