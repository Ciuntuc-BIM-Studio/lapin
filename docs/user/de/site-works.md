---
title: Baustelleneinrichtung
order: 55
summary: Fassadengerüste und Schalung für Decken und Träger, von ihren Konfiguratoren aus dem Modell angeordnet.
---

Die Registerkarte **Baustelleneinrichtung** modelliert die temporären Bauwerke der Baustelle: Fassadengerüste und die Schalung von Decken und Trägern. Lapin ordnet sie aus dem Modell an und zählt, was sie benötigen. Wenn sich das Modell ändert, folgen sie ihm.

![Die Registerkarte Baustelleneinrichtung](shots/ribbon-site-works.webp)

Gerüste und Schalungen sind **Temporär**: Sie werden in der Bauphase **Bauausführung** auf- und abgebaut. Das erste Element, das Sie platzieren, fügt diese Bauphase hinzu, falls das Projekt keine hat, und macht sie zur aktiven Bauphase. Ansichten früherer Bauphasen lassen sie weg. Siehe **Verwalten → Bauphasen**.

![Ein Gerüst rund um das Haus, die Schalung des Obergeschosses auf ihren Stützen](shots/site-works-3d.webp)

## Gerüste

- **Gerüst an Wänden** (<kbd>SW</kbd>): Wählen Sie die Wände aus (Umschalt-Klick für weitere), und klicken Sie dann darauf. Das Gerüst läuft um ihre Außenseiten, im Abstand seines Typs von der Wand, bis zur Oberkante der Wände. Seine Ecken schließt es selbst. Verschieben Sie eine Wand oder machen Sie sie höher, und das Gerüst folgt.
- **Gerüst** (<kbd>SC</kbd>): Zeichnen Sie seine Linie im Grundriss entlang der Fassade, und drücken Sie Enter, um abzuschließen. Das Gerüst steht links der Linie. Die andere Seite wählen Sie in seinen Eigenschaften.

Jeder gerade Abschnitt wird in die Feldlängen des Typs aufgeteilt, mit so wenigen Feldern, wie nötig sind, um sein Ende zu erreichen. Die Lagen steigen, bis der oberste Belag in Reichweite der Arbeitshöhe ist. Der Seitenschutz steht über dem obersten Belag. Die Fußpunkte stehen auf dem Gelände, auf Fußspindeln. Fällt das Gelände stärker ab, als die Spindeln ausgleichen können, meldet Lapin das.

![Die Eigenschaften eines Gerüsts](shots/scaffold-properties.webp)

In seinen Eigenschaften können Sie die **Arbeitshöhe** einstellen: Lassen Sie sie leer für die Oberkante der Wände. Sie können auch wählen, ob die Fußpunkte **auf dem Gelände** oder **auf dem Geschoss** stehen, oder es mit **lösen** von den Wänden trennen.

Jedes Gerüst und jede Schalung hat einen **Typ**. Sie wählen ihn oben in den Eigenschaften oder übertragen den Typ eines Elements mit Typ übertragen (MA) auf andere. Unter **Typparameter** stehen die Werte, die der Konfigurator festlegt: das System, die Feldlängen, die Breite, die Lagenhöhe, das Sperrholz, die Abstände, die Position. Sie können sie dort auch ändern. Eine Änderung betrifft den Typ, sodass ihr jedes Element dieses Typs folgt. Ähnliches erstellen (CS) zeichnet ein neues Element desselben Typs. Ein Typ, den Sie im Konfigurator erstellen, ist derjenige, den das nächste Gerüst oder die nächste Schalung erhält.

**Gerüsttypen…** öffnet den Konfigurator. Er legt das System fest (Modulgerüst oder Rahmen), die Feldlängen, die Breite und die Lagenhöhe, den Abstand von der Wand, die Beläge, den Seitenschutz und die Bordbretter, die Verankerungen, die Fußspindeln, die Aufstiegsfelder und das Schutznetz. Die Ansicht zeigt einen Abschnitt der von Ihnen gewählten Länge und Höhe, mit dem, was er zählt. Ist ein Gerüst ausgewählt, öffnet sich der Konfigurator auf dessen Typ; klicken Sie in der Liste auf einen anderen, um stattdessen diesen zu ändern. Die Liste nennt, wie viele Gerüste jeder Typ hat. **Typ speichern** wendet die Änderung auf jedes Gerüst des Typs an.

![Der Gerüstkonfigurator](shots/scaffold-configurator.webp)

## Schalung

- **Schalung** (<kbd>FW</kbd>): Wählen Sie Decken und Träger aus, und klicken Sie dann darauf. Jedes Element erhält die Schalung aller seiner schalbaren Flächen.
- **Schalung an Fläche** (<kbd>FF</kbd>): Wählen Sie eine Fläche. In 3D leuchtet die Fläche unter dem Cursor mit ihrem Namen auf: die Untersicht einer Decke, die Unterseite eines Trägers oder eine seiner Seiten. Im Grundriss klicken Sie auf den Rand einer Decke oder in ihren Umriss. Eine später gewählte Fläche kommt zur selben Schalung.

Die **Schalung einer Decke** bedeckt ihre Untersicht mit Sperrholz auf Querträgern, auf Jochträgern, auf Stützen. Die Träger unter der Decke werden nicht erneut geschalt, ebenso wenig die Wände und Stützen, auf denen sie aufliegt, oder ihre Öffnungen. Die Abstände stammen aus der Tabelle des Typs, nach der Dicke der Decke. Die Stützen reichen hinunter bis zur Decke darunter, zum Gelände oder zum Geschoss darunter. Die freien Ränder werden ebenfalls geschalt, so hoch wie die Decke plus eine Aufkantung. In den Eigenschaften wählen Sie, welche Flächen geschalt werden, und die Richtung der Jochträger.

Die **Schalung eines Trägers** besteht aus seiner Unterseite, auf Jochen und Stützen, und seinen Seiten. Jede Seite ist so hoch wie der Träger unterhalb der mit ihm betonierten Decke. Stützen und höhere Träger unterbrechen sie. Sie können entlang der unteren Kanten Dreikantleisten hinzufügen.

**Schalungstypen…** öffnet den Konfigurator, für Decken und für Träger. Für Decken legt er das System fest (Holzträger und Stützen, oder Paneele), das Sperrholz, die Träger und ihre Kataloglängen, die Abstandstabelle, die Ränder, die Stützen und die Einsätze. Für Träger legt er die Joche fest, die Stützen, die Zwingen nach Trägerhöhe, die Fase und die Einsätze. Der Schnitt zeigt die Lagen und die Stütze, die die Engine für die von Ihnen gewählte Höhe auswählt. Ist eine Schalung ausgewählt, öffnet er sich auf deren Typ und Art.

![Der Schalungskonfigurator](shots/formwork-configurator.webp)

## Wände, Stützen, Ringbalken und Fundamente

**Schalung** und **Schalung an Fläche** funktionieren auch bei Wänden, Stützen und Fundamenten:

- **Eine Wand** erhält beide Seiten und ihre freien Enden, abzüglich ihrer Türen und Fenster. Eine geschichtete Wand mit einem Ringbalken oder einem Betonband erhält nur diese. In ihren Eigenschaften können Sie beide Seiten, die Ringbalken oder die freien Enden wählen.
- **Eine Stütze** erhält ihre Seiten, bis zur Decke oder zum Träger, der auf ihr betoniert wird.
- **Ein Fundament** erhält die Seiten jeder Stufe eines Einzelfundaments oder die Seiten von Sohle und Steg eines Streifenfundaments.

Was an einer Fläche anliegt, wird ausgelassen: eine andere Wand, eine Stütze. Eine darauf betonierte Decke beendet die Fläche an ihrer Untersicht. Der Typ legt die Elemente (ihre Breiten und Höhen), die Anker durch die Wände, die Stützenzwingen und die Richtstützen fest.

![Der Konfigurator für Wände und Stützen](shots/wall-formwork-configurator.webp)

## Schalungsplan

**Schalungsplan** erstellt einen Grundriss des aktiven Geschosses in der Bauphase Bauausführung, einen Meter über dem Fußboden geschnitten, **von unten gesehen**: einen Deckenspiegel. Die Stützen werden geschnitten. Die Schalung darüber wird so gezeichnet, wie Sie sie beim Blick nach oben sehen würden: die Joch- und Querträger, das Sperrholz und die Ränder. Sie liegt so, wie der Grundriss liegt, sodass beide übereinstimmen.

**Schalungsplan von oben** erstellt denselben Plan von oben gesehen, mit der Schalung gestrichelt über dem Schnitt. Jeder Grundriss lässt sich in seinen Eigenschaften (**Gesehen von**) oder im **Ansichtsbereich** zwischen beiden umschalten.

![Der Schalungsplan des Erdgeschosses](shots/formwork-plan.webp)

## Was sie zählen

Jedes Gerüst und jede Schalung führt ihre Mengen in ihren Eigenschaften auf:

- beim Gerüst die Fläche, Felder, Lagen, Vertikalstiele, Beläge und Verankerungen;
- bei der Schalung die Kontaktfläche nach Fläche, die Stützen nach Größe und die Träger nach Kataloglänge.

**Schalungsliste** und **Gerüstliste** fügen ihre Tabellen dem Projektbrowser unter Bauteillisten hinzu, um sie auf einen Plan zu setzen oder zu exportieren.

Die Schalungsliste schließt mit einer Zeile pro Typ:

- **the set** (der Satz): die meiste Schalung, die auf einmal betoniert wird. Das gilt pro Ausführungsbauphase, wenn die Arbeit in mehrere aufgeteilt ist, sonst pro Geschoss.
- **what to buy** (was zu kaufen ist): der Satz oder die Gesamtmenge geteilt durch die Einsätze des Typs, je nachdem, was größer ist.

Jeder Typ hat seine **Position** für das Leistungsverzeichnis: die Nummer in den Normen, die Ihre Kostenberechnung verwendet, und ihren Text. Sie geben sie im Konfigurator ein, und die Liste zeigt sie in jeder Zeile.

In IFC werden sie als Baugruppen (`IfcElementAssembly` FORMWORK und SCAFFOLD) mit dem Status TEMPORARY geschrieben. Die Einzelteile sind Stäbe, Platten und Zubehörteile. Jedes kommt mit seiner Bauproduktressource, und die Schalung ist mit dem Element verbunden, das sie schalt.
