import { describe, expect, it } from 'vitest';
import { boundsMetricSize, float32ToBase64, lonLatToMercator, lonLatToTile, mercatorCorners, mercatorToLonLat, parseOsmHeight } from './mapTiles';

describe('mapTiles', () => {
  it('round-trips WGS 84 ↔ Web Mercator for Bucharest', () => {
    const [x, y] = lonLatToMercator(26.1025, 44.4268);
    const [lon, lat] = mercatorToLonLat(x, y);
    expect(lon).toBeCloseTo(26.1025, 6);
    expect(lat).toBeCloseTo(44.4268, 6);
  });

  it('computes slippy tile coordinates and metric bounding box size', () => {
    const [tx, ty] = lonLatToTile(0, 0, 1);
    expect(tx).toBeCloseTo(1, 6);
    expect(ty).toBeCloseTo(1, 6);

    const b = { minLon: 26.100, minLat: 44.425, maxLon: 26.105, maxLat: 44.428 };
    const { widthM, heightM } = boundsMetricSize(b);
    expect(widthM).toBeGreaterThan(350);
    expect(widthM).toBeLessThan(450);
    expect(heightM).toBeGreaterThan(300);
    expect(heightM).toBeLessThan(370);

    const corners = mercatorCorners(b);
    expect(corners).toHaveLength(4);
    expect(corners[0][0]).toBeLessThan(corners[1][0]);
    expect(corners[0][1]).toBeGreaterThan(corners[3][1]);
  });

  it('encodes Float32Array to base64 with exact byte length', () => {
    const arr = new Float32Array([100.5, 200.25, -5.5, 0]);
    const b64 = float32ToBase64(arr);
    const raw = atob(b64);
    expect(raw.length).toBe(16);
  });

  it('parses OpenStreetMap height strings in metres and feet', () => {
    expect(parseOsmHeight('12')).toBe(12);
    expect(parseOsmHeight('14.5 m')).toBe(14.5);
    expect(parseOsmHeight('30 ft')).toBeCloseTo(9.144, 3);
    expect(parseOsmHeight('invalid')).toBeUndefined();
  });
});
