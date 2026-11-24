/**
 * Furniture (docs/plan-furniture.md): the Furniture tab, a tool per kind — sofas, a corner sofa, armchairs, chairs,
 * beds, bedside tables, wardrobes, chests of drawers, bookcases, sideboards, tables, coffee tables, desks — each placing
 * its kind's chosen type at a click, backed onto the nearest wall's face (Against walls in the command line's bar) or
 * turned as the bar says; the straight kitchen drawn along a wall; chairs round a table; the types dialog; the two
 * schedules; From a model puts down a CC0 model stretched by zones (decision 477). A piece's Properties show its type, turn, height and a kitchen's length.
 */

import {
  Archive, Armchair, Bath, BookHeart, Package, Ruler, BedDouble, BedSingle, Coffee, CookingPot, Droplet, Grip, LampDesk, Layers3, Library, ListOrdered, PanelsTopLeft, RectangleVertical, Settings2, Shapes, Shirt, ShowerHead, Sofa, Table2, Toilet, Users, UtensilsCrossed,
} from 'lucide-react';
import type { Icon } from '../../icons';
import type { LapinModule, PlaceEntry, ToolDef } from '../../platform/module';
import { openPlaceCatalog } from '../../shell/placeCatalogStore';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { relatingOf } from '../../engine/model';
import { CLASS, TYPE_CLASS, chooseType, chosenType, furnitureGrips, sizeOf, specOf, typesOfKind, useFurniturePrefs } from './furniture';
import { FurnitureSection } from './FurnitureSection';
import { FurnitureTypesDialog, editType, useKinds } from './FurnitureTypesDialog';
import { UserModelsDialog } from './UserModelsDialog';
import { FurnitureLibraryDialog, SaveToLibraryDialog } from './FurnitureLibraryDialog';
import { refreshLibrary, takeEntry, useFurnitureLibrary } from './library';
import { addTemplateAddition } from '../../engine/templates';
import { useEffect } from 'react';
import { activate, findTool } from '../../platform/modules';
import { toolForKind, usePlaceKind } from './placeKind';
// Lapin Extensions: the content packs this module's libraries take (docs/plan-extensions.md).
import './extensionKind';
import { createScheduleOf } from '../../shell/fieldSchedule';
import { useModelsCatalog } from './ModelEditor';
import { thumbnailUrl, type FurnitureModelInfo } from './models';
import { t, useLang } from '../../i18n';

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

/** The kinds of piece the Place tool puts down, by category: the old per-kind tools (their ids and letters) are these, kept. */
const KINDS: { category: string; kind: string; label: string; icon: Icon; shortcut?: string }[] = [
  { category: 'Seating', kind: 'sofa', label: 'Sofa', icon: Sofa, shortcut: 'SO' },
  { category: 'Seating', kind: 'cornerSofa', label: 'Corner sofa', icon: Sofa },
  { category: 'Seating', kind: 'armchair', label: 'Armchair', icon: Armchair, shortcut: 'AC' },
  { category: 'Seating', kind: 'chair', label: 'Chair', icon: Armchair },
  { category: 'Beds', kind: 'bed', label: 'Bed', icon: BedDouble, shortcut: 'BD' },
  { category: 'Beds', kind: 'bedside', label: 'Bedside table', icon: BedSingle },
  // Casework of boards (decision 475): carcasses side by side, configured in Furniture types from a preset.
  { category: 'Storage', kind: 'wardrobe', label: 'Wardrobe', icon: Shirt, shortcut: 'WD' },
  { category: 'Storage', kind: 'casework', label: 'Casework', icon: Package },
  { category: 'Storage', kind: 'chest', label: 'Chest of drawers', icon: Archive },
  { category: 'Storage', kind: 'bookcase', label: 'Bookcase', icon: Library },
  { category: 'Storage', kind: 'sideboard', label: 'Sideboard', icon: PanelsTopLeft },
  { category: 'Tables', kind: 'table', label: 'Table', icon: UtensilsCrossed, shortcut: 'TB' },
  { category: 'Tables', kind: 'coffeeTable', label: 'Coffee table', icon: Coffee },
  { category: 'Tables', kind: 'desk', label: 'Desk', icon: LampDesk },
  // Furniture from a CC0 model stretched by zones (decision 477): sofas, chairs, tables, lamps, plants, rugs; the model and its size in Furniture types.
  { category: 'Models', kind: 'model', label: 'From a model', icon: Shapes },
  // Sanitary fixtures (forum topic #245): written to IFC as IfcSanitaryTerminal with their connection points; the accessories as furniture.
  { category: 'Sanitary', kind: 'toilet', label: 'WC', icon: Toilet },
  { category: 'Sanitary', kind: 'bidet', label: 'Bidet', icon: Toilet },
  { category: 'Sanitary', kind: 'washbasin', label: 'Wash basin', icon: Droplet },
  { category: 'Sanitary', kind: 'vanity', label: 'Vanity unit', icon: RectangleVertical },
  { category: 'Sanitary', kind: 'shower', label: 'Shower', icon: ShowerHead },
  { category: 'Sanitary', kind: 'bath', label: 'Bath', icon: Bath },
  { category: 'Sanitary', kind: 'kitchenSink', label: 'Kitchen sink', icon: CookingPot },
  { category: 'Sanitary', kind: 'bathAccessory', label: 'Bathroom accessory', icon: Grip },
];
const SANITARY = KINDS.filter((k) => k.category === 'Sanitary').map((k) => k.kind);
const CATEGORIES = [...new Set(KINDS.map((k) => k.category))];

/** The bar's and Properties' pieces for a kind, made once each (a component made again would start over). */
const optionsOf = new Map<string, ReturnType<typeof kindOptions>>();
const propertiesOf = new Map<string, ReturnType<typeof kindProperties>>();
const forKind = <T,>(map: Map<string, T>, kind: string, make: (k: string) => T): T => { let c = map.get(kind); if (!c) map.set(kind, c = make(kind)); return c; };

/** The category and kind to place, in the bar and in Properties while the tool is on. */
function KindPicker() {
  const kind = usePlaceKind((s) => s.kind);
  return (
    <span className="cad-layer on">
      <select value={kind} data-testid="furniture-kind" title="What it places: seating, beds, storage, tables, models" onChange={(e) => usePlaceKind.setState({ kind: e.target.value })}>
        {CATEGORIES.map((c) => <optgroup key={c} label={c}>{KINDS.filter((k) => k.category === c).map((k) => <option key={k.kind} value={k.kind}>{k.label}</option>)}</optgroup>)}
      </select>
    </span>
  );
}
function PlaceOptions() { const Kind = forKind(optionsOf, usePlaceKind((s) => s.kind), kindOptions); return <><KindPicker /><Kind /></>; }
function PlaceProperties() {
  const Kind = forKind(propertiesOf, usePlaceKind((s) => s.kind), kindProperties);
  return <><div className="pad new-element"><label className="field-line"><span className="eyebrow">Place</span><KindPicker /></label></div><Kind /></>;
}

/** One tool places every piece of furniture (S8, audit 2026-11-15): the kind is chosen in the bar, one piece at a click. */
const place: ToolDef = {
  id: 'furniture.place', label: 'Place furniture', icon: Sofa,
  gesture: { kind: 'point' },
  options: PlaceOptions,
  properties: PlaceProperties,
  complete: async ({ points: [p] }, ctx) => {
    const { rotation, against } = useFurniturePrefs.getState();
    await ctx.execute('furniture.place', { storeyId: ctx.storeyId, ...typeArg(usePlaceKind.getState().kind), x: p.x, y: p.y, rotation, against });
  },
};

/** A kind's old tool: the Place tool, set to that kind (the id and the letters stay). */
const piece = (k: (typeof KINDS)[number]): ToolDef => ({
  id: `furniture.${k.kind}`, label: k.label, icon: k.icon, shortcut: k.shortcut,
  run: () => { usePlaceKind.setState({ kind: k.kind }); if (useUi.getState().tool !== place.id) activate(place); },
});
const pieces = KINDS.map(piece);

/** Sanitary fixtures (forum topic #245): the Place tool on the sanitary kind last placed, a WC the first time; the kind is changed in the bar. */
const sanitary: ToolDef = {
  id: 'furniture.sanitary', label: 'Sanitary fixture', icon: Bath,
  run: () => {
    const now = usePlaceKind.getState().kind;
    usePlaceKind.setState({ kind: SANITARY.includes(now) ? now : 'toilet' });
    if (useUi.getState().tool !== place.id) activate(place);
  },
};

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

/** My furniture library (decision 476): the user's own furniture types, for every project. */
// The catalogue on the furniture (S15); its My furniture… button opens the library's editor.
const library: ToolDef = { id: 'furniture.library', label: 'My furniture…', icon: BookHeart, run: () => openPlaceCatalog('furniture') };

// Save as template… may put the whole library into the template (decision 476).
addTemplateAddition({
  id: 'furniture', label: 'Add every type in My furniture',
  shown: () => useFurnitureLibrary.getState().entries.length > 0,
  params: async () => ({ furniture: (await refreshLibrary()).map((e) => e.type) }),
});

/** The library read once the engine is there, so Save as template… knows whether to offer it. */
function LibraryWatch() {
  const connected = useSession((s) => s.connected);
  useEffect(() => { if (connected) void refreshLibrary(); }, [connected]);
  return null;
}

const types: ToolDef = { id: 'furniture.types', label: 'Furniture types', icon: Layers3, run: () => editType(useFurniturePrefs.getState().types.sofa) };

const schedule = (id: string, label: string, kind: string, icon: Icon): ToolDef => ({
  id, label, icon,
  run: () => void createScheduleOf(kind).then((r) => { if (r) notify(`${label} made: it is in the project browser, under Schedules.`); }),
});
const byType = schedule('furniture.schedule', 'Furniture schedule', 'furniture', ListOrdered);
const byRoom = schedule('furniture.byRoom', 'Furniture by room', 'furnitureRooms', Table2);
const kitchenUnits = schedule('furniture.kitchenUnits', 'Kitchen units', 'kitchenUnits', CookingPot);
const cutList = schedule('furniture.cutList', 'Cut list', 'cutList', Ruler);

/**
 * The Place catalogue's furniture (S15): the project's types, this computer's library (My furniture), and Lapin's kinds, each a
 * click from the tool that puts it down; My furniture… stays the library's editor.
 */
function useFurnitureEntries(): PlaceEntry[] {
  const nodes = useSession((s) => s.nodes);
  const { entries, read } = useFurnitureLibrary();
  useEffect(() => { if (!read) void refreshLibrary(); }, [read]);
  const start = (kind: string) => { const id = toolForKind(kind); const t = findTool(id); if (t && useUi.getState().tool !== t.id) activate(t); };
  const types = [...nodes.values()].filter((n) => n.type === TYPE_CLASS && specOf(n).kind);
  const models = useModelsCatalog()?.models ?? [];
  useLang((s) => s.lang);
  return [
    ...types.map((t): PlaceEntry => ({ key: `furnitureType:${t.id}`, name: t.name ?? '', group: 'This project', about: KINDS.find((k) => k.kind === specOf(t).kind)?.label ?? String(specOf(t).kind),
      place: () => { const kind = String(specOf(t).kind); chooseType(kind, t.id); start(kind); } })),
    ...entries.map((e): PlaceEntry => ({ key: `furnitureLib:${e.id}`, name: e.name, group: 'This computer', about: e.tags.join(', ') || undefined,
      thumb: e.thumbnail ? <img src={e.thumbnail} alt="" /> : undefined, place: () => takeEntry(e, true) })),
    ...KINDS.map((k): PlaceEntry => ({ key: `furnitureKind:${k.kind}`, name: k.label, group: k.category, thumb: <k.icon size={26} strokeWidth={1.3} />,
      place: () => { usePlaceKind.setState({ kind: k.kind }); if (useUi.getState().tool !== place.id) activate(place); } })),
    // Every model on its own (Lapin's 39 and the project's), by its group: placed through the project's type of it, made the first time.
    ...models.map((m): PlaceEntry => ({ key: `furnitureModel:${m.id}`, name: m.thumbnail ? t(m.name) : m.name, group: t(m.group), about: modelSize(m),
      thumb: m.thumbnail ? <img src={thumbnailUrl(m)} alt="" loading="lazy" width={40} height={40} /> : <Shapes size={26} strokeWidth={1.3} />,
      place: () => placeModel(m) })),
  ];
}

/** A model's size as it starts, in millimetres. */
const modelSize = (m: FurnitureModelInfo) => m.size.map((x) => Math.round(x * 1000)).join(' × ') + ' mm';

/** The Place tool on a model: the project's type of it (the one chosen if it is of this model), else a new one (named by the engine: the model and its size). */
async function placeModel(m: FurnitureModelInfo) {
  const ofModel = [...useSession.getState().nodes.values()].filter((n) => n.type === TYPE_CLASS && specOf(n).kind === 'model' && specOf(n).model === m.id);
  let id: string | undefined = ofModel.find((n) => n.id === chosenType('model'))?.id ?? ofModel[0]?.id;
  if (!id) {
    const r = (await execute('furniture.createType', { kind: 'model', spec: { model: m.id } })) as { id?: string } | null;
    id = r?.id;
    if (!id) return;
  }
  chooseType('model', id);
  usePlaceKind.setState({ kind: 'model' });
  if (useUi.getState().tool !== place.id) activate(place);
}

const furniture: LapinModule = {
  id: 'furniture',
  name: 'Furniture',
  summary: 'Sofas, corner sofas, armchairs, chairs, beds, wardrobes, chests, bookcases, sideboards, tables, desks and kitchens in six layouts, parametric types placed against walls, with their plan symbols, schedules and IFC.',
  // The old tool of each kind (Sofa, Bed…): the Place tool set to it, off the ribbon; their ids and letters work as before.
  legacyTools: pieces,
  placeSources: [{ id: 'furniture', label: 'Furniture', order: 50, useEntries: useFurnitureEntries, manage: { label: 'My furniture…', run: () => useUi.setState({ dialog: 'furnitureLibrary' }) } }],
  tabs: [{
    // On the Interior tab with the finishes (decision 367).
    id: 'interior', label: 'Interior', order: 25, include: ['modify'], i18nCtx: 'furniture',
    groups: [
      { label: 'Furniture', tools: [place], small: [chairsAround] },
      { label: 'Kitchen', tools: [kitchen] },
      { label: 'Sanitary', tools: [sanitary] },
      { label: 'Schedules', tools: [byType], small: [byRoom, kitchenUnits, cutList] },
      { label: 'Types', tools: [types], small: [library] },
    ],
  }],
  propertySections: [
    { id: 'furniture', label: 'Furniture', classes: [CLASS], component: FurnitureSection, replacesFields: () => true },
  ],
  dialogs: { furnitureModels: UserModelsDialog, furnitureTypes: FurnitureTypesDialog, furnitureLibrary: FurnitureLibraryDialog, furnitureSaveToLibrary: SaveToLibraryDialog },
  background: LibraryWatch,
  grips: {
    [CLASS]: (node: NodeDto) => {
      const { nodes, relations } = useSession.getState();
      const spec = specOf(relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType') ?? undefined);
      return furnitureGrips(node, Number(spec.depth ?? 0.8));
    },
  },
  // Create similar: the Place tool set to the piece's kind and type (a kitchen: the Kitchen tool with its type).
  similar: {
    [CLASS]: (n) => {
      const type = relatingOf(useSession.getState(), n.id, 'IfcRelDefinesByType') ?? undefined;
      const kind = String(specOf(type).kind ?? '');
      if (!kind) { notify('This piece has no kind of Lapin furniture to place again.'); return; }
      if (type) chooseType(kind, type.id);
      if (kind === 'kitchen') { activate(kitchen); return; }
      usePlaceKind.setState({ kind });
      if (useUi.getState().tool !== place.id) activate(place);
    },
  },
  editType: { IfcFurnitureType: (id) => editType(id) },
  setType: { [CLASS]: (ids, typeId) => execute('furniture.setType', { ids, typeId }) },
};

export default furniture;
