import { useEffect, useState } from 'react'

/** A first load this slow is most likely the server waking up (the free host stops it when nobody visits). */
const WakingAfterMs = 5_000
/** Waking up takes about a minute; well past that, something else is wrong. */
const GiveUpAfterMs = 90_000

export type SlowStart = 'no' | 'waking' | 'timedOut'

/**
 * How the very first load is going: fine, slow enough to say the server is waking up, or too slow to keep waiting.
 * @param waiting True until the first matches are in.
 * @returns The state, and a function that starts the wait over (for a "try again" button).
 */
export function useSlowStart(waiting: boolean) {
  const [state, setState] = useState<SlowStart>('no')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!waiting) return
    const timers = [
      setTimeout(() => setState('waking'), WakingAfterMs),
      setTimeout(() => setState('timedOut'), GiveUpAfterMs),
    ]
    return () => timers.forEach(clearTimeout)
  }, [waiting, attempt])

  return {
    slowStart: waiting ? state : 'no',
    restart: () => {
      setState('waking')
      setAttempt(n => n + 1)
    },
  }
}
