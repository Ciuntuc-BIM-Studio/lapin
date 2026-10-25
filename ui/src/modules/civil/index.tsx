/**
 * Civil and site: the terrain from a survey file, from LandXML or flat; its
 * contours; platforms and ditches levelled or dug into it with their cut and
 * fill; the brush that works the ground; paths, retaining walls and
 * plantings that stand on it (docs/spec-site.md).
 */

import { BrickWall, Crosshair, FileDown, FileUp, Fence, Mountain, MoveVertical, Paintbrush, Route, Shovel, Sprout, TreeDeciduous, Waves } from 'lucide-react';
import { PlatformIcon, TerrainIcon } from '../../icons';
import type { Grip, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { outlineGrips, outlineRef, toPairs } from '../../viewport/gripKit';
import { edgeMiddles, fromPairs, insertVertex, moveVertex, removeVertex } from '../../viewport/contour';
import { notify, useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { SURVEY, downloadLandXml, pickText } from './survey';
import { TerrainSection } from './TerrainSection';
import { EarthworkSection, PathSection, PlantingSection, RetainingWallSection } from './SiteSections';
import { BrushDialog } from './BrushDialog';
import { isDitch, setBrush, useBrush, type BrushKind } from './prefs';

/** The lowest surveyed elevation, rounded down to the metre: what ±0.00 defaults to. */
function lowest(text: string): number | null {
  let min = Infinity;
  for (const line of text.split('\n')) {
    const t = line.trim();
    if (!t || t.startsWith('#') || t.startsWith('//')) continue;
    const f = t.split(/[\s,;]+/).map(Number);
    const z = f.length >= 4 && f.slice(0, 4).every(Number.isFinite) ? f[3] : f.length >= 3 && f.slice(0, 3).every(Number.isFinite) ? f[2] : NaN;
    if (Number.isFinite(z)) min = Math.min(min, z);
  }
  return Number.isFinite(min) ? Math.floor(min) : null;
}

/** A terrain from a survey file: its points, and the surveyed elevation of the project's ±0.00. */
const terrainFromFile: ToolDef = {
  id: 'civil.terrain', label: 'Terrain from points', icon: TerrainIcon, shortcut: 'TP',
  run: () => void pickText(SURVEY).then(async (file) => {
    if (!file) return;
    const low = lowest(file.text);
    if (low == null) { notify('No points found: one per line, x y z or number northing easting elevation.'); return; }
    const answer = window.prompt('The surveyed elevation of the project\'s ±0.00 (m):', String(low));
    if (answer == null) return;
    const datum = answer.trim() === '' ? low : Number(answer.replace(',', '.'));
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.addTerrain', { text: file.text, name: file.name, datum: Number.isFinite(datum) ? datum : low, ...(storeyId ? { storeyId } : {}) });
    if (r) notify(`Terrain created from ${(r as { points: number }).points} points.`);
  }),
};

/** Terrains from a LandXML file: each surface of it. */
const terrainFromLandXml: ToolDef = {
  id: 'civil.landxml', label: 'Terrain from LandXML', icon: FileUp,
  run: () => void pickText('.xml,.landxml').then(async (file) => {
    if (!file) return;
    const answer = window.prompt('The surveyed elevation of the project\'s ±0.00 (m):', '0');
    if (answer == null) return;
    const storeyId = useUi.getState().storeyId;
    const r = await execute('civil.importLandXml', { text: file.text, datum: Number(answer.replace(',', '.')) || 0, ...(storeyId ? { storeyId } : {}) }) as { ids: string[] } | null;
    if (r) notify(`${r.ids.length} terrain(s) from ${file.name}.`);
  }),
};

/** Level ground where there is no survey: a rectangle in plan at an elevation. */
const flatTerrain: ToolDef = {
  id: 'civil.flatTerrain', label: 'Flat terrain', icon: Mountain, shortcut: 'TF',
  gesture: { kind: 'rectangle' },
  complete: async ({ points: [a, b] }, ctx) => {
    const answer = window.prompt('The ground\'s elevation (m, ±0.00 = 0):', '0');
    if (answer == null) return;
    await ctx.execute('civil.addFlatTerrain', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, elevation: Number(answer.replace(',', '.')) || 0 });
  },
};

/** A spot level at a click: the designed ground's height, written on the plan beside a cross. */
const spot: ToolDef = {
  id: 'civil.spot', label: 'Spot level', icon: Crosshair, shortcut: 'SP',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('civil.addSpot', { x: p.x, y: p.y }); },
};

/** The parcel: the property's boundary on the terrain, drawn dash-dotted with its area. */
const parcel: ToolDef = {
  id: 'civil.parcel', label: 'Parcel', icon: Fence, shortcut: 'PC',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.setParcel', { points: points.map((p) => [p.x, p.y]) }); },
};

/** Every terrain of the project as one LandXML file. */
const landXmlOut: ToolDef = { id: 'civil.landxmlExport', label: 'Export LandXML', icon: FileDown, run: () => void downloadLandXml().catch((e) => notify(e instanceof Error ? e.message : String(e))) };

/** A platform: its outline in plan (Enter or the first corner closes it); it is levelled at the ground's height in its middle, changed in the inspector. */
const platform: ToolDef = {
  id: 'civil.platform', label: 'Platform', icon: PlatformIcon, shortcut: 'PD',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addPlatform', { points: points.map((p) => [p.x, p.y]) }); },
};

/** A ditch along a polyline (Enter finishes), half a metre under the ground; its section is set in the inspector. */
const ditch: ToolDef = {
  id: 'civil.ditch', label: 'Ditch', icon: Waves, shortcut: 'DT',
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

/** A path along a polyline, 1,5 m wide, in the ground; its courses and kerbs are set in the inspector. */
const path: ToolDef = {
  id: 'civil.path', label: 'Path', icon: Route, shortcut: 'PA',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addPath', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

/** A retaining wall along a polyline, the high ground on its left; its section is set in the inspector. */
const retainingWall: ToolDef = {
  id: 'civil.retainingWall', label: 'Retaining wall', icon: BrickWall, shortcut: 'RW',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('civil.addRetainingWall', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

/** One plant at a click (the catalogue's first species; changed in the inspector). */
const plant: ToolDef = {
  id: 'civil.plant', label: 'Plant', icon: TreeDeciduous, shortcut: 'PL',
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
  summary: 'The terrain from survey points or flat, its contours, the brush that works it, platforms and ditches with their cut and fill, paths, retaining walls and plantings.',
  tabs: [{
    id: 'site', label: 'Site', order: 40,
    groups: [
      { label: 'Terrain', tools: [terrainFromFile, flatTerrain, parcel], small: [terrainFromLandXml, landXmlOut, spot] },
      { label: 'Earthworks', tools: [platform, ditch, sculpt('raise', 'Raise', MoveVertical, 'SR'), sculpt('lower', 'Lower', Shovel, 'SL')], small: [sculpt('flatten', 'Flatten', Mountain, 'SF'), sculpt('smooth', 'Smooth', Paintbrush, 'SS'), brush] },
      { label: 'Landscape', tools: [path, retainingWall, plant, plantingZone] },
    ],
  }],
  grips: {
    IfcEarthworksCut: (n: NodeDto) => {
      if (isDitch(n)) return polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updateDitch', args: { id: n.id, points: toPairs(pts) } }));
      const pts = fromPairs(n.props.contour);
      return pts.length >= 3 ? outlineGrips({ pts }, (o) => ({ command: 'civil.updatePlatform', args: { id: n.id, points: toPairs(o.pts) } })) : [];
    },
    IfcPavement: (n: NodeDto) => polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updatePath', args: { id: n.id, points: toPairs(pts) } })),
    LapinRetainingWall: (n: NodeDto) => polylineGrips(pathOf(n), (pts) => ({ command: 'civil.updateRetainingWall', args: { id: n.id, points: toPairs(pts) } })),
    LapinPlanting: (n: NodeDto) => { const pts = fromPairs(n.props.contour); return pts.length >= 3 ? outlineGrips({ pts }, (o) => ({ command: 'civil.updatePlanting', args: { id: n.id, points: toPairs(o.pts) } })) : []; },
  },
  references: {
    IfcEarthworksCut: (n: NodeDto) => { if (isDitch(n)) return pathRefs(n); const pts = fromPairs(n.props.contour); return pts.length >= 3 ? [outlineRef(pts)] : []; },
    IfcPavement: pathRefs,
    LapinRetainingWall: pathRefs,
    LapinPlanting: (n: NodeDto) => { const pts = fromPairs(n.props.contour); return pts.length >= 3 ? [outlineRef(pts)] : []; },
  },
  propertySections: [
    { id: 'terrain', label: 'Terrain', classes: ['IfcGeographicElement'], component: TerrainSection },
    { id: 'earthwork', label: 'Earthwork', classes: ['IfcEarthworksCut'], component: EarthworkSection, replacesFields: () => true },
    { id: 'path', label: 'Path', classes: ['IfcPavement'], component: PathSection, replacesFields: () => true },
    { id: 'retainingWall', label: 'Retaining wall', classes: ['LapinRetainingWall'], component: RetainingWallSection, replacesFields: () => true },
    { id: 'planting', label: 'Planting', classes: ['LapinPlanting'], component: PlantingSection, replacesFields: () => true },
  ],
  dialogs: { brush: BrushDialog },
};

export default civil;
