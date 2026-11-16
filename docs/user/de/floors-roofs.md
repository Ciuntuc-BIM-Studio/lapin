---
title: Decken und Dächer
order: 40
summary: Zeichnen Sie Decken und Dächer über ihren Umriss, wählen Sie die Form und die Kanten eines Dachs und schneiden Sie Öffnungen.
---

## Decken

Wählen Sie **Innenausbau → Decke** (<kbd>SB</kbd>) und klicken Sie dann auf die Ecken ihres Umrisses. Schließen Sie den Umriss am ersten Punkt oder drücken Sie <kbd>Enter</kbd>. Auch eine Decke besteht aus Schichten, die unter **Innenausbau → Deckentypen** festgelegt werden. Eine Schicht eines Deckentyps kann die Dicke 0 haben (eine Abdichtung, ein Anstrich): Sie bildet nichts, wird in Schnitten als Linie gezeichnet und nach der Fläche der Decke gemessen.

In **Deckentypen** hat jede Schicht eine **Priorität** (0 bis 100) und eine **Kategorie**, wie die Schichten eines Wandtyps. Eine Wandschicht, die in die Decke läuft (**In Decken** in **Wandtypen**), durchdringt die Deckenschichten mit niedrigerer Priorität und endet auf der ersten mit gleicher oder höherer. Leer ist die Priorität die des Materials in **Verwalten → Schnittreihenfolge**, sonst 50 für den Kern und 0 für die übrigen Schichten. Eine als **Bekleidung** markierte Schicht entfällt, wenn eine Ansicht die Bodenbeläge weglässt.

Um den Umriss zu ändern, wählen Sie die Decke aus und ziehen Sie ihre Ecken. Um eine Ecke hinzuzufügen, halten Sie <kbd>Strg/⌘</kbd> gedrückt und klicken auf die kleine Raute in der Mitte einer Kante, oder Sie klicken mit der rechten Maustaste nahe einer Kante auf die ausgewählte Decke und wählen **Ecke hier hinzufügen**: Die neue Ecke entsteht dort, wo Sie geklickt haben. Zum Entfernen klicken Sie mit <kbd>Strg/⌘</kbd> auf die Ecke oder klicken mit der rechten Maustaste darauf und wählen **Ecke entfernen** (ein Umriss behält mindestens drei Ecken). Jede Änderung ist ein Schritt zum Rückgängigmachen, und dasselbe gilt für Deckenöffnungen, Dächer, Räume und Balkone. **Deckenöffnung** (<kbd>SH</kbd>) schneidet ein Loch in eine Decke, für eine Treppe oder einen Schacht.

Um den ganzen Umriss auf einmal zu vergrößern oder zu verkleinern, jede Kante um denselben Abstand (Archicads *Offset all edges*), halten Sie <kbd>Shift</kbd> gedrückt und ziehen Sie die Raute in der Mitte einer beliebigen Kante: Der Umriss folgt dem Cursor live, im Grundriss oder in 3D, und der Abstand kann beim Ziehen eingegeben werden (<kbd>Enter</kbd> übernimmt ihn). Oder klicken Sie mit der rechten Maustaste auf die ausgewählte Decke, wählen **Umriss versetzen…** und geben den Abstand ein: positiv geht nach außen, negativ nach innen. Gerade Kanten bleiben parallel und treffen sich an ihren Ecken wieder; eine gekrümmte Kante bleibt ein Bogen um ihren eigenen Mittelpunkt, und wo ein Bogen an einer Ecke auf eine andere Kante trifft, rundet das Vergrößern diese Ecke ab. Ein Versatz, bei dem eine Kante verschwinden oder der Umriss sich selbst schneiden würde, wird abgelehnt, mit dem Grund am Cursor. Er funktioniert bei Decken, Deckenöffnungen, abgehängten Decken, Dächern (jede Kante behält ihre Traufen- oder Giebelregel), Räumen und Balkonen und bei mehreren gleichzeitig ausgewählten; jeder ist ein Schritt zum Rückgängigmachen.

![Der Umriss einer Decke, versetzt durch Shift-Ziehen der Kantenmitte](shots/floor-offset-outline.webp)

### Gebogene Kanten, Abrundungen und runde Decken

Die Leiste des Werkzeugs **Decke** in der Befehlszeile zeichnet den Umriss als **Polygon** (die Ecken angeklickt), **Rechteck** (zwei gegenüberliegende Ecken), **Kreis** (der Mittelpunkt, dann ein Punkt darauf) oder **Innerhalb der Wände** (ein Klick innerhalb der Wände, die sie umschließen; die Decke reicht bis an ihre Flächen). In einem Polygon wird **Linien (Leertaste)** zu **Bögen (Leertaste)**: Die nächsten Kanten sind Bögen, jeder tangential an die vorige Kante anschließend, bis Sie erneut <kbd>Leertaste</kbd> drücken. **Deckenöffnung** und das Werkzeug für abgehängte Decken haben dieselbe Leiste, ohne **Innerhalb der Wände**.

![Das Werkzeug Decke zeichnet einen Kreis neben einer Decke](shots/floor-shapes.webp)

Dieselbe Leiste dient jedem Umrisswerkzeug (Decke, Deckenöffnung, Unterdecke, Loch, Raum und Balkon nach Umriss, Plattform, Grundstück), und drei ihrer Optionen übernehmen den ganzen Umriss mit einem Klick: **Inside walls** (die Wände, die sich um den Punkt schließen), **From drawn shape** (die kleinste geschlossene gezeichnete Form um den Punkt: eine geschlossene Polylinie, ein Rechteck oder ein Kreis, der rund bleibt) und **From room** (der Raum um den Punkt, mit seinen gebogenen Kanten).

![Die Leiste der Umrisswerkzeuge, From drawn shape gewählt](shots/outline-sources.webp)

Für Decke, Deckenöffnung, Unterdecke und Raum nach Umriss bietet die Leiste auch **Ellipse**: Mittelpunkt, Ende einer Halbachse, dann die andere. Die Kanten des Umrisses folgen der wahren Ellipse (vier Viertelkanten), genau gemessen; eine von der Ellipse weggezogene Ecke macht ihre zwei Kanten gerade, eine Ecke an einer elliptischen Kante wird nicht abgerundet. IFC trägt sie als IfcEllipse. **From drawn shape** übernimmt eine gezeichnete Ellipse ebenso.

![Eine elliptische Decke neben einer gezeichneten Ellipse](shots/ellipse-floor.webp)

Jede Kante einer Decke, einer Deckenöffnung oder einer abgehängten Decke kann gebogen sein. Klicken Sie mit der rechten Maustaste nahe einer Kante auf die ausgewählte Decke und wählen Sie **Zum Bogen machen**: Die Kante wölbt sich um ein Achtel ihrer Länge nach außen, und in der Mitte des Bogens erscheint ein runder Griff. Ziehen Sie ihn, um den Bogen durch den gewünschten Punkt zu biegen (die Raute, nun in der Mitte der Sehne, verschiebt weiterhin die Kante). **Gerade machen** macht sie wieder zur Linie. Eine Ecke zwischen zwei geraden Kanten kann abgerundet oder abgefast werden: Klicken Sie sie mit der rechten Maustaste an und wählen Sie **Ecke abrunden…** (Radius eingeben) oder **Ecke abfasen…** (wie weit entlang jeder Kante zurück). Aus der Ecke werden zwei, wo der Bogen oder die Fase auf die Kanten trifft; ein für die Kanten zu großer Radius wird abgelehnt. Jedes ist ein Rückgängig-Schritt.

![Eine Decke mit einer gebogenen Kante und einer abgerundeten Ecke](shots/floor-curved-edge.webp)

Die Decke wird entlang ihrer Bögen gebaut, keine Sehne mehr als 1 mm davon entfernt, in 3D, Grundrissen und Schnitten; ihre Fläche und ihr Umfang werden an den wahren Bögen gemessen. Eine Schicht, die über eine gebogene Kante hinausgeht (siehe [Schichtausdehnungen](walls.md#layer-extents)), folgt dem Bogen. Auch eine Öffnung kann gebogene Kanten haben, und eine gebogene Decke kann Öffnungen haben. In IFC trägt die Decke ihren Umriss mit den wahren Bögen als Grundriss-Darstellung (eine IfcIndexedPolyCurve mit Bogensegmenten) neben ihrem Körper. Dächer behalten gerade Kanten, weil die Kantenregeln eines Dachs (Traufe, Giebel) je gerader Kante gesetzt werden.

Eine Decke oder eine Unterdecke kann **eigene Löcher** haben: Inseln aus ihrem Umriss, Teil des Elements, keine eigenen Öffnungen. Wählen Sie **Hole in the outline** im Werkzeug **Opening** (unter *In a floor*) und zeichnen Sie es in der Decke (oder der gewählten Decke bzw. Unterdecke) mit derselben Leiste: ein Polygon, dessen Kanten Bögen sein können, ein Rechteck oder einen Kreis. Gewählt zeigt die Decke Ecken und Kanten jedes Lochs als Griffe, mit demselben Rechtsklick (**Make arc**, **Fillet corner…**, **Chamfer corner…**); das Kreuz in der Mitte eines Lochs verschiebt es, sein Rechtsklick bietet **Remove hole**. Die Decke wird um ihre Löcher gebaut, ihre Fläche lässt sie aus, an den wahren Bögen gemessen; ein Loch muss im Umriss und getrennt von den anderen liegen, und ein Umriss, der ein Loch nicht mehr fasst, wird abgelehnt. In **Edit sketch** sind die Löcher ebenfalls Schleifen der Skizze: zeichnen Sie eine weitere geschlossene Schleife hinein, und **Finish sketch** macht sie zum Loch (die größte Schleife ist der Umriss). In IFC sind die Löcher weitere Kurven des Fußabdrucks der Decke und werden als Löcher zurückgelesen. Eine Plattform (**Site → Hole in platform**) nimmt Löcher ebenso: das Gelände im Loch behält seine Höhen.

![Eine gewählte Decke mit einem rechteckigen und einem runden Loch im eigenen Umriss](shots/floor-holes.webp)

#### Skizze bearbeiten

Für mehr als eine Ecke oder einen Bogen: Klicken Sie mit der rechten Maustaste auf die ausgewählte Decke (oder Deckenöffnung oder abgehängte Decke) und wählen Sie **Skizze bearbeiten** (auch **Studio → Skizze bearbeiten**). Ihr Umriss öffnet sich als Skizze auf einem eigenen Grundriss, eine Linie oder ein Bogen je Kante, die Ecken zusammengehalten, die Decke selbst dort ausgeblendet; eine Leiste über dem Grundriss sagt, wessen Skizze es ist. Zeichnen und ändern Sie mit der Registerkarte **Zeichnen** (Linie, Bogen, Abrunden, Stutzen, Dehnen, Versatz…) und bestimmen und bemaßen Sie mit **Studio → Zwangsbedingungen** (siehe [Skizzen-Zwangsbedingungen](sketch-constraints.md)): Ändern Sie ein Maß, folgt der Umriss. **Skizze beenden** schreibt sie in einem Rückgängig-Schritt in die Decke zurück, die ihre Identität (ihre GlobalId) behält; **Abbrechen** lässt die Decke, wie sie war. Die Skizze muss ein geschlossener Umriss sein (Kanten Ende an Ende oder ein Kreis allein); sonst sagt Beenden, was offen ist.

![Der Umriss einer Decke als Skizze bearbeitet: eine Abrundung und ein Maß](shots/floor-sketch-edit.webp)

Die Skizze mit ihren Zwangsbedingungen und Maßen bleibt an der Decke (sie geht mit der Entwurfsabsicht der Decke in Lapin_Intent nach IFC), sodass **Skizze bearbeiten** sie wieder so öffnet, wie Sie sie verlassen haben. Wurde der Umriss seitdem mit den Griffen geändert, beginnt die Skizze neu am Umriss, ohne ihre Zwangsbedingungen. Eine aus einer IFC-Datei erkannte Decke erhält eine Skizze erst, wenn Sie sie bearbeiten; eine als Referenz behaltene wird so nicht bearbeitet.

![Die Decke nach Skizze beenden](shots/floor-sketch-done.webp)

Eine außerhalb der Wände, vor einer Tür gezeichnete Decke ist ein Podest. Für die Stufen dort hinauf siehe [Treppen](stairs.md).

Eine Schicht einer Decke kann auch über ihren Umriss hinausgehen oder davor enden, rundum oder an einer Kante, in ihren Eigenschaften eingetippt oder im Grundriss oder Schnitt mit **Schichten bearbeiten** gezogen, und ein Deckentyp kann seinen Decken ihre Verlängerungen geben: siehe [Schichtverlängerungen](walls.md#schichtverlangerungen). Dächer haben noch keinen Schichtaufbau (Eindeckung, Platte und Tragwerk sind eigene Teile), daher keine Schichtverlängerungen.

**Ändern → Element teilen** auf einer Decke fragt nach zwei Punkten einer Trennlinie: Sie läuft über sie hinaus und muss den Umriss genau zweimal schneiden. Die Decke wird zu zwei Decken; der angeklickte Teil behält die ID (GlobalId in IFC), Bögen an den Kanten bleiben Bögen (ein geschnittener Bogen bleibt ein Bogen), und eine Deckenöffnung geht an den Teil, in dem sie liegt. Eine Öffnung, die die Linie kreuzt, wird abgelehnt. **Ändern → Elemente stutzen/dehnen** nimmt eine gerade Grenze (eine Wand, einen Träger oder eine gezeichnete Linie) und schneidet die Decke dort, wobei die angeklickte Seite bleibt.

![Eine Decke mit gekrümmter Kante, entlang einer Linie geteilt: der rechte Teil ist gewählt](shots/floor-split.webp)

## Dächer

Wählen Sie **Architektur → Dach** (<kbd>RF</kbd>) und klicken Sie den Umriss der Wände darunter an. Lapin baut das Dach über diesem Umriss, oben auf seiner Ebene.

![Ein Haus mit Satteldach in der 3D-Ansicht](shots/house-3d.webp)

Wählen Sie das Dach aus, um es im Eigenschaftenbereich zu ändern:

- **Form.** Walmdach, Satteldach, Pultdach, Mansarddach, Tonnendach oder Flachdach. Ein Tonnendach ist ein gekrümmtes Gewölbe zwischen zwei Giebelseiten; **Tonne: Stich am Scheitel** legt fest, wie hoch es am Scheitel ansteigt, höchstens die halbe Spannweite.
- **Neigung und Überstand** für das ganze Dach.
- **Firstrichtung (Sattel-, Pultdach)**: auto, x oder y. Beim Pultdach ist der First die hohe Seite: auf x fällt es nach −y, auf y nach −x; **Pultdach: Neigung umkehren** lässt es zur anderen Seite fallen.
- **Jede Kante** mit ihrer eigenen Art (Traufe oder Ortgang), Neigung und ihrem Überstand. Ein Mansarddach mit Giebelkanten ist ein Mansardgiebeldach.
- **Stil.** Die Dachstile geben mit einer einzigen Wahl ein ganzes Erscheinungsbild vor: die Deckung, die Traufen, die Dachrinnen und Fallrohre.
- **Material der Dachdeckung.** Die Dachdeckung besteht aus einem Material des Projekts, standardmäßig dem der gewählten Deckung (Clay tile, Metal sheet, Slate…), das in der Farbe der Deckung angelegt wird, wenn ein Dach es zum ersten Mal braucht. Wählen Sie in den Eigenschaften oder im Konfigurator unter **Deckung und Entwässerung** ein anderes; wechselt die Deckung, wechselt es mit, außer Sie haben eines gewählt. Ansichten und Schnitte schraffieren die Deckung nach diesem Material (Ziegel, Schindeln und Schiefer in waagrechten Reihen, Blech in senkrechten Falzen, sofern Sie ihm keine eigene **Elevation**-Schraffur geben), ein Schnitt zeichnet die geschnittene Deckung mit der Schnittschraffur des Materials, und IFC, Mengenermittlung und Kostenschätzung führen die Deckung unter ihrem Material.
- **Dicke der Dachdeckung.** Leer ist die Deckung so dick wie die des Katalogs (eine Bahn 2 cm, Blech 3 cm, Ziegel 5 cm…); eine eingegebene Dicke gibt ihr die eigene. Ein aus einem anderen Programm importiertes Dach behält die der Datei.
- **Tragschale.** Ein Flachdach, ein Tonnendach und ein Dach mit **Structure** none haben unter der Dachdeckung eine Tragschale, ein eigenes Teil: **Material der Tragschale** (standardmäßig Reinforced concrete, beim ersten Bedarf angelegt) und **Dicke der Tragschale** (standardmäßig 16 cm), in den Eigenschaften oder im Konfigurator unter **Tragwerk**. Die Dachdeckung liegt darauf, das Dach bleibt so dick wie beide zusammen. Ein Schnitt zeichnet zwei Bänder, jedes nach seinem Material schraffiert; Dachfenster gehen durch beide; eine an ein Flachdach angehängte Wand reicht bis zur Unterseite der Tragschale; IFC schreibt Tragschale und Dachdeckung als zwei Dachplatten aus ihren Materialien, und Mengenermittlung und Kostenschätzung messen die Tragschale (Fläche und Volumen) und die Dachdeckung (Fläche) jeweils unter ihrem Material. Die Eigenschaften zeigen diese beiden Felder nur bei einem Dach ohne Dachstuhl, die Felder der Sparren (Querschnitte, Abstand, Festigkeitsklasse, Pfetten, Sparrenenden) nur bei einem mit Dachstuhl; ein Feld, das nur zu einer Form gehört (das der Mansarde, der Stich der Tonne, das Umdrehen des Pultdachs), erscheint nur bei dieser Form.

![Die Eigenschaften eines Dachs](shots/roof-properties.webp)

![Ein Flachdach im Schnitt: die Betontragschale und die Dachdeckung darüber, jede nach ihrem Material schraffiert](shots/roof-deck-section.webp)

**Dachkonfigurator** (die Schaltfläche neben dem Stil, oder **Architecture → Roof configurator** bei gewähltem Dach) fasst alles zusammen: links die Stile, dann Form und Kanten, Deckung und Entwässerung, Tragwerk und Öffnungen, über einem Grundriss des Dachs (ein Klick auf eine Kante macht sie auto, Traufe oder Ortgang) und, beim Tragwerk, einem Schnitt quer zum First mit dem Binder oder Sparrenpaar in der Mitte. Von einem Stil ausgehen und jedes Feld von Hand ändern; **Apply** macht daraus einen Rückgängig-Schritt. **Als Stil speichern…** behält das Dach, wie es konfiguriert ist, als Stil des Projekts, unter **Project** über denen des Katalogs aufgeführt (und in der Liste der Eigenschaften), für andere und neue Dächer; **Stil aktualisieren** speichert die Änderungen wieder hinein, und jedes Dach des Stils folgt. Ein Projektstil lässt sich umbenennen oder löschen; seine Dächer bleiben, wie sie sind.

![Der Dachkonfigurator](shots/roof-configurator.webp)

**Dachrinnen und Fallrohre.** In **Deckung und Entwässerung** des Konfigurators bekommt die Rinne ihre Größe, wie weit sie vor der Traufe und unter der Deckung hängt, ihr Gefälle zu den Abläufen (mm/m) und ihre Endstücke; die Kantentabelle schaltet die Rinne jeder Traufe ein oder aus. Die Fallrohre sind zunächst die des Dachs, eines je 10 m; **Bearbeitbar machen** macht sie zu Elementen, und **Architecture → Downpipe** setzt eines mit einem Klick auf eine Rinne. Der Griff eines Fallrohrs schiebt es nur an seiner Rinne entlang; in den Eigenschaften stellen Sie Durchmesser, Auslaufbogen und Verlauf ein, eine Kette von Abschnitten mit Länge und Winkel zur Senkrechten (zur Wand), der letzte bis zum Boden. In IFC ist eine Rinne ein IfcPipeSegment GUTTER und ein Fallrohr ein starres Segment namens Downpipe.

Die Wände auf der Ebene des Dachs enden von selbst darunter und steigen bis zu seinen Giebeln, auch unter einem gekrümmten Dach. Eine Wand auf einer anderen Ebene folgt dem Dach nach **Architektur → An Dach anbinden** (<kbd>AR</kbd>): Wählen Sie die Wände aus und klicken Sie dann auf das Dach. Dasselbe Werkzeug nimmt jeden anderen Körper über den Wänden: einen Studio-Volumenkörper, eine Decke, einen Träger, ein aus IFC importiertes Element. Die Wände steigen bis zu seiner Unterseite und werden an ihr entlang geschnitten, welche Form sie auch hat. **Vom Dach lösen** macht dies rückgängig.

**Dachfenster** und **Gaube** fügen einem Dach Öffnungen hinzu: Klicken Sie auf eine Dachfläche. Sie gehören zum Dach. Wählen Sie das Dach aus, und jedes zeigt seine Griffe im Grundriss. Ziehen Sie den Griff in der Mitte, um es auf dem Dach zu verschieben. Ziehen Sie die Kanten- oder Eckgriffe eines Dachfensters, um seine Größe auf der Dachfläche zu ändern (die gegenüberliegende Kante bleibt, wo sie ist; die Länge können Sie beim Ziehen eintippen). Die Breite verläuft entlang der Traufe, die Länge die Dachfläche hinauf, auf der Fläche gemessen. Eine Gaube hat an jeder Seite einen Griff für ihre Breite. Eine Änderung, die nicht auf das Dach passen würde, wird abgelehnt. Zum Entfernen klicken Sie mit der rechten Maustaste auf einen seiner Griffe und wählen **Dachfenster entfernen** (oder **Gaube entfernen**), drücken **Entf**, während der Cursor über seinem Griff steht, oder entfernen es mit **Strg/⌘-Klick** auf den mittleren Griff. Die Eigenschaften des Dachs listen sie ebenfalls auf: Geben Sie dort Breite und Länge eines Fensters ein, oder entfernen Sie es mit **×**. Das Dach einer Gaube ist mit dem Material der Dachdeckung gedeckt und wird mit ihr gemessen: die Mengen des Dachs nennen die **Deckung der Gauben** gesondert (die Fläche ihrer Dachflächen, die Zahl der Gauben), so zählen die Mengenermittlung nach Material, die Kostenberechnung und ein Vorgang auf der Deckung die Dachflächen (ohne die Ausschnitte der Gauben) und die Gaubendächer, jedes einmal.

![Der Griff eines Dachfensters auf dem ausgewählten Dach](shots/roof-window-grip.webp)

### Holztragwerk

**Tragwerk** legt fest, wie das Dach getragen wird:

- **rafters**: Sparren auf Pfetten, mit Firstpfette.
- **collar**: Sparrenpaare mit einem waagerechten Kehlbalken bei **Kehlbalken: Anteil der Firsthöhe** (zwei Drittel als Vorgabe), ohne Pfetten.
- **trusses**: vorgefertigte Binder entlang des Hauptfirsts im **Sparrenabstand**: Obergurte auf den Dachflächen, ein Untergurt auf den Fußpfetten, W-Füllstäbe (ein Pultbinder beim Pultdach). Die Enden eines Walmdachs behalten ihre Sparren.
- **none**: eine Platte, kein Holz.

**Sparren und Gurte** nimmt die alten Querschnitte in cm oder die nordischen in mm (45×95 bis 45×245); dazu **Füllstäbe der Binder** und **Festigkeitsklasse des Holzes** (C14 bis C30, Brettschichtholz). Die Sparren liegen mit einer Kerve auf der Fußpfette (**Sparrenauflager auf der Fußpfette**) und enden an der Traufe lotrecht oder rechtwinklig (**Sparrenköpfe an der Traufe**). Die Stile **Swedish truss roof** und **Nordic collar-tie roof** bringen ihr Tragwerk mit.

Jedes Bauteil ist ein eigener Teil: die Liste **Roof timber** führt sie als Holzliste (Bauteil, Querschnitt, Klasse, Länge, Stück, Volumen), und in IFC wird jedes ein IfcMember seines Typs, jeder Binder ein IfcElementAssembly.

## Abgehängte Decken

**Innenausbau → Decke** (<kbd>CG</kbd>) zeichnet eine abgehängte Decke über einen angeklickten Umriss; **Decke in einem Raum** füllt den angeklickten Raum entlang seines Umrisses. Eine Decke hängt in ihrer **Höhe** über dem Geschoss (2700 mm, sofern in den Eigenschaften nicht anders eingestellt).

Ihr Typ in **Innenausbau → Deckentypen** legt das **System** fest: **Platten auf sichtbarem Raster** (Breite, Länge und Dicke der Platte, Ansichtsbreite, Tiefe und Drehung des Rasters, Abhänger bis zur Rohdecke in ihrem Abstand) oder **Gipskarton, fugenlos**. Das Raster wird auf der Decke zentriert, sodass die Randplatten gleich ausfallen.

**Leuchtenpaneel** und **Luftauslass** setzen eines in das angeklickte Feld; ein Klick auf dasselbe Feld mit dem anderen Werkzeug tauscht es. Im Eigenschaftenbereich setzt **Verteilen** Leuchtenpaneele oder Luftauslässe in jedes soundsovielte Feld, **Leeren** entfernt sie. Die Listen zählen ganze Platten, Leuchtenpaneele und Luftauslässe, und die Decke geht als IfcCovering vom Typ CEILING ins IFC.

![Die Eigenschaften einer Decke: Typ, Höhe und Einbauten](shots/ceiling-properties.webp)

![Rasterdecken mit Leuchtenpaneelen und Luftauslässen in 3D](shots/ceilings-3d.webp)

## Sockelleisten

**Innenausbau → Sockelleiste im Raum** (<kbd>SK</kbd>) legt eine Sockelleiste rund um den angeklickten Raum, an den Wandflächen entlang, an jeder Tür unterbrochen (jede Öffnung, die bis zum Boden reicht); ein Klick zwischen Wänden ohne Raum folgt ihnen genauso. Die Sockelleiste eines Raums folgt dem Raum: Wird eine Wand oder eine Tür verschoben, geht die Sockelleiste mit. **Sockelleiste** zeichnet eine entlang einer Linie, die du auf einer Wandfläche anklickst (Enter beendet); die Leiste liegt auf der von der Wand abgewandten Seite, und **Seite wechseln** in ihren Eigenschaften dreht sie um.

Ihr Typ, unter **Innenausbau → Sockelleistentypen**, legt das **Profil** fest — **Glatt (eckig)**, **Abgeschrägt (MDF)**, **Profiliert (Holz)**, **Mit Hohlkehle (Metall)**, **Halbrund oben** oder **Abgerundete Kante**, maßstäblich daneben gezeichnet —, die **Höhe** (40 bis 300 mm), die **Dicke** (5 bis 40 mm), das **Material** und seine Farbe. **Aus der Bibliothek** holt fertige Typen ins Projekt: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 und Bullnose 70. Solange ein Sockelleisten-Werkzeug aktiv ist, zeigt das Eigenschaftenfenster den Typ, den es legt, vor dem Klick zu wählen.

In ihren Eigenschaften hat eine Sockelleiste ihren Typ, einen **Versatz unten** über dem Boden und **Lücken an den Türen** (aus: Sie läuft vor ihnen durch). **Sockelleistenliste** (oder **Sockelleisten** in der Liste neuer Bauteillisten im Projektbrowser) führt sie nach Typ mit Stücken und Länge auf, ohne die Lücken der Türen. Die Leiste ist an den Ecken auf Gehrung geschnitten; ein Grundriss zeichnet ihre Vorderkante, und in IFC wird eine Sockelleiste ein IfcCovering vom Typ SKIRTINGBOARD, eines IfcCoveringType desselben Typs.

![Die Sockelleistentypen: das Profil maßstäblich gezeichnet, die Bibliothek](shots/skirting-types.webp)

![Die Eigenschaften einer Sockelleiste: ihr Typ, der Versatz unten und die Lücken an den Türen](shots/skirting-properties.webp)
