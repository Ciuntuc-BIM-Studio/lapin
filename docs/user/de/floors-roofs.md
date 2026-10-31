---
title: Decken und Dächer
order: 40
summary: Zeichnen Sie Decken und Dächer über ihren Umriss, wählen Sie die Form und die Kanten eines Dachs und schneiden Sie Öffnungen.
---

## Decken

Wählen Sie **Architektur → Decke** (<kbd>SB</kbd>) und klicken Sie dann auf die Ecken ihres Umrisses. Schließen Sie den Umriss am ersten Punkt oder drücken Sie <kbd>Enter</kbd>. Auch eine Decke besteht aus Schichten, die unter **Architektur → Deckentypen** festgelegt werden.

Um den Umriss zu ändern, wählen Sie die Decke aus und ziehen Sie ihre Ecken. **Deckenöffnung** (<kbd>SH</kbd>) schneidet ein Loch in eine Decke, für eine Treppe oder einen Schacht.

Eine außerhalb der Wände, vor einer Tür gezeichnete Decke ist ein Podest. Für die Stufen dort hinauf siehe [Treppen](stairs.md).

## Dächer

Wählen Sie **Architektur → Dach** (<kbd>RF</kbd>) und klicken Sie den Umriss der Wände darunter an. Lapin baut das Dach über diesem Umriss, oben auf seiner Ebene.

![Ein Haus mit Satteldach in der 3D-Ansicht](shots/house-3d.webp)

Wählen Sie das Dach aus, um es im Eigenschaftenbereich zu ändern:

- **Form.** Walmdach, Satteldach, Pultdach, Mansarddach, Tonnendach oder Flachdach. Ein Tonnendach ist ein gekrümmtes Gewölbe zwischen zwei Giebelseiten; **Tonne: Stich am Scheitel** legt fest, wie hoch es am Scheitel ansteigt, höchstens die halbe Spannweite.
- **Neigung und Überstand** für das ganze Dach.
- **Jede Kante** mit ihrer eigenen Art (Traufe oder Ortgang), Neigung und ihrem Überstand. Ein Mansarddach mit Giebelkanten ist ein Mansardgiebeldach.
- **Stil.** Die Dachstile geben mit einer einzigen Wahl ein ganzes Erscheinungsbild vor: die Deckung, die Traufen, die Dachrinnen und Fallrohre.

![Die Eigenschaften eines Dachs](shots/roof-properties.webp)

Die Wände auf der Ebene des Dachs enden von selbst darunter und steigen bis zu seinen Giebeln, auch unter einem gekrümmten Dach. Eine Wand auf einer anderen Ebene folgt dem Dach nach **Architektur → An Dach anbinden** (<kbd>AR</kbd>): Wählen Sie die Wände aus und klicken Sie dann auf das Dach. Dasselbe Werkzeug nimmt jeden anderen Körper über den Wänden: einen Studio-Volumenkörper, eine Decke, einen Träger, ein aus IFC importiertes Element. Die Wände steigen bis zu seiner Unterseite und werden an ihr entlang geschnitten, welche Form sie auch hat. **Vom Dach lösen** macht dies rückgängig.

**Dachfenster** und **Gaube** fügen einem Dach Öffnungen hinzu.

## Abgehängte Decken

**Architektur → Decke** (<kbd>CL</kbd>) zeichnet eine abgehängte Decke über einen angeklickten Umriss; **Decke in einem Raum** füllt den angeklickten Raum entlang seines Umrisses. Eine Decke hängt in ihrer **Höhe** über dem Geschoss (2700 mm, sofern in den Eigenschaften nicht anders eingestellt).

Ihr Typ in **Architektur → Deckentypen** legt das **System** fest: **Platten auf sichtbarem Raster** (Breite, Länge und Dicke der Platte, Ansichtsbreite, Tiefe und Drehung des Rasters, Abhänger bis zur Rohdecke in ihrem Abstand) oder **Gipskarton, fugenlos**. Das Raster wird auf der Decke zentriert, sodass die Randplatten gleich ausfallen.

**Leuchtenpaneel** und **Luftauslass** setzen eines in das angeklickte Feld; ein Klick auf dasselbe Feld mit dem anderen Werkzeug tauscht es. Im Eigenschaftenbereich setzt **Verteilen** Leuchtenpaneele oder Luftauslässe in jedes soundsovielte Feld, **Leeren** entfernt sie. Die Listen zählen ganze Platten, Leuchtenpaneele und Luftauslässe, und die Decke geht als IfcCovering vom Typ CEILING ins IFC.

![Die Eigenschaften einer Decke: Typ, Höhe und Einbauten](shots/ceiling-properties.webp)

![Rasterdecken mit Leuchtenpaneelen und Luftauslässen in 3D](shots/ceilings-3d.webp)
