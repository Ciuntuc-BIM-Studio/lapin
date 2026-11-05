/**
 * A layer's thickness in a type's layer table (Wall types, Floor types): typed as any length, with its material's
 * catalogue of thicknesses (docs/plan-buildup-tags.md) offered beside it and a warning mark when the layer is in
 * another thickness.
 */

import { TriangleAlert } from 'lucide-react';
import type { ModelState } from '../../engine/model';
import { LengthField } from '../../shell/cells';
import { catalogueOf, inCatalogue } from './materialData';

export function LayerThickness({ model, material, metres, onCommit }: { model: ModelState; material: string; metres: number; onCommit: (m: number) => void }) {
  const sizes = catalogueOf(model, material);
  const off = !inCatalogue(sizes, metres);
  return (
    <span className="layer-thickness">
      <LengthField metres={metres} min={0} testId="layer-thickness" onCommit={(m) => { if (m != null) onCommit(m); }} />
      {sizes.length > 0 && (
        <select className="catalogue" data-field="layer-catalogue" value="" title="The thicknesses its material is made in" onChange={(e) => { const mm = Number(e.target.value); if (mm > 0) onCommit(mm / 1000); }}>
          <option value="">▾</option>
          {sizes.map((s) => <option key={s} value={s}>{s} mm</option>)}
        </select>
      )}
      {off && <span className="off-catalogue" data-testid="layer-off-catalogue" title="Not a catalogue thickness of its material"><TriangleAlert size={12} /></span>}
    </span>
  );
}
