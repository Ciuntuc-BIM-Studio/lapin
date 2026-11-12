/**
 * Snapping to what a plan draws (the forum's topic #30): a plan whose style leaves out the walls' or the floors' finish
 * layers (DrawingDto.hiddenFinishes) offers no snap on those layers' cut pieces (PlanItemDto.sub), so the cores' faces
 * are what a dimension or a wall drawn along them meets.
 */
import type { PlanItemDto } from '../engine/protocol';

/** The category the engine's finish rule names an element's class by (LineStyles.CategoryOf: walls, slabs). */
const CATEGORY: Record<string, string> = { IfcWall: 'walls', IfcWallStandardCase: 'walls', IfcCurtainWall: 'walls', IfcSlab: 'slabs' };

/** Whether a plan item of an element of this class is drawn by a plan that leaves out these categories' finishes. */
export function drawnInPlan(type: string, item: PlanItemDto, hiddenFinishes: readonly string[] | undefined): boolean {
  if (item.sub !== 'finishes' || !hiddenFinishes?.length) return true;
  const category = CATEGORY[type];
  return !category || !hiddenFinishes.includes(category);
}
