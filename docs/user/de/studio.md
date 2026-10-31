---
title: Studio-Körper und Arbeitsebenen
order: 85
summary: Machen Sie aus gezeichneten Formen Körper, geben Sie einer Form mit Ring, Kasten und Wandstreifen eine Dicke, und zeichnen Sie auf Referenzebenen an beliebiger Stelle im Modell.
---

## Körper aus einer Skizze

Die Registerkarte **Studio** macht aus Formen, die Sie zeichnen, Körper. Zeichnen Sie die Form mit **Zeichnen** in einem Grundriss, einem Schnitt, einer Ansicht oder auf einer Referenzebene, und wählen Sie sie dann mit einem Studio-Werkzeug:

- **Extrusion** (<kbd>EXT</kbd>) schiebt eine geschlossene Form um eine Tiefe hinaus. Geschlossene Formen darin werden zu Löchern.
- **Sweep** (<kbd>SW</kbd>) führt ein geschlossenes Profil entlang eines Pfads, **Rotation** (<kbd>REV</kbd>) dreht es um eine Achse, und **Loft** (<kbd>LO</kbd>) verbindet Profile, die in verschiedenen Ansichten gezeichnet sind.

![Die Registerkarte Studio](shots/ribbon-studio.webp)

Jeder Körper bleibt an seine Skizze gebunden. Ändern Sie die Form, und der Körper folgt ihr, mit derselben GlobalId. Wählen Sie einen Körper aus, um im Eigenschaftenbereich seine IFC-Klasse, seinen vordefinierten Typ und seinen Typ zu ändern. **Skizze bearbeiten** öffnet die Ansicht, auf der die Skizze liegt.

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

![Eine Referenzebene auf der Südansicht, mit einem darauf gezeichneten Ring](shots/studio-plane.webp)

Wählen Sie die Ebene im Browser aus, um sie zu verschieben. **Ursprung x**, **y** und **z** setzen sie. **Zeigt nach** ist die Richtung im Grundriss, in die sie zeigt, **Neigung**, wie weit sie sich nach hinten neigt (bei 90° liegt sie flach, nach oben zeigend), und **Drehung in der Ebene** dreht ihre Zeichnung um ihre Normale. **Entlang der Normalen verschieben…** und **Um die Senkrechte drehen…** tun dasselbe in einem Schritt. Was auf der Ebene gezeichnet ist, und jeder Körper, der aus ihr gemacht ist, bewegt sich mit ihr.

![Die Eigenschaften einer Referenzebene](shots/studio-plane-properties.webp)

Auch Schnitte und Ansichten sind Ebenen. Ein Körper, der in einem Schnitt skizziert ist, steht auf dessen Schnittlinie und folgt der Linie, wenn Sie sie verschieben. In einem abgewickelten Schnitt steht er auf dem Stück, auf dem er gezeichnet ist. Eine Ansicht bekommt eine eigene Ebene, wenn Sie zum ersten Mal einen Körper auf ihr machen: an der Vorderseite des Modells, mit ihrem x, wie es jetzt gezeichnet ist. Ändern Sie ihre **Tiefe der Skizzenebene** in den Eigenschaften der Ansicht, um die Ebene und ihre Körper entlang der Ansicht zu verschieben.

![Das Haus mit seinen Studio-Körpern in 3D](shots/studio-3d.webp)

In IFC sind ein Ring, die Wände eines Kastens und ein Wandstreifen IfcWall-Elemente, Boden und Deckel eines Kastens IfcSlab-Elemente, und ein Kasten mit ihnen ein IfcElementAssembly mit IfcRelAggregates. Die Geschichte jedes Körpers wird in Lapins eigenem Eigenschaftssatz Lapin_Intent aufbewahrt.
