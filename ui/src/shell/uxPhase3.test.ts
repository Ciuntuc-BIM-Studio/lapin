import { describe, expect, it } from 'vitest';
import { FLOW_EXTRAS, readPrefs, setPrefs, turnOffFlowExtras, turnOnFlowExtras, usePrefs } from '../app/prefs';
import { playSound, toggleTactileSounds } from '../app/sounds';
import { scanModelHealth } from '../app/modelHealth';
import { buildSequence } from './BuildReplay';
import { cubeTransform } from '../viewport/ViewCube3D';
import type { NodeDto } from '../engine/protocol';

const node = (id: string, type: string, storeyId: string | null, props: NodeDto['props'] = {}, name = id): NodeDto => ({
  id, type, name, storeyId, parentId: null, px: 0, py: 0, module: null, editable: true, props,
});

describe('Phase 3 game-like UX', () => {
  it('keeps tactile sounds off unless chosen, and allows enabling/disabling', () => {
    expect(readPrefs(null).tactileSounds).toBe(false);
    expect(readPrefs({}).tactileSounds).toBe(false);
    expect(readPrefs({ tactileSounds: 'yes' }).tactileSounds).toBe(false);
    expect(readPrefs({ tactileSounds: true }).tactileSounds).toBe(true);
    expect(readPrefs({ tactileSounds: false }).tactileSounds).toBe(false);
    setPrefs({ tactileSounds: false });
    expect(usePrefs.getState().tactileSounds).toBe(false);
    expect(playSound('snap')).toBe(false);
    expect(toggleTactileSounds()).toBe(true);
    expect(usePrefs.getState().tactileSounds).toBe(true);
    setPrefs({ tactileSounds: false });
  });

  it('has every Flow extra on until the user chooses, even over settings kept while they were off by default', () => {
    const fresh = readPrefs(null);
    for (const k of FLOW_EXTRAS) expect(fresh[k], k).toBe(true);
    const before = readPrefs({ theme: 'graphite', gridStep: 0.5, viewCube: false, roleCards: false });
    for (const k of FLOW_EXTRAS) expect(before[k], k).toBe(true);
    expect(before.flowChosen).toBe(false);
    expect(before.roleHotbars).toBe(false);
    expect(before.tactileSounds).toBe(false);
    expect(before.theme).toBe('graphite');
    // A choice made is kept: all off (the start screen's switch), or one switched in Preferences.
    turnOffFlowExtras();
    for (const k of FLOW_EXTRAS) expect(usePrefs.getState()[k], k).toBe(false);
    expect(usePrefs.getState().flowChosen).toBe(true);
    expect(readPrefs(JSON.parse(JSON.stringify(usePrefs.getState()))).viewCube).toBe(false);
    setPrefs({ theme: 'paper' });
    expect(usePrefs.getState().viewCube).toBe(false);
    setPrefs({ viewCube: true });
    expect(usePrefs.getState().viewCube).toBe(true);
    expect(usePrefs.getState().xrayPeek).toBe(false);
    turnOnFlowExtras();
    for (const k of FLOW_EXTRAS) expect(usePrefs.getState()[k], k).toBe(true);
    // Sounds and the role's quick tools stay their own choice.
    expect(usePrefs.getState().tactileSounds).toBe(false);
    expect(usePrefs.getState().roleHotbars).toBe(false);
    turnOffFlowExtras();
  });

  it('computes 3D ViewCube CSS transform from camera azimuth and pitch', () => {
    expect(cubeTransform(Math.PI / 2, 0)).toBe('rotateX(90deg) rotateZ(0deg)');
    expect(cubeTransform(Math.PI / 2, -Math.PI / 2)).toBe('rotateX(0deg) rotateZ(0deg)');
  });

  it('detects sliver walls, duplicate overlapping walls/columns, and missing storeys', () => {
    const nodes = new Map<string, NodeDto>([
      ['st1', node('st1', 'IfcBuildingStorey', null, { elevation: 0 }, 'Ground')],
      ['w1', node('w1', 'IfcWall', 'st1', { start: [0, 0], end: [5, 0] }, 'Wall 1')],
      ['wDup', node('wDup', 'IfcWall', 'st1', { start: [5, 0], end: [0, 0] }, 'Wall Dup')],
      ['wTiny', node('wTiny', 'IfcWall', 'st1', { start: [0, 0], end: [0.02, 0] }, 'Sliver')],
      ['cOrphan', node('cOrphan', 'IfcColumn', null, { x: 1, y: 1 }, 'Floating Col')],
    ]);
    const issues = scanModelHealth(nodes);
    expect(issues.some((i) => i.kind === 'duplicate' && i.nodeId === 'wDup')).toBe(true);
    expect(issues.some((i) => i.kind === 'sliver' && i.nodeId === 'wTiny')).toBe(true);
    expect(issues.some((i) => i.kind === 'orphanStorey' && i.nodeId === 'cOrphan')).toBe(true);
  });

  it('orders build replay elements by storey elevation then construction stage', () => {
    const nodes = new Map<string, NodeDto>([
      ['st0', node('st0', 'IfcBuildingStorey', null, { elevation: 0 })],
      ['st1', node('st1', 'IfcBuildingStorey', null, { elevation: 3 })],
      ['roof', node('roof', 'IfcRoof', 'st1', {})],
      ['wall1', node('wall1', 'IfcWall', 'st1', {})],
      ['foot', node('foot', 'IfcFooting', 'st0', {})],
      ['wall0', node('wall0', 'IfcWall', 'st0', {})],
      ['door0', node('door0', 'IfcDoor', 'st0', {})],
    ]);
    expect(buildSequence(nodes)).toEqual(['foot', 'wall0', 'door0', 'wall1', 'roof']);
  });
});
