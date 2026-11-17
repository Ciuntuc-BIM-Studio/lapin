/** New levels (issue #83): the storeys storey.createMany makes, as its dialog previews them. */

/** The storeys made from `base` (metres) one on another, each `heights[i]` high, named by `pattern` ({n} from `start`). */
export function levelPlan(base: number, heights: readonly number[], pattern: string, start: number): { name: string; elevation: number; height: number }[] {
  const name = pattern.trim() || 'Level {n}';
  let z = base;
  return heights.map((h, i) => {
    const level = { name: name.split('{n}').join(String(start + i)), elevation: z, height: h };
    z = Math.round((z + h) * 1e6) / 1e6;
    return level;
  });
}

/** Whether a pattern names several storeys apart: it holds {n}, or there is one storey. */
export const namesApart = (pattern: string, count: number) => count <= 1 || (pattern.trim() || 'Level {n}').includes('{n}');
