/** Placing a symbol (report #164): it repeats, and Enter, a right-click and Esc end it; Space turns. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), useSession: { getState: vi.fn(() => ({ nodes: new Map(), error: null })), setState: vi.fn() } }));
vi.mock('../../../engine/session', () => engine);
const sym = vi.hoisted(() => ({
  placeSymbol: vi.fn(async () => ({ id: 'placed' })),
  previewSymbol: vi.fn(async () => ({ pens: [] })),
  getSymbol: vi.fn(async () => ({ id: 's1', name: 'North', builtin: false, uses: 0, def: { scale: 'annotation', attributes: [] } })),
  listSymbols: vi.fn(async () => ({ items: [{ id: 's1', name: 'North' }], categories: [] })),
  lastSymbol: vi.fn(() => 's1'), rememberSymbol: vi.fn(),
  explodeSymbol: vi.fn(), flipSymbol: vi.fn(),
}));
vi.mock('./symbols', () => sym);

import { cancel, feedPoint, finishStep, spaceStep, startCommand, submit, useCad } from '../../../viewport/cad/session';
import { usePlace } from './symbolTools';

const P = (x: number, y: number): Vec2 => ({ x, y });

beforeEach(() => {
  sym.placeSymbol.mockClear();
  cancel(true);
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null }, history: [] });
});

describe('SYMBOL', () => {
  it('goes on placing after each click, and says how to finish', async () => {
    await startCommand('SYMBOL');
    await feedPoint(P(0, 0));
    await feedPoint(P(1, 0));
    expect(sym.placeSymbol).toHaveBeenCalledTimes(2);
    expect(useCad.getState().command).toBe('SYMBOL');
    expect(useCad.getState().step?.hint).toMatch(/Enter or Esc to finish/);
  });

  it('Space turns the symbol and keeps placing', async () => {
    await startCommand('SYMBOL');
    expect(usePlace.getState().angle).toBe(0);
    expect(await spaceStep()).toBe(true);
    expect(usePlace.getState().angle).toBe(90);
    expect(useCad.getState().command).toBe('SYMBOL');
  });

  it('Enter on an empty line ends it, and turns nothing', async () => {
    await startCommand('SYMBOL');
    await submit('');
    expect(useCad.getState().command).toBeNull();
    expect(useCad.getState().step).toBeNull();
    expect(usePlace.getState().angle).toBe(0);
  });

  it('a right-click ends it', async () => {
    await startCommand('SYMBOL');
    await feedPoint(P(0, 0));
    expect(await finishStep()).toBe(true);
    expect(useCad.getState().command).toBeNull();
  });

  it('Esc ends it', async () => {
    await startCommand('SYMBOL');
    await feedPoint(P(0, 0));
    cancel();
    expect(useCad.getState().command).toBeNull();
    expect(useCad.getState().step).toBeNull();
  });
});
