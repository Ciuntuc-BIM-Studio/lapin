/**
 * A column placed by an insertion anchor (forum topic 226): the point clicked is where the chosen point of the column's box
 * lies (one of its four corners, four edge middles or its centre), and the column's insertion point goes where that puts it.
 * The anchor is a placement aid: the column is stored as ever, by its insertion point and rotation.
 */

import { useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import type { Vec2 } from '../../platform/module';
import { anchorFor, insertionAt } from '../../platform/anchor';
import { extents, type ColumnTypeProps } from './memberDims';
import { useTypePick } from './typePick';

export const COLUMN_TOOL = 'structure.column';

/** The section's box in the column's own axes, of the type the tool would place with (the one picked in its bar, else the project's first). */
export function columnBox(): { x0: number; x1: number; y0: number; y1: number } {
  const s = useSession.getState();
  const id = useTypePick.getState().column;
  const type = (id ? s.nodes.get(id) : undefined) ?? ofClass(s, 'IfcColumnType')[0];
  return extents((type?.props as ColumnTypeProps | undefined)?.section);
}

/** Where the column's insertion point goes for a click at `at`. */
export const columnInsertion = (at: Vec2, box = columnBox()): Vec2 => insertionAt(at, anchorFor(COLUMN_TOOL), box);

/** The column as it would stand for a cursor at `at`: its box, and a cross at the anchor (the cursor). */
export function columnGhost(at: Vec2): Vec2[][] {
  const b = columnBox(), o = columnInsertion(at, b);
  const P = (x: number, y: number): Vec2 => ({ x: o.x + x, y: o.y + y });
  const k = Math.min(0.15, Math.max(b.x1 - b.x0, b.y1 - b.y0) / 4);
  return [
    [P(b.x0, b.y0), P(b.x1, b.y0), P(b.x1, b.y1), P(b.x0, b.y1), P(b.x0, b.y0)],
    [{ x: at.x - k, y: at.y }, { x: at.x + k, y: at.y }],
    [{ x: at.x, y: at.y - k }, { x: at.x, y: at.y + k }],
  ];
}
