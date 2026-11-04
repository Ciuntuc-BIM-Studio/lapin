import { describe, expect, it } from 'vitest';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { legendBands, WallLayersLegend } from './WallLayersLegend';
import { faceLetters } from '../../viewport/faceMarks';

const layers = [{ thickness: 0.02, core: false }, { thickness: 0.25, core: true }, { thickness: 0.015, core: false }];

describe('the Wall types legend (report #79)', () => {
  it('draws the layers as bands from the L face down to the R face, numbered as the table, the core in the accent', () => {
    const svg = renderToStaticMarkup(createElement(WallLayersLegend, { layers }));
    // Top to bottom: the last layer (the L face) first, layer 1 on the R face.
    expect([...svg.matchAll(/data-layer="(\d)"/g)].map((m) => m[1])).toEqual(['3', '2', '1']);
    expect(svg).toMatch(/data-layer="2"><rect[^>]*class="band core"/);
    expect(svg).toMatch(/data-face="right">R</);
    expect(svg).toMatch(/data-face="left">L</);
  });

  it('gives each band room for its number and the thicker layers more', () => {
    const h = legendBands(layers);
    expect(Math.min(...h)).toBeGreaterThanOrEqual(11);
    expect(h[1]).toBeGreaterThan(h[0]);
    expect(legendBands([{ thickness: 0, core: true }])).toEqual([11]);
  });

  it('names the faces in the interface\'s language, the same letters as the selected wall in plan', () => {
    expect(faceLetters('en')).toEqual({ right: 'R', left: 'L' });
    expect(faceLetters('ro')).toEqual({ right: 'D', left: 'S' });
    expect(faceLetters('de')).toEqual({ right: 'R', left: 'L' });
  });
});
