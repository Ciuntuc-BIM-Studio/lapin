# Urban planning: the buildable volume and the sun study (plan)

Asked on 28.10.2026 (decision 163): a premium module that draws the parcel (or takes it from survey data) in real coordinates, holds the planning rules in a configurator — POT, CUT, maximum height, the least distances from the property lines and from the road's axis — and generates from them a volume named the **buildable volume**; with it, a **sun study** (BimGo's, the MIT-0 Revit walkthrough add-on, as the reference: sun and shadows by date and time, time-lapses, direct sun hours as an early indicator). The site's data come in through imports that belong to the free Site tab: an orthophoto, GeoJSON, CityJSON, points from DXF, CSV from surveys.

## The split

| Part | Edition | Where |
|---|---|---|
| Coordinate systems: the project's CRS, Stereo 70 (EPSG:3844), WGS 84 (EPSG:4326), UTM 34N/35N, eastings and northings ↔ the project's plan | free (platform) | `Lapin.Platform/Geo` |
| Imports into the Site tab: survey points from CSV (exists) and DXF, GeoJSON (parcels, lines, points), CityJSON (neighbouring buildings), an orthophoto (an image with its world file) | free (`civil`) | `Lapin.Civil` |
| The parcel with its sides (street, neighbour, rear), the planning rules, the buildable volume, the indicators (POT, CUT, height) checked against the model | premium (`urban`) | private repo `../lapin-urban` |
| The sun study: the sun's position for the site, date and time; shadows in 3D; shadow plans at chosen hours; time-lapse; direct sun hours on the parcel and the facades | premium (`urban`) | private repo `../lapin-urban` |

## Coordinates (free, stage 1)

The project already has a base point (its origin, elevation and angle to true north) and a survey point (where eastings and northings are known), as Revit has, and the IFC export writes them as `IfcMapConversion`. What is new:

- **The project's CRS** (`location.crs`: `EPSG:3844` by default for a project in Romania, or `EPSG:32634/32635`, or none): written in `IfcProjectedCRS` (name, geodetic datum, map projection) on export.
- **`Geo`** in the platform: the oblique stereographic projection (EPSG method 9809, as PROJ's `sterea`), transverse Mercator (UTM, Krüger series), the Helmert transformation (coordinate frame) between Krasovsky 1940 / Pulkovo 1942(58) and WGS 84 with the parameters EPSG uses for Romania (2.329, −147.042, −92.080 m; 0.309, −0.325, −0.497″; 5.69 ppm, about 3 m). Plan ↔ map ↔ latitude and longitude, both ways. Tested against PROJ's values.
- **Importing in real coordinates**: a file in eastings and northings lands where it belongs in the project's plan through the survey point; the first import into a project with no survey point sets it (the file's first point at the project's origin, the CRS from the file or asked), so the model stays near the origin and the coordinates stay exact.

## The imports (free, stage 1)

- **Survey points from DXF**: `POINT` entities with their elevation, and points whose elevation is a text beside them (the usual Romanian survey drawing: a cross and "245.37"), by layer; into a terrain like the CSV points.
- **GeoJSON**: polygons as parcels (the first one, or each as a parcel outline drawn on the site plan), lines as drafted polylines, points as survey points when they have a third coordinate; its `crs` member read (Romanian cadastral exports are in EPSG:3844), else WGS 84 as the standard says.
- **CityJSON** (1.0–2.0): the buildings' surfaces as neighbouring buildings (reference elements with stored bodies, read-only, on their own layer), at their place; the shadows they cast count in the sun study.
- **Orthophoto**: a JPEG or PNG with its world file (`.jgw`, `.pgw`, `.wld`) or its corners typed, drawn under the site plan and draped on the terrain in 3D; kept in the `.lapin` file. GeoTIFF and online map services (ANCPI's WMS) come later.

## The buildable volume (premium, stage 2)

- **The parcel**: drawn (a closed outline on the site plan) or taken from an import; each side has a kind — **street** (the road's axis is drawn or picked as a line), **side** neighbour, **rear** — set by clicking it.
- **The rules** (the configurator, one per parcel, values from the PUG/PUZ): POT % and CUT; the maximum height (to the cornice and to the ridge, or the floors: P+2E…, with a storey height); the least setbacks from each kind of side (front, sides, rear), the least distance from the road's axis; the side setback as half the height but at least 3 m (as Romanian rules often say), on or off; the alignment on the street (obligatory or not).
- **The volume** (`LapinBuildableVolume`, exported as `IfcSpatialZone` USERDEFINED "BuildableVolume" in IFC4 and as `IfcSpatialZone` in 4.3): the parcel taken in by each side's setback and by the road axis's distance, up to the maximum height; where a setback grows with height (H/2), the volume steps or slopes back. Drawn half-transparent in 3D, outlined on plans, sections and elevations. Its quantities: the footprint area allowed (POT × parcel), the floor area allowed (CUT × parcel), the volume.
- **The indicators**: the model's buildings against the rules — the footprint (the ground floor's outline), the floor areas (the storeys' slabs or rooms), POT and CUT reached, the highest point, the elements standing out of the buildable volume (marked). A table and a schedule source; on a sheet as the planning indicators' table (Romanian "bilanț teritorial").

## The sun study (premium, stage 3)

- **The sun's position** from the site's latitude and longitude (from the CRS and the survey point), the date and the local time (NOAA's algorithm, with the time zone and summer time), and the project's true north.
- **3D**: the sun lights the model and casts shadows (three.js shadow maps), with a date and a time slider, play for a day or a year; the terrain, the neighbours (CityJSON) and the buildable volume cast and take them.
- **Shadow plans**: the shadows on the ground at the hours chosen (e.g. 21 March, 21 June, 21 December at 9, 12, 15), drawn on a plan view as hatched outlines, printable on sheets — what Romanian approvals ask for ("studiu de însorire").
- **Sun hours**: the hours of direct sun on a grid over the parcel and on the facades for a day, as a coloured film, with the minimum the rules ask (e.g. 1½ hours at the winter solstice for neighbours' windows) checked. An early indicator, not a compliance calculation, as BimGo says.

## Open questions (answered by default)

1. **Where the premium code lives**: a private sibling repo `../lapin-urban` like `../lapin-mep` (default), built through `products/lapin-urban.json` with `"external"`; or in `lapin-mep` renamed as one premium repo.
2. **Default CRS**: Stereo 70 for a new project (Lapin's users are mostly in Romania), changeable; none for a project with no georeference.
3. **H/2 side setback**: on by default with 3 m minimum, off for a parcel whose rules say otherwise.
4. **Height**: maximum height to the cornice and the ridge both, the volume above the cornice as a 45° roof envelope when both are given (default), or a flat top at the maximum.
5. **Shadow plans' default days**: the equinox, the summer and the winter solstices, at 9, 12 and 15.

## Progress

- **Stage 1 (free) done (28.10.2026):** `GeoRef` (Stereo 70, UTM, Web Mercator, WGS 84; `GeoTests` against PROJ), the project's system (`location.crs`, `project.geo`, `core.crs`, `IfcProjectedCRS`), `civil.importDxfPoints` (+ `civil.dxfPointLayers`), `civil.importGeoJson`, `civil.importCityJson`, `civil.addOrthophoto` (`GeoImportTests`); `LapinSiteFeature` and `LapinOrthophoto`; the Site tab's Import group; the orthophoto under the plan and on the terrain in 3D; the guide's page `docs/user/site.md` with its scene.
- **Stage 2 (premium) done (28.10.2026)**, in `../lapin-urban` (a local private repository for now): `LapinBuildableVolume` (the rules as its attributes; the parcel a site feature's or its own outline; `sides`, `roadAxisLine`), the body as the parcel raised to the top less, per side, a loft of capsules (exact for a setback growing linearly with height: the H/2 side rule, the 45° envelope above the cornice under a ridge) and the road axis's band, through manifold; `urban.addBuildableVolume`, `urban.setRules`, `urban.setSide`, `urban.setRoadAxis`, `urban.plot` (sides and indicators), the `urban-indicators` schedule; the Planning group in the Site tab, the configurator (rules, sides, indicators), the volume see-through in 3D; `VolumeTests` (6, volumes checked by hand). Its pictures: `../lapin-urban/docs/scenes.mjs`, run by `scripts/user-docs/run.mjs --product lapin-urban --scenes …`.
- **Stage 3 (premium) done (28.10.2026)**, except the sun hours: `Sun` (NOAA's solar position with refraction, European summer time, the direction in the plan by the project's true north; the place from the project's system and survey point, Bucharest when it has none), `urban.sun`; the 3D view's sun through `ui/src/platform/sunlight.ts` (open: a module aims it, the view lights and casts shadows with a shadow-catching ground; what is seen through casts none), the Sun study dialog (day, the equinox and solstices, an hour slider, the day played); the shadow study (`LapinShadowStudy`: a day, its hours, the model's or the volume's shadows or both, neighbours from CityJSON casting too), each hour's shadow as the union of its parts' convex projections, drawn filled under the model on the storey's plans with its hour, printable; `urban.shadows`; `SunTests` (noon heights over Bucharest, solar noon with summer time, a box's shadow area within 1 %).
- Next: the direct sun hours on a grid over the parcel and on facades; GeoTIFF and ANCPI's WMS for orthophotos; the volume's steps by storey (floors) besides its slopes; the lapin-urban repository on GitHub (private).
