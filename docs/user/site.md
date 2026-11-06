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

## Roads, curves and linework

A **road** or a **path** follows its axis. In **Corridor…** (CR), the **Curves** tab gives each vertex of the axis a radius with a clothoid in and out, as a road designer lays a curve; a vertex left at 0 stays a corner, and curves that do not fit between their neighbours are refused with the reason. The stations, the long section and the cross-sections follow the curved axis, and IFC 4.3 writes it as an alignment.

CAD linework becomes site elements: **Road from linework** (LR) and **Breakline from linework** (BL) take a line or polyline drafted or brought in by a DXF, follow every line on its layer that meets it end to end, and make a road along it or a breakline of the terrain. When the lines carry heights (a DXF's 3D lines), they give the road's profile or the breakline's levels. Such lines are kept with their heights and can be shown in 3D (the *linework* category).

## Site plans and location plans

The project browser has a **Site** under the levels: the site's own drawings, which belong to no storey. A new project starts with two:

- the **Site plan** (Romanian *plan de situație*), at 1:500: the whole model seen from above, every storey at once, roofs and all, with the terrain, the paths, the plants, the parcel and the neighbours. What lies wholly under ±0.00 (the foundations) is left out, nothing is cut and nothing hidden is drawn. The parcel's corners are numbered;
- the **Location plan** (*plan de încadrare în zonă*), at 1:2000: the same drawing for the site in its area, over the orthophoto, without the corners' numbers; the project's buildings are filled in red and the parcel is outlined in red, so the site stands out from its neighbours.

**+** beside Site makes another of either; the copy buttons duplicate one. Both carry a **north arrow** pointing to true north (Manage → Location: the angle to true north), a **scale bar** and, when the project is tied to the map, the **coordinate grid**: crosses where round eastings and northings meet, every tenth of the scale (50 m at 1:500, 200 m at 1:2000), with their values. Select the view in the browser and set them in its properties, under **View settings**: what it is (site plan or location plan), the grid's step (0: none), the arrow, the bar, and **Highlight the project** (the red fill and outline, on by default in a location plan, for a site plan too when you want it).

**The map under them.** The orthophoto is drawn under every site plan and location plan, whatever storey it was imported on, and prints with them on sheets and in the PDF. For the cadastral plan or the surveyor's drawing, open the site plan and use **Import DXF…** with **Placed: by the map's coordinates**. Lapin offers this itself when the file's coordinates look like a map's, for example Stereo 70: the drawing's lower-left corner tens of kilometres from zero, in the file's own unit or in metres. A plan of your own in millimetres is placed by its origin even on a site plan, in the unit the file says. The drawing then lands where its eastings and northings say, through the project's survey point and its angle to true north. If the project is not yet tied to the map, the drawing ties it. Tick **As a link** to keep it read-only and reload it when the surveyor sends a new one. Import it once into each drawing that needs it.

![A location plan: the house filled in red, the parcel outlined in red, among its neighbours](shots/site-location-plan.webp)

![A site plan: the house from above on its parcel, the corners numbered, the map's grid, the north arrow and the scale bar](shots/site-drawings.webp)

![The Site in the project browser](shots/site-browser.webp)

The **Parcel coordinates** schedule (Schedules → + → Parcel coordinates) is the parcel's coordinate inventory: each corner as the site plan numbers it, its northing and easting in the project's system, the side to the next corner, and the area. Like any schedule it goes on a sheet beside the site plan, and out as CSV or XLSX.

## Trees and shrubs

**Plant** (PN) sets one plant at a click, **Planting zone** (PZ) sows an outline with a mix of species at a density; select either and choose its species in the properties. The species list is a library of plants native or traditional in Romania, in groups: **fruit trees** (apple, pear, plum, sweet and sour cherry, apricot, quince), **conifers** (silver fir, Norway spruce, Scots and black pine), **deciduous trees** (walnut, lime, birch, maple), **shrubs** (hazel, cornelian cherry, elder, dog rose, lilac, sea buckthorn, redcurrant, thuja, boxwood) and **ground cover**. Each comes with its Latin name, its usual height and crown, and is shown by its Romanian name when the interface is in Romanian. The planting schedule lists the Latin and Romanian names beside each species.

In plan each plant is drawn from above with a thin line, as wide as its crown, by its outline alone: a walnut or a lime a softly lobed crown, a fruit tree a circle, a fir or a spruce a star, a pine a ragged ring, a shrub a small cloud, a clipped hedge plant a circle, with a small cross at a tree's trunk. Elevations and sections draw each plant as one outline: a tree's crown on its trunk, a conifer's cone in tiers, a shrub's dome. A few lines each, so a site plan of hundreds of trees stays light. In 3D conifers are cones, the other trees have a rounded crown on their trunk and shrubs a low dome.

![A small orchard in plan: fruit trees, a walnut, conifers and shrubs](shots/planting-symbols.webp)

Set the view style's **Entourage in 2D** to **detailed** for the landscape drawing's symbols, in black: a fruit tree as a lobed crown with forked branches, a walnut or a lime as a scalloped crown round a dark core, a fir or a spruce as a star of needle sprays, a pine as a round tuft of needles, a shrub as a small cloud. Lapin draws each symbol itself, so neighbouring trees differ a little and a zone never looks stamped; at 1:1000 and smaller (a location plan) only the crowns' outlines are drawn.

![The same orchard drawn detailed](shots/planting-symbols-detailed.webp)

## People and cars

**Entourage** (EN) puts a person or a car at a click; choose which in the command line's bar while the tool is on: a **man standing**, a **woman walking**, a **child**, a **person on the phone**, a **car** or an **SUV / van**. **Entourage row** (ER) puts a row of them along a line you draw: cars every 2.5 m, parked side by side across it with their fronts to its left, people every 1.2 m, walking along it. Each stands on the terrain under it, or on its storey's floor where there is no terrain.

![People at the door and cars parked beside the house](shots/entourage-3d.webp)

Select one to change it in the properties: its kind, its **rotation**, its **height** (its kind's usual size by default), how far **above the ground** it stands (on a deck or a balcony) and the **colour** of its clothes or body. Move, rotate, mirror and copy it as any element.

In 3D they are simple figures: people with their limbs, clothes, hair and shoes, cars with their glass, wheels and lights. Drawings keep them simple, as an architect draws entourage, with a thin line: plans and site plans from above, a person's shoulders and head, a car's outline with its corners rounded and its windscreen; elevations and sections as silhouettes, one outline each, a person's figure, a car's side or its front as the view sees it, never the lines of their bodies.

![In plan](shots/entourage-plan.webp)

![In the south elevation](shots/entourage-elevation.webp)

![A car's properties](shots/entourage-properties.webp)

How people, cars and plants draw in 2D is a setting of the view style, **Entourage in 2D** (in the view's properties or **View → View settings**; for a whole style, **Manage → View styles**): **outlines** (the default), **abstract** (a person a circle in plan and a head over a body in an elevation, a car a rounded box, a tree a circle with its trunk), **detailed** (the plants' landscape symbols and every line of the bodies, with the view's sun shadows, as Lapin drew them before) or **not drawn**. **Silhouette fill** fills the silhouettes of elevations and sections light grey or pale green. They draw with pen 2 (0.13 mm at 1:100) unless **Manage → Object styles** gives the **Entourage** category another, and **Visibility / Graphics** hides that category in a view. Sheets, the PDF and DXF print them as the view draws them; their 3D bodies never change. Projects made before draw them as outlines too.

They give drawings and pictures their scale but are no part of the building. An IFC or Fragments export (File → Export…) leaves them out unless you tick **Entourage** in its list of kinds; IFC writes them as IfcBuildingElementProxy "Entourage". The 3D exports (glTF, USD, COLLADA) take them. Clash tests leave them out.

The figures follow the people and cars of [BimGo](https://github.com/aussieBIMguru/BimGo) by Aussie BIM Guru (MIT-0): their proportions, stances and profiles.

## Planning rules and the sun (Lapin Urban)

The **Lapin Urban** edition adds a **Planning** group to this tab: the parcel's planning rules (POT, CUT, the maximum heights, the setbacks from the street, the sides, the rear and the road's axis), the **buildable volume** they leave, the indicators the model reaches against them, and the **sun study**: the sun and its shadows in 3D for any day and hour at the project's place, and shadow plans for the equinox and the solstices.
