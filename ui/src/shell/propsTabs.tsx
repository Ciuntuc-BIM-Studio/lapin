/**
 * Properties panel tab organisation, inline dimension diagrams, and status badges (forum topic 296).
 * Groups element properties into four discipline/dimension tabs:
 * - Model (3D): geometry, placement, dimensions, and type parameters
 * - 2D / View: view-specific overrides, display settings, hatch origin, cut/projection graphics
 * - Data / Info: OpenBIM metadata, parameters, classification, and discipline sets (Thermal, Structural, MEP…)
 * - 4D / 5D: phasing, scheduling, quantity takeoff, and cost data
 */

import type { NodeDto } from '../engine/protocol';

export type PropsTab = 'model' | 'view' | 'data' | 'plan';

export const PROPS_TABS: readonly { id: PropsTab; label: string }[] = [
  { id: 'model', label: 'Model (3D)' },
  { id: 'view', label: '2D / View' },
  { id: 'data', label: 'Data / Info' },
  { id: 'plan', label: '4D / 5D' },
];

const VIEW_SECTIONS = new Set([
  'viewSettings', 'storeyLevels', 'titleBlock', 'inset', 'callout', 'markerSymbol',
  'elementGraphics', 'hatchOrigin', 'sectionLine', 'detail', 'annotation', 'dimension', 'tag',
]);

const DATA_SECTIONS = new Set([
  'thermal', 'structural', 'analysis', 'rebar', 'steel', 'wood', 'mep', 'fire', 'acoustic',
  'params', 'classification', 'ifcProperties', 'dependencies',
]);

const PLAN_SECTIONS = new Set([
  'phase', 'phases', 'planning', 'schedule', 'tasks', 'cost', 'estimate', 'quantities', 'work',
]);

const ADVANCED_MODEL_SECTIONS = new Set([
  'autoJoin', 'array', 'group', 'light', 'cleanups', 'overrides',
]);

/** Map a module PropertySectionDef id to one of the four Properties panel tabs. */
export function propsTabOf(sectionId: string): PropsTab {
  const k = sectionId.toLowerCase();
  if (VIEW_SECTIONS.has(sectionId) || k.includes('view') || k.includes('graphic') || k.includes('hatch') || k.includes('symbol') || k.includes('callout') || k.includes('titleblock')) return 'view';
  if (PLAN_SECTIONS.has(sectionId) || k.includes('phase') || k.includes('cost') || k.includes('plan') || k.includes('quant') || k.includes('sched') || k.includes('4d') || k.includes('5d')) return 'plan';
  if (DATA_SECTIONS.has(sectionId) || k.includes('thermal') || k.includes('struct') || k.includes('analys') || k.includes('rebar') || k.includes('classif') || k.includes('ifc')) return 'data';
  return 'model';
}

/** Whether a Model (3D) section belongs under the Advanced fold rather than Basic. */
export function isAdvancedModelSection(sectionId: string): boolean {
  return ADVANCED_MODEL_SECTIONS.has(sectionId);
}

/** Compact inline isometric dimension diagram for common building elements (forum topic 296). */
export function DimensionDiagram({ node }: { node: NodeDto }) {
  if (node.type === 'IfcWall' || node.type === 'IfcCurtainWall') {
    return (
      <div className="props-diagram" aria-hidden="true">
        <svg viewBox="0 0 180 104" width="160" height="92">
          {/* Isometric wall block */}
          <polygon points="48,26 118,12 128,17 58,31" fill="var(--accent-wash, #e6dfd5)" stroke="var(--ink, #333)" strokeWidth="1" />
          <polygon points="58,31 128,17 128,74 58,88" fill="var(--wash, #f0ece4)" stroke="var(--ink, #333)" strokeWidth="1" />
          <polygon points="48,26 58,31 58,88 48,83" fill="var(--rule, #d6d0c4)" stroke="var(--ink, #333)" strokeWidth="1" />
          {/* Dimension A: Width */}
          <line x1="36" y1="30" x2="48" y2="26" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <line x1="46" y1="35" x2="58" y2="31" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <line x1="39" y1="29" x2="49" y2="34" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <text x="26" y="34" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">A</text>
          {/* Dimension B: Height */}
          <line x1="138" y1="15" x2="138" y2="72" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <line x1="132" y1="16" x2="142" y2="14" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <line x1="132" y1="73" x2="142" y2="71" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <text x="145" y="47" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">B</text>
          {/* Dimension C: Length */}
          <line x1="58" y1="95" x2="128" y2="81" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <line x1="58" y1="89" x2="58" y2="98" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <line x1="128" y1="75" x2="128" y2="84" stroke="var(--accent, #2e6db4)" strokeWidth="1" />
          <text x="90" y="100" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">C</text>
        </svg>
      </div>
    );
  }
  if (node.type === 'IfcDoor' || node.type === 'IfcWindow') {
    return (
      <div className="props-diagram" aria-hidden="true">
        <svg viewBox="0 0 180 96" width="160" height="84">
          <rect x="56" y="14" width="60" height="62" fill="var(--wash, #f0ece4)" stroke="var(--ink, #333)" strokeWidth="1.2" />
          <rect x="61" y="19" width="50" height="52" fill="var(--panel, #fff)" stroke="var(--ink, #333)" strokeWidth="0.8" />
          {/* Width W */}
          <line x1="56" y1="84" x2="116" y2="84" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <text x="82" y="94" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">W</text>
          {/* Height H */}
          <line x1="128" y1="14" x2="128" y2="76" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <text x="134" y="49" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">H</text>
        </svg>
      </div>
    );
  }
  if (node.type === 'IfcSlab') {
    return (
      <div className="props-diagram" aria-hidden="true">
        <svg viewBox="0 0 180 86" width="160" height="76">
          <polygon points="36,34 94,16 146,34 88,52" fill="var(--wash, #f0ece4)" stroke="var(--ink, #333)" strokeWidth="1" />
          <polygon points="36,34 88,52 88,64 36,46" fill="var(--rule, #d6d0c4)" stroke="var(--ink, #333)" strokeWidth="1" />
          <polygon points="88,52 146,34 146,46 88,64" fill="var(--accent-wash, #e6dfd5)" stroke="var(--ink, #333)" strokeWidth="1" />
          <line x1="154" y1="34" x2="154" y2="46" stroke="var(--accent, #2e6db4)" strokeWidth="1.2" />
          <text x="160" y="44" fontSize="10" fontWeight="600" fill="var(--accent, #2e6db4)">T</text>
        </svg>
      </div>
    );
  }
  return null;
}
