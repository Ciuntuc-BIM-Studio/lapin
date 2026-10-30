---
title: Import und Export
order: 110
summary: Tauschen Sie das Modell als IFC aus, holen Sie DXF-Zeichnungen herein, verknüpfen Sie Dateien und exportieren Sie 3D-Modelle für Rendering und AR.
---

Alles, was hinein- oder hinausgeht, finden Sie im Menü **Datei**.

![Das Menü Datei](shots/file-menu.webp)

## IFC

**IFC exportieren…** schreibt das Modell als IFC. Jede Wand, Decke, Tür und jedes Dach ist ein echtes IFC-Element mit seinen Schichten, seinem Typ und seinen Eigenschaften, sodass sich die Datei in anderen BIM-Werkzeugen als Modell öffnet. Türen und Fenster geben an, wie sie sich öffnen: den Anschlag jedes Tür- und Fensterflügels, in welche Richtung er aufschlägt, und die Felder, so wie IFC sie definiert, damit andere Programme dieselben Aufschläge zeichnen.

**IFC importieren…** liest die IFC-Datei eines anderen Programms. Bevor etwas hereinkommt, liest Lapin die Datei und zeigt, was sie enthält: ihre Elemente nach Disziplin (Architektur, Tragwerk, TGA, Möbel und Ausstattung, Gelände), jede Disziplin nach Art mit der jeweiligen Anzahl, und ihre Geschosse mit der Anzahl der Elemente auf jedem. Deaktivieren Sie, was Sie nicht brauchen, zum Beispiel TGA und Möbel, wenn Sie am Tragwerk arbeiten, oder behalten Sie nur die Geschosse, an denen Sie arbeiten. **Wie** holt die erkannten Elemente als Lapin-Elemente, alle als Referenzen oder verknüpft herein. Während die Datei gelesen und importiert wird, zeigt Lapin, was es gerade tut und wie weit es ist; eine große Datei braucht eine Weile.

![IFC importieren](shots/import-ifc.webp)

Lapin erkennt Wände, Decken, Stützen, Träger, Fundamente, Türen und Fenster und wandelt sie in Lapin-Elemente um, die Sie bearbeiten können. Was es nicht erkennen kann, kommt als Referenz herein: gezeichnet, aber nicht bearbeitbar. Türen und Fenster öffnen sich nach dem Import so, wie die Datei es angibt: ihr Anschlag, die Seite, zu der sie aufgehen, und bei Fenstern, wie sich jeder Flügel öffnet.

Die Räume der Datei (aus Revit, ArchiCAD und anderen) kommen als Lapin-Räume herein, jeder auf seiner eigenen Ebene, mit Umriss, Nummer, Name, Höhe, Funktion, Abteilung und Bodenbelag: siehe [Räume](rooms.md#raume-aus-einer-ifc-datei). Alles, was ein importiertes Element in der Datei hatte (seine Eigenschaftssätze und Mengen), bleibt erhalten und wird in seinen Eigenschaften unter **IFC-Eigenschaften** angezeigt.

## DXF und DWG

**Import DXF…** holt eine CAD-Zeichnung in die vorne liegende Ansicht, mit ihren Layern, Schraffuren, Texten und Bemaßungen. Sie kommt als Zeichnung herein, an der Sie fangen und die Sie bearbeiten können. Um eine DWG zu verwenden, speichern Sie sie zuerst in Ihrem CAD-Programm als DXF (AutoCAD, BricsCAD oder DraftSight: **Save As → DXF**). Ein PDF-Import ist noch nicht verfügbar. In einem Grundriss legt **Placed: by the map's coordinates** eine Zeichnung in echten Koordinaten (einen Katasterplan in Stereo 70) dorthin, wo die Karte sie hinlegt (siehe [Gelände](site.md)).

## Verknüpfungen

**Link IFC…** hält eine andere Datei verknüpft, statt sie zu importieren. Sie ist schreibgeschützt und wird neu geladen, wenn sie sich ändert. Das funktioniert auch für eine DXF-Datei. **Verwalten → Verknüpfungen** zeigt die Verknüpfungen an, lädt sie neu, wandelt sie um oder entfernt sie.

## 3D-Modelle

**3D exportieren…** schreibt das Modell als Oberflächen für Rendering, Visualisierung und Augmented Reality (AR):

| Format | Für |
|---|---|
| GLB, glTF | Blender, three.js, Web-Viewer. Die eigenen Bilder Ihrer Materialien sind enthalten. |
| USDZ | Apples AR Quick Look, Reality Composer, Omniverse. Bilder enthalten. |
| USDA | OpenUSD als Text, eine Datei. |
| DAE | COLLADA, für SketchUp und ältere Werkzeuge. |

![3D exportieren](shots/export-3d.webp)

Sie wählen, was exportiert wird, welche Bauphase und den Ursprung. **Was** ist **alles** oder das Modell, **wie die Ansicht es zeigt**: die vorne liegende 3D-Ansicht, ohne ihre ausgeblendeten Elemente und Kategorien und ohne die Geschosse, die sie nicht zeigt, mit den Schichten, die sie zeigt (ohne Bekleidungen oder nur die Kerne). Wenn diese Ansicht geschnitten ist (der Schnitt über den angezeigten Geschossen, ein in 3D gezeigter Schnitt, der Schnittbereich), schneidet **Schnitt** auch den Export dort und schließt jeden Schnitt in seinen Materialien, sodass die Datei wie der Bildschirm aussieht. Eine 3D-Ansicht, die etwas weglässt, wird so exportiert, wie sie es zeigt, sofern Sie nichts anderes wählen. Jedes Element behält seine IFC-Klasse, seinen Typ und seine GlobalId. Um das Modell selbst statt seiner Oberflächen auszutauschen, verwenden Sie **IFC exportieren…**.
