/** Structure: columns, beams, strip and pad foundations (the engine's structure module). */

import { Grid3x3, Layers3 } from 'lucide-react';
import { BeamIcon, ColumnIcon, PadFootingIcon, StripFootingIcon } from '../../icons';
import type { Grip, LapinModule, RefLine, ToolDef } from '../../platform/module';
import { axisGrips, axisRef, pt } from '../../viewport/gripKit';
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
  const a = pt(node.props.start), b = pt(node.props.end);
  if (!a || !b) return [];
  return axisGrips(a, b, (s, e) => ({ command: 'structure.updatePath', args: { id: node.id, x1: s.x, y1: s.y, x2: e.x, y2: e.y, detachJoinedEnds: true } }));
}
const pathRefs = (node: NodeDto): RefLine[] => { const a = pt(node.props.start), b = pt(node.props.end); return a && b ? [axisRef(a, b)] : []; };
/** A column: its point, and a handle 0.8 m out along its rotation that turns it. */
function pointGrip(node: NodeDto): Grip[] {
  const at = pt(node.props.at);
  if (!at) return [];
  const rot = (Number(node.props.rotation ?? 0) * Math.PI) / 180;
  const handle = { x: at.x + Math.cos(rot) * 0.8, y: at.y + Math.sin(rot) * 0.8 };
  return [
    { key: 'at', kind: 'centre', at, step: (p) => ({ command: 'structure.updateColumn', args: { id: node.id, x: p.x, y: p.y } }) },
    { key: 'rotate', kind: 'rotate', at: handle, step: (p) => ({ command: 'structure.updateColumn', args: { id: node.id, rotation: Math.round(((Math.atan2(p.y - at.y, p.x - at.x) * 180) / Math.PI) * 100) / 100 } }) },
  ];
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
  references: { IfcBeam: pathRefs, IfcFooting: (n) => (n.props.start ? pathRefs(n) : []) },
  setType: {
    IfcColumn: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcBeam: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcFooting: (ids, typeId) => execute('structure.setType', { ids, typeId }),
  },
};

export default structure;
