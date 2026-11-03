---
title: Studio
order: 85
summary: Modellieren Sie freie Körper, skizziert auf jeder Fläche oder Ebene, und drücken, ziehen, runden, höhlen, teilen und wiederholen Sie sie in 3D.
---

## Die Registerkarte Studio

Die Registerkarte **Studio** erzeugt Körper, die keine andere Registerkarte baut: eine Bank, ein Vordach, einen Handlauf, einen Brunnen, einen Sockel an einer Fassade. Jeder Körper beginnt mit einer Skizze oder einem Grundkörper. Danach ändern Sie ihn in 3D mit Schritten, und jeder Schritt bleibt bearbeitbar.

![Die Registerkarte Studio](shots/ribbon-studio.webp)

Ein Studio-Körper hat eine IFC-Klasse wie jedes Element. Wählen Sie sie in seinen Eigenschaften: ein Stab, eine Platte, eine Bekleidung, ein Geländer, eine Decke oder eine andere. Seine Mengen und sein Export folgen dieser Klasse.

## Wo Sie skizzieren

Den Umriss eines Körpers zeichnen Sie mit den Werkzeugen von **Zeichnen**, als geschlossene Polylinien, Rechtecke und Kreise. Linien und Bögen, die sich Ende an Ende treffen, ergeben ebenfalls einen geschlossenen Umriss. Eine geschlossene Form innerhalb des Umrisses ist ein Loch.

Sie können auf vier Arten von Ansichten skizzieren:

- **Ein Grundriss** zeichnet auf der Höhe seines Geschosses.
- **Ein Schnitt** zeichnet auf seiner Schnittlinie, senkrecht.
- **Eine Ansicht** oder **eine Referenzebene**: siehe *Referenzebenen* weiter unten.

## Einen Körper erzeugen

- **Extrusion** (EXTRUDE) schiebt den Umriss gerade hinaus. Geben Sie S für beide Richtungen ab der Skizze ein, T für eine Verjüngung, die die Seiten nach innen neigt, und Cut, damit er die vorher gewählten Elemente schneidet. Um ihn bis zur Fläche eines anderen Elements laufen zu lassen, nutzen Sie **Bis → eine Fläche…** in seinen Eigenschaften oder **Bis Fläche extrudieren**.
- **Sweep** (SWEEP) führt ein Profil entlang eines Pfads. TWist verdreht den Querschnitt unterwegs, SCale vergrößert ihn und RIse hebt ihn an. Ein Kreis, der ansteigt, wird zur Schraubenlinie und läuft TUrns-mal herum.
- **Loft** (LOFT) verbindet Profile, die auf verschiedenen Ansichten gezeichnet sind. Geben Sie S ein, damit er glatt durch sie läuft. Eine Linie, die mit der Rolle *zweiter Pfad* gewählt ist, biegt ihn als Führung.
- **Rotation** (REVOLVE) dreht ein Profil um eine Achslinie.
- **Grundkörper** setzen mit einem Klick einen Quader, einen Zylinder, eine Kugel, einen Kegel, einen Torus, einen Keil oder eine Pyramide. Ihre Größe und Lage ändern Sie in den Eigenschaften.

![Ein Handlauf, entlang eines Kreises geführt, der in zwei Umdrehungen 3 m ansteigt](shots/studio-helix.webp)

Jeder Körper bleibt an seine Skizze gebunden. Ändern Sie die Form, und der Körper folgt ihr, mit derselben GlobalId. Wählen Sie einen Körper aus, um im Eigenschaftenbereich seine IFC-Klasse, seinen vordefinierten Typ und seinen Typ zu ändern. **Skizze bearbeiten** öffnet die Ansicht, auf der die Skizze liegt.

Ein gerade erstellter Körper ist ausgewählt, also leuchtet er mit den Griffen seiner Skizze auf. In einem Grundriss, einem Schnitt oder einer Ansicht wird der Schnitt eines Körpers weiß gefüllt, wo die Ansicht ihn schneidet: Aus den Linien der Skizze wird eine Form, die man als massiv erkennt. Ein Material mit eigener Füllung oder Schraffur zeichnet ihn stattdessen damit.

## Ring, Kasten und Wandstreifen

Diese drei Werkzeuge geben einer gezeichneten Form eine Dicke in ihrer eigenen Ebene und dann eine Höhe von dieser Ebene weg:

- **Ring** (<kbd>RING</kbd>) nimmt einen Kreis, einen Bogen oder einen geschlossenen Umriss und macht daraus einen Ring oder ein Rohr. Seine Dicke liegt **innen**, **außen** oder **mittig** auf der Form.
- **Kasten** (<kbd>BOX</kbd>) nimmt ein Rechteck, eine geschlossene Polylinie oder einen Kreis und macht die Wände ringsum. Ein Boden (**Unten**) und ein Deckel (**Oben**) sind optional, jeder mit seiner eigenen Dicke.
- **Wandstreifen** (<kbd>TWALL</kbd>) nimmt eine Linie, eine Polylinie oder einen Bogen und macht daraus einen Wandstreifen. Seine Dicke liegt **links**, **rechts** oder **mittig**, gesehen beim Entlanggehen der Linie in der Richtung, in der Sie sie gezeichnet haben. Seine Ecken sind auf Gehrung gestoßen.

Starten Sie das Werkzeug und klicken Sie auf die Form. Die Befehlszeile bietet die Optionen als Buchstaben an: <kbd>I</kbd>, <kbd>O</kbd> und <kbd>C</kbd> (oder <kbd>L</kbd>, <kbd>R</kbd> und <kbd>C</kbd>) dafür, wo die Dicke liegt, und bei einem Kasten <kbd>B</kbd> und <kbd>T</kbd>, um Boden und Deckel ein- oder auszuschalten. Geben Sie dann die Dicke und die Höhe in Metern ein, oder drücken Sie <kbd>Enter</kbd>, um den angezeigten Wert zu behalten.

**In einem Grundriss** machen die Werkzeuge standardmäßig native Wände, vom Wandtyp, der unter **Architektur** gewählt ist. Eine geschlossene Form wird zu einer Schleife von Wänden, die an ihren Ecken verbunden sind, eine Polylinie zu einer Kette von Wänden, und ein Bogen oder ein Kreis zu gekrümmten Wänden. Wo die Dicke liegt, entscheidet, wo die Wände stehen: die ganze Dicke des Wandtyps kommt auf die gewählte Seite. Boden und Deckel eines Kastens werden zu Decken. Geben Sie <kbd>S</kbd> ein, bevor Sie die Form wählen, um stattdessen einen Studio-Körper zu machen.

**In einem Schnitt, einer Ansicht oder auf einer Referenzebene** machen die Werkzeuge einen Studio-Körper der IFC-Klasse IfcWall; Boden und Deckel eines Kastens sind eigene Platten (IfcSlab). Die Höhe geht entlang der Normalen der Ebene, auf Sie zu; eine negative Höhe geht von Ihnen weg, wie bei **Extrusion**. Um einer solchen Wand einen Typ zu geben, wählen Sie sie aus und wählen oder erstellen Sie einen unter **Typ** im Eigenschaftenbereich.

![Ein Ring aus nativen gekrümmten Wänden um einen Kreis, ein Kasten mit Boden und Deckel und ein Wandstreifen im Grundriss des Erdgeschosses](shots/studio-thick-plan.webp)

Alles bleibt parametrisch. Wählen Sie den Körper aus und ändern Sie im Eigenschaftenbereich **Dicke**, **Höhe** und **Dicke liegt**. Bei einem Kasten haken Sie **Unten** oder **Oben** an und legen deren Dicken fest: Lapin fügt die Platten hinzu oder entfernt sie. Ein Kasten mit Boden oder Deckel wird mit ihnen zu einer Baugruppe (IfcElementAssembly) zusammengefasst. Ziehen Sie die Griffe der Form, oder bearbeiten Sie sie in ihrer Ansicht, und der Körper wird neu aufgebaut.

![Die Eigenschaften eines Kastens: Dicke, Höhe, Boden und Deckel](shots/studio-box-properties.webp)

## Referenzebenen

Eine Referenzebene ist eine Ebene an beliebiger Stelle im Modell, mit einer eigenen Ansicht. Sie zeichnen darauf mit Zeichnen, wie in einem Schnitt, und Studio macht Körper darauf. Erstellen Sie eine über die Registerkarte **Studio**:

- **Ebene aus Ansicht** (<kbd>REFPLANE</kbd>) liegt parallel zum Grundriss, Schnitt, zur Ansicht oder Ebene vor Ihnen. Geben Sie ein, wie weit sie auf Sie zu liegt (ein negativer Wert geht von Ihnen weg).
- **Ebene auf Fläche**: Klicken Sie in der 3D-Ansicht auf eine Fläche eines beliebigen Elements. Die Ebene liegt auf der Fläche und zeigt von ihr nach außen.
- **Ebene durch 3 Punkte**: Klicken Sie in der 3D-Ansicht auf drei Punkte des Modells. Die Ebene geht durch sie hindurch.

Die neue Ebene öffnet sich sofort. Sie steht unter **Referenzebenen** im Projektbrowser. Ihre Zeichnung ist ein Schnitt auf der Ebene, mit Blick gegen die Seite, in die sie zeigt.

In der 3D-Ansicht wird jede Referenzebene als durchscheinende Scheibe mit ihrem Namen gezeichnet, so groß wie das Modell auf ihr (oder wie **Gezeichnete Breite (3D)** und **Gezeichnete Höhe (3D)** es sagen, mindestens 2 × 2 m), unter dem Cursor hervorgehoben. Klicken Sie darauf, um die Ebene auszuwählen, doppelklicken Sie, um ihre Ansicht zu öffnen. Ist die Ebene ausgewählt, ziehen Sie den Pfeil in ihrer Mitte, um sie entlang ihrer Normalen zu verschieben: der Versatz steht neben dem Pfeil, und was auf der Ebene gezeichnet ist, geht mit. **Ansicht → Referenzebenen** blendet die Scheiben aus und ein; in Exporte, Renderings oder Bilder gelangen sie nie. In Grundrissen, Schnitten und Ansichten wird eine Ebene, die die Ansicht schneidet, als gestrichelte Linie mit ihrem Namen über die ganze Ansicht gezeichnet: ein Klick wählt die Ebene aus, ein Doppelklick öffnet sie. Eine Ebene parallel zur Ansicht wird dort nicht gezeichnet (sie schneidet nichts davon). Diese Linien sind nur für den Bildschirm: Pläne drucken sie nie.

![Eine Referenzebene auf der Südansicht, mit einem darauf gezeichneten Ring](shots/studio-plane.webp)

Wählen Sie die Ebene im Browser aus, um sie zu verschieben. **Ursprung x**, **y** und **z** setzen sie. **Zeigt nach** ist die Richtung im Grundriss, in die sie zeigt, **Neigung**, wie weit sie sich nach hinten neigt (bei 90° liegt sie flach, nach oben zeigend), und **Drehung in der Ebene** dreht ihre Zeichnung um ihre Normale. **Entlang der Normalen verschieben…** und **Um die Senkrechte drehen…** tun dasselbe in einem Schritt. Was auf der Ebene gezeichnet ist, und jeder Körper, der aus ihr gemacht ist, bewegt sich mit ihr.

![Die Eigenschaften einer Referenzebene](shots/studio-plane-properties.webp)

Auch Schnitte und Ansichten sind Ebenen. Ein Körper, der in einem Schnitt skizziert ist, steht auf dessen Schnittlinie und folgt der Linie, wenn Sie sie verschieben. In einem abgewickelten Schnitt steht er auf dem Stück, auf dem er gezeichnet ist. Eine Ansicht bekommt eine eigene Ebene, wenn Sie zum ersten Mal einen Körper auf ihr machen: an der Vorderseite des Modells, mit ihrem x, wie es jetzt gezeichnet ist. Ändern Sie ihre **Tiefe der Skizzenebene** in den Eigenschaften der Ansicht, um die Ebene und ihre Körper entlang der Ansicht zu verschieben.

![Das Haus mit seinen Studio-Körpern in 3D](shots/studio-3d.webp)

In IFC sind ein Ring, die Wände eines Kastens und ein Wandstreifen IfcWall-Elemente, Boden und Deckel eines Kastens IfcSlab-Elemente, und ein Kasten mit ihnen ein IfcElementAssembly mit IfcRelAggregates; ihre Typen werden als IfcWallType und IfcSlabType geschrieben. Jeder 3D-Export (IFC, Fragments, glTF, USD, COLLADA) schreibt einen Studio-Körper dorthin, wo er im Modell steht, auf welcher Ebene er auch skizziert wurde, mit seinen Löchern; eine Baugruppe ist ein Gruppenknoten über ihren Teilen. Wird Lapins eigene IFC-Datei wieder geöffnet, kommen die Körper als Referenzen zurück, an ihrem Platz, mit Klasse, Typ und Baugruppe, aber nicht mehr bearbeitbar (ihre Skizzen sind nicht in der Datei). Die Geschichte jedes Körpers wird in Lapins eigenem Eigenschaftssatz Lapin_Intent aufbewahrt. Studio-Körper nehmen nur einfache Typen ohne Schichten: das ist das freie Modellieren, für das sie da sind; Wände mit Schichten sind Lapins native Wände.
## Einen Körper in 3D ändern

Diese Werkzeuge arbeiten an den Flächen und Kanten des Körpers, wie er jetzt gebaut ist. Die Fläche unter der Maus leuchtet auf.

**Drücken/Ziehen**, **Teilen**, **Abschneiden** und **3D verschieben** arbeiten direkt am Modell. Klicken Sie auf eine Fläche, und ein Pfeil steht auf ihr: Die Maus verschiebt die Fläche entlang des Pfeils, und was der Schritt ergibt, wird laufend gezeichnet. Drücken/Ziehen zeichnet grün, was es hinzufügt, und rot, was es wegnimmt. Teilen und Abschneiden zeichnen ihre Ebene gelb. Der Abstand steht neben dem Pfeil. Geben Sie einen Wert ein, um ihn genau festzulegen, etwa 500 oder -250. Klicken Sie oder drücken Sie <kbd>Enter</kbd>, um ihn anzuwenden, oder <kbd>Esc</kbd>, um abzubrechen; das Werkzeug bleibt für die nächste Fläche aktiv. <kbd>Enter</kbd> ohne Bewegung oder Eingabe wiederholt den letzten Abstand. Beim Ziehen geht es in Zentimeterschritten, mit <kbd>Umschalt</kbd> in Schritten von 10 cm. Die rechte und die mittlere Maustaste drehen und verschieben weiterhin die Ansicht. Die anderen Werkzeuge fragen ihren Wert in einem kleinen Feld neben dem Mauszeiger ab.

- **Drücken/Ziehen** (PU) schiebt eine Fläche hinaus, oder hinein, wenn der Abstand negativ ist. Bei einer Extrusion ändert ihr Ende die Tiefe, und ihre Seite verschiebt die Kante der Skizze darunter. Bei einer Wand oder einem anderen Element fügt Ziehen einen Körper im Material der Fläche hinzu, und Drücken schneidet in das Element.
- **Kante abrunden** (FE) rundet die Kante, die dem Klick am nächsten liegt, und **Kante fasen** (CE) fast sie. Wählen Sie *rings um die Fläche*, um alle Kanten der Fläche auf einmal zu bearbeiten.
- **Schale** höhlt die gewählten Körper aus, mit so dicken Wänden, wie Sie eingeben. Sie kann geschlossen bleiben oder am Ende, an der Skizze oder an beiden offen sein.
- **Teilen** schneidet einen Körper in zwei, an einer Ebene parallel zur gewählten Fläche, so tief in die Fläche, wie Sie ziehen oder eingeben. Beide Teile behalten die Skizze. **Abschneiden** behält nur eine Seite: Wählen Sie **Dahinter behalten** oder **Davor behalten** in der Leiste unten in der Ansicht.
- **Reihe** wiederholt den Körper in einer Linie, und **Polare Anordnung** wiederholt ihn um eine Senkrechte.
- **3D drehen** dreht den Körper um eine gewählte Kante. **3D spiegeln** spiegelt ihn an der Ebene einer Fläche. **3D verschieben** setzt Pfeile entlang x, y und z auf die gewählten Körper: Ziehen Sie an einem, oder geben Sie einen Abstand für die Achse ein, die neben dem Wert steht (<kbd>Tab</kbd> geht zur nächsten Achse).

![Drücken/Ziehen: die obere Fläche nach oben gezogen, was hinzukommt grün gezeichnet, der Abstand neben dem Pfeil](shots/studio-push-pull.webp)

![Die abgerundete Kante: die Rundung folgt dem Block, wenn sich seine Tiefe ändert](shots/studio-fillet.webp)

## Auf einer Fläche zeichnen

In der 3D-Ansicht können Sie direkt auf eine Fläche zeichnen und das Gezeichnete hineindrücken oder herausziehen. Die Gruppe **Auf Fläche** der Registerkarte **Studio** enthält **Rechteck auf Fläche** (FRE), **Kreis auf Fläche** (FCI) und **Polygon auf Fläche** (FPL).

1. Klicken Sie auf eine Fläche eines beliebigen Elements: eines Studio-Körpers, einer Wand, einer Decke oder einer Stütze. Der Klick ist der erste Punkt: die Ecke eines Rechtecks, der Mittelpunkt eines Kreises oder die erste Ecke eines Polygons. Die Fläche bleibt umrandet, solange Sie zeichnen.
2. Bewegen Sie die Maus. Die Form folgt ihr auf der Fläche, ihre Größe daneben. Punkte fangen an den Ecken der Fläche, an den Mitten ihrer Kanten und an ihrer Mitte; sonst gehen sie in Zentimeterschritten, mit <kbd>Umschalt</kbd> in Schritten von 10 cm. Klicken Sie auf die gegenüberliegende Ecke oder auf einen Punkt des Kreises. Sie können die Größe auch eingeben: Breite und Höhe eines Rechtecks als `1200, 600`, oder den Radius eines Kreises. Ein Polygon nimmt einen Klick an jeder Ecke, oder eine eingegebene Länge in Richtung der Maus. Klicken Sie auf seine erste Ecke oder drücken Sie <kbd>Enter</kbd>, um es zu schließen; <kbd>Rücktaste</kbd> nimmt die letzte Ecke zurück.
3. Sobald die Form geschlossen ist, steht ein Pfeil auf ihr, wie bei **Drücken/Ziehen**. Ziehen Sie ihn heraus, und sie wird ein Körper, der auf der Fläche steht, im Material der Fläche. Drücken Sie ihn hinein, und sie wird ein Schnitt in das Element: eine Nische, eine Tasche oder ein Loch, wenn sie ganz hindurchgeht. Klicken Sie oder drücken Sie <kbd>Enter</kbd>, um ihn anzuwenden. <kbd>Esc</kbd> oder ein Abstand von 0 lässt die Form als Skizze auf der Fläche, um sie später zu extrudieren.

Die Form ist eine Skizze auf einer Referenzebene, die auf der Fläche liegt. Die Ebene ist nach dem Element und der Fläche benannt, etwa *Extrude 1, north face*, und Formen, die auf dieselbe Fläche gezeichnet werden, teilen sie sich. Die 3D-Ansicht zeichnet diese Ebenen nicht, weil die Fläche schon zeigt, wo sie liegen. Öffnen Sie eine im Projektbrowser, um die Skizze in 2D zu ändern; der Körper oder der Schnitt folgt ihr. **Drücken/Ziehen** auf dem Ende des Körpers ändert seine Tiefe. **Drücken/Ziehen** auf dem Boden einer Tasche macht den Schnitt tiefer oder flacher, statt dem Element etwas hinzuzufügen.

![Ein Rechteck auf der Seite eines Blocks gezeichnet, seine Größe neben dem Mauszeiger](shots/studio-on-face.webp)

## Die Schritte

Die Eigenschaften eines Studio-Körpers listen seine Schritte der Reihe nach. Jeder Schritt lässt sich ausschalten, früher oder später setzen, ändern oder entfernen. Ändern Sie die Skizze, wird jeder Schritt am neuen Körper neu ausgeführt. Ein Schritt, der seine Fläche oder Kante nicht mehr findet, sagt das unter **Probleme**.

![Ein Zylinder, zu einem Rohr ausgehöhlt und dreimal wiederholt: seine zwei Schritte in den Eigenschaften](shots/studio-steps.webp)

Die Eigenschaften einer Polylinie listen ihre Seiten. Geben Sie die Länge einer Seite ein, und ihr Ende wandert entlang der Seite. Die Seiten danach folgen bis zur nächsten Seite, die in dieselbe Richtung läuft, sodass ein Rechteck ein Rechteck bleibt. Ein darauf gezeichneter Körper folgt ebenfalls.

## Netze aus anderen Programmen

**Datei → Netz importieren (OBJ, STL, glTF)…** holt ein Netz als Referenzen herein, die Sie sehen und fangen können. Eine OBJ- oder STL-Datei sagt weder ihre Einheit noch, welche Achse oben ist, also fragt Lapin. Wählen Sie eine Referenz und klicken Sie auf **Zu Studio-Körper machen**, um sie bearbeitbar zu machen. Dann nimmt sie Drücken/Ziehen, Abrundungen, boolesche Operationen und die anderen Schritte an. Dasselbe Werkzeug wirkt auf Wände, Decken und Elemente aus IFC-Dateien.

## Boolesche Operationen

**Abziehen**, **Vereinigen** und **Schneiden** kombinieren beliebige Elemente, das zuerst gewählte ist das Ziel. Die Werkzeuge bleiben bearbeitbar, und das Ergebnis folgt ihnen. **Operanden anzeigen** zeigt die verborgenen Werkzeuge einer Vereinigung zum Bearbeiten. **Operation entfernen** trennt die Elemente wieder.
