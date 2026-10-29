---
title: The site and its data
order: 52
summary: Bring the survey, the parcel, the neighbours and an orthophoto in real coordinates, from DXF, CSV, GeoJSON, CityJSON and a georeferenced image.
---

The **Site** tab holds the ground the building stands on: the terrain, the parcel, the neighbouring buildings and an aerial photo, each where it really is. The **Import** group brings them from the files surveyors and the cadastre hand over.

![The Site tab](shots/ribbon-site.webp)

## Real coordinates

A survey comes in map coordinates: eastings and northings, hundreds of kilometres from zero. Lapin keeps the model near its origin and ties it to the map through the **survey point**. The first import into a project does this for you: the survey point goes to the plan's origin, on the file's first coordinates rounded to the metre, and every later import lands in the same place.

**Manage → Location** shows the **coordinate system** the eastings and northings are in, and the base and survey points. Lapin knows Stereo 70 (EPSG:3844, Romania's national grid), UTM zones 34 and 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) and longitude and latitude (WGS 84, EPSG:4326). A file in another of these systems is converted when it comes in: a GeoJSON in longitude and latitude lands on the Stereo 70 plan where it belongs. The IFC export writes the system and the survey point, so other programs place the model on the map too.

![Manage → Location: the coordinate system and the survey point](shots/site-location.webp)

## Importing

- **Terrain from points** reads a CSV or text file from a survey: x y z, or number northing easting elevation, one point a line.
- **Points from DXF** reads a survey drawing: POINT entities with their heights, blocks inserted at a height or carrying it as an attribute, heights written beside points ("245.37" next to its cross), and contour lines at their elevation. When the drawing has points on several layers, Lapin lists them with how many points each holds, and you choose which to take. The points make a terrain.
- **GeoJSON** brings parcels, lines and points. A file with one polygon gives the **parcel**; one with several gives areas. The parcel also outlines the terrain's parcel, with its area. Files from the cadastre in Stereo 70 say so in the file, and Lapin reads it; a file that says nothing is longitude and latitude, as the GeoJSON standard requires.
- **CityJSON** brings the neighbouring buildings with their surfaces, at their place and height. They are read-only references; they will cast shadows in the sun study.
- **Orthophoto** lays an aerial photo under the site. Pick the image (JPEG or PNG, up to 8 MB) together with its world file (`.jgw`, `.pgw` or `.wld`): the world file says where the image lies. The photo is drawn under the plan and wears the terrain in 3D.

![The site plan: the orthophoto, the survey terrain's contours, the parcel and the neighbour's footprint](shots/site-plan.webp)

![The same site in 3D: the photo on the ground, the neighbour at its height](shots/site-3d.webp)

Everything imported is kept in the project file. A feature takes its name from the file (a parcel's cadastral number, when the file has one) and keeps the file's other properties with it; select it to see its area or length.

## Site plans and location plans

The project browser has a **Site** under the levels: the site's own drawings, which belong to no storey. A new project starts with two:

- the **Site plan** (Romanian *plan de situație*), at 1:500: the whole model seen from above, every storey at once, roofs and all, with the terrain, the paths, the plants, the parcel and the neighbours. What lies wholly under ±0.00 (the foundations) is left out, nothing is cut and nothing hidden is drawn. The parcel's corners are numbered;
- the **Location plan** (*plan de încadrare în zonă*), at 1:2000: the same drawing for the site in its area, over the orthophoto, without the corners' numbers.

**+** beside Site makes another of either; the copy buttons duplicate one. Both carry a **north arrow** pointing to true north (Manage → Location: the angle to true north), a **scale bar** and, when the project is tied to the map, the **coordinate grid**: crosses where round eastings and northings meet, every tenth of the scale (50 m at 1:500, 200 m at 1:2000), with their values. Select the view in the browser and set them in its properties, under **View settings**: what it is (site plan or location plan), the grid's step (0: none), the arrow and the bar.

![A site plan: the house from above on its parcel, the corners numbered, the map's grid, the north arrow and the scale bar](shots/site-drawings.webp)

![The Site in the project browser](shots/site-browser.webp)

The **Parcel coordinates** schedule (Schedules → + → Parcel coordinates) is the parcel's coordinate inventory: each corner as the site plan numbers it, its northing and easting in the project's system, the side to the next corner, and the area. Like any schedule it goes on a sheet beside the site plan, and out as CSV or XLSX.

## Planning rules and the sun (Lapin Urban)

The **Lapin Urban** edition adds a **Planning** group to this tab: the parcel's planning rules (POT, CUT, the maximum heights, the setbacks from the street, the sides, the rear and the road's axis), the **buildable volume** they leave, the indicators the model reaches against them, and the **sun study**: the sun and its shadows in 3D for any day and hour at the project's place, and shadow plans for the equinox and the solstices.
