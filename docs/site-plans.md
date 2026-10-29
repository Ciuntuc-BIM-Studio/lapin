# The Site: site plans and location plans

Asked on 29.10.2026 (decision 169): "a 2D level named Site, a new category, from which we configure site plans (planuri de situație) and location plans (planuri de încadrare în zonă)".

## What it is

- The project browser has **Site** under the levels, with its own drawings; they are not filed in the discipline sets. A new project starts with a **Site plan** (1:500) and a **Location plan** (1:2000); **+** makes more, the copy buttons duplicate them.
- A site view is a plan view (`LapinView`, `kind: plan`) with `site: situation | location`, so everything a plan has works on it: sheets, PDF and DXF, annotations, graphics, phases. It stands on a host storey (`SitePlans.Host`: the first terrain's storey, else the one nearest ±0.00), where what is drawn on it goes.
- **Drawn** (`Views.Gather`, `Views.Spec`): every storey (`StoreyDisplay` is `all`, not halftone), cut 1 m over the model's highest point so all is seen from above, no hidden lines, no view range, storeys or reference level of its own (`view.setRange` and `view.setStoreys` refuse it). Every part except the site's symbolic classes draws by its body, roofs included (so a roof hides the walls under it); what lies wholly below ±0.00 (`SitePlans.Underground`) is left out. The terrain, paths, plants and site features (parcels, neighbours) draw by their symbols, from every storey.
- **Over it** (`SitePlans.Marks`): a north arrow at the top right, to true north (the base point's angle, as the IFC export writes it); a scale bar at the bottom left (about 60 mm, 1/2/5 × 10ⁿ m, five parts); the map's coordinate grid when the project has a survey point: crosses at round eastings and northings every `gridStep` metres (default a tenth of the scale: 50 m at 1:500), the eastings under the lowest crosses, the northings beside the leftmost. `view.setSite {id, site?, gridStep?, northArrow?, scaleBar?}`; in the view's properties under View settings.
- **The parcel** (civil): on a site plan its corners are numbered; the **Parcel coordinates** schedule (`ParcelCoordinates`, kind `parcel`) lists each corner's northing and easting in the project's system (plan coordinates when not tied to the map), the side to the next corner, and the area.
- Tests: `SitePlanTests` (engine), `browserSets.test.ts`; the guide's Site page (EN, RO) and its `site` scene (`site-drawings`, `site-browser`).

## Decided by default (to confirm)

1. Site views are plan views with a host storey, not a new spatial level: no element is contained in IfcSite, and drawing tools on a site plan put elements on its host storey.
2. Underground means wholly below ±0.00, not below the terrain.
3. The location plan is the same drawing at 1:2000 without the corners' numbers; it does not yet fill the building or outline the parcel in red, nor fetch a cadastral map (ANCPI's WMS comes with GeoTIFF, `docs/plan-urban.md`).
4. Old projects get no site views on open; + makes them.
