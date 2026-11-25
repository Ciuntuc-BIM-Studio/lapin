/**
 * A roof's Panels section in the properties panel (docs/plan-roof-panels.md §4, S2): the profile it is covered with (none:
 * the covering as one layer, as before), the profile's colour, a made-to-order profile's longest panel, and what the
 * layout works out: the panels to buy by length, the offcuts reused, the sheet bought, the warnings. Each change is one
 * command (roofpanels.setRoofPanels), one undo step; Panel layout… opens the layout itself.
 */

import { Fragment, useEffect, useState } from 'react';
import { LayoutGrid } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { choiceOf, colourLabel, isLayout, ITEM_GROUPS, itemRows, profilesOf, purchaseRows, setArgs, takeoffOptions, type LayoutAnswer } from './roofpanels';

/** The roof's layout, asked again whenever the model changes. */
export function useLayout(roofId: string | null, preview?: Record<string, unknown> | null): LayoutAnswer | null {
  const nodes = useSession((s) => s.nodes);
  const [answer, setAnswer] = useState<LayoutAnswer | null>(null);
  const key = JSON.stringify(preview ?? null);
  useEffect(() => {
    if (!roofId) { setAnswer(null); return; }
    let live = true;
    void query<LayoutAnswer>('roofpanels.layout', { id: roofId, ...(preview ?? {}) }).then((r) => { if (live) setAnswer(r); });
    return () => { live = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roofId, nodes, key]);
  return answer;
}

/** What each of the takeoff's options changes. */
const OPTION_TIPS: Record<string, string> = {
  ridge: 'The ridge and hip caps the accessories are counted in: the large or the small profile',
  support: 'What carries the panels: boards 150 mm wide with 50 mm gaps, or the older guide\'s battens',
  fixing: 'How the battens and counter-battens are fixed to the rafters: nails 4 × 100 mm or screws 6 × 100 mm',
};

export function PanelsSection({ node }: { node: NodeDto }) {
  const info = useSession((s) => s.info);
  const profiles = profilesOf(info?.catalogs as Record<string, unknown> | undefined);
  const choice = choiceOf(node);
  const profile = profiles.find((p) => p.id === choice?.profile) ?? null;
  const layout = useLayout(choice ? node.id : null);
  const send = (next: Parameters<typeof setArgs>[1]) => void execute('roofpanels.setRoofPanels', setArgs(node.id, next, profiles));
  const open = () => useUi.setState({ dialog: 'roofPanelLayout', roofId: node.id });
  return (
    <div className="layers roof-panels" data-testid="roof-panels-section">
      <dl className="fields">
        <dt title="What the roof is covered with: metal tiles or panels laid out on its slopes; none, the covering as one layer">Profile</dt>
        <dd>
          <select className="cell" data-testid="roof-panels-profile" value={choice?.profile ?? ''} disabled={!node.editable}
            onChange={(e) => send(e.target.value ? { profile: e.target.value } : null)}>
            <option value="">none</option>
            {profiles.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.name}</option>)}
          </select>
        </dd>
        {profile && profile.colors.length > 0 && (
          <>
            <dt>Colour</dt>
            <dd>
              <select className="cell" data-testid="roof-panels-colour" value={choice?.color ?? profile.colors[0].code} disabled={!node.editable || profile.colors.length < 2}
                title={(profile.colors.find((c) => c.code === (choice?.color ?? profile.colors[0].code)) ?? profile.colors[0]).name} data-no-i18n
                onChange={(e) => send({ ...choice!, color: e.target.value })}>
                {profile.colors.map((c) => <option key={c.code} value={c.code} title={c.name} data-no-i18n>{colourLabel(c)}</option>)}
              </select>
            </dd>
          </>
        )}
        {profile && profile.maxLengths.length > 0 && (
          <>
            <dt title="The longest panel the layout uses: shorter ones put joints on long slopes, as the maker's guide advises">Longest panel</dt>
            <dd>
              <select className="cell" data-testid="roof-panels-max" value={choice?.maxLength ?? profile.maxLength} disabled={!node.editable}
                onChange={(e) => send({ ...choice!, maxLength: Number(e.target.value) })}>
                {[...new Set([...profile.maxLengths, profile.maxLength])].sort((a, b) => a - b).map((n) => <option key={n} value={n} data-no-i18n>{`${n} mm`}</option>)}
              </select>
            </dd>
          </>
        )}
        {choice && isLayout(layout) && takeoffOptions(layout.takeoff).map((o) => (
          <Fragment key={o.key}>
            <dt title={OPTION_TIPS[o.key]}>{o.label}</dt>
            <dd>
              <select className="cell" data-testid={`roof-panels-${o.key}`} value={o.value} disabled={!node.editable}
                onChange={(e) => send({ ...choice, [o.key]: e.target.value })}>
                {o.choices.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </dd>
          </Fragment>
        ))}
      </dl>
      {!choice && <p className="muted small pad">No panels: the covering is drawn as one layer. Pick a profile to lay metal tiles or panels on the roof's slopes.</p>}
      {choice && layout && !isLayout(layout) && <p className="error small pad" data-testid="roof-panels-problem">{layout.problem}</p>}
      {choice && isLayout(layout) && (
        <div className="pad" data-testid="roof-panels-summary">
          <p className="eyebrow">To buy</p>
          <table className="roof-panels-buy">
            <tbody>
              {purchaseRows(layout).map((r) => (
                <tr key={r.length}><td className="num" data-no-i18n>{`${r.length} mm`}</td><td className="num" data-no-i18n>{`× ${r.count}`}</td></tr>
              ))}
            </tbody>
          </table>
          <dl className="fields">
            <dt>Panels laid</dt><dd className="num" data-no-i18n>{layout.totals.panels}</dd>
            <dt title="Valley pieces cut from what the hip pieces leave of their panels: not bought">Reused offcuts</dt><dd className="num" data-no-i18n>{layout.totals.reused}</dd>
            <dt title="Each panel bought: its nominal length by its whole width, as panels are priced">Sheet bought (m²)</dt><dd className="num" data-no-i18n>{layout.totals.sheetArea.toFixed(2)}</dd>
          </dl>
          {layout.takeoff && layout.takeoff.items.length > 0 && (
            <>
              <p className="eyebrow">Accessories and support</p>
              <table className="roof-panels-items" data-testid="roof-panels-items">
                <tbody>
                  {ITEM_GROUPS.flatMap((g) => itemRows(layout.takeoff, g.id)).map((i) => (
                    <tr key={i.code} title={i.note || undefined} data-item={i.code}>
                      <td>{i.name}</td><td className="num" data-no-i18n>{i.quantity}</td><td className="muted" style={{ paddingLeft: 6, whiteSpace: 'nowrap' }}>{i.unit}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
          {layout.warnings.length > 0 && (
            <>
              <p className="eyebrow">Warnings</p>
              <ul className="small roof-panels-warnings" data-testid="roof-panels-warnings">{layout.warnings.map((w) => <li key={w}>{w}</li>)}</ul>
            </>
          )}
        </div>
      )}
      {choice && (
        <div className="pad">
          <button type="button" className="btn compact" data-testid="roof-panels-open" onClick={open}><LayoutGrid size={13} /> Panel layout…</button>
        </div>
      )}
    </div>
  );
}
