---
title: Möbel
order: 47
summary: Sofas, Ecksofas, Sessel, Stühle, Betten, Kleiderschränke, Kommoden, Bücherregale, Sideboards, Tische, Schreibtische und Küchen in sechs Anordnungen aus parametrischen Typen, mit einem Klick an Wände gestellt.
---

## Möbel platzieren

Die Registerkarte **Innenausbau** hat, nach dem Ausbau, ein Werkzeug für jede Art von Möbelstück: **Sofa** (<kbd>SO</kbd>), **Ecksofa**, **Sessel** (<kbd>AC</kbd>), **Stuhl**, **Bett** (<kbd>BD</kbd>), **Nachttisch**, **Kleiderschrank** (<kbd>WD</kbd>), **Kommode**, **Bücherregal**, **Sideboard**, **Tisch** (<kbd>TB</kbd>), **Couchtisch** und **Schreibtisch**. Wählen Sie eines und klicken Sie in den Grundriss.

Klicken Sie nahe an einer Wand, und das Möbel stellt sich mit dem Rücken an sie. Seine Rückseite liegt an der Oberfläche der Wand (am Putz, nicht am Kern), seine Vorderseite zeigt in den Raum, und es rutscht an der Wand entlang, bis es nicht mehr in die Wand an einer Ecke hineinragt. Klicken Sie weiter weg von jeder Wand, und das Möbel steht dort, wo Sie geklickt haben.

Solange ein Werkzeug aktiv ist, zeigt die Leiste der Befehlszeile:

- **den Typ**, den das Werkzeug platziert, gewählt aus den Typen dieser Art im Projekt (gibt es noch keinen, entsteht der erste beim Platzieren);
- **die Drehung**, 0°, 90°, 180° oder 270°, für ein frei stehendes Möbel;
- **An Wände**, das Sie ausschalten können, um ein Möbel genau dort zu platzieren, wo Sie klicken, auch neben einer Wand.

Der Eigenschaftenbereich zeigt denselben Typ schon vor dem Klick, mit seiner Größe und **Typen bearbeiten…**.

![Ein möbliertes Wohnzimmer, eine Küche und ein Schlafzimmer im Grundriss](shots/furniture-plan.webp)

Grundrisse zeichnen Möbel mit ihrem Symbol, nach Zeichenkonvention, nicht geschnitten:

- ein Sofa mit Rückenlehne, Armlehnen und Kissen;
- ein Bett mit Kopfteil, Kissen und umgeschlagener Bettdecke;
- ein Kleiderschrank mit Kleiderstange und Türen;
- eine Küche mit Spüle und Kochfeld, die Hängeschränke gestrichelt.

Ansichten und Schnitte zeichnen die Körper mit den Schraffuren ihrer Materialien. In 3D sind die Möbel Low-Poly-Körper, die Lapin erzeugt.

![Das möblierte Haus in 3D](shots/furniture-3d.webp)

## Stühle um einen Tisch und die Küche

**Stühle um einen Tisch:** Klicken Sie auf einen Tisch oder einen Schreibtisch. Stühle kommen an die langen Seiten, etwa alle 600 mm, und je einer an jedes Ende, wenn es breit genug ist; ein runder Tisch bekommt sie im Kreis. Jeder Stuhl blickt zum Tisch. Ein Schreibtisch bekommt Stühle nur an seiner Vorderseite. Die Stühle haben den Typ, der für das Werkzeug **Stuhl** gewählt ist.

**Küche** (<kbd>KT</kbd>) wird mit zwei Punkten entlang einer Wand gezeichnet:

- die Länge der Linie ist die Länge der Küche, und ihre Rückseite liegt an der Oberfläche der Wand;
- die Unterschränke werden gleichmäßig auf etwa 600 mm aufgeteilt;
- sie hat eine Arbeitsplatte, eine Spüle und ein Kochfeld (wo sie entlang der Zeile stehen, legt der Typ fest), Hängeschränke darüber und an einem Ende einen Hochschrank für den Kühlschrank.

Die **Länge** einer Küche bleibt in ihren Eigenschaften änderbar; die Schränke werden neu aufgeteilt.

### Küchenanordnungen

Ein Küchentyp hat ein **Layout** (Anordnung): **One wall** (die gerade Zeile), **Galley** (zwei parallele Zeilen mit einem Gang dazwischen), **L-shaped**, **U-shaped**, **Island** (eine Zeile an der Rückwand und eine freie Insel mit ihrem Gang) oder **Peninsula** (ein L mit einer Zeile, die aus dem Schenkel herausragt, die Arbeitsplatte für Hocker überstehend). Die Wahl startet den Typ mit den Maßen der Anordnung, und jedes bleibt neben der Vorschau änderbar: die Schenkellängen ab der Rückwand, der Gang zwischen den Zeilen, die Länge der gegenüberliegenden Zeile, Länge, Tiefe und Lage der Insel, Länge und Überstand der Halbinsel und bei einem L oder einer Halbinsel die Ecke links oder rechts.

![Die sechs Anordnungen im Grundriss: einzeilig, zweizeilig, L, U, Insel und Halbinsel](shots/kitchen-layouts.webp)

**Sink on** und **Hob on** setzen sie auf die Hauptzeile oder auf den anderen Teil (Schenkel, gegenüberliegende Zeile, Insel, Halbinsel), **Sink at** und **Hob at** sagen, wo entlang, und der **Tall unit (fridge)** steht an einem freien Ende. Die Eckschränke sind blind; Hängeschränke gibt es nur an Zeilen an einer Wand, nicht an Insel oder Halbinsel.

![Ein Küchentyp: L-förmig](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) zeichnet jede Anordnung mit zwei Punkten entlang der Wand, an der die Hauptzeile steht: Die Linie ist ihre Länge, ihre Rückseite liegt auf der Wandfläche, und ein L, eine Halbinsel, ein U oder eine Zweizeile wird in die Ecke oder zwischen die Wände geschoben, in deren Nähe gezeichnet wird. **Flip** gibt einem L oder einer Halbinsel die Ecke auf der anderen Seite. Die Möbelliste nennt die Anordnung und die Tiefe der Grundfläche. In der IFC bleibt es ein IfcFurniture eines benutzerdefinierten Kitchen-Typs.

### Küchenschränke und Geräte

Ein Küchentyp kann unter seinen Parametern in **Möbeltypen** seine eigenen **Schränke** auflisten, in zwei Reihen:

- die **Unterschrankreihe**: die Schränke der Hauptzeile von links, von vorn gesehen (nach einer Blindecke);
- die **Oberschrankreihe**: die Schränke, die darüber hängen.

**Hinzufügen** setzt einen Schrank der gewählten Art ans Ende einer Reihe. **Typische Zeile** beginnt die Unterschrankreihe mit einer Spüle, einem Geschirrspüler, einem Schubladenschrank und einem Kochfeld über einem Backofen. Die Pfeile verschieben einen Schrank nach links oder rechts, und der Papierkorb nimmt ihn heraus.

Jeder Schrank hat eine **Breite**. Ein Schrank nimmt jede Breite von 100 bis 800 mm: Wählen Sie 150, 200, 300, 400, 450, 500, 600 oder 800, oder tippen Sie eine andere. Ein Gerät nimmt die Größen, in denen es hergestellt wird. Die Schränke der Unterschrankreihe sind:

- **Unterschrank**, dessen **Front** eine Schublade über einer Tür ist, Türen (eine oder zwei, bei einer einzelnen Tür mit der Seite, auf der sie **angeschlagen** ist), Schubladen (1 bis 4), Topfauszüge (eine flache Schublade über zwei tiefen) oder offene Regale;
- **Spülenschrank**: eine Einzelspüle, eine 1½-Becken-Spüle, eine Doppelspüle (ab 800) oder eine Eckspüle, mit normaler Armatur, ausziehbarer Armatur oder ohne. Eine Eckspüle reicht in die Blindecke, wenn sie neben ihr steht.
- **Kochfeld über Backofen**, 600, 800 oder 900 breit, Induktion oder Gas, mit oder ohne den Backofen darunter;
- **Freistehender Herd**, 500, 600 oder 900 breit, Induktion oder Gas. Die Arbeitsplatte endet auf beiden Seiten von ihm.
- **Geschirrspüler**, integriert, 450 oder 600 breit, hinter einer raumhohen Front, die zu den anderen passt;
- **Kühlschrank**: Unterbau, hoch und integriert (eine Kühlschranktür über einer Gefrierfachtür) oder freistehend;
- **Hochschrank für Backofen**, mit Schubladen unter dem Backofen und einer Tür darüber;
- **Hochschrank**, mit Türen oder offenen Regalen;
- **Blende** und **Lücke**.

Ein Hochschrank, ein Kühlschrank oder ein Backofenschrank hat seine eigene **Höhe**. In der Oberschrankreihe hat ein **Oberschrank** Türen, Glastüren oder offene Regale, seine eigene **Höhe** und seine eigene Höhe **Ab Boden**. Lassen Sie eines von beiden leer, um die Werte der Zeile zu nehmen. Eine **Dunstabzugshaube** ist eine Kaminhaube (650 über der Arbeitsplatte, mit ihrem Rohr bis zur Oberkante der Oberschränke) oder eine in einen Oberschrank integrierte Haube. Eine **Lücke** lässt die Wand frei. Oberschränke weichen einem Hochschrank aus.

Ist keine Oberschrankreihe aufgelistet, bekommt jeder Unterschrank einen Oberschrank seiner Breite, und ein Kochfeld oder ein Herd bekommt eine Kaminhaube. Bei einer Insel oder einer Halbinsel hängt **Dunstabzugshaube über dem Kochfeld dort** eine Haube über das Kochfeld, das auf ihr steht.

Was die Schränke von der Zeile übrig lassen, wird wie zuvor gleichmäßig verteilt, und weniger als 100 mm wird zu einer Blende. Schränke, die nicht in die Zeile passen, werden weggelassen, und die Vorschau und das Element sagen es. **An Schränke anpassen** in den Eigenschaften einer Küche setzt ihre Länge auf das, was ihre Schränke brauchen: ihre eigene, plus eine Blindecke und den Hochschrank am Ende. Ein Typ ohne eigene Schränke wird wie zuvor gebaut.

Im Grundriss hat jeder Schrank sein eigenes Symbol:

- die Türanschläge der Unterschränke, gestrichelt und nur bei feinster Detailstufe;
- die Becken der Spüle und ihre Armatur;
- die Zonen oder Brenner des Kochfelds;
- Hochschränke mit Diagonalen;
- **DW** am Geschirrspüler, **F** an einem Kühlschrank und **OV** an einem Backofenschrank;
- Oberschränke und Hauben darüber.

Ansichten und 3D zeigen jede Front, jeden Griff und jedes Gerät.

Die Materialien haben zwei Rollen mehr: **Sockel** und **Griffe**, neben den Fronten, dem Korpus, der Platte (Arbeitsplatte), Metall und Glas. Ein Küchentyp, der davor erstellt wurde, nimmt für den Sockel das Material seines Korpus und für die Griffe das seines Metalls.

![Die Schränke eines Küchentyps: die Unterschrankreihe und die Oberschrankreihe, mit der Vorschau der Engine](shots/kitchen-units-types.webp)

![Eine Küche aus gemischten Schränken und Geräten in 3D](shots/kitchen-units-3d.webp)

**Innenausbau → Küchenschränke** erstellt eine Liste der Schränke jeder Küche, von links, erst die Unterschrankreihe, dann die Oberschrankreihe, mit Breite, Höhe und Front oder Gerät. In IFC bleibt die Küche ein einziges IfcFurniture ihres Typs, und die Schränke wandern mit dem Typ, sodass Lapin sie zurückliest.

## Die Eigenschaften eines Möbels

Wählen Sie ein Möbel aus, um zu sehen:

- **Typ**, seine Art und Größe;
- **Drehung**;
- **Über dem Boden**, für ein Möbel auf einem Podest;
- **Länge**, bei einer Küche.

**Umkehren** gibt einem Möbel an Ort und Stelle seine andere Hand: die Recamiere auf der anderen Seite, den Kühlschrank am anderen Ende, die Ecke des Ecksofas auf der anderen Seite. Seine Vorderseite bleibt, wo sie war. **Ändern → Spiegeln** tut dasselbe.

Ziehen Sie den Griff in der Mitte eines Möbels, um es zu verschieben; ziehen Sie den Griff davor, um seine Vorderseite zum Cursor zu drehen.

![Die Eigenschaften eines Sofas](shots/furniture-properties.webp)

## Möbeltypen

**Innenausbau → Möbeltypen** listet die Typen des Projekts nach Gruppen (Sitzmöbel, Betten, Stauraum, Tische, Küche), jeweils mit der Zahl der Möbel, die ihn verwenden. Wählen Sie eine Art und **Neuer Typ**, oder **Duplizieren** Sie einen Typ, um eine Kopie zu ändern. Ein Typ enthält:

- seinen **Namen**, seine **Breite**, **Tiefe** und **Höhe** (bei einem Sofa die der Rückenlehne, bei einem Bett die des Kopfteils, bei einer Küche die der Arbeitsplatte);
- die eigenen Parameter seiner Art:
  - Sofas und Sessel: Sitzplätze, Armlehnen, Armlehnenbreite, Sitzhöhe, eine Recamiere links oder rechts und ihre Länge, Füße;
  - ein Ecksofa: die Seite seiner Ecke und die Länge dieser Seite;
  - ein Bett: Oberkante Matratze, Kopfteil, Fußteil, Kissen, eine umgeschlagene Bettdecke;
  - ein Kleiderschrank: Türen, Dreh- oder Schiebetüren, Schubladen unten, Sockel;
  - eine Kommode: Reihen und Spalten von Schubladen;
  - ein Bücherregal: Fachböden und Türen unten bis zu einer Höhe;
  - ein Sideboard: Türen und Schubladen;
  - ein Tisch: rechteckig, rund oder oval, auf vier Beinen oder einem Mittelfuß;
  - ein Schreibtisch: ein Schubladencontainer links oder rechts;
  - eine Küche: Breite der Unterschränke, Sockel, Arbeitsplatte, Lage von Spüle und Kochfeld, Hängeschränke, Hochschrank und seine eigenen Schränke (oben);
- seine **Materialien** nach Rolle: Korpus, Polsterung, Platte, Fronten, Bettwäsche, Metall, Glas und bei einer Küche Sockel und Griffe. Geben Sie einen beliebigen Materialnamen ein; eines, das das Projekt nicht hat, wird angelegt, und **Verwalten → Materialien** legt seine Farbe und Schraffuren fest.

Die Vorschau neben den Feldern zeigt das Grundrisssymbol und die Vorderansicht des Typs, während Sie ihn ändern. Jedes Möbel eines Typs folgt jeder Änderung.

![Der Dialog Möbeltypen mit einem Sofa und seiner Recamiere](shots/furniture-types.webp)

## Listen und IFC

**Möbelliste** führt jeden Typ mit seiner Art, Größe, der Zahl der Möbel und den Räumen, in denen sie stehen, mit der Summe. **Möbel nach Raum** führt jeden Raum mit dem, was darin steht. Beide stehen im Projektbrowser unter **Bauteillisten** und werden wie jede Liste gedruckt und exportiert. Ein Möbeltyp mit einer Kostenposition wird in der Kostenberechnung pro Stück bepreist.

In IFC ist ein Möbel ein **IfcFurniture** mit dem vordefinierten Typ seiner Art (SOFA, CHAIR, BED, TABLE, DESK, SHELF oder USERDEFINED mit Wardrobe, Kitchen…). Sein **IfcFurnitureType** ist über IfcRelDefinesByType zugeordnet, und seine Körper tragen ihre Materialien. Lapin liest seine eigenen Möbel als native, bearbeitbare Möbel zurück. Möbel aus anderen Programmen kommen als Referenzen herein.
