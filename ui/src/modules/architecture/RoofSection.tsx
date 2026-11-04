/**
 * A roof in the inspector: its style (one click puts every rule back), and
 * its edges, each an eave or a gable with its own pitch and overhang (blank:
 * as the roof). The roof's other fields are in the class's own list below.
 */

import { Settings2 } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { t, useLang } from '../../i18n';
import { LengthField, NumberField } from '../../shell/cells';
import { mergeEdge, type Edge } from './roofEdges';
import { lengthText } from '../../engine/unitsStore';

type Style = { id: string; label: string; family: string; shape: string; pitch: number; covering: string };

export function RoofSection({ node }: { node: NodeDto }) {
  const info = useSession((s) => s.info);
  useLang((s) => s.lang); // a style's option names its shape in the language chosen
  const styles = (info?.catalogs['architecture.roofStyles'] as unknown as Style[] | undefined) ?? [];
  // The project's own styles (decision 233) first.
  const project = useSession((s) => [...s.nodes.values()].find((n) => n.type === 'IfcProject'));
  const own = (project?.props.roofStyles as { id: string; label: string; shape: string; pitch: number }[] | undefined) ?? [];
  const contour = (node.props.contour as number[][] | undefined) ?? [];
  const edges = (node.props.edges as Edge[] | undefined) ?? [];
  const setEdge = (i: number, patch: Partial<NonNullable<Edge>> | null) => void execute('architecture.updateRoof', { id: node.id, edges: mergeEdge(edges, contour.length, i, patch) });
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Style</span>
        <span className="row">
        <select value={(node.props.style as string | undefined) ?? ''} disabled={!node.editable}
          onChange={(e) => void execute('architecture.roofStyle', { id: node.id, style: e.target.value })}>
          {!node.props.style && <option value="">Shaped by hand</option>}
          {own.length > 0 && <optgroup label={t('Project')}>{own.map((s) => <option key={s.id} value={s.id}>{`${s.label} · ${t(s.shape)}${s.shape === 'barrel' ? '' : ` ${s.pitch}°`}`}</option>)}</optgroup>}
          {styles.map((s) => <option key={s.id} value={s.id}>{`${s.label} · ${t(s.shape)}${s.shape === 'barrel' ? '' : ` ${s.pitch}°`}`}</option>)}
        </select>
        <button type="button" className="icon" title="Roof configurator…" disabled={!node.editable} onClick={() => useUi.setState({ dialog: 'roof', roofId: node.id })}><Settings2 size={14} /></button>
        </span>
      </label>
      <table className="roof-edges">
        <thead><tr><th>Edge</th><th>Kind</th><th>°</th><th>Overhang</th></tr></thead>
        <tbody>
          {contour.map((p, i) => {
            const q = contour[(i + 1) % contour.length];
            const e = edges[i] ?? null;
            const len = Math.hypot(q[0] - p[0], q[1] - p[1]);
            return (
              <tr key={i}>
                <td className="num" title={lengthText(len)}>{i + 1}</td>
                <td>
                  <select value={e?.kind ?? ''} disabled={!node.editable} data-i18n-ctx="edge" onChange={(ev) => setEdge(i, { kind: (ev.target.value || undefined) as 'eave' | 'gable' | undefined })}>
                    <option value="">auto</option><option value="eave">eave</option><option value="gable">gable</option>
                  </select>
                </td>
                <td><NumberField value={e?.pitch} blank readOnly={!node.editable} placeholder={String(node.props.pitch ?? '')} onCommit={(v) => setEdge(i, { pitch: v ?? undefined })} /></td>
                <td><LengthField metres={e?.overhang} blank noUnit readOnly={!node.editable} onCommit={(v) => setEdge(i, { overhang: v ?? undefined })} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {Array.isArray(node.props.openings) && node.props.openings.length > 0 && (
        <table className="roof-openings">
          <caption className="muted small">Roof windows and dormers: drag one's grip in plan to move it; × or Ctrl/⌘-click on its grip removes it.</caption>
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
