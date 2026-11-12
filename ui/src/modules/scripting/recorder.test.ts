import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { baseName, Recording, type RecordedCall, type RecorderContext } from './recorder';

// The project the window recorded in: a storey, a wall type, a wall drawn before the recording.
const NODES: Record<string, { type: string; name: string; props: Record<string, unknown>; category?: string }> = {
  S: { type: 'IfcBuildingStorey', name: 'Ground floor', props: { elevation: 0 }, category: 'spatial' },
  T: { type: 'IfcWallType', name: 'Brick 250', props: {}, category: 'type' },
  OLD: { type: 'IfcWall', name: 'Wall', props: {}, category: 'element' },
};

const ctx = (over: Partial<RecorderContext> = {}): RecorderContext => ({ node: (id) => NODES[id], known: null, selection: [], useSelection: true, ...over });
const exec = (command: string, args: Record<string, unknown>, result: unknown = null): RecordedCall => ({ method: 'doc.execute', params: { command, args }, result });

describe('the macro recorder (decision 489, stage 3)', () => {
  it('writes the steps of the window as the script python/tests runs on a new project', () => {
    const r = new Recording();
    r.add(exec('architecture.addWall', { storeyId: 'S', x1: 0, y1: 0, x2: 5, y2: 0 }, { id: 'W1' }), ctx());
    r.add(exec('architecture.addWall', { storeyId: 'S', x1: 5, y1: 0, x2: 5, y2: 4 }, { id: 'W2' }), ctx());
    r.add(exec('architecture.addWall', { storeyId: 'S', x1: 5, y1: 4, x2: 0, y2: 4 }, { id: 'W3' }), ctx());
    // Undo takes the third wall out of the script: it holds what the window did in the end.
    expect(r.add({ method: 'doc.undo', params: {} }, ctx())).toEqual(['wall3 = lapin.architecture.add_wall(storey_id=storey1, x1=5, y1=4, x2=0, y2=4)']);
    r.add(exec('element.update', { id: 'W2', attrs: { height: 3.1 } }), ctx());
    r.add(exec('edit.batch', { label: 'Names', steps: [{ command: 'element.rename', args: { id: 'W1', name: 'South' } }] }, [null]), ctx());
    r.add({ method: 'doc.undo', params: {} }, ctx());
    r.add({ method: 'doc.redo', params: {} }, ctx());
    // Queries are not recorded.
    r.add({ method: 'doc.query', params: { name: 'schedule.csv' } }, ctx());
    const fixture = readFileSync(new URL('../../../../python/tests/fixtures/recorded_walls.py', import.meta.url), 'utf8');
    expect(r.text()).toBe(fixture);
  });

  it('names what an earlier line made by its variable, an existing type by its name, another element by its id', () => {
    const r = new Recording();
    r.add(exec('architecture.createWallType', { name: 'Mine', layers: [{ material: 'Brick', thickness: 0.25, core: true }] }, { id: 'NT' }), ctx());
    const lines = r.add(exec('architecture.addWall', { storeyId: 'S', typeId: 'T', x1: 0, y1: 0, x2: 1.25, y2: -0.1, joinStyle: 'auto' }, { id: 'W' }), ctx());
    expect(lines).toEqual([
      'storey1 = lapin.model.storey("Ground floor", 0)',
      'wall_type2 = lapin.model.find("IfcWallType", "Brick 250")',
      'wall1 = lapin.architecture.add_wall(storey_id=storey1, type_id=wall_type2, x1=0, y1=0, x2=1.25, y2=-0.1, join_style="auto")',
    ]);
    // The type the recording made is its variable's id, the wall drawn before it is itself.
    expect(r.add(exec('element.update', { ids: ['W', 'OLD'], attrs: { typeId: 'NT' } }), ctx())).toEqual([
      'lapin.element.update(ids=[wall1.id, lapin.model["OLD"]], attrs={"typeId": wall_type1.id})',
    ]);
    expect(r.lines[0]).toBe('wall_type1 = lapin.architecture.create_wall_type(name="Mine", layers=[{"material": "Brick", "thickness": 0.25, "core": True}])');
  });

  it('binds a result of several ids, and a batch’s results inside its block', () => {
    const r = new Recording();
    expect(r.add(exec('analysis.defaultCases', {}, { ids: ['C1', 'C2'] }), ctx())).toEqual(['default_cases1 = lapin.analysis.default_cases()']);
    expect(r.add(exec('element.delete', { ids: ['C2'] }), ctx())).toEqual(['lapin.element.delete(ids=[default_cases1.ids[1]])']);
    expect(r.add(exec('edit.batch', { steps: [{ command: 'architecture.addWall', args: { x1: 0 } }, { command: 'architecture.addWall', args: { x1: 1 } }] }, [{ id: 'A' }, { id: 'B' }]), ctx())).toEqual([
      'with lapin.batch():',
      '    wall1 = lapin.architecture.add_wall(x1=0)',
      '    wall2 = lapin.architecture.add_wall(x1=1)',
    ]);
    expect(r.add(exec('element.rename', { id: 'B', name: 'x' }), ctx())).toEqual(['lapin.element.rename(id=wall2.id, name="x")']);
  });

  it('writes the window’s selection as lapin.selection() when asked, else each element', () => {
    const sel = ctx({ selection: ['OLD', 'S'] });
    expect(new Recording().add(exec('element.delete', { ids: ['S', 'OLD'] }), sel)).toEqual(['lapin.element.delete(ids=lapin.selection())']);
    expect(new Recording().add(exec('element.delete', { ids: ['OLD'] }), sel)).toEqual(['lapin.element.delete(ids=[lapin.model["OLD"]])']);
    expect(new Recording().add(exec('element.delete', { ids: ['OLD', 'S'] }), ctx({ selection: ['OLD', 'S'], useSelection: false }))[1]).toBe('lapin.element.delete(ids=[lapin.model["OLD"], storey1])');
  });

  it('sends what a keyword cannot say in a dict, and a command the catalogue lacks by its engine name', () => {
    const r = new Recording();
    expect(r.add(exec('element.setParam', { id: 'OLD', name: 'Fire rating', value: null, IFCType: 'x', 'odd-key': true }), ctx())).toEqual([
      'lapin.element.set_param({"value": None, "IFCType": "x", "odd-key": True}, id=lapin.model["OLD"], name="Fire rating")',
    ]);
    const known = ctx({ known: (c) => c !== 'premium.thing' });
    expect(r.add(exec('premium.thing', { a: 1, b: [true, false, null] }), known)).toEqual(['lapin.execute("premium.thing", {"a": 1, "b": [True, False, None]})']);
    expect(r.add(exec('architecture.addWall', { from: 1, x1: Number.NaN, x2: 5.116666666666667, s: 'a "quote"\n' }), known)).toEqual(['lapin.architecture.add_wall(from_=1, x1=float("nan"), x2=5.116667, s="a \\"quote\\"\\n")']);
    // The panel's own commands (scripting.*) are left out by the store; undo of what came before is lapin.undo().
    expect(new Recording().add({ method: 'doc.undo', params: {} }, ctx())).toEqual(['lapin.undo()']);
    expect(new Recording().add({ method: 'doc.redo', params: {} }, ctx())).toEqual(['lapin.redo()']);
  });

  it('forgets a lookup with the step that bound it, and binds it again where it is needed', () => {
    const r = new Recording();
    r.add(exec('architecture.addWall', { storeyId: 'S', x1: 0 }, { id: 'A' }), ctx());
    r.add({ method: 'doc.undo', params: {} }, ctx());
    expect(r.add(exec('architecture.addWall', { storeyId: 'S', x1: 1 }, { id: 'B' }), ctx())).toEqual([
      'storey2 = lapin.model.storey("Ground floor", 0)',
      'wall2 = lapin.architecture.add_wall(storey_id=storey2, x1=1)',
    ]);
    // A redo after a new step is nothing to bring back.
    expect(r.add({ method: 'doc.redo', params: {} }, ctx())).toEqual(['lapin.redo()']);
  });

  it('names a result after its command without the verb', () => {
    expect(baseName('architecture.addWall')).toBe('wall');
    expect(baseName('architecture.createWallType')).toBe('wall_type');
    expect(baseName('furniture.insertFromLibrary')).toBe('from_library');
    expect(baseName('storey.create')).toBe('create');
    expect(baseName('x.new')).toBe('new');
  });
});
