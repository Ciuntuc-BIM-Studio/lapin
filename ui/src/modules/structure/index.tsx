/** Structure: columns, beams, strip and pad foundations (the engine's structure module). */

import { Grid3x3, Layers3 } from 'lucide-react';
import { BeamIcon, ColumnIcon, CurvedBeamIcon, PadFootingIcon, StripFootingIcon } from '../../icons';
import type { Grip, LapinModule, RefLine, ToolDef } from '../../platform/module';
import { flipAcross, pathGrips, pathRef, pt } from '../../viewport/gripKit';
import { arcOf } from '../../viewport/arc';
import { usageOf } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { ensureStorey, execute, useSession } from '../../engine/session';
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
/** A curved beam: its ends, then a point the arc passes through. */
const curvedBeam: ToolDef = {
  id: 'structure.curvedBeam', label: 'Curved beam', icon: CurvedBeamIcon, shortcut: 'BC',
  gesture: { kind: 'threePoints', arc: true },
  complete: async ({ points: [a, b, p] }, ctx) => { await ctx.execute('structure.addBeam', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, x3: p.x, y3: p.y }); },
};
const strip: ToolDef = {
  id: 'structure.strip', label: 'Strip footing', icon: StripFootingIcon, shortcut: 'SF',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('structure.addStripFooting', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y }); },
};
/** A curved strip foundation: its ends, then a point the arc passes through. */
const curvedStrip: ToolDef = {
  id: 'structure.curvedStrip', label: 'Curved strip footing', icon: StripFootingIcon, shortcut: 'SC',
  gesture: { kind: 'threePoints', arc: true },
  complete: async ({ points: [a, b, p] }, ctx) => { await ctx.execute('structure.addStripFooting', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, x3: p.x, y3: p.y }); },
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

/**
 * The ends of a beam or a strip foundation, to drag (a joined corner moves
 * with it), a curved beam's arc, and a flip arrow across it: a beam's section
 * mirrored, a strip foundation's layers to the other face.
 */
function memberGrips(node: NodeDto): Grip[] {
  const c = arcOf(node.props);
  if (!c) return [];
  const beam = node.type === 'IfcBeam';
  return [
    ...pathGrips(c, (s, e, rise) => ({ command: 'structure.updatePath', args: { id: node.id, x1: s.x, y1: s.y, x2: e.x, y2: e.y, arc: rise, detachJoinedEnds: true } })),
    flipAcross(c, (beam ? node.props.flipped : usageOf(useSession.getState(), node.id)?.negative) ? 1 : -1, beam ? 'Flip: mirror the section across the axis' : 'Flip: swap the foundation\'s faces', () => ({ command: 'structure.flip', args: { id: node.id } })),
  ];
}
const pathRefs = (node: NodeDto): RefLine[] => { const c = arcOf(node.props); return c ? [pathRef(c)] : []; };
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
      { label: 'Frame', tools: [column, beam], small: [curvedBeam, atGrid] },
      { label: 'Foundation', tools: [strip, pad], small: [curvedStrip] },
      { label: 'Types', tools: [types] },
    ],
  }],
  hotbar: [column, beam],
  propertySections: [{ id: 'structure', label: 'Structure', classes: ['IfcColumn', 'IfcBeam', 'IfcFooting'], component: StructureSection }],
  dialogs: { structureTypes: StructureTypesDialog },
  grips: { IfcBeam: memberGrips, IfcFooting: (n) => (n.props.start ? memberGrips(n) : []), IfcColumn: pointGrip },
  references: { IfcBeam: pathRefs, IfcFooting: (n) => (n.props.start ? pathRefs(n) : []) },
  setType: {
    IfcColumn: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcBeam: (ids, typeId) => execute('structure.setType', { ids, typeId }),
    IfcFooting: (ids, typeId) => execute('structure.setType', { ids, typeId }),
  },
};

export default structure;
