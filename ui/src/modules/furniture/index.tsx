/**
 * Furniture (docs/plan-furniture.md): the Furniture tab, a tool per kind — sofas, a corner sofa, armchairs, chairs,
 * beds, bedside tables, wardrobes, chests of drawers, bookcases, sideboards, tables, coffee tables, desks — each placing
 * its kind's chosen type at a click, backed onto the nearest wall's face (Against walls in the command line's bar) or
 * turned as the bar says; the straight kitchen drawn along a wall; chairs round a table; the types dialog; the two
 * schedules. A piece's Properties show its type, turn, height and a kitchen's length.
 */

import {
  Archive, Armchair, BedDouble, BedSingle, Coffee, CookingPot, LampDesk, Layers3, Library, ListOrdered, PanelsTopLeft, Settings2, Shirt, Sofa, Table2, Users, UtensilsCrossed,
} from 'lucide-react';
import type { Icon } from '../../icons';
import type { LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { relatingOf } from '../../engine/model';
import { CLASS, chooseType, chosenType, furnitureGrips, sizeOf, specOf, typesOfKind, useFurniturePrefs } from './furniture';
import { FurnitureSection } from './FurnitureSection';
import { FurnitureTypesDialog, editType, useKinds } from './FurnitureTypesDialog';

/** The bar of a kind's tool: its type, its turn when it stands free, and whether it backs onto walls. */
function kindOptions(kind: string, placing = true) {
  return function FurnitureOptions() {
    const nodes = useSession((s) => s.nodes);
    const prefs = useFurniturePrefs();
    const types = typesOfKind(nodes, kind);
    const current = prefs.types[kind] && types.some((t) => t.id === prefs.types[kind]) ? prefs.types[kind] : types[0]?.id ?? '';
    return (
      <>
        <span className="cad-layer on">
          <select value={current} data-testid={`furniture-type-${kind}`} title="The type it puts down" onChange={(e) => chooseType(kind, e.target.value)}>
            {!types.length && <option value="">(its first type is made)</option>}
            {types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
        </span>
        {placing && <>
          <span className="cad-layer">
            <select value={prefs.rotation} data-testid="furniture-rotation" title="Its turn where it stands free (against a wall it faces the room)" onChange={(e) => useFurniturePrefs.setState({ rotation: Number(e.target.value) })}>
              {[0, 90, 180, 270].map((a) => <option key={a} value={a}>{a}°</option>)}
            </select>
          </span>
          <button type="button" className={`cad-toggle${prefs.against ? ' on' : ''}`} data-testid="furniture-against" title="Backed onto the nearest wall's face, its front to the room"
            onClick={() => useFurniturePrefs.setState({ against: !prefs.against })}>AGAINST WALLS</button>
        </>}
      </>
    );
  };
}

/** Properties while a kind's tool is on: the type it puts down, its size, and the types dialog. */
function kindProperties(kind: string) {
  return function FurnitureNew() {
    const nodes = useSession((s) => s.nodes);
    const kinds = useKinds();
    const prefs = useFurniturePrefs();
    const types = typesOfKind(nodes, kind);
    const current = types.find((t) => t.id === prefs.types[kind]) ?? types[0];
    return (
      <div className="pad new-element" data-field="new-element">
        <label className="field-line"><span className="eyebrow">Type</span>
          {types.length
            ? <select value={current?.id ?? ''} onChange={(e) => chooseType(kind, e.target.value)}>{types.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}</select>
            : <span className="muted small">The first one is made as you place it.</span>}
        </label>
        {current && <p className="small" data-no-i18n>{sizeOf(current, kinds)}</p>}
        <button type="button" className="btn small" onClick={() => editType(current)}><Settings2 size={13} /> Edit types…</button>
      </div>
    );
  };
}

const typeArg = (kind: string) => {
  const id = chosenType(kind);
  return id && useSession.getState().nodes.has(id) ? { typeId: id } : { kind };
};

/** A kind's tool: one piece at a click. */
function piece(kind: string, label: string, icon: Icon, shortcut?: string): ToolDef {
  return {
    id: `furniture.${kind}`, label, icon, shortcut,
    gesture: { kind: 'point' },
    options: kindOptions(kind),
    properties: kindProperties(kind),
    complete: async ({ points: [p] }, ctx) => {
      const { rotation, against } = useFurniturePrefs.getState();
      await ctx.execute('furniture.place', { storeyId: ctx.storeyId, ...typeArg(kind), x: p.x, y: p.y, rotation, against });
    },
  };
}

const sofa = piece('sofa', 'Sofa', Sofa, 'SO');
const cornerSofa = piece('cornerSofa', 'Corner sofa', Sofa);
const armchair = piece('armchair', 'Armchair', Armchair, 'AC');
const chair = piece('chair', 'Chair', Armchair);
const bed = piece('bed', 'Bed', BedDouble, 'BD');
const bedside = piece('bedside', 'Bedside table', BedSingle);
const wardrobe = piece('wardrobe', 'Wardrobe', Shirt, 'WD');
const chest = piece('chest', 'Chest of drawers', Archive);
const bookcase = piece('bookcase', 'Bookcase', Library);
const sideboard = piece('sideboard', 'Sideboard', PanelsTopLeft);
const table = piece('table', 'Table', UtensilsCrossed, 'TB');
const coffeeTable = piece('coffeeTable', 'Coffee table', Coffee);
const desk = piece('desk', 'Desk', LampDesk);

/** A kitchen of any layout: two points along a wall, its back on the wall's face, as long as the line (an L or a U tucked into the corner it is drawn near). */
const kitchen: ToolDef = {
  id: 'furniture.kitchen', label: 'Kitchen', icon: CookingPot, shortcut: 'KT',
  gesture: { kind: 'twoPoints' },
  options: kindOptions('kitchen', false),
  properties: kindProperties('kitchen'),
  complete: async ({ points: [a, b] }, ctx) => {
    const t = chosenType('kitchen');
    await ctx.execute('furniture.kitchen', { storeyId: ctx.storeyId, ...(t && useSession.getState().nodes.has(t) ? { typeId: t } : {}), x1: a.x, y1: a.y, x2: b.x, y2: b.y });
  },
};

/** Chairs round a table: the table picked, the chair type of the Chair tool. */
const chairsAround: ToolDef = {
  id: 'furniture.chairsAround', label: 'Chairs round a table', icon: Users,
  gesture: { kind: 'pick', types: [CLASS] },
  options: kindOptions('chair', false),
  complete: async ({ entity }, ctx) => {
    if (!entity) return;
    const { nodes, relations } = useSession.getState();
    const kind = String(specOf(relatingOf({ nodes, relations }, entity.id, 'IfcRelDefinesByType') ?? undefined).kind ?? '');
    if (kind !== 'table' && kind !== 'desk') { notify('Pick a table or a desk.'); return; }
    const t = chosenType('chair');
    const r = (await ctx.execute('furniture.chairsAround', { tableId: entity.id, ...(t && nodes.has(t) ? { typeId: t } : {}) })) as { ids?: string[] } | null;
    if (r?.ids?.length) useUi.setState({ selected: r.ids, selection: r.ids[0] });
  },
};

const types: ToolDef = { id: 'furniture.types', label: 'Furniture types', icon: Layers3, run: () => editType(useFurniturePrefs.getState().types.sofa) };

const schedule = (id: string, label: string, kind: string, icon: Icon): ToolDef => ({
  id, label, icon,
  run: () => void execute('architecture.createSchedule', { kind }).then((r) => { if (r) notify(`${label} made: it is in the project browser, under Schedules.`); }),
});
const byType = schedule('furniture.schedule', 'Furniture schedule', 'furniture', ListOrdered);
const byRoom = schedule('furniture.byRoom', 'Furniture by room', 'furnitureRooms', Table2);
const kitchenUnits = schedule('furniture.kitchenUnits', 'Kitchen units', 'kitchenUnits', CookingPot);

const furniture: LapinModule = {
  id: 'furniture',
  name: 'Furniture',
  summary: 'Sofas, corner sofas, armchairs, chairs, beds, wardrobes, chests, bookcases, sideboards, tables, desks and kitchens in six layouts, parametric types placed against walls, with their plan symbols, schedules and IFC.',
  tabs: [{
    // On the Interior tab with the finishes (decision 367).
    id: 'interior', label: 'Interior', order: 25, include: ['modify'], i18nCtx: 'furniture',
    groups: [
      { label: 'Seating', tools: [sofa, armchair], small: [cornerSofa, chair] },
      { label: 'Beds', tools: [bed], small: [bedside] },
      { label: 'Storage', tools: [wardrobe], small: [chest, bookcase, sideboard] },
      { label: 'Tables', tools: [table], small: [coffeeTable, desk, chairsAround] },
      { label: 'Kitchen', tools: [kitchen] },
      { label: 'Schedules', tools: [byType], small: [byRoom, kitchenUnits] },
      { label: 'Types', tools: [types] },
    ],
  }],
  propertySections: [
    { id: 'furniture', label: 'Furniture', classes: [CLASS], component: FurnitureSection, replacesFields: () => true },
  ],
  dialogs: { furnitureTypes: FurnitureTypesDialog },
  grips: {
    [CLASS]: (node: NodeDto) => {
      const { nodes, relations } = useSession.getState();
      const spec = specOf(relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined);
      return furnitureGrips(node, Number(spec.depth ?? 0.8));
    },
  },
  setType: { [CLASS]: (ids, typeId) => execute('furniture.setType', { ids, typeId }) },
};

export default furniture;
