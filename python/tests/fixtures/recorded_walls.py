# Recorded in Lapin (View → Script → Record): each line is a step done in the window.
# Run it in the Script panel (one undo step), or from a terminal with Lapin open.
import lapinbim as lapin

storey1 = lapin.model.storey("Ground floor", 0)
wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)
wall2 = lapin.architecture.add_wall(storey_id=storey1, x1=5, y1=0, x2=5, y2=4)
lapin.element.update(id=wall2.id, attrs={"height": 3.1})
with lapin.batch("Names"):
    lapin.element.rename(id=wall1.id, name="South")
