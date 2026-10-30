---
title: Räume und Raumfarben
order: 45
summary: Räume per Klick zwischen Wänden platzieren, benennen und nummerieren und sie im Grundriss nach Nutzung oder Abteilung einfärben oder schraffieren, mit einer Legende.
---

## Einen Raum platzieren

Wählen Sie **Architektur → Raum** (<kbd>RM</kbd>) und klicken Sie in einen von Wänden umschlossenen Bereich. Lapin findet den Umriss des Raums zwischen den Flächen der Wände, Vorhangfassaden und Stützen um den Punkt herum. Türen öffnen den Raum nicht: Er schließt sich über sie hinweg. Eine Stütze, die im Raum steht, wird von seiner Fläche abgezogen.

Der Raum folgt seinen Wänden. Verschieben Sie eine Wand, und der Raum wächst oder schrumpft mit ihr. Wird eine Wand gelöscht und schließt nichts mehr den Raum ein, behält er den Umriss, den er hatte, und sein Raumstempel wird rot. **Architektur → Räume aktualisieren** speichert die Umrisse, die die Räume jetzt finden.

Für einen Bereich ohne umlaufende Wände, etwa eine Terrasse oder ein Ende eines offenen Grundrisses, verwenden Sie **Raum nach Umriss** und klicken Sie seine Ecken an. Dieser Raum behält den Umriss, den Sie zeichnen.

![Ein Grundriss mit zwei nach Nutzung eingefärbten Räumen und einer Farblegende](shots/rooms-plan.webp)

## Die Eigenschaften eines Raums

Wählen Sie einen Raum aus, indem Sie auf seinen Raumstempel oder irgendwo in ihn klicken. Im Eigenschaftenbereich:

- **Name** und **Nummer.** Neue Räume werden nach Ebene nummeriert: 101, 102… im Erdgeschoss.
- **Nutzung**, **Abteilung** und **Bodenbelag.** Geben Sie beliebigen Text ein, oder wählen Sie einen Wert, den andere Räume bereits haben.
- **Höhe.** Leer bedeutet die Höhe der Ebene.
- **Eigene Farbe** und **Eigene Schraffur** setzen für diesen einen Raum das Farbschema außer Kraft.

Die Mengen darunter zeigen die Nettofläche des Raums (ohne die Stützen), seinen Umfang und sein Volumen. Ziehen Sie den Griff am Raumstempel, um den Raumstempel zu verschieben.

![Die Eigenschaften eines Raums](shots/room-properties.webp)

## Raumfarben

**Ansicht → Raumfarben** färbt die Räume eines Grundrisses ein. Wählen Sie ein Schema, wählen Sie, wonach es einfärbt (**Nutzung**, **Name**, **Abteilung**, **Nummer** oder **Bodenbelag**), und stellen Sie **Dieser Grundriss zeigt** auf das Schema. Jeder Wert, den die Räume haben, erhält eine Zeile mit:

- einer **Farbe**,
- einer **Schraffur** aus den Mustern des Projekts, mit ihrem Maßstab (**Skalieren**) und ihrer Farbe (**Tinte**),
- und einer Vorschau, wie es aussieht.

**Fehlende Farben ergänzen** gibt jedem Wert ohne Farbe eine aus einer sanften Palette. **Neu** und **Duplizieren** erstellen weitere Schemata, zum Beispiel eines für eine Kundenpräsentation und eines nach Abteilung.

![Der Dialog Raumfarben](shots/room-colors.webp)

Die Farben werden unter den Wänden, der Möblierung und den Türaufschlägen gezeichnet, sodass der Grundriss lesbar bleibt. Sie werden auf Plänen gedruckt und gehen in PDF- und DXF-Exporte ein. Ein neuer Grundriss zeigt keine Raumfarben, bis Sie ein Schema für ihn wählen, in **Raumfarben** oder in den Eigenschaften des Grundrisses.

## Farblegende

**Beschriften → Farblegende**: Klicken Sie dort in den Grundriss, wo die Legende stehen soll. Sie listet die Farben, die die Räume des Grundrisses verwenden, mit ihren Namen, und folgt jeder Änderung am Schema. Verschieben Sie sie mit **Verschieben**.

## Räume in 3D und in IFC

Räume sind in der 3D-Ansicht ausgeblendet, damit sie das Gebäude nicht verdecken. **Sichtbarkeit / Grafiken** in der 3D-Ansicht kann sie anzeigen.

In einem IFC-Export ist jeder Raum ein IfcSpace, mit seiner Nummer, seinem Namen, seiner Nutzung, seinen Flächen und seinem Volumen.

## Räume aus einer IFC-Datei

**Datei → IFC importieren…** übernimmt die Räume (Spaces) der Datei als Räume, egal welches Programm sie geschrieben hat (Revit, ArchiCAD, Allplan, Bonsai…):

- jeder kommt auf **seine eigene Ebene**, diejenige, in die die Datei ihn setzt;
- sein **Umriss** stammt aus seiner Form (einer Extrusion, einer B-Rep oder einem Netz, oder dem daneben gezeichneten Footprint); er behält diesen Umriss und folgt nicht den Wänden;
- sein **Name** ist der Langname des Space und seine **Nummer** dessen Name (so wie Revit und ArchiCAD sie schreiben); seine **Höhe** und sein **Versatz unten**, wenn er über seiner Ebene beginnt (eine Galerie, ein Doppelboden), stammen aus seiner Form;
- seine **Nutzung**, seine **Abteilung** und sein **Bodenbelag** stammen aus der Datei, wo sie diese angibt (der Objekttyp oder die Raumkategorie, eine Eigenschaft Department, der Bodenbelag), sodass ein Farbschema nach Nutzung oder nach Abteilung sie sofort einfärbt;
- alles andere, was der Space in der Datei hatte, seine Eigenschaftssätze und Mengen (Pset_SpaceCommon, BaseQuantities, die eigenen Sätze des Programms), wird beibehalten und schreibgeschützt unter **IFC-Eigenschaften** in seinen Eigenschaften aufgelistet, mit einem Filter, wenn es viele sind.

Die Meldung des Imports sagt, wie viele Räume übernommen wurden. Suchen Sie sie in den Grundrissen ihrer Ebenen (jeder zeigt seinen Raumstempel), in einer Bauteilliste **Räume** (**+** unter Bauteillisten im Browser) oder in der Registerkarte IFC des Browsers; die 3D-Ansicht blendet Räume aus.

Ein Space, dessen Form keinen Umriss ergibt, wird trotzdem übernommen, mit seinem Namen und seinen Eigenschaften, als Raum ohne Umriss: Er erscheint in der Bauteilliste, wird aber nirgends gezeichnet. Wählen Sie ihn aus (in der Registerkarte IFC des Browsers), öffnen Sie den Grundriss seiner Ebene und dann **Per Klick finden** (klicken Sie zwischen seine Wände) oder **Umriss zeichnen** in seinen Eigenschaften.
