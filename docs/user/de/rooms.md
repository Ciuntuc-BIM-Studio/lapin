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

- **Name** und **Nummer.** Neue Räume werden nach Ebene nummeriert: 101, 102… im Erdgeschoss. Der Name eines Raums ist der seiner Nutzung, bis Sie ihm einen eigenen geben; geleert, kehrt er zur Nutzung zurück.
- **Nutzung**, **Abteilung** und **Bodenbelag.** Geben Sie beliebigen Text ein, oder wählen Sie einen Wert, den andere Räume bereits haben.
- **Höhe.** Leer bedeutet die Höhe der Ebene.
- **Eigene Farbe** und **Eigene Schraffur** setzen für diesen einen Raum das Farbschema außer Kraft.

Die Mengen darunter zeigen die Nettofläche des Raums (ohne die Stützen), seinen Umfang und sein Volumen. Ziehen Sie den Griff am Raumstempel, um den Raumstempel zu verschieben.

![Die Eigenschaften eines Raums](shots/room-properties.webp)

## Raumstempel

**Architektur → Raumstempel** (oder **Raumstempel…** in den Eigenschaften eines Raums) legt fest, was der Stempel jedes Raums in Grundrissen zeigt, Zeile für Zeile: Name, Nummer, Nutzung, Abteilung, Fläche, Bruttofläche, Umfang, Höhe, Volumen oder Bodenbelag, oder einen eigenen Text. Die erste Zeile ist fett. **Zeichnungsstandards → Beschriftungen** zeigt denselben Stempel als Text: `{name}|{number}|{area}`.

![Der Dialog Raumstempel](shots/room-tag.webp)

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

## Raumbemaßung

**Annotate → Room dimensions** (<kbd>ID</kbd>) bemaßt das Innere der Räume eines Grundrisses auf einmal. Jede Seite eines Raums erhält eine ausgerichtete Maßkette direkt innerhalb des Raums. Die Kette hat einen Punkt an jeder Ecke der Seite und an den Leibungen jeder Tür und jedes Fensters darin.

- Sind Räume ausgewählt, werden nur diese bemaßt. Ohne Auswahl werden alle Räume der Ebene des Grundrisses bemaßt.
- Auf einem Lageplan aufgerufen, der das ganze Modell von oben sieht und keine Wand schneidet, kommen die Ketten stattdessen in den Grundriss der Ebene. Die Texthöhe einer Bemaßung lässt sich einzeln in den Eigenschaften setzen (**Text height**, mm auf dem Papier; leer: die des Bemaßungsstils). Texthöhen gelten auf dem Papier, auf einem Lageplan 1:500 ist derselbe Text im Modell also fünfmal größer als auf einem Grundriss 1:100.
- Die Punkte haften an den Wänden wie bei jeder Bemaßung. Verschieben Sie eine Wand, folgen ihr die Ketten.
- Führen Sie es für einen Raum erneut aus, um seine Ketten neu zu erzeugen, etwa nach dem Einfügen einer Tür. Die alten Ketten werden ersetzt, nicht verdoppelt.
- Sehr kurze Seiten unter 30 cm werden ausgelassen. Jede Kette lässt sich wie eine von Hand gezeichnete bearbeiten oder löschen.

![Zwei innen bemaßte Räume: die Kette jeder Seite mit Tür und Fenstern](shots/room-dimensions.webp)

## Raumbekleidungen

**Ausbau → Raum bekleiden** (<kbd>FR</kbd>) legt eine Bekleidung in den Raum, in den Sie klicken: seinen Boden, die Bekleidung seiner Wände und seine Decke, jede dem Umriss des Raums folgend, wie der Raum selbst. Verschieben Sie eine Wand, und die Bekleidungen gehen mit. **Ausbau → Raumbekleidungen** bearbeitet die Bekleidungen; **Aus der Bibliothek** holt fertige: Wohnzimmer, Schlafzimmer, Bad, Küche, Erdgeschoss auf Bodenplatte und Technikraum. Solange das Werkzeug aktiv ist, zeigt das Eigenschaftenfenster die Bekleidung, die es verlegt, zur Wahl vor dem Klick. Die **Bekleidung** eines Raums, in seinen Eigenschaften, verlegt eine andere oder nimmt sie weg.

Eine Bekleidung hat:

- **Boden**, seine Schichten vom fertigen Fußboden nach unten (Fliesen, Kleber, Estrich…). Die Deckenöffnungen im Raum (ein Treppenauge) werden ausgespart. **Durch die Türen, bis zur Wandachse** führt den Boden unter jeder Tür weiter, sodass der Boden des Nachbarraums ihn in der Wandachse trifft.
- **Wände**, ihre Schichten von der Wandfläche in den Raum, auch um die Inseln des Raums. Jede Schicht kann über ihre Höhe das Material wechseln, wie die Schicht eines Wandtyps: im Bad Anstrich über Fliesen bis 2100 (13 mm dick, Fliesen und Kleber); in der Küche ein Fliesenband von 900 bis 1500. Ein Stück ohne Material lässt dort nichts. Die Türen und Fenster der Wände um den Raum werden ausgeschnitten und ihre Laibungen mit denselben Schichten bekleidet, von der Raumseite bis zum Rahmen (bei einer leeren Öffnung bis zur Wandachse); auch die untere Laibung eines Fensters, es sei denn, eine Fensterbank deckt sie raumseitig ab. **Höhe**: leer, bis zur Decke.
- **Decke**, ihre Schichten von der Rohdecke über dem Raum nach unten (von der Raumhöhe, wenn keine Decke da ist), ihre Öffnungen ausgespart.
- **Sockelleiste**: ein Sockelleistentyp, um den Raum verlegt, an seinen Türen unterbrochen (siehe Sockelleisten, unter Decken und Dächer).
- **Eine tragende Platte unter dem Boden**: eine eigene Stahlbetondecke unter den Schichten des Bodens, mit ihrer Dicke und wie weit sie über den Umriss des Raums hinausgeht (unter die Wände). Sie ist eine Decke wie jede andere, im Tragwerk und in ihrer Liste.

**Speichern** ändert die Bekleidung, und jeder Raum, der sie hat, wird neu verlegt. **Ausgewählte Räume bekleiden** verlegt sie in den ausgewählten Räumen.

**Schraffur im Grundriss.** In Grundrissen wird der Boden eines bekleideten Raums so schraffiert, wie seine Bekleidung sagt, unter den Wänden, also drucken Pläne, PDF und DXF ihn (Layer FINISH-HATCH). Im **Boden** der Bekleidung wählt **Schraffur im Grundriss** **nach dem Material** (Standard: das Oberflächenmuster der obersten Schicht, ihre Schraffur für Ansichten, wo sie gesehen wird, unter **Verwalten → Materialien**, sonst eine, die ihr Name nahelegt: Fliesen, Dielen für Parkett und Laminat, ein Kreuz für Teppich, Linien für Vinyl, Punkte für einen Gussboden, sonst schräge Linien), **eigenes Muster** (jedes Muster des Projekts, mit Maßstab, Winkel und Farbe) oder **keine Schraffur**; Maßstab und Winkel gelten auch für das Muster des Materials. Ein Grundriss von unten (ein Deckenspiegel) schraffiert stattdessen die Decken, nach der **Schraffur im Grundriss** der **Decke**, ebenso. **Bodenschraffur** in den Eigenschaften des Raums schaltet sie für diesen Raum aus. **Schraffuren der Bodenbeläge** eines Ansichtsstils (**Ansicht → Ansichtseinstellungen**, **Verwalten → Ansichtsstile**) schaltet sie für ganze Ansichten aus: an in Architectural, aus in Structural, MEP und Coordination. Mit Raumfarben im Grundriss füllt die Farbe den Raum und die Schraffur liegt darüber.

![Ein Grundriss mit dem Boden des Bads als Fliesen und dem des Wohnzimmers als Dielen schraffiert](shots/room-finish-hatch.webp)

Jede Bekleidung misst, nach Material, ihre **Brutto**fläche (der Umriss des Raums, nichts abgezogen: die Bodenfläche, der Wandumfang mal der Höhe jedes Stücks) und ihre **Netto**fläche (was gebaut wird: Türen, Fenster und Deckenöffnungen abgezogen, die Laibungen und der Boden unter den Türen hinzugefügt; der Anteil der Laibungen steht in den Mengen, als RevealArea) sowie ihr Volumen. **Ausbau → Bekleidungsliste** listet jeden bekleideten Raum nach Teil und Material, mit den Summen je Material am Ende; wie jede Bauteilliste erstellt sie Diagramme, wird exportiert und kommt auf einen Plan. In IFC ist eine Bekleidung ein IfcCovering: FLOORING, CLADDING oder CEILING.

Eine Bekleidung bekleidet, was die Wände des Raums schon haben: Wenn die Wandtypen ihren eigenen Putz tragen, geben Sie den Wänden des Raums nur, was darüber kommt (Anstrich, Fliesen), oder verwenden Sie Wandtypen nur mit dem Kern.

![Die Raumbekleidungen: der Boden des Bads, die Wände mit Fliesen bis 2100 und die Decke](shots/room-finishes.webp)

![Die Eigenschaften eines Raums mit seiner Bekleidung](shots/room-finish-properties.webp)

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
