/** Civil: the terrain from a survey file, its contours, and platforms levelled into it with their cut and fill. */

import { FileUp, LandPlot, Mountain } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';

/** A text file the user picks (a survey: xyz or PENZD), or null when cancelled. */
export function pickText(accept: string): Promise<{ name: string; text: string } | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.onchange = () => {
      const f = input.files?.[0];
      if (!f) { resolve(null); return; }
      void f.text().then((text) => resolve({ name: f.name.replace(/\.[^.]+$/, ''), text }));
    };
    input.click();
  });
}

const SURVEY = '.csv,.txt,.xyz,.pts';

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
  id: 'civil.terrain', label: 'Terrain from points', icon: Mountain, shortcut: 'TP',
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

/** A platform: its outline in plan (Enter or the first corner closes it); it is levelled at the ground's height in its middle, changed in the inspector. */
const platform: ToolDef = {
  id: 'civil.platform', label: 'Platform', icon: LandPlot, shortcut: 'PF',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    await ctx.execute('civil.addPlatform', { points: points.map((p) => [p.x, p.y]) });
  },
};

/** A terrain in the inspector: its points, and replacing them from a new survey. */
function TerrainSection({ node }: { node: NodeDto }) {
  const count = Array.isArray(node.props.points) ? node.props.points.length : 0;
  const relations = useSession((s) => s.relations);
  const pads = [...relations.values()].filter((r) => r.class === 'IfcRelVoidsElement' && r.relating === node.id).reduce((n, r) => n + r.related.length, 0);
  return (
    <dl className="fields">
      <dt>Points</dt><dd>{count}</dd>
      <dt>Platforms</dt><dd>{pads}</dd>
      <dt />
      <dd><button type="button" disabled={!node.editable} onClick={() => void pickText(SURVEY).then((f) => f && execute('civil.updateTerrain', { id: node.id, text: f.text }))}>
        <FileUp size={13} /> Replace the points…
      </button></dd>
    </dl>
  );
}

const civil: LapinModule = {
  id: 'civil',
  name: 'Civil',
  summary: 'The terrain from survey points, its contours, platforms with their cut and fill.',
  tabs: [{
    id: 'site', label: 'Site', order: 30,
    groups: [{ label: 'Terrain', tools: [terrainFromFile, platform] }],
  }],
  propertySections: [{ id: 'terrain', label: 'Terrain', classes: ['IfcGeographicElement'], component: TerrainSection }],
};

export default civil;
