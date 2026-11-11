/**
 * The interface's entry. On the desktop the user's settings kept in their folder go back into the page's storage first
 * (app/userSettings.ts, decision 470): the app's modules read that storage as they load, so they load after it (boot.tsx).
 */
import { restoreUserSettings } from './app/userSettings';

void restoreUserSettings().finally(() => import('./boot'));
