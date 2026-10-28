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
