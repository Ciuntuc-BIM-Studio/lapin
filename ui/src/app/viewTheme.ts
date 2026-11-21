/**
 * The look of the drawing areas (3D, plans, sections, sheets and what sits on them), which may differ from the panels'
 * (Preferences → General → Viewport; forum request: dark panels, a paper-white viewport).
 */
import { useUi } from './store';
import { resolveViewTheme, usePrefs, type ThemeName } from './prefs';

/** The viewport's theme now (for code that reads it outside a render). */
export const currentViewTheme = (): ThemeName => resolveViewTheme(useUi.getState().theme, usePrefs.getState().viewportTheme);

/** The viewport's theme, following the panels' and the preference. */
export function useViewTheme(): ThemeName {
  const panel = useUi((s) => s.theme);
  const choice = usePrefs((s) => s.viewportTheme);
  return resolveViewTheme(panel, choice);
}
