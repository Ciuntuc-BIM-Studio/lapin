/**
 * The mouse navigation style in the status bar (decision 516): its short name, and a click lists the styles, each with
 * its main gestures in a line (app/navStyle.ts). Kept per user in the preferences.
 */

import { Mouse } from 'lucide-react';
import { NAV_STYLES, NAV_STYLE_IDS } from '../app/navStyle';
import { setNavStyle, useNavStyle } from '../app/navStyleStore';
import { openContextMenu, type MenuItem } from './ContextMenu';
import { t } from '../i18n';

export function NavStyleMenu() {
  const style = useNavStyle();
  const choose = (e: React.MouseEvent<HTMLButtonElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    const items: MenuItem[] = NAV_STYLE_IDS.map((id) => ({
      label: NAV_STYLES[id].label, raw: true, hint: NAV_STYLES[id].hint, checked: style.id === id, onSelect: () => setNavStyle(id),
    }));
    // Opens above the bar (the menu is moved back inside the window when it would run off): each entry is two or three lines tall.
    openContextMenu(r.left, r.top - 4 - items.length * 52, items);
  };
  return (
    <button type="button" className="status-link status-nav" data-testid="nav-style" title={`${t('Mouse navigation')}: ${style.label}. ${t(style.hint)}`} onClick={choose}>
      <Mouse size={12} aria-hidden="true" /> <span data-no-i18n>{style.label}</span>
    </button>
  );
}
