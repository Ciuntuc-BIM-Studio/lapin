/** A type's layer's category in its build-up (the forum's topic #30): one of the materials' categories, or its material's. */
import { LAYER_CATEGORIES } from './layerFlags';

export const CATEGORY_TIP = 'What the layer is in this build-up; empty: its material\'s category (Manage → Materials). View styles leave out the finishes by it';

export function LayerCategory({ value, onChange }: { value: string | null; onChange: (v: string | null) => void }) {
  return (
    <select className="layer-category" data-testid="layer-category" value={value ?? ''} title={CATEGORY_TIP} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">material's</option>
      {LAYER_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
    </select>
  );
}
