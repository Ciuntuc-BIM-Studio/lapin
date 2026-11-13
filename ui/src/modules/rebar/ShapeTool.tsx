/** The tool bar of "Bar of a shape": the project's bar shape the next click lays. */

import { useEffect } from 'react';
import { useShapeLibrary } from './barShapes';
import { useShapePick } from './partitions';
import { PartitionOptions } from './partitions';

export function ShapeToolOptions() {
  const lib = useShapeLibrary();
  const shapeId = useShapePick((s) => s.shapeId);
  const shapes = lib?.shapes ?? [];
  // The first shape stands when none is chosen (or the one chosen was deleted).
  useEffect(() => { if (shapes.length && !shapes.some((s) => s.id === shapeId)) useShapePick.setState({ shapeId: shapes[0].id }); }, [shapes.map((s) => s.id).join(','), shapeId]);
  return (
    <>
      <span className="cad-layer on">
        <span className="small muted">Shape</span>
        {shapes.length === 0
          ? <span className="small muted">none yet: Bar shapes…</span>
          : <select value={shapeId} data-field="rebar-tool-shape" onChange={(e) => useShapePick.setState({ shapeId: e.target.value })}>{shapes.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}</select>}
      </span>
      <PartitionOptions />
    </>
  );
}
