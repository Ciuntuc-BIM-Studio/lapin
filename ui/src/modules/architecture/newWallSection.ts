/**
 * The section of the wall the Wall tool would draw now (decisions 692 and 693): the type picked (else the project's first) laid
 * out across its axis (mirrored when Space flipped it on the core's centre), and how far left of the axis the line clicked lies, as the bar's line and Space leave it.
 */

import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { ofClass, typeLayers } from '../../engine/model';
import { drawsFlipped, useWallPrefs } from './prefs';
import { bandsOf, lineLeft, mirrorBands, type Band } from './wallLine';

export function newWallSection(): { bands: Band[]; left: number } {
  const s = useSession.getState();
  const wanted = useUi.getState().wallTypeId;
  const type = (wanted ? s.nodes.get(wanted) : undefined) ?? ofClass(s, 'IfcWallType')[0];
  const prefs = useWallPrefs.getState();
  const { line, lineOffset } = prefs;
  // Mirrored by Space on the core's centre (forum topic 238): the exterior to the left.
  const laid = bandsOf(type ? typeLayers(s, type.id) : []);
  const bands = drawsFlipped(prefs) ? mirrorBands(laid) : laid;
  return { bands, left: lineLeft(bands, line, lineOffset) };
}
