---
title: Aufzüge
order: 43
summary: Aufzüge aus einem Konfigurator mit zwölf Arten, bemessen nach EN 81-20 und EN 81-70, mit Schacht, Grube, Schachtkopf, Triebwerksraum, Fahrkorb und Schachttüren, den Öffnungen in den durchquerten Decken, Grundrissen in jedem Geschoss, einer Liste und IFC.
---

## Einen Aufzug setzen

**Architektur → Aufzug** (<kbd>LF</kbd>) setzt einen Aufzug mit einem Klick in den Grundriss. Der Klick ist die Mitte seines Schachts, und seine Türen zeigen im Plan nach unten (drehen Sie ihn mit 0°, 90°, 180° oder 270° in der Leiste, oder später mit seinem Griff). Der Aufzug reicht vom Geschoss des Grundrisses bis zum höchsten Geschoss des Projekts und hält in jedem Geschoss dazwischen. Sein Konfigurator öffnet sich sofort.

Solange das Werkzeug aktiv ist, zeigen die Leiste der Befehlszeile und das Eigenschaftenfenster **den Stil**, den es setzt: eine der zwölf Arten des Katalogs oder einen der eigenen Stile des Projekts.

## Der Konfigurator

![Der Aufzugskonfigurator mit dem rollstuhlgerechten Stil](shots/elevator-configurator.webp)

Links stehen die Stile. Ein Stil füllt alle Felder aus, danach können Sie jedes Feld von Hand ändern. Der Katalog enthält diese Arten:

| Stil | Personen | Nennlast | Fahrkorb (mm) | Antrieb |
|---|---|---|---|---|
| Kleiner Wohnaufzug | 4 | 320 kg | 900 × 1000 | ohne Triebwerksraum |
| Personenaufzug Standard | 6 | 450 kg | 1000 × 1250 | ohne Triebwerksraum |
| Personenaufzug mittel | 8 | 630 kg | 1100 × 1400 | Seil, Triebwerksraum oben |
| Personenaufzug groß | 10 | 800 kg | 1350 × 1400 | Seil |
| Personenaufzug, starker Verkehr | 13 | 1000 kg | 1600 × 1400 | Seil, 1,6 m/s |
| Rollstuhlgerecht | 8 | 630 kg | 1100 × 1400, Tür 900 | EN 81-70 Typ 2 |
| Bettenaufzug (Krankenhaus) | 17 | 1275 kg | 1200 × 2300 | Seil, Durchlader |
| Lastenaufzug | 26 | 2000 kg | 1500 × 2700 | Seil, 0,5 m/s |
| Hydraulisch (niedrige Gebäude) | 8 | 630 kg | 1100 × 1400 | Heber, Aggregatraum neben dem Schacht |
| Ohne Triebwerksraum (MRL) | 13 | 1000 kg | 1100 × 2100 | Antrieb im Schachtkopf |
| Panoramaaufzug aus Glas | 8 | 630 kg | 1100 × 1400 | gläserner Fahrkorb im Glasschacht |
| Homelift | 3 | 250 kg | 800 × 950 | 0,15 m/s, flache Grube |

Rechts drei Registerkarten:

- **Fahrkorb und Last**: der Antrieb (Seil, ohne Triebwerksraum, hydraulisch, Homelift), Nennlast und Personen, die Geschwindigkeit, die Barrierefreiheit (EN 81-70 Typ 1, 2 oder 3), Breite, Tiefe und Höhe des Fahrkorbs, Fahrkorbwände aus Glas, ein Feuerwehraufzug, ein Evakuierungsaufzug. **Last und Personen aus dem Fahrkorb** setzt die kleinste Nennlast, die EN 81-20 der Fläche des Fahrkorbs gibt, und die Personen, die er befördert.
- **Türen und Haltestellen**: ein Zugang oder zwei (Durchlader), Breite und Höhe der Tür, mittig oder seitlich öffnend und ihre Türblätter (zwei oder drei Blätter gleiten hintereinander), die oberste Haltestelle und die Geschosse, in denen er hält. An einem Geschoss, an dem er ohne Halt vorbeifährt, bleibt die Schachtwand geschlossen und es gibt keine Schachttür.
- **Schacht**: die Seite des Gegengewichts (oder des Hebers bei einem hydraulischen Aufzug), die Grube unter der untersten und der Schachtkopf über der obersten Haltestelle (beide folgen Antrieb und Geschwindigkeit, bis Sie eigene Werte eingeben), der Triebwerksraum (oben beim Seilaufzug, unten neben dem Schacht beim hydraulischen, sonst keiner), die Schachtwände (ein Wandtyp des Projekts oder Beton in einer Dicke, die Sie angeben), ein Glasschacht, die Drehung.

![Der Schnitt durch den Fahrkorb, von der Grube bis zum Triebwerksraum](shots/elevator-section.webp)

Die Vorschau zeigt den Grundriss an der untersten Haltestelle und auf den anderen Registerkarten einen Schnitt durch den Fahrkorb mit allen Haltestellen (bediente rot, durchfahrene gestrichelt). Darunter, was EN 81 sagt: ein Fahrkorb, der größer ist, als seine Nennlast erlaubt, mehr Personen, als Last oder Fläche tragen, ein zu kleiner barrierefreier Fahrkorb oder eine zu kleine Tür, ein hydraulischer Aufzug mit mehr als etwa 18 m Förderhöhe. Weiter unten die lichten und äußeren Maße des Schachts, die Fahrkorbfläche gegenüber der, die seine Last erlaubt, die Haltestellen, die Förderhöhe und die Decken, die der Schacht öffnet.

**Anwenden** ändert den Aufzug in einem Schritt zum Rückgängigmachen. **Als Stil speichern…** behält ihn als Stil des Projekts; **Stil aktualisieren** speichert ihn erneut, und alle Aufzüge dieses Stils folgen. Ein Projektstil lässt sich umbenennen oder löschen (seine Aufzüge bleiben, wie sie sind).

## In den Zeichnungen

![Ein Aufzug im Grundriss: der geschnittene Schacht, der Fahrkorb mit seinem Kreuz, das Gegengewicht und die Schachttüren](shots/elevator-plan.webp)

Der Schacht öffnet jede Decke, durch die er geht, mit einer eigenen Öffnung in jeder Decke. Wenn Sie den Aufzug verschieben, drehen oder kopieren oder seine Geschosse ändern, folgen die Öffnungen. Eine nach dem Aufzug gezeichnete Decke öffnet **Architektur → Schachtöffnungen**.

Jeder Grundriss eines Geschosses, das der Aufzug erreicht, zeichnet ihn: die geschnittenen Schachtwände, den Fahrkorb mit seinem Diagonalkreuz, das Gegengewicht mit seiner Diagonale (den Heber eines hydraulischen Aufzugs als Kreis) und in den Geschossen, in denen er hält, die Blätter der Schachttür und der Fahrkorbtür, gestrichelt dort, wohin sie beim Öffnen gleiten. Schnitte und Ansichten zeichnen den ganzen Schacht, die Grube, die Schachtdecke, den Triebwerksraum, den Fahrkorb an der untersten Haltestelle und die Schachttüren. In 3D steht der Fahrkorb an der untersten Haltestelle mit geschlossenen Türen.

![Ein Aufzug in 3D, sein Triebwerksraum über dem Haus](shots/elevator-3d.webp)

## Eigenschaften, Liste und IFC

![Die Eigenschaften eines Aufzugs](shots/elevator-properties.webp)

Wählen Sie einen Aufzug aus, um seine unterste und oberste Haltestelle zu ändern, die Geschosse anzuhaken, in denen er hält, den Wandtyp seines Schachts zu wählen, die Warnungen nach EN 81 zu lesen und den **Konfigurator…** zu öffnen. Seine Felder (Antrieb, Last, Fahrkorb, Türen, Grube, Schachtkopf…) stehen darunter; nicht zutreffende werden ausgeblendet (kein Triebwerksraum bei einem Aufzug ohne Triebwerksraum, keine Gegengewichtsseite bei einem hydraulischen). Ziehen Sie seinen mittleren Griff, um ihn zu verschieben, und den Griff vor seinen Türen, um ihn zu drehen. **Verschieben**, **Kopieren**, **Drehen** und **Spiegeln** wirken wie bei jedem Element; ein gespiegelter Aufzug nimmt sein Gegengewicht auf die andere Seite.

**Architektur → Aufzugsliste** führt jeden Aufzug mit Art, Antrieb, Nennlast, Personen, Fahrkorb, Türen, den bedienten Haltestellen, den Geschossen, zwischen denen er fährt, Förderhöhe, Geschwindigkeit und Schacht auf.

Jeder Aufzug hat einen Typ aus seiner Art, Last und seinem Fahrkorb (Personenaufzug mittel 630 kg, 1100 × 1400): gleiche Aufzüge teilen ihn, sodass eine Preisposition auf dem Typ (**Kosten → Positionen zuordnen…**) sie alle bepreist, und die Planung findet sie über ihre Klasse (IfcTransportElement).

In IFC ist ein Aufzug ein **IfcTransportElement** mit dem vordefinierten Typ **ELEVATOR**, eines **IfcTransportElementType**, mit den IFC-eigenen **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) und **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin liest seine eigene Datei als nativen Aufzug zurück, mit seinen Haltestellen und den Öffnungen der Decken.
