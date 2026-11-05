---
title: Türen und Fenster
order: 30
summary: Türen und Fenster in Wände setzen, verschieben und ihre Typen im Konfigurator gestalten.
---

<!-- video doors-windows -->

## Eine Tür oder ein Fenster platzieren

Wählen Sie **Architektur → Tür** (<kbd>DR</kbd>) oder **Fenster** (<kbd>WN</kbd>) und klicken Sie dann auf eine Wand dort, wo Sie die Öffnung haben möchten. Lapin schneidet die Öffnung in jede Schicht der Wand und fügt den Sturz hinzu, den der Wandtyp verlangt.

Solange Door oder Window aktiv ist, zeigt der Eigenschaftenbereich den Typ, der gesetzt wird, mit seiner Größe: wählen Sie dort einen anderen, bevor Sie auf die Wand klicken.

![Ein Grundriss mit einer Tür und Fenstern](shots/openings-plan.webp)

Um eine Tür oder ein Fenster entlang seiner Wand zu verschieben, wählen Sie es aus und ziehen Sie es. Um es zu ändern, verwenden Sie den Eigenschaftenbereich: seinen Typ, seine Brüstungshöhe und die Seite, zu der es sich öffnet (**Seite**). Sind mehrere Türen und Fenster ausgewählt, ändern **Anschlag**, **Seite**, die Brüstung und der Typ alle in einem Schritt, ebenso ein Umkehrpfeil im Grundriss.

Für einen genauen Abstand verwenden Sie **Verschieben** (<kbd>M</kbd>) oder **Kopieren** (<kbd>CO</kbd>) in der Befehlszeile, wie bei jedem Element: Wählen Sie die Tür, das Fenster oder die Wandöffnung aus, klicken Sie einen Basispunkt an und geben Sie dann den Abstand ein (`500`) oder klicken Sie einen zweiten Punkt an. Es zählt nur der Anteil der Verschiebung entlang der Wand: Die Öffnung gleitet entlang ihrer Wand und behält ihre Brüstung. In eine andere Wand desselben Geschosses verschoben, wechselt sie in diese Wand und öffnet weiterhin gleich in den Raum. Eine Kopie ist eine neue Tür oder ein neues Fenster desselben Typs mit der nächsten Nummer. Passt sie nicht (über das Wandende hinaus oder über eine andere Öffnung), bewegt sich nichts. **Drehen** und **Spiegeln** platzieren sie ebenso, und Spiegeln wechselt ihren Anschlag.

![Die Eigenschaften einer Tür](shots/door-properties.webp)

## Öffnungen ohne Tür oder Fenster

**Architecture → Wall opening** (<kbd>WO</kbd>) schneidet dort, wo Sie klicken, eine Öffnung allein durch eine Wand: einen Durchgang, eine Luke, ein Loch für eine spätere Tür. Breite, Höhe und Brüstung stellen Sie vor dem Klick im Eigenschaftenbereich ein. Ausgewählt werden dort ihre Größe, ihre Lage entlang der Wand und ihre Brüstung geändert; wie eine Tür muss sie in die Wand passen und von deren anderen Öffnungen frei bleiben. In IFC ist sie eine Öffnung, die die Wand ausspart, ohne Füllung.

Ausgewählt hat eine Öffnung Griffe wie ein Fenster: ziehen Sie ihre Mitte, um sie entlang der Wand zu schieben, oder eine Laibung, um sie breiter oder schmaler zu machen, während die andere bleibt; die Länge neben der Maus lässt sich eingeben, wie beim Zeichnen. Sie wird mit einem Klick in ihre Lücke im Grundriss oder in ihr Loch in der 3D-Ansicht gewählt, wo ihr Umriss aufleuchtet. **Verschieben** (<kbd>M</kbd>) schiebt sie um einen eingegebenen Abstand entlang der Wand, und Höhe und Brüstung stehen in den Eigenschaften.

**Sturzform** formt ihren oberen Abschluss: **Rechteck**, **Segmentbogen** (ein flacher Bogen), **Ellipsenbogen** (eine halbe Ellipse, ohne Angabe ein Viertel der Breite hoch), **Halbkreis** oder **Abgerundete Ecken** (ein gerader Sturz mit gerundeten oberen Ecken). **Stichhöhe** legt fest, wie hoch ein Segment- oder Ellipsenbogen steigt, **Eckradius**, wie rund die Ecken sind (ohne Angabe 150 mm, nie mehr als die halbe Breite); leer gilt der Wert der Form. Die Wandöffnung folgt dem Umriss in 3D, in Ansichten und Schnitten und im IFC; der Grundriss zeigt ihre Laibungen wie bisher.

## Der Konfigurator

**Architektur → Türen und Fenster** öffnet den Konfigurator. Jeder Tür- und Fenstertyp des Projekts ist links aufgelistet. Sie können ändern:

- den Namen und das Kennzeichen des Typs (den Buchstaben in den Beschriftungen);
- seine Breite und Höhe;
- die Tiefe und Breite des Rahmens;
- seine Spalten und Reihen von Flügeln und wie sich jeder Flügel öffnet;
- die Laibungsoberfläche, die Umrahmung an der Fassade, die Schwelle und die Farben.

Die Zeichnungen rechts zeigen den Typ so, wie er in Ansicht, Grundriss und Schnitt gezeichnet wird, während Sie ihn ändern.

**Links und rechts.** Der Anschlag einer Tür oder eines Fensters wird so benannt, wie IFC ihn benennt: gesehen von der Person, die sie aufdrückt. Eine Tür *swing left* hat ihre Bänder links von der Person, die sie aufdrückt; vom Raum aus, in den sie aufgeht, liegen sie also rechts. (DIN benennt umgekehrt, von der Öffnungsseite aus.) Das Raster **Flügel** ist von außen gesehen, von der Seite, von der die Flügel aufgedrückt werden, sodass sein Links das Links der Namen ist. Die Ansicht ist von innen gesehen, von der Seite, zu der die Flügel aufgehen, sofern **Von außen** nicht angehakt ist; wie der Grundriss zeigt sie die Tür so, wie sie im Modell steht. Eine auf den anderen Anschlag umgekehrte Tür (**Anschlag** oder ein Umkehrpfeil entlang der Wand) ist das Spiegelbild ihres Typs; die Bauteillisten führen sie auf einer eigenen Karte (siehe [Bauteillisten](schedules.md)).

![Der Konfigurator für Türen und Fenster](shots/joinery-configurator.webp)

**Typ speichern** übernimmt Ihre Änderungen. Jede Tür und jedes Fenster dieses Typs folgt ihnen. **Damit platzieren** schließt den Konfigurator und beginnt, diesen Typ zu platzieren.

### Fenster mit mehreren Flügeln

**Flügelspalten** nimmt die relativen Breiten der Flügel auf, eine Zahl für jeden: `1 1` sind zwei gleiche Flügel, `1 1 1` drei, `1 2 1` ein breiter zwischen zwei schmalen. **Flügelreihen** teilt das Fenster auf dieselbe Weise in der Höhe, von unten nach oben: `1 1` ergibt zwei Reihen. Sechs Flügel sind `1 1 1` in Flügelspalten und `1 1` in Flügelreihen. Wählen Sie dann unter **Flügel**, wie sich jeder Flügel öffnet: fest, Dreh, Dreh-Kipp, Kipp, Klapp, Schwing oder Schiebe.

### Fensterläden, Gitter, Blumenkästen und Vordächer

<!-- video window-dressings -->

Unter **An der Fassade** kann ein Fenstertyp Folgendes haben:

- **Fensterläden**: mit Lamellen, als Füllung oder aus Brettern, ein- oder zweiflügelig, geöffnet dargestellt (neben dem Fenster an die Wand geklappt) oder davor geschlossen;
- ein **Fenstergitter**: in der Laibung innen (die Vorgabe, wo Fenstergitter meist sitzen), in der Laibung außen, an der Fassade oder an der Innenseite, mit waagerechten oder senkrechten Stäben oder beidem, deren Querschnitt und Abstand;
- einen **Blumenkasten** unter dem Fenster;
- ein **Vordach** darüber, mit seiner Auskragung und Neigung.

Jedes Teil richtet sich nach den tatsächlichen Flächen der Wand dort, wo das Fenster sitzt, aus den Schichten der Wand, und sitzt daher auf jedem Wandtyp richtig. Hat das Fenster eine Umrahmung, steht das, was an der Fassade ist, vor der Umrahmung. Sie können jedes Teil von Hand verschieben: **Von der Wandfläche** legt fest, wie weit es von der Wand absteht (bei einem Gitter in der Laibung legt **Von der Fläche zurückgesetzt** fest, wie tief es sitzt), **Von der Fensterachse** verschiebt es entlang der Wand und **Nach oben oder unten** in der Höhe. Sie erscheinen in 3D, in Grundrissen, Ansichten und Schnitten, und die Bauteillisten messen sie.

![Fensterläden, ein Gitter, ein Blumenkasten und ein Vordach im Konfigurator](shots/joinery-dressings.webp)

![Die ausgestatteten Fenster in 3D](shots/window-dressings-3d.webp)

### Formen, Stile und Schmuck

**Form** gibt einer Tür oder einem Fenster einen Bogenabschluss (**Rundbogen**, **Segmentbogen**, **Ellipsenbogen**, **Abgerundete Ecken**, **Spitzbogen**, **Dreiecksabschluss**) oder macht es **Rund oder oval** (rund bei gleicher Breite und Höhe). Ein Bogentyp behält seine Flügel unter einem Kämpfer am Bogenansatz, darüber ein festes Oberlicht, das dem Bogen folgt, auf Wunsch mit Sprossen: **Strahlen**, **Die Pfosten weitergeführt** oder **Kreuz**. **Stichhöhe** legt fest, wie hoch ein Segment-, Ellipsen-, Spitz- oder Dreiecksabschluss steigt, **Eckradius**, wie rund die Ecken eines Abschlusses mit abgerundeten Ecken sind. Die Wandöffnung, der Rahmen, das Glas, die Faschen und die Zeichnungen folgen dem Umriss.

**Sprossen** teilen die Scheiben: **Raster (Kolonial)** mit Feldern in der Breite und Höhe, **Prairie** mit Sprossen nahe den Rändern, oder ein **Rautenmuster**.

An der Fassade kann ein Fenster neben der Fasche einen **Schlussstein**, eine **Bekrönung über der Öffnung** (ein **Gesims**, eine **Giebelverdachung**, einen **Segmentgiebel** oder ein **Überschlagsgesims**, das einem Bogen folgt) und ein **Brüstungsfeld unter der Sohlbank** mit Konsolen haben.

**Einen Stil anwenden…** setzt Form, Sprossen und Schmuck auf einmal: Modern, Klassisch, Georgianisch, Viktorianisch, Romanisch, Barock, Gotisch, Tudor, Prairie, Traditionell oder ein Rundfenster. Alles bleibt änderbar.

![Ein barockes Fenster: Rundbogen mit Strahlen, Schlussstein und Segmentgiebel](shots/joinery-styles.webp)

![Bogenfenster in 3D](shots/window-styles-3d.webp)

### Griffe

**Griff** wählt den Griff des Typs: **Drücker**, **Knauf**, **Stoßgriff**, **Fenstergriff** oder keinen. Standardmäßig hat eine Tür einen Drücker und ein Fenster einen Fenstergriff. Eine Tür hat den Griff auf beiden Seiten, in der angegebenen **Griffhöhe**; **Griffarbe** setzt seine Farbe. Griffe erscheinen in 3D, in Schnitten und Ansichten und in den Zeichnungen des Konfigurators, wenn **Beschläge** angehakt ist.

### Parameter

**Parameter · in Bauteillisten**, unten im Konfigurator, enthält die eigenen Parameter des Typs: **+ Feuerwiderstand** (in IFC in Pset_DoorCommon oder Pset_WindowCommon als FireRating geführt), **+ Hersteller**, **+ Produktcode**, **+ Website** oder jeden Parameter, den Sie benennen. Die Türen und Fenster des Typs erben sie, und jeder wird zu einer Zeile der Tür- und Fensterlisten: siehe [Bauteillisten und Diagramme](schedules.md).

## Beschriftungen im Grundriss

**Beschriften → Tür- und Fensterbeschriftungen** setzt eine Markierung an jede Tür und jedes Fenster eines Grundrisses: eine Linie quer durch die Wand, das Kennzeichen außen sowie Größe und Brüstung innen. Um das Aussehen der Beschriftungen zu ändern, gehen Sie zu **Verwalten → Zeichnungsstandards → Beschriftungen**.

### Eine Markierung auf dem Bildschirm bearbeiten

Jede Markierung lässt sich von Hand anordnen, Teil für Teil. Klicken Sie auf ihre Linie oder einen ihrer Texte, um sie auszuwählen: Jeder Text hat dann einen quadratischen Griff (ziehen, um den Text zu verschieben) und einen runden hinter seinem Ende (ziehen, um den Text zu drehen), und jedes Linienende hat einen Griff (ziehen, um die Linie zu verlängern oder zu kürzen; der Text an diesem Ende geht mit). Strg/⌘-Klick auf einen Griff setzt diesen Teil zurück. Jedes Ziehen ist ein Schritt zum Rückgängigmachen. Sind mehrere Markierungen desselben Grundrisses ausgewählt, ändert ein an einer gezogener Teil denselben Teil bei allen.

![Die Markierung einer Tür ausgewählt: ein Griff je Text, sein Drehgriff und einer an jedem Linienende](shots/marker-grips.webp)

![Das Kennzeichen verschoben und gedreht, das äußere Linienende verlängert](shots/marker-edited.webp)

Die Änderungen gehören zur Markierung und damit zu diesem Grundriss: Dieselbe Tür kann in einem anderen Grundriss anders angeordnet sein. Sie werden längs und quer zur Wand gespeichert und folgen der Tür, wenn sie verschoben, gedreht oder auf die andere Seite gespiegelt wird. In den Eigenschaften der Markierung listet **Markierung** ihre Teile: Geben Sie die Drehung eines Textes in Grad oder die Reichweite eines Endes in Papiermillimetern ein, setzen Sie einen Teil mit ↺ zurück oder alle mit **Beschriftung zurücksetzen**. Die Engine zeichnet Markierungen wie bearbeitet, daher drucken Pläne, PDF und DXF sie genauso.

![Die Teile einer Markierung in ihren Eigenschaften](shots/marker-properties.webp)

Ob die Texte quer zur Markierungslinie (längs der Wand, die Vorgabe) oder entlang der Linie liegen, wird je Beschriftungstyp in **Verwalten → Zeichnungsstandards → Beschriftungen**, Spalte **Textrichtung**, festgelegt: **senkrecht zur Beschriftungslinie** oder **entlang der Beschriftungslinie**. In beiden Fällen werden sie lesbar gezeichnet, nie kopfüber; auch ein von Hand gedrehter Text bleibt lesbar.

![Die Texte der Türmarkierung entlang der Beschriftungslinie](shots/marker-along.webp)
