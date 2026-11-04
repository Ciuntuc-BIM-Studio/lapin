import { describe, expect, it } from 'vitest';
import { joinTargets, mainArgs } from './gas';

describe('a node joining another system', () => {
  const systems = [
    { id: 'h', name: 'Heating', service: 'heating' },
    { id: 'g', name: 'Gas', service: 'gas' },
    { id: 'g2', name: 'Gas main', service: 'gas' },
    { id: 'w', name: 'Water', service: 'coldWater' },
  ];

  it('offers the systems of the services its open ports take, never its own', () => {
    const boiler = [
      { key: 'supply', service: 'heating', taken: true },
      { key: 'return', service: 'heating', taken: true },
      { key: 'gas', service: 'gas', taken: false },
    ];
    expect(joinTargets(boiler, 'heating', systems).map((s) => s.id)).toEqual(['g', 'g2']);
  });

  it('offers nothing once the port has its run, nor for an any-service port', () => {
    expect(joinTargets([{ key: 'gas', service: 'gas', taken: true }], 'heating', systems)).toEqual([]);
    expect(joinTargets([{ key: 'in', service: '*', taken: false }], 'gas', systems)).toEqual([]);
  });
});

describe('a gas main', () => {
  it('follows an element picked, or the points drawn, at its offset and cover', () => {
    expect(mainArgs({ alongId: 'road' }, 1.5, 0.9)).toEqual({ alongId: 'road', offset: 1.5, depth: 0.9 });
    expect(mainArgs({ points: [{ x: 0, y: 0 }, { x: 10, y: 2 }] }, 0, 1, 's1')).toEqual({ points: [[0, 0], [10, 2]], offset: 0, depth: 1, storeyId: 's1' });
  });
});
