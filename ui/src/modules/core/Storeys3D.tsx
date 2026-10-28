/**
 * Which storeys a 3D view shows (report #29, viewport/storeys3d.ts): the
 * editor (in the 3D view's properties, and View → Storeys shown with a 3D
 * view in front), and the looks that leave the other storeys' elements out
 * of the 3D (not drawn, not picked, not fitted, not walked on). The ribbon,
 * the browser's level menu and the 3D's chip set it (storeys3dActions.ts).
 */

import { useEffect } from 'react';
import { useUi } from '../../app/store';
import { activeStorey, execute, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { setLooks } from '../../platform/looks';
import { hiddenByStoreys, settingOf, shownStoreys, type Storeys3d } from '../../viewport/storeys3d';

const level = (z: number) => `${z >= 0 ? '+' : '−'}${Math.abs(z).toFixed(2)}`;

/** The editor: a 3D view's storeys shown ("3d": {3D}). */
export function Storeys3DEditor({ viewId }: { viewId: string }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  useUi((s) => s.storeyId);
  const all = storeys({ nodes, relations });
  const active = activeStorey();
  const s = settingOf(nodes, viewId);
  const shown = shownStoreys(s, all, active?.id ?? null);
  const set = (patch: Partial<Storeys3d>) => void execute('view.setStoreys', { id: viewId, ...patch });
  const toggle = (id: string, on: boolean) => {
    const base = shown ? [...shown] : all.map((st) => st.id);
    const ids = on ? [...new Set([...base, id])] : base.filter((x) => x !== id);
    if (!ids.length) return;
    set({ mode: ids.length === all.length ? 'all' : 'some', ids });
  };
  return (
    <div className="plan-storeys storeys-3d">
      <dl className="fields">
        <dt title="Which storeys' elements the 3D view draws; the others are not drawn, picked or walked on">Show</dt>
        <dd>
          <select value={s.mode} onChange={(e) => {
            const mode = e.target.value as Storeys3d['mode'];
            set(mode === 'some' ? { mode, ids: shown ? [...shown] : all.map((st) => st.id) } : { mode });
          }}>
            <option value="all">every storey</option>
            <option value="some">the storeys ticked</option>
            <option value="active">the active storey only</option>
            <option value="below">the active storey and those below</option>
          </select>
        </dd>
        <dt title="Elements on no storey: the site, the terrain, linked files">On no storey</dt>
        <dd><input type="checkbox" checked={s.unplaced} onChange={(e) => set({ unplaced: e.target.checked })} /></dd>
        <dt title="The model cut above the highest storey shown, this far over its level, as a plan cuts it">Cut above its level</dt>
        <dd className="storeys-3d-cut">
          <input type="checkbox" checked={s.cut !== null} onChange={(e) => set({ cut: e.target.checked ? 1.2 : null })} />
          {s.cut !== null && (
            <input type="number" step={0.1} min={-5} max={50} className="cell" defaultValue={s.cut} key={s.cut}
              onBlur={(e) => { const v = Number(e.target.value); if (Number.isFinite(v) && v !== s.cut) set({ cut: v }); }}
              onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
          )}
          {s.cut !== null && <span className="muted small"> m</span>}
        </dd>
      </dl>
      <ul className="plan-storeys-list">
        {[...all].reverse().map((st) => {
          const on = !shown || shown.has(st.id);
          const isActive = st.id === active?.id;
          return (
            <li key={st.id} className={isActive ? 'own' : undefined}>
              <label>
                <input type="checkbox" checked={on} disabled={s.mode === 'active' || s.mode === 'below'} onChange={(e) => toggle(st.id, e.target.checked)} />
                {' '}<span data-no-i18n>{st.name}</span> <span className="muted small">{level(Number(st.props.elevation ?? 0))}{isActive ? ' · active' : ''}</span>
              </label>
            </li>
          );
        })}
      </ul>
      {(s.mode === 'active' || s.mode === 'below') && <p className="muted small">The 3D follows the active storey: pick another level in the project browser.</p>}
    </div>
  );
}

/** The 3D leaves out the storeys its view does not show. */
export function Storeys3DLooks() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const render = useSession((s) => s.render);
  const view3dId = useUi((s) => s.view3dId);
  const storeyId = useUi((s) => s.storeyId);
  useEffect(() => {
    const all = storeys({ nodes, relations });
    const hidden = hiddenByStoreys(nodes, render.keys(), settingOf(nodes, view3dId), all, activeStorey()?.id ?? null);
    setLooks('storeys3d', Object.fromEntries(hidden.map((id) => [id, { hidden: true }])));
  }, [nodes, relations, render, view3dId, storeyId]);
  return null;
}
