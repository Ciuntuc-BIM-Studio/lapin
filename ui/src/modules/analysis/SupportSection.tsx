/** A support's analysis in its properties (docs/plan-fem.md §14.2, §14.4): springs on the six freedoms, Winkler's ks, compression only (lifts), and the stiffness each of its nodes got. Indicative: ks is the geotechnical engineer's. */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { selectionWith } from '../../shell/params';
import { useAnalysis } from './hooks';
import {
  formatSoilRange, formatStiffness, ownCompressionOnly, ownSprings, ownWinkler, parseSpring, parseWinkler, SPRING_LABELS, SPRING_UNITS, SUPPORT_CLASSES, withSpring,
  type SupportOptionsDto,
} from './stability';

/** A number typed in a field: written when it reads and differs, else put back. */
function ValueField({ label, unit, title, value, test, parse, onApply, placeholder }: {
  label: string; unit: string; title: string; value: number | null; test: string; placeholder?: string;
  parse: (t: string) => number | null | undefined; onApply: (v: number | null) => void;
}) {
  const [text, setText] = useState(value == null ? '' : String(value));
  useEffect(() => setText(value == null ? '' : String(value)), [value]);
  const apply = () => {
    const v = parse(text);
    if (v === undefined || v === value) setText(value == null ? '' : String(value));
    else onApply(v);
  };
  return (
    <label className="row small" title={title}>
      <span style={{ minWidth: 34 }} data-no-i18n>{label}</span>
      <input aria-label={label} data-testid={test} style={{ width: 84 }} value={text} placeholder={placeholder} onChange={(e) => setText(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === 'Enter') apply(); }} />
      <span className="muted" data-no-i18n>{unit}</span>
    </label>
  );
}

export function SupportSection({ node }: { node: NodeDto }) {
  const ids = selectionWith(node, (n) => (SUPPORT_CLASSES as readonly string[]).includes(n.type));
  const support = node.props.analysisSupport;
  const springs = ownSprings(support);
  const ks = ownWinkler(support);
  const opt = useAnalysis<SupportOptionsDto>('analysis.supportOptions', ids.length === 1, { id: node.id });
  const kind = node.type === 'IfcSlab' ? 'slab' : node.type === 'IfcFooting' ? 'footing' : 'base';
  const set = (args: Record<string, unknown>) => void execute('analysis.setSupport', { ids, ...args });
  const mine = opt && opt.id === node.id ? opt : null;
  return (
    <div data-testid="support-section">
      {kind !== 'slab' && (
        <>
          <span className="eyebrow">Springs</span>
          <div className="support-springs" data-testid="support-springs">
            {SPRING_LABELS.map((l, i) => (
              <ValueField key={l} label={l} unit={SPRING_UNITS[i]!} test={`support-spring-${l}`} value={springs[i] ?? null} parse={parseSpring} placeholder="as held"
                title="A spring's stiffness replaces how the support holds this freedom; empty: as the support holds it, 0: free" onApply={(v) => set({ springs: withSpring(springs, i, v) })} />
            ))}
          </div>
        </>
      )}
      {kind !== 'base' && (
        <>
          <span className="eyebrow">Winkler&apos;s bed</span>
          <ValueField label="ks" unit="kN/m³" test="support-winkler" value={ks} parse={parseWinkler}
            title="The modulus of subgrade reaction of the ground under it: the geotechnical engineer's value" onApply={(v) => set({ winkler: v })} />
          {mine && mine.typical.length > 0 && (
            <div data-testid="support-typical">
              <p className="muted small">Typical ks, indicative: the ground&apos;s value is the geotechnical engineer&apos;s</p>
              <ul className="muted small" style={{ margin: 0, paddingLeft: 16 }}>
                {mine.typical.map((t) => <li key={t.soil}>{t.soil}: <span data-no-i18n>{formatSoilRange(t.min, t.max)}</span></li>)}
              </ul>
            </div>
          )}
        </>
      )}
      <label className="row small" title="A support that would pull lifts off the ground, free there: each combination is solved again without it">
        <input type="checkbox" data-testid="support-compression-only" checked={ownCompressionOnly(support)} onChange={(e) => set({ compressionOnly: e.target.checked ? true : null })} />
        <span>Compression only (lifts)</span>
      </label>
      {mine && mine.nodes.length > 0 && (
        <table className="standards-table" data-testid="support-nodes">
          <caption className="muted small">Stiffness at each node of the last analysis</caption>
          <thead><tr><th>Node</th>{SPRING_LABELS.map((l) => <th key={l} data-no-i18n>{l}</th>)}</tr></thead>
          <tbody>
            {mine.nodes.slice(0, 12).map((n) => (
              <tr key={n.node}>
                <td data-no-i18n>{n.node}</td>
                {n.k.map((k, i) => <td key={i} className={n.held[i] ? 'muted' : undefined} data-no-i18n>{n.held[i] ? 'held' : formatStiffness(k)}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <p className="muted small">Indicative: a spring or ks is the engineer&apos;s own to give; the forces in the springs are reactions. A slab takes Winkler&apos;s ks only, a column base springs only.</p>
    </div>
  );
}
