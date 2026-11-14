/**
 * An elevator in Properties (above its fields): its style, the storeys it runs between and those it stops on, its
 * shaft's wall type, what EN 81 says of it, the way to the configurator and the floors' openings made again.
 */

import { useEffect, useState } from 'react';
import { AlertTriangle, Settings2, SquareDashed } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { relatingOf } from '../../engine/model';
import { stopsBetween, toggleStop, type ElevatorStyle, type OwnStyle, type Preview } from './elevator';
import { openConfigurator } from './openConfigurator';

export function ElevatorSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const [preview, setPreview] = useState<Preview | null>(null);
  useEffect(() => {
    let live = true;
    void query<Preview>('transport.preview', { id: node.id }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [node]);
  const storeys = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').sort((a, b) => Number(a.props.elevation ?? 0) - Number(b.props.elevation ?? 0));
  const bottom = relatingOf({ nodes, relations }, node.id, 'IfcRelContainedInSpatialStructure')?.id;
  const top = (node.props.topStoreyId as string | undefined) ?? bottom;
  const stops = stopsBetween(storeys, bottom, top);
  const unserved = (node.props.unserved as string[] | undefined) ?? [];
  const wallTypes = [...nodes.values()].filter((n) => n.type === 'IfcWallType').sort((a, b) => a.name.localeCompare(b.name));
  const styles = (info?.catalogs['transport.styles'] as unknown as ElevatorStyle[] | undefined) ?? [];
  const project = [...nodes.values()].find((n) => n.type === 'IfcProject');
  const own = (project?.props.elevatorStyles as OwnStyle[] | undefined) ?? [];
  const styleId = node.props.style as string | undefined;
  const styleLabel = own.find((s) => s.id === styleId)?.label ?? styles.find((s) => s.id === styleId)?.label ?? '—';
  const update = (args: Record<string, unknown>) => void execute('transport.update', { id: node.id, ...args });
  const setAttrs = (attrs: Record<string, unknown>) => update({ attrs });
  return (
    <div data-testid="elevator-section" data-i18n-ctx="transport">
      <dl className="fields">
        <dt>Style</dt><dd data-no-i18n={own.some((s) => s.id === styleId) || undefined}>{styleLabel}</dd>
        <dt>Lowest stop</dt>
        <dd>
          <select value={bottom ?? ''} data-field="elevator-bottom" onChange={(e) => update({ bottomStoreyId: e.target.value })}>
            {storeys.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
          </select>
        </dd>
        <dt>Highest stop</dt>
        <dd>
          <select value={top ?? ''} data-field="elevator-top" onChange={(e) => setAttrs({ topStoreyId: e.target.value })}>
            {storeys.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
          </select>
        </dd>
        <dt title="A storey the elevator passes without a landing door">Stops on</dt>
        <dd className="stops">
          {stops.map((s, i) => (
            <label key={s.id} className="row small" style={{ gap: 4 }}>
              <input type="checkbox" checked={!unserved.includes(s.id)} disabled={i === 0 || i === stops.length - 1}
                onChange={(e) => setAttrs({ unserved: toggleStop(unserved, s.id, e.target.checked) })} />
              <span data-no-i18n>{s.name}</span>
            </label>
          ))}
        </dd>
        {node.props.drive !== 'vacuum' && (
          <>
            <dt>Shaft walls</dt>
            <dd>
              <select value={(node.props.shaftWallTypeId as string | undefined) ?? ''} data-field="elevator-shaft-type" onChange={(e) => setAttrs({ shaftWallTypeId: e.target.value || null })}>
                <option value="">Concrete, the thickness below</option>
                {wallTypes.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
              </select>
            </dd>
          </>
        )}
      </dl>
      {preview?.checks && preview.checks.length > 0 && (
        <ul className="elevator-checks small pad" data-no-i18n>
          {preview.checks.map((c) => <li key={c}><AlertTriangle size={12} /> {c}</li>)}
        </ul>
      )}
      <div className="row pad" style={{ gap: 6 }}>
        <button type="button" className="btn small" data-action="elevator-configure" onClick={() => openConfigurator(node.id)}><Settings2 size={13} /> Configurator…</button>
        <button type="button" className="btn small" title="Open the floors the shaft passes through again (a floor drawn after the elevator)"
          onClick={() => void execute('transport.syncOpenings', { ids: [node.id] })}><SquareDashed size={13} /> Shaft openings</button>
      </div>
    </div>
  );
}
