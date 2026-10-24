/** Structure: columns, beams, strip and pad foundations (the engine's structure module). */

import { Grid3x3, Layers3 } from 'lucide-react';
import { BeamIcon, ColumnIcon, PadFootingIcon, StripFootingIcon } from '../../icons';
import type { Grip, LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { ensureStorey, execute } from '../../engine/session';
import { StructureSection } from './StructureSection';
import { StructureTypesDialog } from './StructureTypesDialog';

const column: ToolDef = {
  id: 'structure.column', label: 'Column', icon: ColumnIcon, shortcut: 'CL',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('structure.addColumn', { storeyId: ctx.storeyId, x: p.x, y: p.y }); },
};
const beam: ToolDef = {
  id: 'structure.beam', label: 'Beam', icon: BeamIcon, shortcut: 'BM',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('structure.addBeam', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y }); },
};
const strip: ToolDef = {
  id: 'structure.strip', label: 'Strip footing', icon: StripFootingIcon, shortcut: 'SF',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('structure.addStripFooting', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y }); },
};
const pad: ToolDef = {
  id: 'structure.pad', label: 'Pad footing', icon: PadFootingIcon, shortcut: 'PF',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('structure.addPadFooting', { storeyId: ctx.storeyId, x: p.x, y: p.y }); },
};
/** A column on every grid crossing of the storey (none twice). */
const atGrid: ToolDef = {
  id: 'structure.columnsAtGrid', label: 'Columns at grid', icon: Grid3x3,
  run: () => void ensureStorey().then((storeyId) => storeyId && execute('structure.addColumnsAtGrid', { storeyId })),
};
const types: ToolDef = { id: 'structure.types', label: 'Structure types', icon: Layers3, run: () => useUi.setState({ dialog: 'structureTypes' }) };

/** The ends of a beam or a strip foundation, to drag (a joined corner moves with it). */
function pathGrips(node: NodeDto): Grip[] {
  const a = node.props.start as number[] | undefined, b = node.props.end as number[] | undefined;
  if (!a || !b) return [];
  return [
    { key: `${node.id}:start`, at: { x: a[0], y: a[1] }, move: (p) => execute('structure.updatePath', { id: node.id, x1: p.x, y1: p.y }) },
    { key: `${node.id}:end`, at: { x: b[0], y: b[1] }, move: (p) => execute('structure.updatePath', { id: node.id, x2: p.x, y2: p.y }) },
  ];
}
function pointGrip(node: NodeDto): Grip[] {
  const at = node.props.at as number[] | undefined;
  return at ? [{ key: `${node.id}:at`, at: { x: at[0], y: at[1] }, move: (p) => execute('structure.updateColumn', { id: node.id, x: p.x, y: p.y }) }] : [];
}

const structure: LapinModule = {
  id: 'structure',
  name: 'Structure',
  summary: 'Columns, beams, strip and pad foundations, ring beams, lintels.',
  tabs: [{
    id: 'structure', label: 'Structure', order: 30, include: ['modify'],
    groups: [
      { label: 'Frame', tools: [column, beam], small: [atGrid] },
      { label: 'Foundation', tools: [strip, pad] },
      { label: 'Types', tools: [types] },
    ],
  }],
  hotbar: [column, beam],
  propertySections: [{ id: 'structure', label: 'Structure', classes: ['IfcColumn', 'IfcBeam', 'IfcFooting'], component: StructureSection }],
  dialogs: { structureTypes: StructureTypesDialog },
  grips: { IfcBeam: pathGrips, IfcFooting: (n) => (n.props.start ? pathGrips(n) : []), IfcColumn: pointGrip },
  setType: {
    IfcColumn: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcBeam: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcFooting: (ids, typeId) => execute('structure.setType', { ids, typeId }),
  },
};

export default structure;
