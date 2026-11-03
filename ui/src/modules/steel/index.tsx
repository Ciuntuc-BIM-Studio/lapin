/**
 * Steel (docs/plan-steel.md, decisions 268, 270): members with configurable ends, and portal frame halls. The Steel tab
 * places a member along two points (on the storey's top, as a beam, with the type chosen), converts drafted closed
 * outlines into members (two long parallel edges: the axis and the width; the edges across the ends: the cuts; a notch on
 * a section: a cope), mitres two members or fits one to another, edits the steel types (the catalogue's profiles, hollow
 * and cold-formed sections of one's own) as member, beam or column types, and opens the hall configurator (a new hall, or
 * the hall of the member selected). The properties panel shows a member's ends and haunches.
 */

import { ArrowRightToLine, Layers3, Slash, WandSparkles, Warehouse } from 'lucide-react';
import { BeamIcon } from '../../icons';
import type { Grip, LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { newElement, typedBy } from '../architecture/NewElement';
import { SteelSection } from './SteelSection';
import { HallDialog, useHall } from './HallDialog';
import { hallOf } from './hall';
import { SectionGlyph, SteelTypesDialog } from './SteelTypesDialog';
import { kgPerMetre, sectionLabel, type SectionDto } from './sections';

const STEEL = ['LapinSteelMember', 'IfcBeam', 'IfcColumn'];

/** A member type in the catalog: its section drawn, its name and weight. */
const ProfileThumb = ({ type }: { type: NodeDto }) => <SectionGlyph section={type.props.section as SectionDto | undefined} size={56} />;

const member: ToolDef = {
  id: 'steel.member', label: 'Member', icon: BeamIcon, shortcut: 'SM',
  gesture: { kind: 'twoPoints', repeat: true },
  properties: newElement('LapinSteelMemberType', 'memberTypeId', 'steelTypes'),
  typed: {
    ...typedBy('LapinSteelMemberType', 'memberTypeId', 'steelTypes', ProfileThumb),
    about: (t) => { const s = t.props.section as SectionDto | undefined; return s ? `${sectionLabel(s)} · ${kgPerMetre(s).toFixed(1)} kg/m` : null; },
  },
  complete: async ({ points: [a, b] }, ctx) => {
    await ctx.execute('steel.addMember', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, typeId: useUi.getState().memberTypeId ?? undefined });
  },
};

/** The selected drafted closed outlines made members, in one step; what is not a band is left and said. */
const convert: ToolDef = {
  id: 'steel.convert', label: 'Convert to member', icon: WandSparkles,
  run: () => {
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((id) => { const n = nodes.get(id); const s = n?.props.shape as { type?: string; closed?: boolean } | undefined; return n?.type === 'IfcAnnotation' && s?.type === 'polyline' && s.closed; });
    if (!ids.length) { notify('Select the closed outlines to convert: drafted polylines or rectangles, on a plan, a section or an elevation.'); return; }
    void execute('steel.convertSketch', { ids }).then((r) => {
      const res = r as { ids?: string[]; skipped?: { id: string; problem: string }[] } | null;
      if (!res?.ids) return;
      useUi.setState({ selected: res.ids, selection: res.ids[0] ?? null });
      if (res.skipped?.length) notify('Some outlines are not bands (two long parallel edges, an edge across each end) and were left as they are.');
    });
  },
};

const pair = (id: string, label: string, icon: ToolDef['icon'], command: string, ask: string): ToolDef => ({
  id, label, icon,
  run: () => {
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((x) => STEEL.includes(nodes.get(x)?.type ?? ''));
    if (ids.length !== 2) { notify(ask); return; }
    void execute(command, { ids });
  },
});
const mitre = pair('steel.mitre', 'Mitre', Slash, 'steel.mitre', 'Select the two members to mitre.');
const fit = pair('steel.fit', 'Fit', ArrowRightToLine, 'steel.fit', 'Select the member to fit, then the one it is fitted to.');
const types: ToolDef = { id: 'steel.types', label: 'Steel types', icon: Layers3, run: () => useUi.setState({ dialog: 'steelTypes' }) };

/** The hall configurator: on the hall of the member selected, else a new hall. */
export function openHall(id?: string | null) {
  const { nodes, relations } = useSession.getState();
  const hall = hallOf({ nodes, relations }, id ?? useUi.getState().selection);
  useHall.setState({ id: hall?.id ?? null });
  useUi.setState({ dialog: 'steelHall' });
}
const hall: ToolDef = { id: 'steel.hall', label: 'Portal frame hall…', icon: Warehouse, run: () => openHall() };

/** A member's two ends, to drag in plan; their heights stay. */
function memberGrips(node: NodeDto): Grip[] {
  const p = (v: unknown) => (Array.isArray(v) && v.length >= 2 ? { x: Number(v[0]), y: Number(v[1]) } : null);
  const [s, e] = [p(node.props.start), p(node.props.end)];
  if (!s || !e) return [];
  return [
    { key: 'start', kind: 'end', at: s, step: (to) => ({ command: 'steel.updateMember', args: { id: node.id, x1: to.x, y1: to.y } }) },
    { key: 'end', kind: 'end', at: e, step: (to) => ({ command: 'steel.updateMember', args: { id: node.id, x2: to.x, y2: to.y } }) },
  ];
}

const steel: LapinModule = {
  id: 'steel',
  name: 'Steel',
  summary: 'Steel members with configurable ends (extension, cuts by a plane, mitres, fits, copes), made from a drafted outline too; hollow and cold-formed sections; portal frame halls with haunches.',
  tabs: [{
    id: 'steel', label: 'Steel', order: 34, include: ['modify'],
    groups: [
      { label: 'Halls', tools: [hall] },
      { label: 'Members', tools: [member, convert] },
      { label: 'Ends', tools: [mitre, fit] },
      { label: 'Types', tools: [types] },
    ],
  }],
  propertySections: [{ id: 'steel', label: 'Steel', classes: STEEL, component: SteelSection }],
  dialogs: { steelTypes: SteelTypesDialog, steelHall: HallDialog },
  grips: { LapinSteelMember: memberGrips },
  setType: { LapinSteelMember: (ids, typeId) => execute('steel.setType', { ids, typeId }) },
};

export default steel;
