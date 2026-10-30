/**
 * A roof in the inspector: its style (one click puts every rule back), and
 * its edges, each an eave or a gable with its own pitch and overhang (blank:
 * as the roof). The roof's other fields are in the class's own list below.
 */

import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { t, useLang } from '../../i18n';

type Style = { id: string; label: string; family: string; shape: string; pitch: number; covering: string };
type Edge = { kind?: 'eave' | 'gable'; pitch?: number; overhang?: number } | null;

export function RoofSection({ node }: { node: NodeDto }) {
  const info = useSession((s) => s.info);
  useLang((s) => s.lang); // a style's option names its shape in the language chosen
  const styles = (info?.catalogs['architecture.roofStyles'] as unknown as Style[] | undefined) ?? [];
  const contour = (node.props.contour as number[][] | undefined) ?? [];
  const edges = (node.props.edges as Edge[] | undefined) ?? [];
  const setEdge = (i: number, patch: Partial<NonNullable<Edge>> | null) => {
    const next: Edge[] = contour.map((_, k) => edges[k] ?? null);
    const merged = patch === null ? null : { ...(next[i] ?? {}), ...patch };
    if (merged) for (const k of Object.keys(merged) as (keyof typeof merged)[]) if (merged[k] === undefined) delete merged[k];
    next[i] = merged && Object.keys(merged).length ? merged : null;
    void execute('architecture.updateRoof', { id: node.id, edges: next });
  };
  const num = (t: string) => (t.trim() === '' ? undefined : Number(t.replace(',', '.')));
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Style</span>
        <select value={(node.props.style as string | undefined) ?? ''} disabled={!node.editable}
          onChange={(e) => void execute('architecture.roofStyle', { id: node.id, style: e.target.value })}>
          {!node.props.style && <option value="">Shaped by hand</option>}
          {styles.map((s) => <option key={s.id} value={s.id}>{`${s.label} · ${t(s.shape)}${s.shape === 'barrel' ? '' : ` ${s.pitch}°`}`}</option>)}
        </select>
      </label>
      <table className="roof-edges">
        <thead><tr><th>Edge</th><th>Kind</th><th>°</th><th>mm</th></tr></thead>
        <tbody>
          {contour.map((p, i) => {
            const q = contour[(i + 1) % contour.length];
            const e = edges[i] ?? null;
            const len = Math.hypot(q[0] - p[0], q[1] - p[1]);
            return (
              <tr key={i}>
                <td className="num" title={`${len.toFixed(2)} m`}>{i + 1}</td>
                <td>
                  <select value={e?.kind ?? ''} disabled={!node.editable} data-i18n-ctx="edge" onChange={(ev) => setEdge(i, { kind: (ev.target.value || undefined) as 'eave' | 'gable' | undefined })}>
                    <option value="">auto</option><option value="eave">eave</option><option value="gable">gable</option>
                  </select>
                </td>
                <td><input className="num" defaultValue={e?.pitch ?? ''} key={`p${i}${e?.pitch}`} disabled={!node.editable} placeholder={String(node.props.pitch ?? '')}
                  onBlur={(ev) => num(ev.target.value) !== e?.pitch && setEdge(i, { pitch: num(ev.target.value) })} /></td>
                <td><input className="num" defaultValue={e?.overhang != null ? Math.round(e.overhang * 1000) : ''} key={`o${i}${e?.overhang}`} disabled={!node.editable}
                  onBlur={(ev) => { const v = num(ev.target.value); const m = v === undefined ? undefined : v / 1000; if (m !== e?.overhang) setEdge(i, { overhang: m }); }} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {Array.isArray(node.props.openings) && node.props.openings.length > 0 && (
        <table className="roof-openings">
          <tbody>
            {(node.props.openings as { id: string; name: string; kind: string; width?: number; length?: number }[]).map((o) => (
              <tr key={o.id}>
                <td>{o.name}</td>
                <td className="muted small">{o.kind === 'dormer' ? 'dormer' : `${Math.round((o.width ?? 0.78) * 100)}×${Math.round((o.length ?? 1.18) * 100)}`}</td>
                <td><button type="button" className="icon mini" title="Remove" onClick={() => void execute('architecture.removeRoofOpening', { roofId: node.id, id: o.id })}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
