/**
 * Civil and site: the terrain from a survey file, from LandXML or flat; its
 * contours; platforms and ditches levelled or dug into it with their cut and
 * fill; the brush that works the ground; paths, retaining walls and
 * plantings that stand on it (docs/spec-site.md).
 */

import { create } from 'zustand';
import { activate } from '../../platform/modules';
import { promptText } from '../../shell/prompts';
import { BrickWall, Combine, Landmark, TrainFrontTunnel as TunnelIcon, Crop, Hexagon, Layers, MousePointerClick, PenLine, Scissors, Building2, Crosshair, FileDown, FileUp, Fence, Image as ImageIcon, ImagePlus, Map as MapIcon, MapPin, Mountain, MoveVertical, Paintbrush, Route, Shovel, Spline, Sprout, TreeDeciduous, TrendingDown, Waves } from 'lucide-react';
import { PlatformIcon, TerrainIcon } from '../../icons';
import type { Grip, Grip3D, GripView, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { curvedOutlineGrips, holeGrips, outlineGrips, outlineRef, toPairs } from '../../viewport/gripKit';
import { arcsOf, edgeMiddles, fromPairs, insertVertex, moveVertex, removeVertex } from '../../viewport/contour';
import { PlainShapeBar, toggleOutlineArcs } from '../../viewport/outlineShape';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { formatLength, levelUnit, parseLevel, unitName } from '../../engine/unitsStore';
import { SURVEY, downloadLandXml, pickBinary, pickText, pickFiles } from './survey';
import { TerrainSection } from './TerrainSection';
import { FeatureLineSection } from './FeatureLineSection';
import { contourAt, dragContour, featureLine, featureLineGrips, featureLineGrips3d } from './featureLineTools';
import { EarthworkSection, PathSection, PlantingSection, RetainingWallSection, TunnelLiningSection } from './SiteSections';
import { isDitch, setBrush, useBrush, useTerrainEdit, type BrushKind } from './prefs';
import { MAX_POINT_GRIPS, decorateCornerGrips, terrainPlanPointGrips, terrainPointGrips } from './pointGrips';
import { useCamera } from '../../viewport/camera';
import { EntourageSection, entourage, entourageRow, useEntouragePrefs } from './entourage';
import { lazyPart } from '../../platform/lazyPart';
import { HardscapeSection, KerbSection, hardscape, hardscapeGrips, hardscapeGrips3d, hardscapeRefs, hardscapeSlope, similarHardscape, useHardscapeTypeEdit } from './hardscapeTools';
import { drawPortalPath, openPortal, placePortal, placePortalOnRoad } from './portalTools';
import { drawTunnelAxis, openTunnel, placeTunnelOnAxis } from './tunnelTools';

// Fetched the first time each is opened (platform/lazyPart.tsx).
const BrushDialog = lazyPart(() => import('./BrushDialog').then((m) => m.BrushDialog), { title: 'Terrain brush' });
const CorridorDialog = lazyPart(() => import('./CorridorDialog').then((m) => m.CorridorDialog), { title: 'Corridor' });
const HardscapeTypesDialog = lazyPart(() => import('./HardscapeTypesDialog').then((m) => m.HardscapeTypesDialog), { title: 'Hardscape types' });
const EntourageKindsDialog = lazyPart(() => import('./EntourageKindsDialog').then((m) => m.EntourageKindsDialog), { title: 'Entourage kinds' });
const PortalDialog = lazyPart(() => import('./PortalDialog').then((m) => m.PortalDialog), { title: 'Portal or abutment' });
const TunnelDialog = lazyPart(() => import('./TunnelDialog').then((m) => m.TunnelDialog), { title: 'Tunnel lining' });

/** The lowest surveyed elevation, rounded down to the metre: what ±0.00 defaults to. */
function lowest(text: string): number | null {
  let min = Infinity;
  for (const line of text.split('\n')) {
    const t = line.trim();
    if (!t || t.startsWith('#') || t.startsWith('//')) continue;
    // ';' or tabs between the fields: the commas are decimals (a European export).
    const f = (/[;\t]/.test(t) ? t.split(/[;\t]+/).map((x) => x.trim().replace(',', '.')) : t.split(/[\s,;]+/)).map(Number);
    const z = f.length >= 4 && f.slice(0, 4).every(Number.isFinite) ? f[3] : f.length >= 3 && f.slice(0, 3).every(Number.isFinite) ? f[2] : NaN;
    if (Number.isFinite(z)) min = Math.min(min, z);
  }
  return Number.isFinite(min) ? Math.floor(min) : null;
}

/**
 * The datum typed: empty takes the default, a value that does not read is refused (null) rather than replaced by a guess.
 * Both survey imports go through it.
 */
export function datumOf(answer: string, dflt: number): number | null {
  if (answer.trim() === '') return dflt;
  const v = parseLevel(answer);
  return v != null && Number.isFinite(v) ? v : null;
}

const refuseDatum = () => notify('That elevation does not read: type a number in the project\'s unit (empty: the suggested one).');

/** A terrain from a survey file: its points, and the surveyed elevation of the project's ±0.00. */
const terrainFromFile: ToolDef = {
  id: 'civil.terrain', label: 'Terrain from points', icon: TerrainIcon, shortcut: 'TP',
  run: () => void pickText(SURVEY).then(async (file) => {
    if (!file) return;
    const low = lowest(file.text);
    if (low == null) { notify('No points found: one per line, x y z or number northing easting elevation.'); return; }
    const answer = promptText('The surveyed elevation of the project\'s ±0.00 ({unit}):', formatLength(low, levelUnit()), { unit: unitName(levelUnit()) });
    if (answer == null) return;
    const datum = datumOf(answer, low);
    if (datum === null) { refuseDatum(); return; }
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.addTerrain', { text: file.text, name: file.name, datum, ...(storeyId ? { storeyId } : {}) });
    if (r) notify(`Terrain created from ${(r as { points: number }).points} points.`);
  }),
};

/** Terrains from a LandXML file: each surface of it. */
const terrainFromLandXml: ToolDef = {
  id: 'civil.landxml', label: 'Terrain from LandXML', icon: FileUp,
  run: () => void pickText('.xml,.landxml').then(async (file) => {
    if (!file) return;
    const answer = promptText('The surveyed elevation of the project\'s ±0.00 ({unit}):', '0', { unit: unitName(levelUnit()) });
    if (answer == null) return;
    const datum = datumOf(answer, 0);
    if (datum === null) { refuseDatum(); return; }
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.importLandXml', { text: file.text, datum, ...(storeyId ? { storeyId } : {}) }) as { ids: string[] } | null;
    if (r) notify(`${r.ids.length} terrain(s) from ${file.name}.`);
  }),
};

/** Where an import landed, said once: the system and how many elements. */
const landed = (what: string, n: number) => notify(`${n} ${what} imported, in the project's coordinates (Manage → Location shows its system and survey point).`);

/** Survey points from a DXF drawing (POINTs with heights, blocks, heights written beside points, contour lines): a terrain. */
const terrainFromDxf: ToolDef = {
  id: 'civil.dxfPoints', label: 'Points from DXF', icon: MapPin,
  run: () => void pickBinary('.dxf').then(async (file) => {
    if (!file) return;
    // A question, not an edit: asked with query (run as a command it answered "Unknown command", desktop and browser alike).
    const layers = await query<{ layer: string; points: number }[]>('civil.dxfPointLayers', { data: file.data });
    if (!layers) { notify('The drawing could not be read as DXF.'); return; }
    let chosen: string[] | undefined;
    if (layers && layers.length > 1) {
      const answer = promptText('Layers with surveyed points: {layers}.\nWhich to take (comma separated; empty: all)?', '', { layers: layers.map((l) => `${l.layer} (${l.points})`).join(', ') });
      if (answer == null) return;
      chosen = answer.split(',').map((x) => x.trim()).filter(Boolean);
    }
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.importDxfPoints', { data: file.data, name: file.name, ...(chosen?.length ? { layers: chosen } : {}), ...(storeyId ? { storeyId } : {}) }) as { points: number; crs?: string; unknownSystem?: boolean } | null;
    if (r?.unknownSystem) notify("Survey points imported with the file's own numbers: they look like map coordinates, but their system is not one Lapin knows (Stereo 70, Lambert-93, UTM, Web Mercator, WGS 84). Name it in Manage → Location, or convert the drawing first.");
    else if (r) landed('survey points', r.points);
  }),
};

/** GeoJSON: parcels (one polygon alone becomes the parcel), lines and points, converted to the project's coordinates. */
const geoJson: ToolDef = {
  id: 'civil.geojson', label: 'GeoJSON', icon: MapIcon,
  run: () => void pickText('.geojson,.json').then(async (file) => {
    if (!file) return;
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.importGeoJson', { text: file.text, ...(storeyId ? { storeyId } : {}) }) as { ids: string[] } | null;
    if (r) landed('feature(s)', r.ids.length);
  }),
};

/** CityJSON: the neighbouring buildings with their surfaces, at their place and height. */
const cityJson: ToolDef = {
  id: 'civil.cityjson', label: 'CityJSON', icon: Building2,
  run: () => void pickText('.json,.city.json').then(async (file) => {
    if (!file) return;
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.importCityJson', { text: file.text, ...(storeyId ? { storeyId } : {}) }) as { ids: string[] } | null;
    if (r) landed('building(s)', r.ids.length);
  }),
};

/** An orthophoto: a JPEG or PNG and its world file (.jgw, .pgw, .wld), picked together. */
const orthophoto: ToolDef = {
  id: 'civil.orthophoto', label: 'Orthophoto', icon: ImageIcon,
  run: () => void pickFiles('.jpg,.jpeg,.png,.jgw,.pgw,.wld,.jpgw,.pngw').then(async (files) => {
    if (!files.length) return;
    const image = files.find((f) => /\.(jpe?g|png)$/i.test(f.file.name));
    const world = files.find((f) => /\.(jgw|pgw|wld|jpgw|pngw)$/i.test(f.file.name));
    if (!image) { notify('Pick the image (JPEG or PNG) and its world file together.'); return; }
    if (!world) { notify('Pick the image together with its world file (.jgw, .pgw or .wld): it says where the image lies.'); return; }
    const data = await toBase64(image.file);
    const mime = /png$/i.test(image.file.name) ? 'image/png' : 'image/jpeg';
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.addOrthophoto', { data, mime, name: image.file.name.replace(/\.[^.]+$/, ''), worldFile: await world.file.text(), ...(storeyId ? { storeyId } : {}) });
    if (r) notify('Orthophoto laid under the site.');
  }),
};

async function toBase64(f: File): Promise<string> {
  const bytes = new Uint8Array(await f.arrayBuffer());
  let s = '';
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

/** Level ground where there is no survey: a rectangle in plan at an elevation. */
const flatTerrain: ToolDef = {
  id: 'civil.flatTerrain', label: 'Flat terrain', icon: Mountain, shortcut: 'TF',
  gesture: { kind: 'rectangle' },
  complete: async ({ points: [a, b] }, ctx) => {
    const answer = promptText('The ground\'s elevation ({unit}, ±0.00 = 0):', '0', { unit: unitName(levelUnit()) });
    if (answer == null) return;
    await ctx.execute('civil.addFlatTerrain', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, elevation: parseLevel(answer) ?? 0 });
  },
};

/** How a terrain drawn on an outline takes its heights: level at a height asked, or on the ground under it (T1). */
const useOutlineHeights = create<{ ground: boolean }>(() => ({ ground: false }));
function OutlineTerrainBar() {
  const ground = useOutlineHeights((s) => s.ground);
  return (
    <>
      <PlainShapeBar />
      <select className={`cad-layer${ground ? ' on' : ''}`} value={ground ? 'ground' : 'level'} data-testid="terrain-outline-heights" title="Level at a height you type, or on the ground of the terrain under it"
        onChange={(e) => useOutlineHeights.setState({ ground: e.target.value === 'ground' })}>
        <option value="level">At a height</option>
        <option value="ground">On the ground</option>
      </select>
    </>
  );
}

/** The ground's elevation asked: a number in the level unit, or empty (`empty` says what that means); undefined when cancelled or unread. */
function askElevation(text: string, initial: string): number | null | undefined {
  const answer = promptText(text, initial, { unit: unitName(levelUnit()) });
  if (answer == null) return undefined;
  if (answer.trim() === '') return null;
  const v = parseLevel(answer);
  if (v == null || !Number.isFinite(v)) { refuseDatum(); return undefined; }
  return v;
}

/**
 * A terrain straight from an outline (T1, docs/plan-site-grading.md; forum topic 230): a polygon whose edges may be arcs
 * (Space), a rectangle or a circle; every vertex of it a point with a grip, level at the height typed or on the ground under it.
 */
const terrainOnOutline: ToolDef = {
  id: 'civil.terrainOutline', label: 'Terrain on outline', icon: Hexagon, shortcut: 'TO',
  gesture: { kind: 'polygon', shapes: true },
  options: OutlineTerrainBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    const ground = useOutlineHeights.getState().ground;
    const z = ground ? null : askElevation('The ground\'s elevation ({unit}, ±0.00 = 0):', '0');
    if (z === undefined) return;
    await ctx.execute('civil.addTerrainOnOutline', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}), ...(ground ? { ground: true } : { elevation: z ?? 0 }) });
  },
};

/** A terrain on the outline of what is picked: a room, a parcel, a terrain's parcel, a platform (at its level), or a closed drafted shape (Convert to…). */
const terrainOnPicked: ToolDef = {
  id: 'civil.terrainPicked', label: 'Terrain on picked outline', icon: MousePointerClick,
  gesture: { kind: 'pick', types: ['IfcSpace', 'LapinSiteFeature', 'IfcGeographicElement', 'IfcEarthworksCut', 'IfcAnnotation'] },
  complete: async ({ entity }, ctx) => {
    if (!entity) return;
    if (entity.type === 'IfcAnnotation') {
      const { convertPicked } = await import('../drafting/convert/convert');
      convertPicked(entity.id, 'terrainOutline');
      return;
    }
    // A platform gives its level; the others ask, empty taking the ground under it.
    const z = entity.type === 'IfcEarthworksCut' ? null : askElevation('The ground\'s elevation ({unit}, ±0.00 = 0; empty: the ground under it):', '');
    if (z === undefined) return;
    const r = await ctx.execute('civil.addTerrainOnOutline', { fromId: entity.id, ...(z == null ? (entity.type === 'IfcEarthworksCut' ? {} : { ground: true }) : { elevation: z }) });
    if (r) notify('A terrain on the outline picked: each of its vertices is a point with a grip.');
  },
};

/** A point's elevation asked (model height, ±0.00 = 0), its present one offered; null when cancelled or unread. */
const askPointElevation = (now: number): number | null => askElevation('The point\'s elevation ({unit}, ±0.00 = 0):', formatLength(now, levelUnit())) ?? null;

/** A spot level at a click: the designed ground's height, written on the plan beside a cross. */
const spot: ToolDef = {
  id: 'civil.spot', label: 'Spot level', icon: Crosshair, shortcut: 'SP',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('civil.addSpot', { x: p.x, y: p.y }); },
};

/** The parcel: the property's boundary on the terrain, drawn dash-dotted with its area; its edges may be arcs (audit S4). */
const parcel: ToolDef = {
  id: 'civil.parcel', label: 'Parcel', icon: Fence, shortcut: 'PC',
  gesture: { kind: 'polygon', shapes: true },
  options: PlainShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('civil.setParcel', { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}) });
  },
};

/** An irregular boundary clipping the terrain's TIN (T1). */
const terrainBoundary: ToolDef = {
  id: 'civil.boundary', label: 'Terrain boundary', icon: Crop,
  gesture: { kind: 'polygon', shapes: true },
  options: PlainShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('civil.setBoundary', { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}) });
  },
};

/** A slope tag between two points on the terrain (T2). */
const slopeTag: ToolDef = {
  id: 'civil.slopeTag', label: 'Slope tag', icon: TrendingDown,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    await ctx.execute('civil.addSlope', { x1: points[0].x, y1: points[0].y, x2: points[1].x, y2: points[1].y });
  },
};

/** A material sub-region on the terrain without modifying elevation (T4). */
const subregion: ToolDef = {
  id: 'civil.subregion', label: 'Subregion', icon: Layers,
  gesture: { kind: 'polygon', shapes: true },
  options: PlainShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('civil.addSubregion', { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}) });
  },
};

/** A terrain split in two along a line drawn across it: the larger half stays the terrain, what lies in the other moves with it (T1). */
const splitTerrain: ToolDef = {
  id: 'civil.splitTerrain', label: 'Split terrain', icon: Scissors,
  gesture: { kind: 'twoPoints' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    await ctx.execute('civil.splitTerrain', { x1: points[0].x, y1: points[0].y, x2: points[1].x, y2: points[1].y });
  },
};

/** The selected terrains as one: the first selected keeps its identity and takes the others' points and platforms (T1). */
const mergeTerrains: ToolDef = {
  id: 'civil.mergeTerrains', label: 'Merge terrains', icon: Combine,
  run: () => {
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((id) => { const n = nodes.get(id); return n?.type === 'IfcGeographicElement' && n.props.predefinedType === 'TERRAIN'; });
    if (ids.length < 2) { notify('Select two terrains or more to merge them.'); return; }
    // Their boundaries are joined into one; when they cannot be (apart, or a gap between them), the engine says so.
    void execute('civil.mergeTerrains', { ids }).then((r) => {
      const problems = (r as { problems?: string[] } | null)?.problems;
      if (problems?.length) notify(problems.join(' '));
    });
  },
};

/** Every terrain of the project as one LandXML file. */
const landXmlOut: ToolDef = { id: 'civil.landxmlExport', label: 'Export LandXML', icon: FileDown, run: () => void downloadLandXml().catch((e) => notify(e instanceof Error ? e.message : String(e))) };

/** A platform: its outline in plan (Enter or the first corner closes it); it is levelled at the ground's height in its middle, changed in the inspector. */
const platform: ToolDef = {
  id: 'civil.platform', label: 'Platform', icon: PlatformIcon, shortcut: 'PD',
  gesture: { kind: 'polygon', shapes: true },
  options: PlainShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('civil.addPlatform', { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}) });
  },
};

/** A hole in a platform (audit S4): its outline drawn inside it; the ground inside the hole keeps its own levels. */
const platformHole: ToolDef = {
  id: 'civil.platformHole', label: 'Hole in platform', icon: PlatformIcon,
  gesture: { kind: 'polygon', shapes: true },
  options: PlainShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('civil.addOutlineHole', { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}) });
  },
};

/** A ditch along a polyline (Enter finishes), half a metre under the ground; its section is set in the inspector. */
const ditch: ToolDef = {
  id: 'civil.ditch', label: 'Ditch', icon: Waves, shortcut: 'DH',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addDitch', { points: points.map((p) => [p.x, p.y]) }); },
};

/** A brush tool: every click one stroke on the terrain under it, with the brush's settings. */
const sculpt = (kind: BrushKind, label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `civil.sculpt.${kind}`, label, icon, shortcut,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    setBrush({ kind });
    const b = useBrush.getState();
    await ctx.execute('civil.sculpt', { kind, x: p.x, y: p.y, radius: b.radius, amount: b.amount, strength: b.strength, ...(b.level != null ? { level: b.level } : {}) });
  },
});
const brush: ToolDef = { id: 'civil.brush', label: 'Brush…', icon: Paintbrush, run: () => useUi.setState({ dialog: 'brush' }) };

/** Which of the two the Path tool lays: a path (1,5 m wide) or a road (S3: Path and Road were one tool with two templates). */
const usePathKind = create<{ kind: 'path' | 'road' }>(() => ({ kind: 'path' }));
function PathKindOption() {
  const kind = usePathKind((s) => s.kind);
  return (
    <select className={`cad-layer${kind === 'road' ? ' on' : ''}`} value={kind} data-testid="path-kind" title="A path, 1,5 m wide, or a road: two 3 m lanes on a 2,5 % crown, kerbs and 1,5 m sidewalks"
      onChange={(e) => usePathKind.setState({ kind: e.target.value as 'path' | 'road' })}>
      <option value="path">Path</option>
      <option value="road">Road</option>
    </select>
  );
}
/** A path or a road along a polyline (the choice in the bar): a path is 1,5 m wide in the ground, its courses and kerbs set in the inspector; a road has two 3 m lanes on a 2,5 % crown, kerbs and 1,5 m sidewalks, the configurator sets the rest. */
const path: ToolDef = {
  id: 'civil.path', label: 'Path', icon: Route, shortcut: 'PA',
  gesture: { kind: 'polyline' },
  options: PathKindOption,
  complete: async ({ points }, ctx) => {
    const pts = points.map((p) => [p.x, p.y]);
    if (usePathKind.getState().kind === 'path') { await ctx.execute('civil.addPath', { storeyId: ctx.storeyId, points: pts }); return; }
    const side = { kerb: true, sidewalk: true, kerbWidth: 0.15, kerbHeight: 0.12, sidewalkWidth: 1.5 };
    await ctx.execute('civil.addPath', { storeyId: ctx.storeyId, points: pts, name: 'Road', template: 'road', laneWidth: 3, crossfall: 2.5, fall: 'crown', surfaceThickness: 0.08, baseThickness: 0.3, left: side, right: side });
  },
};
/** The old Road tool: the Path tool, set to Road. */
const road: ToolDef = { id: 'civil.road', label: 'Road', icon: Route, shortcut: 'RD', run: () => { usePathKind.setState({ kind: 'road' }); if (useUi.getState().tool !== path.id) activate(path); } };

/** The corridor configurator of the path, road, retaining wall or ditch selected. */
/**
 * CAD linework (a line drafted, or a DXF's, with the heights it brought) made a civil element: the line picked is followed
 * through what meets it end to end on its layer; its heights give the profile or the breakline's levels. A dense line
 * (a surveyed axis) is read as a design alignment, a drawn one gives its vertices as PIs. A shortcut to Draft → Convert to…
 * (audit S12), opened on the line picked and the target, where the chain it joins shows before converting; a kind a module
 * adds of its own (a track) still goes straight to civil.fromLinework.
 */
export function fromLinework(id: string, label: string, as: string, shortcut: string): ToolDef {
  return {
    id, label, icon: PenLine, shortcut,
    gesture: { kind: 'pick', types: ['IfcAnnotation'] },
    complete: async ({ entity }, ctx) => {
      if (!entity) return;
      if (as === 'road' || as === 'breakline' || as === 'path') {
        const { convertPicked } = await import('../drafting/convert/convert');
        convertPicked(entity.id, as === 'path' ? 'civilPath' : as);
        return;
      }
      const r = await ctx.execute('civil.fromLinework', { id: entity.id, as, storeyId: ctx.storeyId }) as { kind: string; points: number; pieces: number; heights: boolean; curves?: number; deviation?: number; breaklines?: number } | null;
      if (!r) return;
      const what = as === 'track' ? 'A track' : 'A breakline';
      const design = r.curves != null ? `, read as a design: ${r.curves} curve${r.curves === 1 ? '' : 's'}, within ${Math.round((r.deviation ?? 0) * 1000)} mm` : '';
      notify(`${what} from ${r.pieces} line${r.pieces === 1 ? '' : 's'} joined (${r.points} points${r.heights ? ', with heights' : ''})${design}.`);
    },
  };
}
const roadFromLinework = fromLinework('civil.roadFromLinework', 'Road from linework', 'road', 'LR');
const breaklineFromLinework = fromLinework('civil.breaklineFromLinework', 'Breakline from linework', 'breakline', 'BL');

const corridor: ToolDef = { id: 'civil.corridor', label: 'Corridor…', icon: Spline, shortcut: 'CR', run: () => useUi.setState({ dialog: 'corridor' }) };
const entourageKinds: ToolDef = { id: 'civil.entourageKinds', label: 'Entourage kinds…', icon: ImagePlus, run: () => useUi.setState({ dialog: 'entourageKinds' }) };

/** A retaining wall along a polyline, the high ground on its left; its section is set in the inspector. */
const retainingWall: ToolDef = {
  id: 'civil.retainingWall', label: 'Retaining wall', icon: BrickWall, shortcut: 'RW',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addRetainingWall', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

/** The tunnel-portal and bridge-abutment configurator (docs/plan-wing-walls.md, S3): a face wall and its wings from a preset. */
const portal: ToolDef = { id: 'civil.portal', label: 'Portal or abutment…', icon: Landmark, shortcut: 'PO', run: () => openPortal() };

/** The tunnel lining configurator (docs/plan-wing-walls.md, S4): a section swept along an axis, in casting blocks or rings. */
const tunnel: ToolDef = { id: 'civil.tunnel', label: 'Tunnel lining…', icon: TunnelIcon, shortcut: 'TU', run: () => openTunnel() };

/** One plant at a click (the catalogue's first species; changed in the inspector). */
const plant: ToolDef = {
  id: 'civil.plant', label: 'Plant', icon: TreeDeciduous, shortcut: 'PN',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('civil.addPlanting', { storeyId: ctx.storeyId, x: p.x, y: p.y }); },
};

/** A planting zone: its outline, sown with the default mix at 3 plants per 100 m². */
const plantingZone: ToolDef = {
  id: 'civil.plantingZone', label: 'Planting zone', icon: Sprout, shortcut: 'PZ',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addPlanting', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

/** An open polyline's grips: its vertices (dragged, or taken out with Ctrl-click) and the middles of its segments (Ctrl-click inserts a vertex). */
function polylineGrips(pts: Vec2[], save: (next: Vec2[]) => { command: string; args: Record<string, unknown> }): Grip[] {
  const o = { pts };
  const grips: Grip[] = pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex' as const, at: p,
    step: (to: Vec2) => save(moveVertex(o, i, to).pts),
    remove: () => (pts.length > 2 ? save((removeVertex(o, i) ?? o).pts) : null),
  }));
  edgeMiddles(pts).slice(0, pts.length - 1).forEach((m, i) => grips.push({ key: `e${i}`, kind: 'edgeMid', at: m, insert: () => save(insertVertex(o, i).pts) }));
  return grips;
}
const pathOf = (n: NodeDto) => fromPairs(n.props.path);
const pathRefs = (n: NodeDto): RefLine[] => { const pts = pathOf(n); return pts.length >= 2 ? [{ points: pts, closed: false, kind: 'axis' }] : []; };

const civil: LapinModule = {
  id: 'civil',
  name: 'Civil',
  summary: 'The terrain from survey points or flat, its contours, the brush that works it, platforms and ditches with their cut and fill, paths, retaining walls, plantings, people and cars.',
  // The old Road tool: the Path tool, set to Road.
  legacyTools: [road, placePortal, placePortalOnRoad, drawPortalPath, placeTunnelOnAxis, drawTunnelAxis],
  // Create similar: the tool that made the element, set as it was (a road's template, a zone or a single plant, the entourage's kind).
  similar: {
    IfcEarthworksCut: (n) => activate(n.props.contour ? platform : ditch),
    IfcPavement: (n) => { usePathKind.setState({ kind: n.props.template === 'road' ? 'road' : 'path' }); if (useUi.getState().tool !== path.id) activate(path); },
    LapinRetainingWall: () => activate(retainingWall),
    LapinHardscape: (n) => { similarHardscape(n); if (useUi.getState().tool !== hardscape.id) activate(hardscape); },
    LapinFeatureLine: (n) => { if (useUi.getState().tool !== (n.props.elevation != null ? contourAt : featureLine).id) activate(n.props.elevation != null ? contourAt : featureLine); },
    LapinPlanting: (n) => activate(n.props.contour ? plantingZone : plant),
    LapinEntourage: (n) => { if (typeof n.props.kind === 'string') useEntouragePrefs.setState({ kind: n.props.kind as never }); if (useUi.getState().tool !== entourage.id) activate(entourage); },
  },
  tabs: [{
    id: 'site', label: 'Site', order: 40, include: ['modify'],
    groups: [
      { label: 'Terrain', tools: [terrainFromFile, flatTerrain, terrainOnOutline, parcel], small: [terrainOnPicked, terrainBoundary, splitTerrain, mergeTerrains, slopeTag, terrainFromLandXml, landXmlOut, spot, breaklineFromLinework] },
      { label: 'Grading', tools: [featureLine, contourAt, dragContour], small: [] },
      { label: 'Import', tools: [], small: [terrainFromDxf, geoJson, cityJson, orthophoto] },
      { label: 'Earthworks', tools: [platform, ditch, sculpt('raise', 'Raise', MoveVertical, 'SU'), sculpt('lower', 'Lower', Shovel, 'SL')], small: [platformHole, subregion, sculpt('flatten', 'Flatten', Mountain, 'FN'), sculpt('smooth', 'Smooth', Paintbrush, 'SS'), brush] },
      { label: 'Landscape', tools: [path, hardscape, retainingWall, plant, plantingZone], small: [corridor, roadFromLinework, hardscapeSlope, portal, tunnel] },
      { label: 'Entourage', tools: [entourage], small: [entourageRow, entourageKinds] },
    ],
  }],
  grips: {
    IfcEarthworksCut: (n: NodeDto) => {
      if (isDitch(n)) return polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updateDitch', args: { id: n.id, points: toPairs(pts) } }));
      return [
        ...curvedOutlineGrips(n.id, fromPairs(n.props.contour), n.props.arcs, (pts, arcs) => ({ command: 'civil.updatePlatform', args: { id: n.id, points: toPairs(pts), arcs } }), 'civil'),
        // Its own holes (audit S4), their ground kept.
        ...holeGrips(n.id, n.props.holes, n.props.holeArcs, (holes, holeArcs) => ({ command: 'civil.updatePlatform', args: { id: n.id, holes, holeArcs } }), 'civil'),
      ];
    },
    // A terrain's parcel and irregular boundary, by their corners; edges may be arcs. While editing its points (T1), its survey
    // points too, each one's right-click setting its elevation or picking it; a boundary corner standing on a point offers the same.
    IfcGeographicElement: (n: NodeDto, view?: GripView) => {
      const edit = useTerrainEdit.getState().editPoints && n.props.predefinedType === 'TERRAIN';
      const boundary = curvedOutlineGrips(n.id, fromPairs(n.props.boundary), n.props.boundaryArcs, (pts, arcs) => ({ command: 'civil.setBoundary', args: { terrainId: n.id, points: toPairs(pts), arcs } }), 'civil');
      return [
        ...curvedOutlineGrips(n.id, fromPairs(n.props.parcel), n.props.parcelArcs, (pts, arcs) => ({ command: 'civil.setParcel', args: { terrainId: n.id, points: toPairs(pts), arcs } }), 'civil'),
        ...(edit ? decorateCornerGrips(n, boundary, askPointElevation) : boundary),
        // In a plan only (it names its view): the 3D view has the points' own height grips.
        ...(edit && view?.view ? terrainPlanPointGrips(n, useCamera.getState().target, askPointElevation) : []),
      ];
    },
    // In a plan only (it names its view): in 3D a corner's plan grips would stand on the storey's level, under the ground; there it has its height grips.
    LapinFeatureLine: (n: NodeDto, view?: GripView) => (view?.view ? featureLineGrips(n) : []),
    // In a plan only: in 3D their plan grips would stand on the storey's level, under the ground they follow.
    IfcPavement: (n: NodeDto, view?: GripView) => (view?.view ? polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updatePath', args: { id: n.id, points: toPairs(pts) } })) : []),
    LapinRetainingWall: (n: NodeDto, view?: GripView) => (view?.view ? polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updateRetainingWall', args: { id: n.id, points: toPairs(pts) } })) : []),
    LapinHardscape: hardscapeGrips,
    LapinTunnelLining: (n: NodeDto, view?: GripView) => (view?.view ? polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updateTunnelLining', args: { id: n.id, points: toPairs(pts) } })) : []),
    LapinPlanting: (n: NodeDto) => { const pts = fromPairs(n.props.contour); return pts.length >= 3 ? outlineGrips({ pts }, (o) => ({ command: 'civil.updatePlanting', args: { id: n.id, points: toPairs(o.pts) } })) : []; },
  },
  grips3d: {
    // A terrain's survey points, while "edit in 3D" is on: the MAX_POINT_GRIPS nearest the view's centre (pointGrips.ts).
    IfcGeographicElement: (n: NodeDto): Grip3D[] =>
      useTerrainEdit.getState().editPoints ? terrainPointGrips(n, useCamera.getState().target, 0, MAX_POINT_GRIPS, useTerrainEdit.getState().gripMode) : [],
    // A feature line's corners: a height grip on each.
    LapinFeatureLine: featureLineGrips3d,
    // A hardscape's top: its elevation, or each corner's height.
    LapinHardscape: hardscapeGrips3d,
  },
  references: {
    IfcEarthworksCut: (n: NodeDto) => { if (isDitch(n)) return pathRefs(n); const pts = fromPairs(n.props.contour); return pts.length >= 3 ? [outlineRef(pts, arcsOf(n.props.arcs, pts.length))] : []; },
    // None for a feature line: a reference stands on the storey's level, and in 3D it would run under the ground (the line is drawn itself in plan and in 3D).
    // In a plan only, as their grips (in 3D the axis would run on the storey's level, under the ground).
    IfcPavement: (n: NodeDto, view?: GripView) => (view?.view ? pathRefs(n) : []),
    LapinRetainingWall: (n: NodeDto, view?: GripView) => (view?.view ? pathRefs(n) : []),
    LapinHardscape: hardscapeRefs,
    LapinTunnelLining: (n: NodeDto, view?: GripView) => (view?.view ? pathRefs(n) : []),
    LapinPlanting: (n: NodeDto) => { const pts = fromPairs(n.props.contour); return pts.length >= 3 ? [outlineRef(pts)] : []; },
  },
  propertySections: [
    { id: 'terrain', label: 'Terrain', classes: ['IfcGeographicElement'], component: TerrainSection },
    { id: 'featureLine', label: 'Feature line', classes: ['LapinFeatureLine'], component: FeatureLineSection, replacesFields: () => true },
    { id: 'earthwork', label: 'Earthwork', classes: ['IfcEarthworksCut'], component: EarthworkSection, replacesFields: () => true },
    { id: 'path', label: 'Path', classes: ['IfcPavement'], component: PathSection, replacesFields: () => true },
    { id: 'hardscape', label: 'Hardscape', classes: ['LapinHardscape'], component: HardscapeSection, replacesFields: () => true },
    { id: 'kerb', label: 'Kerb', classes: ['LapinKerb'], component: KerbSection, replacesFields: () => true },
    { id: 'retainingWall', label: 'Retaining wall', classes: ['LapinRetainingWall'], component: RetainingWallSection, replacesFields: () => true },
    { id: 'tunnelLining', label: 'Tunnel lining', classes: ['LapinTunnelLining'], component: TunnelLiningSection },
    { id: 'planting', label: 'Planting', classes: ['LapinPlanting'], component: PlantingSection, replacesFields: () => true },
    { id: 'entourage', label: 'Entourage', classes: ['LapinEntourage'], component: EntourageSection, replacesFields: () => true },
  ],
  dialogs: { brush: BrushDialog, corridor: CorridorDialog, entourageKinds: EntourageKindsDialog, portal: PortalDialog, tunnel: TunnelDialog, hardscapeTypes: HardscapeTypesDialog },
  editType: { LapinHardscapeType: (id: string) => { useHardscapeTypeEdit.setState({ id }); useUi.setState({ dialog: 'hardscapeTypes' }); } },
};

export default civil;
