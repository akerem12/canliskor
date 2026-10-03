import { useCallback, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { getPlayer } from '../api/http'
import type { PlayerProfile } from '../api/types'
import { useFetch } from '../api/useFetch'
import { EmptyState, Skeleton, TeamLogo } from '../components/common'
import { useI18n } from '../i18n/useI18n'
import { competitionLabel, statLines, totalStats } from './playerStats'
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
  const { t } = useI18n()
  const dialogRef = useRef<HTMLDialogElement>(null)
  const profile = useFetch(`player/${player.playerId}`, () => getPlayer(player.leagueCode, player.playerId))

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  const data = profile.data
  const subtitle = data && [data.jersey && `#${data.jersey}`, data.position && t.positions[data.position]].filter(Boolean).join(' · ')

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
          <button className="sheet__close" onClick={() => dialogRef.current?.close()} aria-label={t.common.close}>×</button>
        </header>

        {profile.loading && <Skeleton rows={7} />}
        {profile.error && <EmptyState icon="👤" title={t.player.cantLoad} onRetry={profile.retry} />}
        {data && <ProfileBody profile={data} />}
      </div>
    </dialog>
  )
}

function ProfileBody({ profile }: { profile: PlayerProfile }) {
  const { t } = useI18n()
  // The first tab is everything added up; with a single competition that would only repeat it.
  const hasTotal = profile.competitions.length > 1
  const tabs = hasTotal
    ? [{ label: t.player.total, heading: t.player.allCompetitions, stats: totalStats(profile.competitions) }]
    : []
  for (const c of profile.competitions) {
    tabs.push({ label: competitionLabel(c.name), heading: [c.name, c.teamName].filter(Boolean).join(' · '), stats: c })
  }
  const [picked, setPicked] = useState(0)
  const tab = tabs[picked]

  const nationality = profile.nationality && (
    <span className="profile__nation">
      {profile.flagUrl && <Flag url={profile.flagUrl} />}
      {profile.nationality}
    </span>
  )
  const facts: [string, ReactNode][] = []
  if (nationality) facts.push([t.player.nationality, nationality])
  if (profile.age !== null) facts.push([t.player.age, profile.age])
  if (profile.heightCm !== null) facts.push([t.player.height, `${profile.heightCm} cm`])
  if (profile.position) facts.push([t.player.position, t.positions[profile.position]])

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

      <h3 className="detail__heading">{t.player.thisSeason}</h3>
      {tab ? (
        <>
          <div className="quickbar profile__tabs" role="tablist" aria-label={t.player.competitions}>
            {tabs.map(({ label, heading }, i) => (
              <button
                key={heading}
                role="tab"
                aria-selected={i === picked}
                className={i === picked ? 'chip chip--active' : 'chip'}
                onClick={() => setPicked(i)}
              >
                {label}
              </button>
            ))}
          </div>

          <p className="profile__for">{tab.heading}</p>
          <dl className="profile__stats" role="tabpanel">
            {statLines(tab.stats).map(line => (
              <div key={line.key} className="profile__stat">
                <dd>{line.value}</dd>
                <dt>{t.player.stats[line.key]}</dt>
              </div>
            ))}
          </dl>
          <p className="profile__note">{t.player.note}</p>
        </>
      ) : (
        <p className="sheet__empty">{t.player.noStats}</p>
      )}
    </>
  )
}

function Flag({ url }: { url: string }) {
  const [broken, setBroken] = useState(false)
  return broken ? null : <img className="profile__flag" src={url} alt="" loading="lazy" onError={() => setBroken(true)} />
}
