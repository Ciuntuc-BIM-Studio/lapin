/**
 * What the selected element measures, as the engine works it out from its
 * geometry: the element's own set, then each part's (a zone, a finish layer),
 * with blocks, mortar, concrete and steel where they apply.
 */

import { useEffect, useState } from 'react';
import { engine, useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';
import { areaText, lengthText, useStandards } from '../engine/unitsStore';

interface Quantity { name: string; value: number; kind: string; label?: string | null; unit: string }
interface QuantitySet { name: string; quantities: Quantity[]; part?: string | null; partName?: string | null; material?: string | null }

const SHOWN = new Set(['Length', 'Height', 'Width', 'NetSideArea', 'NetVolume', 'BlockCount', 'MortarVolume', 'ReinforcementWeight', 'CrossSectionArea', 'NetFloorArea', 'GrossPerimeter']);

/** A space's floor areas, shown to the project's area rounding (decision 471); other areas are material quantities, kept to the millimetre. */
const SPACE_AREAS = new Set(['NetFloorArea', 'GrossFloorArea']);

const format = (q: Quantity) => q.kind === 'COUNT' ? `${Math.round(q.value)} ${q.unit}` : q.kind === 'LENGTH' ? lengthText(q.value)
  : q.kind === 'AREA' && SPACE_AREAS.has(q.name) ? areaText(q.value) : `${q.value.toFixed(q.kind === 'WEIGHT' ? 1 : 3)} ${q.unit}`;

export function QuantitiesSection({ node }: { node: NodeDto }) {
  const version = useSession((s) => s.version);
  useStandards((s) => s.units);
  useStandards((s) => s.areaRounding);
  const [sets, setSets] = useState<QuantitySet[]>([]);
  useEffect(() => {
    let live = true;
    engine.call<QuantitySet[]>('quantities.of', { id: node.id }).then((r) => { if (live) setSets(r ?? []); }).catch(() => live && setSets([]));
    return () => { live = false; };
  }, [node.id, version]);
  if (sets.length === 0) return null;
  return (
    <section className="sect">
      <h3 className="eyebrow">Quantities</h3>
      {sets.map((s, i) => (
        <div key={`${s.name}:${s.part ?? i}`} className="qset">
          <div className="qset-head">{s.partName ?? 'Element'}<span className="muted small"> · {s.name}</span></div>
          <dl className="fields">
            {s.quantities.filter((q) => SHOWN.has(q.name)).map((q) => (
              <span key={q.name} className="qrow"><dt>{q.label ?? q.name}</dt><dd className="num">{format(q)}</dd></span>
            ))}
          </dl>
        </div>
      ))}
    </section>
  );
}
