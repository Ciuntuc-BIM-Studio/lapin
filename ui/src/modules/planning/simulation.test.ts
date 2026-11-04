import { describe, expect, it } from 'vitest';
import { dayOf } from './gantt';
import {
  advance, clampDay, counts, crc32, DEFAULT_LOOK, frameDays, layerStates, legend, lookKit, looksAt, prepare, stateOf, step, tasksOn, videoType, zipStore, type TimelineDto,
} from './simulation';

/** The engine's sample (PlanningSimulationTests): an old slab demolished 1–5 March 2027, the walls' brick 8–12, their plaster 15–19, a slab cast 15–19 in its formwork. */
const T: TimelineDto = {
  start: '2027-03-01', finish: '2027-03-19', dataDate: null, look: DEFAULT_LOOK,
  elements: [
    { id: 'old', demolish: ['2027-03-01', '2027-03-05'] },
    { id: 'w1', build: ['2027-03-08', '2027-03-19'], layers: { 'layer:0': ['2027-03-15', '2027-03-19'], 'layer:1': ['2027-03-08', '2027-03-12'], 'layer:2': ['2027-03-15', '2027-03-19'] } },
    { id: 'slab', build: ['2027-03-15', '2027-03-19'] },
    { id: 'fw', temporary: 'formwork', present: ['2027-03-15', '2027-04-13'] },
  ],
  tasks: [
    { id: 'd', name: 'Demolish', start: '2027-03-01', finish: '2027-03-05', duration: 5 },
    { id: 'm', name: 'Masonry', start: '2027-03-08', finish: '2027-03-12', duration: 5 },
    { id: 'p', name: 'Plaster', start: '2027-03-15', finish: '2027-03-19', duration: 5 },
  ],
  stages: { chapters: ['2027-03-05', '2027-03-12', '2027-03-19'], months: ['2027-03-19'] },
};
const t = prepare(T);
const d = dayOf;
const el = (id: string) => t.elements.find((e) => e.id === id)!;

describe('the state of an element on a day', () => {
  it('is not started, under way, then built; a demolition existing, red, then gone; a temporary only while it stands', () => {
    expect(stateOf(el('w1'), d('2027-03-05'))).toBe('planned');
    expect(stateOf(el('w1'), d('2027-03-10'))).toBe('active');
    expect(stateOf(el('w1'), d('2027-03-20'))).toBe('done');
    expect(stateOf(el('old'), d('2027-02-26'))).toBe('existing');
    expect(stateOf(el('old'), d('2027-03-03'))).toBe('demolishing');
    expect(stateOf(el('old'), d('2027-03-08'))).toBe('gone');
    expect(stateOf(el('fw'), d('2027-03-12'))).toBe('gone');
    expect(stateOf(el('fw'), d('2027-03-15'))).toBe('temporary');
    expect(stateOf(el('fw'), d('2027-04-14'))).toBe('gone');
  });

  it('reads a layered wall\'s layers apart: the brick, then the plaster on both faces', () => {
    expect(layerStates(el('w1'), d('2027-03-10'))?.map(([, s]) => s)).toEqual(['planned', 'active', 'planned']);
    expect(layerStates(el('w1'), d('2027-03-16'))?.map(([, s]) => s)).toEqual(['active', 'done', 'active']);
    expect(layerStates(el('slab'), d('2027-03-16'))).toBeNull();
  });

  it('counts the states and names the tasks under way', () => {
    expect(counts(t, d('2027-03-16'))).toEqual({ gone: 1, active: 2, temporary: 1 });
    expect(tasksOn(t, d('2027-03-10')).map((x) => x.name)).toEqual(['Masonry']);
  });
});

describe('the looks of a day', () => {
  const kit = lookKit(DEFAULT_LOOK);

  it('hides what is not started and what is gone, colours what is under way, leaves what is built as it is', () => {
    const { looks } = looksAt(t, d('2027-03-03'), kit);
    expect(looks.w1).toEqual({ hidden: true });
    expect(looks.old).toEqual({ color: '#d64545' });
    expect(looks.fw).toEqual({ hidden: true });
    const later = looksAt(t, d('2027-04-20'), kit).looks;
    expect(later.w1).toBeUndefined();
    expect(later.slab).toBeUndefined();
    expect(later.old).toEqual({ hidden: true });
  });

  it('shows a layered wall\'s brick under way and its plaster not laid yet, one shared look per mix', () => {
    const a = looksAt(t, d('2027-03-10'), kit).looks.w1;
    expect(a.parts).toEqual({ 'layer:0': { hidden: true }, 'layer:1': { color: '#e8a33d' }, 'layer:2': { hidden: true } });
    expect(looksAt(t, d('2027-03-11'), kit).looks.w1).toBe(a);
    // Layers apart switched off: the wall is one piece, under way.
    expect(looksAt(t, d('2027-03-10'), lookKit({ ...DEFAULT_LOOK, layers: false })).looks.w1).toEqual({ color: '#e8a33d' });
  });

  it('keeps its signature while nothing changes, so the views restyle nothing', () => {
    const a = looksAt(t, d('2027-03-09'), kit);
    const b = looksAt(t, d('2027-03-10'), kit);
    const c = looksAt(t, d('2027-03-15'), kit);
    expect(b.signature).toBe(a.signature);
    expect(c.signature).not.toBe(b.signature);
    expect(b.looks.slab).toBe(a.looks.slab);
  });

  it('shows ghosts in the planned colour, and hides what no task takes when asked', () => {
    const ghosts = lookKit({ ...DEFAULT_LOOK, plannedMode: 'ghost', unlinked: 'hide' });
    const { looks } = looksAt(t, d('2027-03-03'), ghosts, ['w1', 'loose']);
    expect(looks.w1).toEqual({ color: '#b8c0c8', opacity: 0.18 });
    expect(looks.loose).toEqual({ hidden: true });
    expect(looksAt(t, d('2027-03-03'), kit, ['loose']).looks.loose).toBeUndefined();
  });

  it('says what each colour means, the ghosts only when shown', () => {
    expect(legend(DEFAULT_LOOK).map((x) => x.state)).toEqual(['active', 'demolishing', 'temporary']);
    expect(legend({ ...DEFAULT_LOOK, plannedMode: 'ghost' }).map((x) => x.color)).toEqual(['#e8a33d', '#d64545', '#3d8fd6', '#b8c0c8']);
  });
});

describe('playback', () => {
  it('moves the day by the speed as time passes and stops at the finish', () => {
    expect(advance(t.start, 500, 4, t)).toEqual({ pos: t.start + 2, playing: true });
    expect(advance(t.start, 250, 1, t).pos).toBeCloseTo(t.start + 0.25);
    expect(advance(t.finish - 1, 1000, 5, t)).toEqual({ pos: t.finish, playing: false });
  });

  it('steps a day or a week, within the schedule', () => {
    expect(step(t.start, 'day', 1, t)).toBe(t.start + 1);
    expect(step(t.start, 'week', 1, t)).toBe(t.start + 7);
    expect(step(t.start + 3, 'week', -1, t)).toBe(t.start);
    expect(step(t.finish - 2, 'week', 1, t)).toBe(t.finish);
    expect(clampDay(t.start - 10.4, t)).toBe(t.start);
  });

  it('records a frame a day or a week, the last day always in', () => {
    expect(frameDays(10, 13, 1)).toEqual([10, 11, 12, 13]);
    expect(frameDays(0, 18, 7)).toEqual([0, 7, 14, 18]);
    expect(frameDays(5, 5, 7)).toEqual([5]);
  });

  it('records WebM where it can, MP4 where only that is offered, nothing where neither is', () => {
    expect(videoType((m) => m.startsWith('video/webm'))).toEqual({ mime: 'video/webm;codecs=vp9', ext: 'webm' });
    expect(videoType((m) => m === 'video/mp4')).toEqual({ mime: 'video/mp4', ext: 'mp4' });
    expect(videoType(() => false)).toBeNull();
  });
});

describe('the frames as a ZIP', () => {
  it('stores each file with its CRC, the directory at the end', () => {
    expect(crc32(new TextEncoder().encode('123456789'))).toBe(0xcbf43926);
    const z = zipStore([{ name: 'a.png', bytes: new Uint8Array([1, 2, 3]) }, { name: 'b.png', bytes: new Uint8Array([4]) }]);
    const v = new DataView(z.buffer);
    expect(v.getUint32(0, true)).toBe(0x04034b50);
    const end = z.length - 22;
    expect(v.getUint32(end, true)).toBe(0x06054b50);
    expect(v.getUint16(end + 10, true)).toBe(2);
    const dirAt = v.getUint32(end + 16, true);
    expect(v.getUint32(dirAt, true)).toBe(0x02014b50);
    expect(new TextDecoder().decode(z.slice(30, 35))).toBe('a.png');
    expect([...z.slice(35, 38)]).toEqual([1, 2, 3]);
  });
});
