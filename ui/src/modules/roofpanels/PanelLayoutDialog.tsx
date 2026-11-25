/**
 * Panel layout (docs/plan-roof-panels.md §4, S2): a roof's panels as the Panotaj Bilka app shows them. The roof in plan
 * (or its planes laid flat, each in true size), every panel coloured by its length, the offcuts reused in orange with a
 * line from the panel they are cut from, the edges coloured by kind; a panel's code, length and donor on hover. Beside it
 * the tables: what to buy, each roof plane's totals, the edges and the offcuts, and (S3) the accessories, fixings, battens and
 * membrane, with each slope's battens and membrane laps; openings drawn as the panels are cut round them. Another profile can be tried first (the
 * layout query takes a profile without changing the roof) and taken with Use this profile, one undo step. Stage S4: the Cost tab,
 * each line bought with its price, amount and source from the project's price lists, the lines to quote, what is cut from flat
 * sheet and the joints between ridge caps apart, the subtotal of the priced lines; indicative, as the prices it reads.
 */

import { useMemo, useState } from 'react';
import { useUi } from '../../app/store';
import { Dialog } from '../../shell/Dialog';
import { notify } from '../../app/store';
import { engine, engineInBrowser, execute, useSession } from '../../engine/session';
import { useLayout } from './PanelsSection';
import {
  BILKA_TEMPLATE, bounds, centroid, choiceOf, COST_GROUPS, costRows, costState, EDGE_COLOUR, EDGE_LABEL, edgeRows, faceTotals, isLayout, ITEM_GROUPS, itemRows, laidFlat,
  lengthColours, money, needText, profilesOf, purchaseRows, quantityText, REUSED, reuseLinks, setArgs, sheetRows, subtotalText, type CostDto, type CostLineDto,
  type LayoutDto, type PanelDto, type Pt,
} from './roofpanels';

type Mode = 'plan' | 'flat';
type Tab = 'quantities' | 'cost';

/** A price's page (its supplier's), in the system's browser; a new tab on the web. */
function openPage(url: string) {
  if (engineInBrowser) { window.open(url, '_blank', 'noopener'); return; }
  void engine.call('support.open', { link: 'extensionPage', url }).catch(() => notify('The link could not be opened.'));
}

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
  const [tab, setTab] = useState<Tab>('quantities');
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
        {isLayout(layout) && layout.cost?.available && (
          <span className="seg small" role="group">
            <button type="button" className={`seg-item${tab === 'quantities' ? ' on' : ''}`} data-action="roof-panels-quantities" onClick={() => setTab('quantities')}>Quantities</button>
            <button type="button" className={`seg-item${tab === 'cost' ? ' on' : ''}`} data-action="roof-panels-cost" onClick={() => setTab('cost')}>Cost</button>
          </span>
        )}
        {trying && <span className="muted small">Compare: the roof keeps its own profile until you use this one.</span>}
      </div>
      {!layout && <p className="muted">Laying the panels…</p>}
      {layout && !isLayout(layout) && <p className="error" data-testid="roof-panel-layout-problem">{layout.problem}</p>}
      {isLayout(layout) && tab === 'cost' && layout.cost?.available && <CostTable cost={layout.cost} />}
      {isLayout(layout) && !(tab === 'cost' && layout.cost?.available) && (
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

/** What a line is counted from: its number as it is, its unit translated. */
function Need({ i }: { i: Parameters<typeof needText>[0] }) {
  const t = needText(i);
  if (!t) return null;
  const at = t.indexOf(' ');
  return <><span data-no-i18n>{t.slice(0, at)}</span>{' '}<span>{t.slice(at + 1)}</span></>;
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
      {layout.faces.flatMap((f) => (f.holes ?? []).map((h) => (
        <path key={`${f.id}-${h.id}`} d={d(mode === 'plan' ? h.plan : at(f.id, h.outline))} data-hole={h.kind} fill="var(--bg, #fff)" stroke="#b8860b" strokeWidth={stroke * 2} pointerEvents="none">
          <title>{h.name}</title>
        </path>
      )))}
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
      {layout.takeoff && (
        <div data-testid="roof-panels-takeoff">
          <div data-testid="roof-panels-takeoff-items">
            {ITEM_GROUPS.map((g) => {
              const rows = itemRows(layout.takeoff, g.id);
              if (rows.length === 0) return null;
              return (
                <div key={g.id}>
                  <p className="eyebrow">{g.label}</p>
                  <table className="layer-table" data-testid={`roof-panels-items-${g.id}`}>
                    <thead><tr><th>Item</th><th>Quantity</th><th>Unit</th><th title="What the quantity is counted from: the length run, the area, the fixings">Needed</th></tr></thead>
                    <tbody>
                      {rows.map((i) => (
                        <tr key={i.code} title={i.note || undefined} data-item={i.code}>
                          <td>{i.name}</td><td className="num" data-no-i18n>{i.quantity}</td><td>{i.unit}</td><td className="num"><Need i={i} /></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              );
            })}
          </div>
          <p className="eyebrow">Battens and membrane per roof plane</p>
          <table className="layer-table" data-testid="roof-panels-support">
            <thead><tr><th>Roof plane</th><th>Area (m²)</th><th title="The membrane's strips lapped across the slope">Membrane laps (m²)</th><th>Battens (m)</th><th>Rows</th></tr></thead>
            <tbody>
              {layout.takeoff.faces.map((f) => (
                <tr key={f.id}>
                  <td data-no-i18n>{f.id}</td><td className="num" data-no-i18n>{n2(f.area)}</td><td className="num" data-no-i18n>{n2(f.membraneLaps)}</td>
                  <td className="num" data-no-i18n>{n2(f.battens)}</td><td className="num" data-no-i18n>{f.rows}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
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

/** A line's price, amount and source; „to quote” when no list prices it. */
function CostRow({ l }: { l: CostLineDto }) {
  const priced = l.amount != null;
  return (
    <tr data-cost={l.code} title={l.note || undefined}>
      <td>{l.group === 'panels' ? <span data-no-i18n>{l.name}</span> : <span>{l.name}</span>}{l.pieces != null && <span className="muted" data-no-i18n>{` (${l.pieces})`}</span>}</td>
      <td className="num" data-no-i18n>{quantityText(l)}</td>
      <td style={{ whiteSpace: 'nowrap' }}>{l.unit}</td>
      {priced ? (
        <>
          <td className="num" data-no-i18n style={{ whiteSpace: 'nowrap' }} title={l.unitDiffers ? `${l.priceUnit}` : undefined}>{money(l.price ?? 0)}{l.unitDiffers ? ' ⚠' : ''}</td>
          <td className="num" data-no-i18n style={{ whiteSpace: 'nowrap' }}>{money(l.amount ?? 0)}</td>
          <td className="small" style={{ whiteSpace: 'nowrap' }} title={[l.list, l.checked, l.url].filter(Boolean).join(' · ')}>
            {l.url?.startsWith('https://') ? <a href={l.url} data-no-i18n onClick={(e) => { e.preventDefault(); openPage(l.url!); }}>{l.source ?? l.list}</a> : <span data-no-i18n>{l.source ?? l.list}</span>}
          </td>
          <td className="small muted" data-no-i18n style={{ whiteSpace: 'nowrap' }}>{l.checked ?? ''}</td>
        </>
      ) : (
        <td colSpan={4} className="muted small" data-testid="roof-panels-to-quote">To quote</td>
      )}
    </tr>
  );
}

/**
 * The Cost tab: the lines bought by group, each with its price, amount and source; the subtotal of the priced lines; the
 * lines cut from flat sheet and the joints between ridge caps apart; the note that it is indicative. With no price for any
 * line, the Bilka – Dedeman template can be added from here (when asked: never by itself).
 */
function CostTable({ cost }: { cost: CostDto }) {
  const state = costState(cost);
  const sheet = sheetRows(cost);
  return (
    <div className="roof-panels-cost" data-testid="roof-panels-cost-table">
      {state === 'unpriced' && (
        <div className="roof-panels-cost-hint" data-testid="roof-panels-cost-empty">
          <p className="muted small">No price list of the project prices these lines. Add the Bilka – Dedeman list (indicative prices read on 24.11.2026), or import your own with the same codes.</p>
          <div className="row" style={{ gap: 8 }}>
            <button type="button" className="btn" data-action="roof-panels-add-template" onClick={() => void execute('cost.addPriceList', { template: BILKA_TEMPLATE })}>Add the Bilka – Dedeman template</button>
            <button type="button" className="btn" onClick={() => useUi.setState({ dialog: 'cost.prices' })}>Price lists…</button>
          </div>
        </div>
      )}
      <table className="layer-table" data-testid="roof-panels-cost-lines">
        <thead><tr><th>Item</th><th>Quantity</th><th>Unit</th><th>Price</th><th>Amount</th><th>Source</th><th title="The day the price was read">Price checked on</th></tr></thead>
        {COST_GROUPS.map((g) => {
          const rows = costRows(cost, g.id);
          if (rows.length === 0) return null;
          return (
            <tbody key={g.id}>
              <tr className="group"><td colSpan={7} className="eyebrow">{g.label}</td></tr>
              {rows.map((l) => <CostRow key={`${l.code}#${l.variant ?? ''}`} l={l} />)}
            </tbody>
          );
        })}
        <tbody>
          <tr className="total" data-testid="roof-panels-cost-subtotal">
            <td colSpan={4}>Subtotal of the priced lines</td>
            <td className="num" data-no-i18n style={{ whiteSpace: 'nowrap' }}>{subtotalText(cost) || '—'}</td><td colSpan={2} />
          </tr>
        </tbody>
      </table>
      <dl className="fields small">
        {cost.toQuote > 0 && <><dt title="Lines no price list prices: ask the supplier">To quote</dt><dd className="num" data-no-i18n>{cost.toQuote}</dd></>}
        {cost.joints > 0 && <><dt title="Where two ridge or hip caps lap: an operation, the material is in the caps">Joints between ridge caps</dt><dd className="num" data-no-i18n>{cost.joints}</dd></>}
      </dl>
      {sheet.length > 0 && (
        <>
          <p className="eyebrow">Cut from flat sheet</p>
          <table className="layer-table" data-testid="roof-panels-cost-sheet">
            <tbody>{sheet.map((l) => <tr key={l.code} title={l.note || undefined}><td>{l.name}</td><td className="num" data-no-i18n>{quantityText(l)}</td><td>{l.unit}</td></tr>)}</tbody>
          </table>
        </>
      )}
      <p className="small roof-panels-indicative" data-testid="roof-panels-indicative">Indicative — check prices when ordering. Labour, transport and the rainwater system are not included.</p>
    </div>
  );
}

