import { otherLanguage } from '../i18n/language'
import { useI18n } from '../i18n/useI18n'
import { useTheme } from '../theme/useTheme'

/** The two switches in the header: language (English / Türkçe) and theme (dark / light). */
export function HeaderTools() {
  const { language, t, setLanguage } = useI18n()
  const { theme, toggle } = useTheme()
  const themeLabel = theme === 'dark' ? t.settings.toLight : t.settings.toDark

  return (
    <div className="tools">
      {/* Named in the language it leads to, so it can be found by someone who can't read the current one. */}
      <button
        className="tools__button"
        lang={otherLanguage(language)}
        title={t.settings.languageTitle}
        onClick={() => setLanguage(otherLanguage(language))}
      >
        {t.settings.language}
      </button>
      <button className="tools__button tools__button--icon" aria-label={themeLabel} title={themeLabel} onClick={toggle}>
        {/* Shows the theme that is on: a moon in the dark, a sun in the light. */}
        <span aria-hidden>{theme === 'dark' ? '🌙' : '☀️'}</span>
      </button>
    </div>
  )
}
