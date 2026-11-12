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

**Laibungsbekleidung** bekleidet ihre Laibungen und den Sturz im Loch: **Putz** (15 mm, wenn nicht anders angegeben) oder ein **Futter** (20 mm Holz), mit **Belagsdicke** und **Belagsmaterial**; eine Öffnung über dem Boden wird auch unten bekleidet. Die Bekleidung gehört zum Körper der Wand, in 3D, Ansichten, Schnitten und Mengen, und die lichte Öffnung ist, was sie übrig lässt. **Sturz** ist **Regel der Wand** (der Sturz, den der Wandtyp verlangt, wie über einer Tür) oder **Keine**. Mehrere ausgewählte Öffnungen ändern sich zusammen, in einem Rückgängig-Schritt.

**Öffnung bearbeiten…** öffnet ihren Konfigurator: dieselben Felder über einem Bild der Öffnung in ihrer Wand, zusammen angewendet. **In Tür ändern…** und **In Fenster ändern…** füllen sie mit einer Tür oder einem Fenster eines gewählten Typs, wo sie ist: Lage, Brüstung und GlobalId bleiben, und **Typ an die Öffnung anpassen** gibt ihr einen Typ ihrer Größe und Sturzform (den des Projekts in dieser Größe, sonst einen aus dem gewählten Typ, nach der Größe benannt). Umgekehrt nimmt **In Öffnung ändern** bei einer ausgewählten Tür oder einem Fenster diese heraus und lässt das Loch als Öffnung gleicher Größe und Form. Jedes ist ein Rückgängig-Schritt.

![Eine Wandöffnung in ihrem Konfigurator: halbrunder Sturz, die Laibungen verputzt](shots/wall-opening-dialog.webp)

## Der Konfigurator

**Architektur → Türen und Fenster** öffnet den Konfigurator. Jeder Tür- und Fenstertyp des Projekts ist links aufgelistet. Sie können ändern:

- den Namen und das Kennzeichen des Typs (den Buchstaben in den Beschriftungen);
- seine Breite und Höhe;
- die Tiefe und Breite des Rahmens;
- seine Spalten und Reihen von Flügeln und wie sich jeder Flügel öffnet;
- die Laibungsoberfläche, die Umrahmung an der Fassade, die Schwelle und die Farben.

Die Zeichnungen rechts zeigen den Typ so, wie er in Ansicht, Grundriss und Schnitt gezeichnet wird, während Sie ihn ändern. Darunter zeigt eine **3D-Vorschau** den Typ in einem Wandstück vom ersten Wandtyp des Projekts, mit Laibung, Fensterbänken, Futter und Materialien, während Sie ihn ändern (auch ein Erker-, Bogen- oder Kastenfenster): ziehen zum Drehen, Mausrad zum Zoomen, mit der rechten Taste ziehen zum Verschieben. Sie beginnt mit der Ansicht von außen.

**Links und rechts.** Der Anschlag einer Tür wird so benannt, wie Architekten ihn benennen: Man blickt vom Drücker zu den Bändern; ein Flügel, der links dieser Linie aufgeht, schlägt links an, einer, der rechts aufgeht, rechts. Die Auswahl des Konfigurators für einen Flügel sagt es so, mit der Seite seiner Bänder unter **Öffnungsart** eines Feldes, von außen gesehen, von der Seite, von der die Flügel aufgedrückt werden: *Drehflügel rechts (Bänder links)*. Eine IFC-Datei benennt den Anschlag auf ihre eigene Weise (die Bänder gesehen in die Richtung, in die der Flügel aufgeht), daher wird dieselbe Tür dort als SINGLE_SWING_LEFT geschrieben; Lapin übersetzt beim Schreiben und Lesen von IFC das eine in das andere. (DIN benennt wieder anders.) Links und rechts eines Fensters werden von innen gesehen, von der Seite, zu der die Flügel aufgehen: *Drehflügel rechts (Bänder links)* im Konfigurator, dessen Felder von außen gesehen sind. IFC schreibt dasselbe Fenster als links. Die Ansicht ist von innen gesehen, von der Seite, zu der die Flügel aufgehen, sofern **Von außen** nicht angehakt ist; wie der Grundriss zeigt sie die Tür so, wie sie im Modell steht. Eine auf den anderen Anschlag umgekehrte Tür (**Anschlag** oder ein Umkehrpfeil entlang der Wand) ist das Spiegelbild ihres Typs; die Bauteillisten führen sie auf einer eigenen Karte (siehe [Bauteillisten](schedules.md)).

![Der Konfigurator für Türen und Fenster](shots/joinery-configurator.webp)

**Typ speichern** übernimmt Ihre Änderungen. Jede Tür und jedes Fenster dieses Typs folgt ihnen. **Damit platzieren** schließt den Konfigurator und beginnt, diesen Typ zu platzieren.

### Felder, Pfosten und Profile

Eine Tür oder ein Fenster ist ein Raster aus Feldern. Jedes Feld wählt, wie es sich öffnet und was es füllt, sodass ein Typ zwei Flügel mit Seitenteilen und einem Oberlicht darüber aufnehmen kann oder ein Fenster mit einem Vollpaneel unter einem Flügel.

**Spalten (relativ)** und **Reihen (relativ)** teilen die Öffnung schnell: `1 1` sind zwei gleiche Felder, `1 1 1` drei, `2 1` ein breites neben einem schmalen. Reihen zählen von unten nach oben.

**Spaltenbreiten** und **Reihenhöhen** geben dieselben Maße in Millimetern. Geben Sie sie ein, das letzte nimmt den Rest. In der Ansicht können Sie auch einen Pfosten oder einen Riegel ziehen; der Abstand wird beim Ziehen angezeigt.

**Pfostenbreite** und **Riegelbreite** sind eigene Maße, getrennt von der **Rahmenbreite**. Ein Rahmen von 50 mm mit Pfosten von 50 mm wirkt überall 50 mm breit.

**Felder** listet jedes Feld auf. Klicken Sie eines an, in der Liste oder in der Ansicht, um es zu bearbeiten:

- **Öffnungsart**: fest, ein Flügel (Dreh, Dreh-Kipp, Kipp, Klapp, Schwing, Schiebe) oder, bei einer Tür, ein Türflügel.
- **Füllung**: **Glas** oder **Vollpaneel**. Die Flügel und Seitenteile einer Tür können geschlossen sein, ebenso ein Fensterfeld (ein Paneel unter einem Flügel, ein undurchsichtiges Feld).
- **Teilen**: **In Spalten** oder **In Reihen**, in einem Abstand in Millimetern von der linken oder unteren Kante des Feldes (standardmäßig die Hälfte). Quer geteilt behält ein Türflügel seinen unteren Teil, und der obere wird ein festes verglastes Oberlicht.
- **Zusammenführen**: mit dem Feld daneben, darüber oder darunter, wenn sie eine ganze Kante teilen. Pfosten und Riegel, die kein Feld mehr braucht, entfallen.

![Eine Ladentür in einem Typ: zwei Flügel, Seitenteile und ein durchgehendes Oberlicht](shots/joinery-panes.webp)

**Profilsystem** beginnt den Typ mit einem Profil aus **PVC**, **Aluminium**, **Holz** oder **Stahl (schlank)**: Es setzt die Maße von Rahmen, Pfosten, Riegeln und Flügeln und gibt den Profilen ihre feineren Formen. Der Rahmen hat einen **Falz**, auf den der Flügel schließt, der Flügel überdeckt den Rahmen um seinen **Flügelüberschlag**, **Glasleisten** halten das Glas, und ein Fenster hat eine Tropfkante unter dem unteren Holz. Jedes Maß bleibt bearbeitbar. **Einfach (glatte Profile)** behält schlichte rechteckige Teile wie bisher.

**Rahmen aus einer Bibliothek** und **Flügel aus einer Bibliothek** nehmen ein Profil aus Ihren [Profilbibliotheken](profiles.md) (etwa einen aus einer DXF gelesenen Aluminium- oder PVC-Querschnitt): gerade Rahmen- und Flügelstäbe haben dann seine wirkliche Kontur samt Hohlräumen, in 3D, in Schnitten und Grundrissen, mit seiner Breite und Tiefe. Es ersetzt, wovon es die wirkliche Form ist: Anschlag, Lippe und Tropfkanten des Systems; Glasleisten bleiben. Gebogene Köpfe und runde Fenster behalten ein einfaches Band, ebenso die Pfosten und Riegel des Rasters. **Einfach** kehrt zu einem Rechteck dieser Größe zurück.

![Ein PVC-Fenster: die Flügel über dem Rahmen, die Glasleisten, ein Vollpaneel unter einem festen Feld](shots/joinery-profiles-3d.webp)

Unter **Details** schalten **Fensterbank innen** und **Fensterbank außen** die Fensterbänke ein und aus, und **Laibungsverkleidung** schaltet den Abschluss der Laibung an den Seiten und am Sturz ein. Wo Fensterbänke und Verkleidungen nicht gebraucht werden, entfernen Sie die Haken.

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

**Kämpfer am Bogenansatz**, standardmäßig angehakt, behält diesen Kämpfer und das feste Oberlicht darüber. Ohne Haken wird es ein Rundbogen-Drehflügel: die oberste Reihe der Scheiben reicht in den Bogen, und Rahmen, Flügel, Glas, Sprossen und Öffnungssymbol folgen dem Bogen ringsum, ohne Sprosse darunter. Die Scheibe öffnet, wie Sie es wählen (Dreh, Dreh-Kipp, fest), Pfosten laufen bis zum Bogen, und Grundrisse, Schnitte, Ansichten, die 3D-Vorschau und IFC zeigen es so. Der Stil **Rundbogen-Drehflügel** setzt auf einmal einen Segmentbogen mit einem Drehflügel.

![Ein Rundbogen-Drehflügel: ein Drehflügel, der einem Segmentbogen folgt, ohne Kämpfer darunter](shots/joinery-arched-casement.webp)

### Rahmenloses Glas

**Rahmenloses Glas** macht eine Tür oder ein Fenster nur aus Einscheibensicherheitsglas, ohne Zarge und ohne Flügelrahmen; der Stil **Rahmenloses Glas** setzt es auf einmal, mit einem Flügel auf Drehlagern. Bei einer Tür sagt **Befestigung**, was den Flügel hält: **Drehlager und Eckbeschläge** (ein Bodentürschließer und ein oberes Drehlager, ein Beschlag an jeder Ecke der Bandseite und ein Schlosskasten unten gegenüber) oder **Glastürbänder** (zwei, drei bei einem Flügel über 2,30 m, an das Glas geklemmt und an der Wand oder der Scheibe daneben befestigt). Die **Glasdicke** ist 6 bis 25 mm (12 mm voreingestellt), die **Fuge um das Glas** der Abstand zur Öffnung und zwischen den Scheiben, die **Fuge unter dem Flügel** der zum Boden. Ein Flügel dreht, pendelt oder schiebt; eine Scheibe daneben oder darüber ist fest und steht in einem U-Profil am Boden und unter dem Sturz. Ein Schiebeflügel hängt an einer Laufschiene über die ganze Öffnung, vor den festen Scheiben zum Raum hin, und läuft 50 mm über sie hinaus, wie bei einer Glastrennwand oder Duschwand. Der Griff ist ein Stangengriff auf beiden Seiten, wenn du keinen anderen wählst. Ein rahmenloses Fenster ist eine Festverglasung. Grundrisse zeichnen den Flügel offen als sein Glas, Ansichten die Beschläge und die Schiene; in IFC hat der Typ keine Zarge (seine Dicke ist die des Glases).

![Eine rahmenlose Glastür auf Drehlagern mit Eckbeschlägen, im Konfigurator](shots/joinery-frameless-door.webp)

**Bodentürschließer (Bodenscharnier)** hängt einen Drehflügel an einen im Boden eingelassenen Türschließer unter seinem Drehpunkt, mit einem oberen Drehlager und einem Eckbeschlag darüber; **Neue rahmenlose Glastür auf Bodentürschließer** legt eine an (1000 × 2100, pendelnd, bei 90° festgestellt). **Bodentürschließer** wählt das Modell nach dem schwersten und breitesten Flügel, den es trägt, und seiner Schließkraft (EN 1154); **Automatisch (der leichteste, der den Flügel trägt)** wählt für Sie (12 mm Glas wiegt 30 kg/m²), ein zu schwaches Modell wird abgelehnt. **Öffnungsart** ist **Einseitig öffnend** oder **Pendelnd (öffnet in beide Richtungen)**, beide Schwünge im Grundriss gezeichnet; **Drehpunkt vom Rand** ist standardmäßig 55 mm (30 bis 150 mm); **Unterer Beschlag** ist ein **Unterer Eckbeschlag** oder ein **Fersenband**; **Feststellung** ist keine, 80°, 90°, 105° oder 120°. Der Kasten liegt unter dem Fußbodenbelag: Grundrisse zeichnen seine Abdeckplatte und schwenken den Flügel um seinen Drehpunkt, Ansichten den Kasten gestrichelt und die Drehachse. Die Türliste nennt die Aufhängung, und IFC schreibt sie in den Satz `Lapin_FloorSpring` (Modell, Flügelgewicht, Abstand des Drehpunkts, Beschläge, Feststellwinkel). Die Modelle sind Richtwerte: prüfen Sie die Herstellerangaben.

![Eine rahmenlose Glastür auf Bodentürschließer, pendelnd, mit Fersenband, im Konfigurator](shots/joinery-floor-spring.webp)

**Sprossen** teilen die Scheiben: **Raster (Kolonial)** mit Feldern in der Breite und Höhe, **Prairie** mit Sprossen nahe den Rändern, oder ein **Rautenmuster**.

An der Fassade kann ein Fenster neben der Fasche einen **Schlussstein**, eine **Bekrönung über der Öffnung** (ein **Gesims**, eine **Giebelverdachung**, einen **Segmentgiebel** oder ein **Überschlagsgesims**, das einem Bogen folgt) und ein **Brüstungsfeld unter der Sohlbank** mit Konsolen haben.

**Einen Stil anwenden…** setzt Form, Sprossen und Schmuck auf einmal: Modern, Klassisch, Georgianisch, Viktorianisch, Romanisch, Barock, Gotisch, Tudor, Prairie, Traditionell oder ein Rundfenster. Alles bleibt änderbar. Die Auswahl zeigt danach den Stil des Typs; sobald Sie etwas ändern, das der Stil gesetzt hat, steht dort *Georgianisch (geändert)*, und den Stil erneut zu wählen wendet ihn erneut an.

![Ein barockes Fenster: Rundbogen mit Strahlen, Schlussstein und Segmentgiebel](shots/joinery-styles.webp)

![Bogenfenster in 3D](shots/window-styles-3d.webp)

### Erker-, Rund- und Kastenfenster

Ein Fenstertyp kann aus der Wand vorspringen. Im Konfigurator macht **Projects** ihn zu einem **Bay** (eine Mitte und zwei schräge Seiten), einem **Bow** (gleiche Felder auf einem Bogen) oder einem **Box** (Seiten rechtwinklig zur Wand), und **New bay window** beginnt einen neuen Typ als 45°-Erker. Das Fenster füllt weiterhin eine Öffnung in der Wand, so breit und hoch wie der Typ: es wird gesetzt, verschoben, kopiert, gespiegelt, nummeriert und gelistet wie jedes Fenster. **Architecture → Bay window** (<kbd>BW</kbd>) setzt eines in die angeklickte Wand: den im Konfigurator gewählten Typ, wenn er vorspringt, sonst den ersten Erker des Projekts, sonst einen 45°-Erker.

Die Spalten des Rasters sind seine Flächen. Beim Erker und Kasten sind die erste und letzte Spalte seine Seiten, die dazwischen seine Mitte; ein Rundfenster hat eine Fläche je Spalte, deren Breiten sich den Bogen teilen. Jedes Feld öffnet, wie seine **Öffnungsart** sagt, die Seiten können also öffnen und die Mitte fest sein. Dann:

- **Projection**, von der Außenfläche der Wand bis zur Außenfläche der Rahmen, beim Erker der Winkel **Sides to the wall** (15° bis 90°);
- **Sides**: die Seiten des Kastens verglast oder massiv, mit ihrem Material;
- **Seat inside**: die Sitzbank an der Brüstung, wie weit sie in den Raum reicht, ihr Material;
- **Sill outside** unter den Flächen;
- **Base**: keiner (an der Wand hängend), **Wände bis zum Boden**, **eine auskragende Platte** oder **eine Platte auf Konsolen**, mit Dicke, Überstand und Material;
- **Head**: ein **Walm**-, **Pult**- oder **Flachdach** oder eine **Platte**, die Dicke an der Traufe, Überstand, Neigung und Material, und ein **Anschlussblech an der Wand**.

Rahmen, Flügel, Glas und Griffe sind die des Typs, seine Materialien und Griffe gelten also für alle Flächen. **Bay style** setzt alles auf einmal: 45°- oder flacher 30°-Erker, Erker auf dem Boden, Rundfenster mit fünf oder vier Feldern, Kasten mit verglasten oder massiven Seiten, Erker auf Konsolen. Wie bei **Einen Stil anwenden…** zeigt die Auswahl den Erkerstil des Fensters, mit *(geändert)*, sobald Sie ändern, was er gesetzt hat. **Save as bay style…** behält den konfigurierten als Stil des Projekts, neben Lapins; umbenennen oder löschen in der Liste unter den Feldern.

Im Grundriss sind die Flächen geschnitten, Sitzbank, Fensterbank und Unterbau unter dem Schnitt gezeichnet und das Dach gestrichelt darüber; Ansichten, Schnitte und 3D zeigen den ganzen Körper. Die Fensterliste hat eine Karte für den Typ, die Flächen abgewickelt, mit einer Zeile **Projecting** (Art, Auskragung, Flächen); seine Fläche ist die der verglasten Flächen, seine Details messen Dach, Sitzbank und Unterbau. In IFC ist es ein IfcWindow mit benutzerdefinierter Teilung (*Bay 45°, projection 600, 3 faces, 4 panels*), dessen Körper der ganze Erker ist, die Felder Fläche für Fläche; Lapin liest den Erker daraus zurück.

![Ein Rundfenster im Konfigurator](shots/bay-configurator.webp)

![Erker, Rund-, Kasten- und Konsolerker im Grundriss](shots/bay-plan.webp)

![Erker, Rund-, Kasten- und Konsolerker in 3D](shots/bay-3d.webp)

### Griffe

**Griff** wählt den Griff des Typs: **Drücker**, **Knauf**, **Stoßgriff**, **Fenstergriff** oder keinen. Standardmäßig hat eine Tür einen Drücker und ein Fenster einen Fenstergriff. Eine Tür hat den Griff auf beiden Seiten, in der angegebenen **Griffhöhe**; **Griffarbe** setzt seine Farbe. Griffe erscheinen in 3D, in Schnitten und Ansichten und in den Zeichnungen des Konfigurators, wenn **Beschläge** angehakt ist.

### Lage des Rahmens und Fensterbänke

**Position in the wall** (Lage in der Wand) legt fest, wo der Rahmen in der Wanddicke sitzt, als Maß und nicht nur zentriert: Wählen Sie **From the exterior finish**, **From the core** oder **From the wall’s centre** und **Set back**, wie weit die Außenfläche des Rahmens hinter dieser Bezugsfläche liegt (ab der Mitte: wie weit die Rahmenmitte). *From the exterior finish* und 50 mm setzt den Rahmen 50 mm hinter den Putz. Rahmen, Laibungen, Fensterbänke und Öffnungssymbole wandern mit, in Grundrissen, Schnitten, 3D und IFC. Ein Typ ohne Lage bleibt **Centred in the wall**. In der Eigenschaftenleiste hat eine Tür oder ein Fenster **Frame in the wall** für eine eigene Lage (**As the type** gibt sie auf) und **Set back** für den Abstand.

![Der Konfigurator mit dem Rahmen 50 mm hinter der Außenbekleidung](shots/joinery-position.webp)

Die **Exterior sill** (Fensterbank außen) hat ein **Slope** (Gefälle) in Grad (zunächst 5°, 15° für eine Metallbank). Eine geneigte Bank fällt vom Rahmen weg und nimmt alles darüber aus der Wand: Putz, Kern und Innenputz werden nach ihrer geneigten Oberseite geschnitten, in 3D, in Schnitten und Ansichten und in den Mengen.

### Daten: U-Wert, Feuerwiderstand, Beschläge

**Data…** unter dem Typ im Konfigurator (und in der Eigenschaftenleiste für die gewählten Türen und Fenster) öffnet ein Feld mit freiem Text: **U-value**, **g-value**, **Safety glass** (innen Verbundglas, beidseitig vorgespannt…), **Fire rating** (EI 30, EI2 60-C5…) und **Fittings**; **More data** fügt eigene hinzu, etwa *Sauna window* oder *Frosted glass*. Nichts wird aus einer Liste gewählt. Der eigene Wert einer Tür oder eines Fensters geht dem des Typs vor; ein leeres Feld fällt auf den des Typs zurück (grau angezeigt).

![Das Datenfeld eines Fenstertyps](shots/joinery-data.webp)

Es sind die Parameter des Typs: Die Türen- und Fensterlisten zeigen jeden als Zeile, Beschriftungen lesen sie als `{U-value}`, `{g-value}`, `{Safety glass}`, `{Fire rating}` und `{Fittings}`, und die IFC behält sie. Der Feuerwiderstand ist `FireRating` in `Pset_WindowCommon` oder `Pset_DoorCommon`; ein U-Wert, der als Zahl lesbar ist (0.9 oder *0,9 W/m²K*), ist dort auch `ThermalTransmittance`, bei einem Fenster mit `GlazingAreaFraction`; der Rest steht im Set `Lapin_Joinery`.

![Die eigenen Daten eines Fensters in der Eigenschaftenleiste](shots/joinery-data-properties.webp)

### Parameter

**Parameter · in Bauteillisten**, unten im Konfigurator, enthält die eigenen Parameter des Typs: **+ Feuerwiderstand** (in IFC in Pset_DoorCommon oder Pset_WindowCommon als FireRating geführt), **+ Hersteller**, **+ Produktcode**, **+ Website** oder jeden Parameter, den Sie benennen. Die Türen und Fenster des Typs erben sie, und jeder wird zu einer Zeile der Tür- und Fensterlisten: siehe [Bauteillisten und Diagramme](schedules.md).

## Sonnenschutz

**Architecture → Sunshade** (<kbd>SN</kbd>) setzt einen Sonnenschutz an die angeklickte Tür oder das Fenster: so groß wie die Öffnung, über die Laibungen hinaus, über dem Sturz oder davor, und er folgt ihm, wenn es verschoben wird. **Sunshade along a facade** nimmt zwei Punkte: an einer Wand läuft er entlang ihrer Außenfläche, sonst entlang der Linie, nach rechts. Die Leiste beider Werkzeuge wählt den Typ oder einen Stil, aus dem ein neuer Typ entsteht.

Die Arten:

- **Canopy** (Vordach): massiv, mit Lamellen oder aus Glas, Auskragung, Dicke, Neigung und eine Blende, auf Konsolen, Zugstangen oder Stützen;
- **Vertical fins**: Tiefe, Dicke, Abstand und Winkel, Riegel oben und unten;
- **Louvres (brise-soleil)**: horizontale Lamellen, flach, Tragflächenprofil oder Kasten, Tiefe, Abstand und Winkel, abgerückt von der Wand, zwischen Endplatten;
- **Egg-crate**: horizontale und vertikale Platten zusammen;
- **Perforated screen**: ein Blech vor der Wand, mit runden oder quadratischen Löchern oder Schlitzen, Größe und Teilung;
- **Awning** (Markise): Stoff im Gefälle, Volant, Kassette und Gelenkarme;
- **Pergola**: Sparren, ein Balken, Latten oben und Stützen bis zum Boden;
- **External venetian blind**: Lamellen, wie weit herabgelassen, Kasten und Führungsschienen.

**Sunshade types** (unter Types) bearbeitet einen Typ: seine Art und jeden Parameter, die Materialien, neben seiner Zeichnung von außen, im Schnitt und im Grundriss. Jeder Sonnenschutz des Typs folgt einer Änderung. Ein neuer Typ beginnt mit einem Stil: Betonvordach, Holzvordach auf Konsolen, Glasvordach mit Zugstangen, Lamellenvordach, Ziegeldach, Brise-Soleil, vertikale Lamellen, Betonwabenraster, gelochter Cortenschirm, Stoffmarkise, Holzpergola, Außenjalousie; **Save as style…** behält einen Typ als Stil des Projekts.

![Die Eigenschaften eines Vordachs nach der Mittagshilfe](shots/sunshade-properties.webp)

Ausgewählt zeigt ein Sonnenschutz seinen Typ, worauf er steht, und seine eigene **Projection** (leer: die des Typs). **Full shade at noon, 21 June** berechnet für ein Vordach, eine Markise oder eine Pergola über einem Fenster, wie weit es auskragen muss, damit das ganze Fenster im Schatten liegt, wenn die Sonne am längsten Tag am höchsten steht, am Ort des Projekts (**Manage → Location**; Bukarest, bis einer gesetzt ist), und setzt es. An einer Südfassade auf 45° N steht die Sonne dann 68,4° hoch: ein 1,5 m hohes Fenster unter einem Vordach 150 mm über dem Sturz braucht 650 mm. Seine Griffe ziehen seine Enden entlang der Wand oder Linie und seine Auskragung.

![Die Sonnenschutztypen: Lamellen, von außen, im Schnitt und im Grundriss](shots/sunshade-types.webp)

Grundrisse zeichnen einen Sonnenschutz über dem Schnitt gestrichelt, einen, der ihn kreuzt, geschnitten (Lamellen, ein Schirm) und einen darunter gesehen; Ansichten, Schnitte und 3D zeigen seinen Körper mit der Schraffur seines Materials, und er wirft seinen Schatten mit dem Rest (**View → Sun and shadows**). **Sunshade schedule** listet sie nach Typ mit Ort, Länge und Auskragung, Anzahl und beschatteter Fläche; ihre Mengen (Länge, Auskragung, Fläche, Volumen) nehmen die Kostenpositionen ihres Typs. In IFC sind sie IfcShadingDevice eines IfcShadingDeviceType: eine Markise AWNING, eine Außenjalousie JALOUSIE, Lamellen LOUVER in IFC 4.3 (benutzerdefiniert in IFC4), die anderen benutzerdefiniert (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin liest sie zurück, wie sie waren.

![Sonnenschutz im Grundriss](shots/sunshades-plan.webp)

![Ein Vordach, Lamellen, vertikale Lamellen, eine Markise, eine Pergola und eine Jalousie in 3D](shots/sunshades-3d.webp)

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
