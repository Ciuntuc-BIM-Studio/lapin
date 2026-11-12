---
title: Lasten
order: 51
summary: Lastfälle, Flächen-, Linien- und Punktlasten, die Nutzlasten der Räume, die abgeleiteten Lasten und die Kombinationen nach EN 1990, für eine Tragwerksberechnung.
---

Die Gruppe **Lasten** im Reiter **Tragwerk** enthält die Lasten, die eine Tragwerksberechnung übernimmt: **Lastfälle…**, **Flächenlast**, **Linienlast** und **Punktlast**. Die Werte sind in kN, kN/m und kN/m².

![Die Gruppe Lasten im Reiter Tragwerk](shots/ribbon-loads.webp)

> Die Werte, die Lapin mitbringt (die Nutzlasten nach EN 1991-1-1 und die Beiwerte nach EN 1990, jeweils die empfohlenen), sind **Richtwerte**. Setze die Werte deines Nationalen Anhangs ein und prüfe sie, bevor du die Ergebnisse verwendest.

## Lastfälle

**Lastfälle…** öffnet den Dialog Lasten. **Standardfälle hinzufügen** legt G1 (Eigengewicht), G2 (Ausbau und Trennwände), Q-A (Nutzlast, Kategorie A), S (Schnee) und W (Wind) an.

![Der Reiter Fälle im Dialog Lasten](shots/loads-cases.webp)

Ein Fall hat einen **Code**, einen **Namen**, einen **Typ** (Ständig, Veränderlich oder Außergewöhnlich), eine **Quelle** (Eigengewicht / ständige Last, Ausbau (Beläge), Nutzlast, Schnee, Wind, Erdbeben oder Benutzerdefiniert) und, bei einem Nutzlastfall, eine **Kategorie**. Die drei Felder ψ0, ψ1 und ψ2 sind die Kombinationsbeiwerte; lass sie leer, um die der Kategorie aus der Tabelle zu nehmen. Ein Wert, der keine Zahl von 0 bis 1 ist, wird markiert und nicht übernommen.

**Fehlende Fälle hinzufügen** legt für jede von den Räumen verwendete Kategorie, die keinen hat, einen Nutzlastfall an.

## Lasten setzen

- **Flächenlast**: klicke die Ecken an und drücke <kbd>Enter</kbd>.
- **Linienlast**: klicke ihre beiden Enden an.
- **Punktlast**: klicke dorthin, wo sie wirkt.

Solange ein Werkzeug aktiv ist, bietet die Leiste der Befehlszeile den **Wert** der Last und den **Lastfall**, in den sie geht (standardmäßig **Nutzlast (Q-A)**). Die Last kommt auf das aktive Geschoss.

![Eine Flächenlast von 2,5 kN/m² im Wohnzimmer](shots/loads-plan.webp)

## Was Lapin für dich berechnet

Diese zeichnest du nicht; Lapin leitet sie bei jeder Abfrage aus dem Modell ab, und sie folgen jeder Änderung:

- Das **Eigengewicht** von Decken, Wänden, Stützen und Trägern aus den Dichten ihrer Materialien, im Fall, dessen Quelle das Eigengewicht ist (G1).
- Die **Deckenbeläge** aus den Schichten der Decken und die **nichttragenden Wände** als Linienlasten, im Fall, der Ausbau und Trennwände aufführt (G2).
- Die **Räume**: die Nutzlast der Kategorie eines Raums über seine Fläche, im Fall dieser Kategorie (Q-A, Q-B…).

Ein Material ohne Dichte bleibt außen vor, und die Übersicht sagt, welches.

## Die Nutzlast eines Raums

Wähle einen Raum: seine Eigenschaften haben einen Abschnitt **Nutzlast**. Seine **Kategorie** ist **Aus der Funktion** (ein Wohnzimmer, ein Schlafzimmer oder eine Küche ist A, ein Büro B, ein Lagerraum E1, eine Garage F), sofern du keine andere wählst, oder **None**. Darunter steht das **qk**, das die Kategorie ergibt.

![Die Nutzlast eines Raums in seinen Eigenschaften](shots/loads-room.webp)

## Die Tabelle der Nutzlasten

Der Reiter **Nutzlasten** führt jede Kategorie mit ihrer Nutzung und ihrem **qk** in kN/m² auf, wie EN 1991-1-1 es empfiehlt. Trage im Feld den Wert deines Nationalen Anhangs ein; **Geändert** setzt auf den Standardwert zurück.

![Der Reiter Nutzlasten](shots/loads-imposed.webp)

## Übersicht

Der Reiter **Übersicht** zeigt für jeden Fall die von dir gezeichneten Lasten und die Summe in kN, insgesamt und je Geschoss, dann die abgeleiteten Lasten einzeln mit ihrer Herkunft. Er warnt vor Räumen, deren Kategorie keinen Fall hat, und vermerkt, was ausgelassen wurde.

![Der Reiter Übersicht](shots/loads-summary.webp)

## Kombinationen

Der Reiter **Kombinationen** listet die Kombinationen der Fälle nach EN 1990 mit den empfohlenen Werten, als Richtwerte: im Grenzzustand der Tragfähigkeit 1,35 auf die ständigen Einwirkungen und 1,5 auf die führende veränderliche, die übrigen mit ihrem ψ0; im Grenzzustand der Gebrauchstauglichkeit die charakteristische, die häufige und die quasi-ständige. Ein Fall, der nichts enthält, nimmt nicht teil.

![Der Reiter Kombinationen](shots/loads-combinations.webp)

## Berechnen

**Berechnung starten** (in derselben Gruppe und im Dialog Lasten) berechnet den Rahmen unter diesen Lasten: siehe [Tragwerksberechnung](analysis.md).

## In der IFC-Datei

**IFC, Fragments oder BOS exportieren…** hat **Structural Analysis View (Analysemodell und Lasten)**: die Datei enthält dann das Analysemodell der tragenden Wände, Decken, Stützen und Träger mit den Lastfällen, den Lasten und den Kombinationen, als IFC Structural Analysis View. Siehe [Import und Export](import-export.md).
