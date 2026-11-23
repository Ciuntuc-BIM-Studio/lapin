/**
 * The insertion anchor's selector in a tool's bar (forum topic 226): a 3×3 of the element's box (a column), or a row of
 * three (a door, a window: its left edge, centre or right edge at the cursor along the wall). F4 steps through it, Shift+F4
 * back. The choice is remembered per tool on this computer; nothing of it is stored in the project.
 */

import { anchorFor, anchorsOf, setAnchor, useAnchors, type Anchor, type AnchorKind } from '../platform/anchor';
import { bindingText, useBindings } from '../app/commands';
import { t } from '../i18n';

const NAMES: Record<string, string> = {
  '-1,1': 'top left corner', '0,1': 'middle of the top edge', '1,1': 'top right corner',
  '-1,0': 'middle of the left edge', '0,0': 'centre', '1,0': 'middle of the right edge',
  '-1,-1': 'bottom left corner', '0,-1': 'middle of the bottom edge', '1,-1': 'bottom right corner',
};
const ALONG_NAMES: Record<number, string> = { [-1]: 'left edge of the opening', 0: 'centre of the opening', 1: 'right edge of the opening' };

export function AnchorPicker({ tool, kind }: { tool: string; kind: AnchorKind }) {
  useAnchors((s) => s.byTool[tool]);
  useBindings();
  const cur = anchorFor(tool);
  const list = anchorsOf(kind);
  const key = bindingText('app.anchorNext').split(', ')[0];
  const title = kind === 'box'
    ? `${t('Insertion anchor: the point of the box that sits at the cursor')}${key ? ` (${key})` : ''}`
    : `${t('Insertion anchor: which edge of the opening sits at the cursor, along the wall')}${key ? ` (${key})` : ''}`;
  return (
    <span className={`anchor-picker ${kind}`} role="group" aria-label={t('Insertion anchor')} title={title} data-testid="anchor-picker">
      {list.map((a: Anchor) => {
        const on = a.h === cur.h && (kind === 'along' || a.v === cur.v);
        const name = kind === 'box' ? NAMES[`${a.h},${a.v}`] : ALONG_NAMES[a.h];
        return (
          <button key={`${a.h},${a.v}`} type="button" className={`anchor-cell${on ? ' on' : ''}`} aria-pressed={on} data-anchor={`${a.h},${a.v}`} title={t(name)}
            onClick={() => setAnchor(tool, a, kind)}><span className="anchor-dot" /></button>
        );
      })}
    </span>
  );
}
