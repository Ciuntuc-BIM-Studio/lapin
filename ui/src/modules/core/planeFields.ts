/** Which of a view's fields its kind shows (docs/plan-studio-thick.md): a reference plane's place and turn, an elevation's sketch plane. */

/** A reference plane's own fields: its origin and its three angles, and how large the 3D view draws it. */
export const PLANE_KEYS = ['x', 'y', 'z', 'bearing', 'tilt', 'spin', 'width', 'height'];

/** Whether a view of this kind shows the field: a reference plane's place only on a plane, the sketch plane depth only on an elevation. */
export function viewShowsField(kind: unknown, key: string): boolean {
  if (PLANE_KEYS.includes(key)) return kind === 'plane';
  if (key === 'planeDepth') return kind === 'elevation';
  return true;
}
