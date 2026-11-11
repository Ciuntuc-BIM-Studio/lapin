/** The 3D view's visual styles (report #142): shaded, shaded with edges, X-ray (see-through with edges), wireframe. */

export type VisualStyle = 'shaded' | 'edges' | 'xray' | 'wireframe';

/** The style View → Visual style goes to next. */
export function nextVisualStyle(s: VisualStyle): VisualStyle {
  return s === 'shaded' ? 'edges' : s === 'edges' ? 'xray' : s === 'xray' ? 'wireframe' : 'shaded';
}

/** How opaque an element is drawn in X-ray (the selection stays solid). */
export const XRAY_OPACITY = 0.25;

export const XRAY_NOTICE = 'The 3D view shows every surface see-through, with its edges; the selection stays solid. Transparency per category is in Visibility / Graphics.';
