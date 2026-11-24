/**
 * Panel layout (docs/plan-roof-panels.md §4, S2): a roof's panels as the Panotaj Bilka app shows them. The roof in plan
 * (or its planes laid flat, each in true size), every panel coloured by its length, the offcuts reused in orange with a
 * line from the panel they are cut from, the edges coloured by kind; a panel's code, length and donor on hover. Beside it
 * the tables: what to buy, each roof plane's totals, the edges and the offcuts. Another profile can be tried first (the
 * layout query takes a profile without changing the roof) and taken with Use this profile, one undo step.
 */

import { useMemo, useState } from 'react';
import { useUi } from '../../app/store';
import { Dialog } from '../../shell/Dialog';
import { execute, useSession } from '../../engine/session';
import { useLayout } from './PanelsSection';
import {
  bounds, centroid, choiceOf, EDGE_COLOUR, EDGE_LABEL, edgeRows, faceTotals, isLayout, laidFlat, lengthColours, profilesOf,
  purchaseRows, REUSED, reuseLinks, setArgs, type LayoutDto, type PanelDto, type Pt,
} from './roofpanels';

type Mode = 'plan' | 'flat';

export function PanelLayoutDialog() {
  const close = () => useUi.setState({ dialog: null });
  const roofId = useUi((s) => s.roofId);
  const node = useSession((s) => (roofId ? s.nodes.get(roofId) : undefined));
  const info = useSession((s) => s.info);
  const profiles = profilesOf(info?.catalogs as Record<string, unknown> | undefined);
  const choice = choiceOf(node);
  const [tried, setTried] = useState<string | null>(null);
  const trying = tried != null && tried !== choice?.profile ? tried : null;
  const layout = useLayout(roofId, trying ? { profile: trying } : null);
  const [mode, setMode] = useState<Mode>('plan');
  const [hover, setHover] = useState<PanelDto | null>(null);
  const use = () => {
    if (!roofId || !trying) return;
    void execute('roofpanels.setRoofPanels', setArgs(roofId, { profile: trying }, profiles)).then(() => setTried(null));
  };
  return (
    <Dialog title="Panel layout" onClose={close} className="roof-panel-layout" testId="roof-panel-layout" style={{ width: 'min(1180px, 96vw)' }}
      footer={<>
        <span className="grow" />
        {trying && <button type="button" className="btn primary" data-action="roof-panels-use" disabled={!node?.editable} onClick={use}>Use this profile</button>}
        <button type="button" onClick={close}>Close</button>
      </>}>
      <div className="row" style={{ gap: 12, flexWrap: 'wrap', alignItems: 'center', marginBottom: 8 }}>
        <label className="row" style={{ gap: 6 }}>
          <span>Profile</span>
          <select data-testid="roof-panel-layout-profile" value={trying ?? choice?.profile ?? ''} onChange={(e) => setTried(e.target.value || null)}>
            {!choice && <option value="">none</option>}
            {profiles.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.id === choice?.profile ? `${p.name} ✓` : p.name}</option>)}
          </select>
        </label>
        <span className="seg small" role="group">
          <button type="button" className={`seg-item${mode === 'plan' ? ' on' : ''}`} data-action="roof-panels-plan" onClick={() => setMode('plan')}>In plan</button>
          <button type="button" className={`seg-item${mode === 'flat' ? ' on' : ''}`} data-action="roof-panels-flat" onClick={() => setMode('flat')}>Laid flat</button>
        </span>
        {trying && <span className="muted small">Compare: the roof keeps its own profile until you use this one.</span>}
      </div>
      {!layout && <p className="muted">Laying the panels…</p>}
      {layout && !isLayout(layout) && <p className="error" data-testid="roof-panel-layout-problem">{layout.problem}</p>}
      {isLayout(layout) && (
        <div className="row" style={{ gap: 16, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          <div style={{ flex: '1 1 560px', minWidth: 320 }}>
            <Drawing layout={layout} mode={mode} onHover={setHover} />
            <p className="small roof-panels-hover" data-testid="roof-panels-hover" style={{ minHeight: '1.4em' }}>
              {hover ? <HoverLine p={hover} /> : <span className="muted">Point at a panel: its code, its length and where it is cut from.</span>}
            </p>
            <Legend layout={layout} />
          </div>
          <div style={{ flex: '1 1 320px', minWidth: 280 }}>
            <Tables layout={layout} />
          </div>
        </div>
      )}
    </Dialog>
  );
}

function HoverLine({ p }: { p: PanelDto }) {
  return (
    <span>
      <b data-no-i18n>{p.id}</b>
      <span data-no-i18n>{` · ${p.length} mm · `}</span>
      {p.donor ? <><span>Cut from</span><span data-no-i18n>{` ${p.donor} (+${p.shift ?? 0} mm)`}</span></> : <span>Bought</span>}
      {p.donatesTo && <><span data-no-i18n>{' · '}</span><span>Its offcut goes to</span><span data-no-i18n>{` ${p.donatesTo}`}</span></>}
    </span>
  );
}

/** The roof in plan or laid flat, as SVG in metres (y up). */
function Drawing({ layout, mode, onHover }: { layout: LayoutDto; mode: Mode; onHover: (p: PanelDto | null) => void }) {
  const colours = useMemo(() => lengthColours(layout.panels.map((p) => p.length)), [layout]);
  const flat = useMemo(() => new Map(laidFlat(layout.faces).map((o) => [o.id, o])), [layout]);
  const at = (face: string, pts: Pt[]): Pt[] => {
    if (mode === 'plan') return pts;
    const o = flat.get(face) ?? { dx: 0, dy: 0 };
    return pts.map(([x, y]) => [x + o.dx, y + o.dy]);
  };
  const faces = layout.faces.map((f) => ({ id: f.id, pts: mode === 'plan' ? f.planOutline : at(f.id, f.outline) }));
  const panels = layout.panels.map((p) => ({ p, pts: mode === 'plan' ? p.plan : at(p.face, p.net) }));
  const box = bounds(faces.flatMap((f) => f.pts));
  const pad = Math.max(box.x1 - box.x0, box.y1 - box.y0) * 0.04 + 0.2;
  const view = `${box.x0 - pad} ${-(box.y1 + pad)} ${box.x1 - box.x0 + 2 * pad} ${box.y1 - box.y0 + 2 * pad}`;
  const stroke = Math.max(box.x1 - box.x0, box.y1 - box.y0) / 600;
  const d = (pts: Pt[]) => `M${pts.map(([x, y]) => `${x} ${-y}`).join('L')}Z`;
  const links = mode === 'plan' ? reuseLinks(layout) : layout.panels.filter((p) => p.donor).map((p) => {
    const donor = layout.panels.find((q) => q.id === p.donor)!;
    return { donor: p.donor!, recipient: p.id, from: centroid(at(donor.face, donor.net)), to: centroid(at(p.face, p.net)) };
  });
  return (
    <svg className="roof-panels-drawing" data-testid="roof-panels-drawing" viewBox={view} style={{ width: '100%', height: 460, background: 'var(--raised)', borderRadius: 6 }}
      onMouseLeave={() => onHover(null)}>
      {faces.map((f) => <path key={f.id} d={d(f.pts)} fill="none" stroke="#7d8590" strokeWidth={stroke} />)}
      {panels.map(({ p, pts }) => (
        <path key={p.id} d={d(pts)} data-panel={p.id} fill={p.donor ? REUSED : colours.get(p.length)} fillOpacity={0.9} stroke="#36404a" strokeWidth={stroke}
          onMouseEnter={() => onHover(p)}>
          <title>{`${p.id} · ${p.length} mm${p.donor ? ` ← ${p.donor}` : ''}`}</title>
        </path>
      ))}
      {mode === 'plan' && layout.edges.map((e, i) => (
        <line key={i} x1={e.a[0]} y1={-e.a[1]} x2={e.b[0]} y2={-e.b[1]} stroke={EDGE_COLOUR[e.kind] ?? '#555'} strokeWidth={stroke * 3} strokeLinecap="round" pointerEvents="none" />
      ))}
      {links.map((l) => (
        <line key={l.recipient} x1={l.from[0]} y1={-l.from[1]} x2={l.to[0]} y2={-l.to[1]} stroke={REUSED} strokeWidth={stroke * 2.5} strokeDasharray={`${stroke * 8} ${stroke * 5}`} pointerEvents="none" />
      ))}
      {mode === 'flat' && layout.faces.map((f) => {
        const c = centroid(at(f.id, f.outline));
        return <text key={f.id} x={c[0]} y={-c[1]} fontSize={stroke * 28} textAnchor="middle" fill="#1d232a" pointerEvents="none" data-no-i18n>{f.id}</text>;
      })}
    </svg>
  );
}

function Legend({ layout }: { layout: LayoutDto }) {
  const colours = lengthColours(layout.panels.map((p) => p.length));
  const kinds = [...new Set(layout.edges.map((e) => e.kind))];
  return (
    <div className="small row roof-panels-legend" style={{ gap: 10, flexWrap: 'wrap' }}>
      {[...colours].map(([n, c]) => <span key={n} className="row" style={{ gap: 4 }}><Swatch c={c} /><span data-no-i18n>{`${n} mm`}</span></span>)}
      {layout.totals.reused > 0 && <span className="row" style={{ gap: 4 }}><Swatch c={REUSED} /><span>Reused offcut</span></span>}
      {kinds.map((k) => <span key={k} className="row" style={{ gap: 4 }}><Swatch c={EDGE_COLOUR[k] ?? '#555'} line /><span>{EDGE_LABEL[k] ?? k}</span></span>)}
    </div>
  );
}

const Swatch = ({ c, line }: { c: string; line?: boolean }) => (
  <span style={{ display: 'inline-block', width: 14, height: line ? 3 : 10, background: c, border: line ? 'none' : '1px solid #36404a' }} />
);

function Tables({ layout }: { layout: LayoutDto }) {
  const buy = purchaseRows(layout);
  const faces = faceTotals(layout);
  const edges = edgeRows(layout);
  const offcuts = layout.panels.filter((p) => p.donor);
  const n2 = (v: number) => v.toFixed(2);
  return (
    <div className="roof-panels-tables" data-testid="roof-panels-tables">
      <p className="eyebrow">To buy</p>
      <table className="layer-table" data-testid="roof-panels-buy">
        <thead><tr><th>Length (mm)</th><th>Pieces</th><th>Length (m)</th></tr></thead>
        <tbody>
          {buy.map((r) => <tr key={r.length}><td className="num" data-no-i18n>{r.length}</td><td className="num" data-no-i18n>{r.count}</td><td className="num" data-no-i18n>{n2(r.metres)}</td></tr>)}
          <tr className="total"><td>Total</td><td className="num" data-no-i18n>{layout.totals.bought}</td><td className="num" data-no-i18n>{n2(layout.totals.boughtLength)}</td></tr>
        </tbody>
      </table>
      <dl className="fields small">
        <dt>Roof area</dt><dd className="num" data-no-i18n>{`${n2(layout.totals.roofArea)} m²`}</dd>
        <dt>Coverage</dt><dd className="num" data-no-i18n>{`${n2(layout.totals.coverage)} m²`}</dd>
        <dt>Sheet bought (m²)</dt><dd className="num" data-no-i18n>{n2(layout.totals.sheetArea)}</dd>
      </dl>
      <p className="eyebrow">Per roof plane</p>
      <table className="layer-table" data-testid="roof-panels-faces">
        <thead><tr><th>Roof plane</th><th>Pitch</th><th>Area (m²)</th><th>Panels</th><th>Bought</th><th>Reused</th><th>Sheet bought (m²)</th></tr></thead>
        <tbody>
          {faces.map((f) => (
            <tr key={f.id}>
              <td data-no-i18n>{f.id}</td><td className="num" data-no-i18n>{`${f.pitch.toFixed(1)}°`}</td><td className="num" data-no-i18n>{n2(f.area)}</td>
              <td className="num" data-no-i18n>{f.panels}</td><td className="num" data-no-i18n>{f.bought}</td><td className="num" data-no-i18n>{f.reused}</td><td className="num" data-no-i18n>{n2(f.sheet)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="eyebrow">Edges</p>
      <table className="layer-table" data-testid="roof-panels-edges">
        <thead><tr><th>Kind</th><th>Number</th><th>Length (m)</th><th title="Flashings and caps of the profile's accessory length, lapped">Accessory pieces</th></tr></thead>
        <tbody>
          {edges.map((e) => (
            <tr key={e.kind}><td><Swatch c={EDGE_COLOUR[e.kind] ?? '#555'} line /> <span>{EDGE_LABEL[e.kind] ?? e.kind}</span></td><td className="num" data-no-i18n>{e.count}</td><td className="num" data-no-i18n>{n2(e.length)}</td><td className="num" data-no-i18n>{e.pieces}</td></tr>
          ))}
        </tbody>
      </table>
      {offcuts.length > 0 && (
        <>
          <p className="eyebrow">Offcuts</p>
          <table className="layer-table" data-testid="roof-panels-offcuts">
            <thead><tr><th>Panel</th><th>Cut from</th><th>Shift (mm)</th></tr></thead>
            <tbody>
              {offcuts.map((p) => <tr key={p.id}><td data-no-i18n>{p.id}</td><td data-no-i18n>{p.donor}</td><td className="num" data-no-i18n>{p.shift ?? 0}</td></tr>)}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
