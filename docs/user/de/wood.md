---
title: Holz
order: 54
summary: Holzrahmenwände aus dem Konfigurator: Ständer im Raster, Schwellen und Rähme, Riegel, und jede Tür und jedes Fenster nach seinen Regeln gerahmt.
---

## Holzrahmenwände

Ein Wandtyp kann als Holzrahmen gebaut werden: **Wood → Timber frame** öffnet den Konfigurator, links die Wandtypen des Projekts. Kreuzen Sie **Timber frame** an, und der Kern des Typs besteht aus Bauteilen statt aus einer Schicht: Schwellen und Rähme, die Ständer im Raster, die Riegelreihen und um jede Tür und jedes Fenster die durchgehenden Ständer, die Auflagerständer, der Sturz und der Brüstungsriegel. Die übrigen Schichten bleiben, wie sie sind, und die Dämmung füllt die Gefache.

![Der Holzrahmen-Konfigurator](shots/wood-frame.webp)

- **Vorgaben**: **nordisch** (45 × 145 im 600-mm-Raster, C24) oder **rumänisch** (50 × 150 im 625-mm-Raster, C24, eine Riegelreihe bei 1,25 m). Danach lässt sich jedes Feld ändern.
- **Ständer, Abstand, Klasse, Schwellen und Rähme, Endständer, Riegelreihen**. **Raster ab** legt fest, wo das Ständerraster beginnt: am Anfang jeder Wand oder am Projektursprung, damit die Wände einer Fassade dasselbe haben.
- **Tragend**: eine Trennwand trägt nichts und bekommt über ihren Öffnungen ein liegendes Holz statt eines Sturzes.

## Türen und Fenster

Türen und Fenster bleiben die des Architekten: verschieben Sie eines oder ändern seinen Typ, folgt seine Rahmung. Der Rahmen wird von der **Rohbauöffnung** aus gemessen: Tür oder Fenster plus die Einbaufugen (seitlich, oben, unter dem Fenster; die Tür geht bis zum Boden).

Jede Öffnung nimmt die Regel ihrer **Breitenstufe** (die Tabelle unter **Openings**: bis 900 mm, bis 1800 mm, darüber, als Vorgabe): durchgehende und Auflagerständer je Seite, den Sturz (keiner, Vollholz, LVL oder Brettschichtholz, Querschnitt und Lagen) und die Brüstungsriegel. In einer Rohbauöffnung steht nie ein Ständer; die kurzen über dem Sturz und unter der Brüstung halten das Raster, damit die Plattenstöße auf Ständern liegen. Zwei Öffnungen, die näher als der **kleinste Pfeiler** liegen, teilen sich einen Sturz und die äußeren durchgehenden Ständer. Eine Öffnung zu nah am Wandende wird in den Eigenschaften der Wand gemeldet, nie verschoben.

Wählen Sie eine Tür oder ein Fenster: der Abschnitt **Wood** zeigt die Rohbauöffnung, die Regel und was es bekommt. Kreuzen Sie **Eigene Regel** an, um nur dieser Öffnung andere Ständer, einen anderen Sturz oder Brüstungsriegel zu geben.

## Ecken, Platten und die Rahmenansicht

- **Ecken**: wo eine andere Wand am Ende dieser Wand anschließt (stumpfe Ecke), bekommt die durchlaufende Wand **drei Ständer** (einen hinter der Innenseite der anderen), eine **California**-Ecke (ein liegender Ständer an der Innenseite, für die Beplankung) oder **nur ihre Endständer**. Eine Gehrungsecke behält die Endständer beider Wände.
- **T-Stöße**: wo eine Wand im Verlauf dieser Wand anschließt, bekommt diese **je einen Ständer beidseits** oder **Ständer und Leiterriegel** dazwischen.
- **Platten**: kreuzen Sie eine Schicht an (OSB, Holzfaserplatte, Gipskarton) und geben Sie das Plattenformat an: die Schicht wird aus stehenden Platten verlegt, die Stöße im Ständerraster, an den Rohbauöffnungen und am oberen Rand geschnitten. **Wood → Boards schedule** zählt die Platten je Wand, ihre Nettofläche und den Verschnitt.
- **Rahmenansicht**: im Abschnitt **Wood** einer Holzrahmenwand erzeugt die Schaltfläche einen Schnitt entlang der Wand durch die Kernmitte, 1:20, alle Bauteile geschnitten, mit einer Maßkette durch die Rohbauöffnungen, dem Ständerraster darunter und der Größe jeder Rohbauöffnung. Sie kommt wie jede Ansicht auf einen Plan.

## Vorgefertigte Elemente

Hake **Prefabricated panels** an, um den Rahmen in der Werkstatt zu fertigen: gib das **längste Element** an (so lang, wie Werktisch und Lkw es erlauben) und das **kürzeste**. Jede Wand wird am längsten Stoß innerhalb dieser Länge in Elemente geteilt, wo möglich im Ständerraster, nie durch die durchgehenden Ständer, die Auflagerständer, den Sturz oder die Brüstung einer Öffnung und nie durch die Hinterlegung eines T-Anschlusses. Jedes Element wird für sich abgebunden, mit eigenen Endständern und Schwellen an den Stößen. Ein Element, das nicht in der Länge bleiben kann (eine zu breite Öffnung), bleibt ganz, und die Wand meldet es.

![Eine vorgefertigte Wand im Konfigurator](shots/wood-panels.webp)

Die Elemente tragen den Namen der Wand und ihre Nummer entlang der Wand (W3-P1, W3-P2…). Der Abschnitt **Wood** der Wand zeigt ihre Längen, die Rahmenansicht zeichnet Stöße und Marken über die Wand, und **Wood → Wall panels** listet jedes Element mit Wand, Länge, Höhe, Öffnungen, Teilen, Holzvolumen und Masse, damit sie der Reihe nach bestellt, gefertigt und gehoben werden können. Die Platten eines Elements tragen seine Marke ebenfalls. In IFC ist jedes Element ein IfcElementAssembly (Timber frame panel) mit seinen Teilen und Platten.

## BSP (CLT)

Im selben Konfigurator kann ein Wand- oder Deckentyp **aus BSP-Elementen** gebaut werden: Lagen (die Dicke jeder, 3, 5, 7 oder 9), Klasse, das längste und höchste (oder breiteste) Element der Presse, der Stoß zwischen Elementen (stumpf, Stufenfalz, Feder), der Abstand einer Öffnung zum Elementrand und die Einbaufuge. Eine Wand wird der Länge nach in Elemente geteilt, die Stöße von den Öffnungen weg verschoben; jedes Element ist der Körper des Kerns, die Rohbauöffnungen ausgeschnitten. Eine Decke wird über ihre Spannweite geteilt (in kurzer Richtung, entlang x oder y). **Wood → CLT panels** listet jedes Element mit Marke, Maßen, Lagen, Klasse, Stoß, Fläche und Volumen; in IFC ist jedes ein IfcBuildingElementPart namens CLT panel.

- **Stöße**: beim **Halbholzstoß** (half lap) überlappen die beiden Hälften um das angegebene Maß; ein **Federstoß** (spline) hat einen Falz auf einer Seite und darin einen Sperrholzstreifen als eigenes Teil. Beide sind in den Elementen modelliert, Volumen und Liste sind also das, was die Werkstatt schneidet.
- **Ecken der Öffnungen**: eine Fräse schneidet keine scharfe Innenecke. **Der Fräserradius** bleibt in jeder Ecke stehen; die **Dogbone-Freifräsung** schneidet an jeder Ecke einen Kreis, damit ein rechteckiger Rahmen passt. Der Konfigurator zeichnet die Fräskontur.
- **Elementansicht**: der Abschnitt **Wood** einer BSP-Wand erzeugt einen Schnitt entlang der Wand in 1:20, mit Stößen und Marken der Elemente und den Rohbaumaßen der Öffnungen.
- **BSP-Dächer**: wähle ein geneigtes Dach und hake **CLT panels** in seinem Abschnitt **Wood** an: seine Flächen werden aus BSP-Platten statt Sparren oder Bindern gebildet (Fußpfetten und Deckung bleiben), Dachfenster und Gauben ausgeschnitten, in Streifen von Elementbreite entlang der Traufe, stumpf gestoßen, wo eine Fläche länger als ein Element ist.

## Holzbalkendecken

Ein Deckentyp kann **auf Balken** gebaut werden: wähle ihn im Konfigurator und wähle **on joists**. Gib Querschnitt, Abstand und Festigkeitsklasse der Balken an, die Spannrichtung (die kurze, oder nach x oder y), ob ein **Randbalken** die Ränder schließt, die **Reihen Knaggen** und um jede Deckenöffnung (ein Treppenloch, ein Schacht) die **Wechselbalken** zu jeder Seite und die **Lagen des Wechsels** an ihren Enden. Von der Öffnung unterbrochene Balken enden am Wechsel, und ein zusätzlicher Balken kommt dorthin, wo die Wechselbalken eine Lücke größer als der Abstand lassen. **Fill the cavities** füllt das Material des Kerns zwischen die Balken. Der Konfigurator zeigt eine Beispieldecke mit Treppenloch.

![Ein Deckentyp auf Balken](shots/wood-joists.webp)

Die Balken ersetzen den Kern der Decke; die Schichten darüber und darunter bleiben. Ihre Teile stehen in der Holzliste, und in IFC ist jeder ein IfcBeam (JOIST, Randbalken oder Wechsel nach Name) unter der Decke.

## Die Holzliste

**Wood → Timber schedule** erstellt die Zuschnittliste allen Holzes im Projekt, der Wände und der Dachstühle: Bauteil, Querschnitt, Klasse, Länge, Stück, Gesamtlänge und Volumen, mit Summen je Querschnitt. In IFC ist jedes Bauteil ein IfcMember (STUD, PLATE, Sturz und Brüstungsriegel mit Namen) unter seiner Wand, die Dämmung ein IfcCovering INSULATION.
