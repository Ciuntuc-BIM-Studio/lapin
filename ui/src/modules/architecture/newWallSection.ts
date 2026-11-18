/**
 * The section of the wall the Wall tool would draw now (decisions 692 and 693): the type picked (else the project's first) laid
 * out across its axis, and how far left of the axis the line clicked lies, as the bar's line and Space leave it.
 */

import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { ofClass, typeLayers } from '../../engine/model';
import { useWallPrefs } from './prefs';
import { bandsOf, lineLeft, type Band } from './wallLine';

export function newWallSection(): { bands: Band[]; left: number } {
  const s = useSession.getState();
  const wanted = useUi.getState().wallTypeId;
  const type = (wanted ? s.nodes.get(wanted) : undefined) ?? ofClass(s, 'IfcWallType')[0];
  const bands = bandsOf(type ? typeLayers(s, type.id) : []);
  const { line, lineOffset } = useWallPrefs.getState();
  return { bands, left: lineLeft(bands, line, lineOffset) };
}
