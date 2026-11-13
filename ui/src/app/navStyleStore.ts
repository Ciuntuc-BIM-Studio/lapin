/** The navigation style in force (app/navStyle.ts), from the user's preferences: read now, or followed by a component. */

import { navStyleOf, type NavStyle, type NavStyleId } from './navStyle';
import { setPrefs, usePrefs } from './prefs';

export const currentNavStyle = (): NavStyle => navStyleOf(usePrefs.getState().navStyle);
export const useNavStyle = (): NavStyle => navStyleOf(usePrefs((s) => s.navStyle));
export const setNavStyle = (id: NavStyleId): void => setPrefs({ navStyle: id });
