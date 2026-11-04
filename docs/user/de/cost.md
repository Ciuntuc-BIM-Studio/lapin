---
title: Kostenberechnung
order: 98
summary: Die Kostenberechnung aus dem Modell: Ihre Preislisten aus CSV oder Excel importiert, Positionen auf Typen und Regeln auf Materialien, Mengen aus der Mengenermittlung mal Einheitspreise, aufgeteilt in Material, Lohn, Geräte und Transport, Gemeinkosten, Gewinn und MwSt., in RON oder EUR; die rumänischen Formulare F3, F2 und C6 bis C9 auf Plänen, in Excel und als PDF; IFC-Kostenpläne.
---

## Die Registerkarte Kosten

![Die Registerkarte Kosten](shots/ribbon-cost.webp)

Die Registerkarte, die **Mengen** hieß, heißt jetzt **Kosten**. Sie behält die Mengen und fügt die Kostenberechnung hinzu:

- **Mengenermittlung**, **Bauteillisten…** und **XLSX exportieren**: die Mengen des Modells (siehe [Bauteillisten und Diagramme](schedules.md)).
- **Preislisten…**: Ihre Preise, aus einer CSV- oder Excel-Datei importiert (unten).
- **Positionen zuordnen…**: welche Position jeden Typ und jedes Material bepreist (unten).
- **Kostenberechnung…**: öffnet die Kostenberechnung unter der Ansicht (beim ersten Mal wird sie erstellt).
- **Formular auf Plan**, **Kostenberechnung XLSX** und **Kostenberechnung PDF**: die Kostenberechnung und ihre Formulare auf einem Plan, in einer Arbeitsmappe oder in einem PDF (unten).

Die Kostenberechnung wird bei jeder Änderung neu aus dem Modell berechnet. Eine Wand, die Sie jetzt zeichnen, steht sofort mit ihren Mengen darin. Gespeichert werden nur die Preise, die Positionen auf den Typen, die Regeln und Ihre eigenen Zeilen.

## Preislisten

![Eine Preisliste importieren: die Bedeutung jeder Spalte aus ihrer Überschrift erraten](shots/price-import.webp)

Die Preise sind Ihre: Lapin liefert keinen Katalog mit Preisen oder Normen. **Kosten → Preislisten… → Importieren…** liest eine CSV- oder XLSX-Datei, zum Beispiel einen Export aus Ihrem Kalkulationsprogramm oder die Liste eines Lieferanten. Lapin zeigt die ersten Zeilen der Datei unter ihren Überschriften. Über jeder Spalte steht, was Lapin aus der Überschrift (englisch oder rumänisch) als ihren Inhalt erraten hat:

- **Code** und **Beschreibung** (*Cod*, *Simbol*, *Denumire*), **Einheit** (*U.M.*: m³ oder mc, m² oder mp, m, pcs oder buc, kg, t, 100 m²).
- Der Einheitspreis, aufgeteilt in **Material**, **Lohn**, **Geräte** und **Transport** (*Material*, *Manoperă*, *Utilaj*, *Transport*), oder ein einziger **Einheitspreis (nicht aufgeteilt)**, der dann als Material zählt.
- Optional: **Arbeitsstunden je Einheit** (*Normă de timp*) und **Beruf** (*Meserie*), für die Lohnliste (C6).
- Optional: die Ressourcen der Position, Zeilen, die die Position nennen, zu der sie gehören (**Zugehörige Position**), ihre **Art der Ressource** (M, Man, U, T) und ihren **Verbrauch je Einheit**. Eine Position, die nur durch ihre Ressourcen gegeben ist, wird durch sie bepreist, und die Ressourcenlisten (C7 bis C9) führen sie einzeln auf.

Ändern Sie jede falsch erratene Spalte, wählen Sie die Zeile mit den Überschriften und ob die Preise in RON oder EUR sind, dann klicken Sie auf **Importieren**. Die Liste kommt ins Projekt. Ist **Auch in der Bibliothek dieses Computers behalten** angehakt, bleibt sie auch auf diesem Computer: Unter **Auf diesem Computer** holt **Zum Projekt hinzufügen** sie in Ihr nächstes Projekt.

**Vorlage** speichert eine CSV-Datei mit den Spalten und einigen Zeilen mit Lapins eigenen Richtwerten, als *Richtwert* markiert: Tragen Sie Ihre Preise ein und importieren Sie sie. **Richtwerte verwenden** legt diese Richtwerte als Liste ins Projekt, damit Sie die Kostenberechnung ausprobieren können, bevor Sie Ihre eigenen haben. Ersetzen Sie sie, bevor Sie eine Kostenberechnung an jemanden schicken.

## Positionen auf Typen, Regeln auf Materialien

![Positionen zuordnen: jeder Typ und jedes Material mit seiner Position](shots/map-articles.webp)

Eine Zeile der Kostenberechnung kommt aus einer **Position**: einem Code aus Ihrer Preisliste. Es gibt zwei Wege, Elementen eine Position zu geben:

- **Auf einem Typ.** Jedes Element des Typs wird durch sie bepreist. Die Position sagt, was sie misst: das ganze Element oder einen Teil davon (den **Kern** eines mehrschichtigen Typs oder seine **Ausbauschichten**), und mit welcher Messgröße (nach der Einheit des Preises, oder Netto- oder Bruttovolumen, Netto- oder Bruttofläche, Länge, Anzahl, Gewicht). Sie kann auch einen Faktor und den Verschnitt (%) hinzufügen.
- **Auf einem Material (eine Regel).** Jede Schicht aus diesem Material, in jedem Typ, wird durch sie bepreist: Eine Regel für den Kalkputz bepreist den Putz aller Ihrer Wände.

Die Position des Typs geht vor. Eine Schicht, die die Positionen ihres Typs schon messen, wird nicht noch einmal durch eine Regel bepreist. Eine Position auf dem ganzen Element bepreist alles daran, die Regeln treten also zurück. Geben Sie einer mehrschichtigen Wand eine Position auf ihrem **Kern** (das Mauerwerk nach Volumen) und Regeln für ihre Ausbauschichten (der Putz nach Fläche). Die Wand wird dann Schicht für Schicht bepreist: das Mauerwerk unter **Wände**, der Putz unter **Ausbau und Dämmung**.

**Kosten → Positionen zuordnen…** listet die Typen des Projekts, die Elemente verwenden, und die Materialien ihrer Schichten. Jede Zeile beginnt mit der Position, die sie hat, oder der, die Sie in einem früheren Projekt für einen Typ oder ein Material desselben Namens gewählt haben, oder der, die Lapin vorschlägt: einem Code in ihrem Namen oder der Position, die die meisten Wörter mit ihm teilt (*zidărie*, *BCA*, *tencuială*, *C25/30*). Für einen mehrschichtigen Typ schlägt es den Kern vor. Tippen oder wählen Sie einen Code, wählen Sie den Teil und die Messgröße, haken Sie die gewünschten Zeilen an und klicken Sie auf **Anwenden**: ein Schritt zum Rückgängigmachen. Ihre Auswahl wird auf diesem Computer für Ihre nächsten Projekte gemerkt.

Die Schalungs- und Gerüsttypen der Registerkarte **Baustelleneinrichtung** hatten schon einen Positionscode (Entscheidung 152): Er ist eine Position auf dem Typ, nach der Einheit des Preises gemessen.

## Die Kostenberechnung

![Die Kostenberechnung: Kapitel, Zeilen, die Sätze und die Summen](shots/estimate.webp)

**Kosten → Kostenberechnung…** öffnet die Kostenberechnung unter der Ansicht, wie den Gantt. Ihre Zeilen sind nach Leistungsbereich in **Kapitel** gruppiert: Erdarbeiten, Gründung, Tragwerk, Wände, Dach, Türen und Fenster, Ausbau und Dämmung, Installationen, Baustelleneinrichtung, Sonstiges. Eine Position kann ein eigenes Kapitel nennen. **Nach Geschoss und Leistungsbereich** stellt das Geschoss vor jedes Kapitel.

Jede Zeile zeigt den Code, die Beschreibung und die Einheit aus der Preisliste, die **Menge** aus dem Modell (dieselbe wie in der Mengenermittlung), den Einheitspreis, aufgeteilt in **Material**, **Lohn**, **Geräte** und **Transport**, den Einheitspreis und den **Wert**. Klicken Sie auf eine Zeile, um ihre Elemente in den Ansichten zu wählen. Wählen Sie Elemente in einer Ansicht, werden ihre Zeilen in der Kostenberechnung markiert.

Unter den Zeilen stehen die Summen, wie rumänische Kostenberechnungen sie aufstellen:

1. **Direkte Kosten**: die Summe der Zeilen.
2. **Arbeitsversicherungsabgabe (CAM)**: standardmäßig 2,25 % des Lohns.
3. **Gemeinkosten** (*cheltuieli indirecte*): standardmäßig 10 % der direkten Kosten mit der Abgabe.
4. **Gewinn**: standardmäßig 5 % der direkten Kosten und der Gemeinkosten.
5. **Summe ohne MwSt.**, die **MwSt.** (standardmäßig 21 %) und die **Summe mit MwSt.**

Ändern Sie jeden Satz in der Werkzeugleiste. Jeder Wert wird auf zwei Dezimalstellen gerundet, wie eine Kostenberechnung ihn druckt.

Die Kostenberechnung ist in **RON** oder **EUR**. **1 EUR =** ist der Kurs des Projekts: Eine Preisliste in der anderen Währung wird damit umgerechnet, und das Kästchen daneben zeigt jeden Wert auch in der anderen Währung.

Die Werkzeugleiste warnt auch vor dem, was keinen Preis hat: **N Typ(en) ohne Preis** (ein Klick öffnet Positionen zuordnen) und die Codes, die keine Preisliste hat. **Eine eigene Zeile** fügt Arbeit hinzu, die das Modell nicht hat (den Bauzaun, eine Pauschale): Tippen Sie ihren Code, ihre Beschreibung, Einheit, Menge und Preise ein. Eine Zeile mit Code und ohne Preise nimmt sie aus der Preisliste.

## Die rumänischen Formulare

![Das Leistungsverzeichnis (F3) derselben Kostenberechnung](shots/estimate-f3.webp)

Die Liste über der Kostenberechnung zeigt die Kostenberechnung selbst oder eines ihrer Formulare. Sie sind Gruppierungen derselben Kostenberechnung und ergeben daher dieselben Summen. Ihre Überschriften sind rumänisch, so wie sie in Rumänien eingereicht werden:

- **Leistungsverzeichnis (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, Kapitel für Kapitel, die Einheitspreise und die Werte aufgeteilt in Material, Lohn, Geräte und Transport, und die Summen darunter.
- **Kostenberechnung je Objekt (F2)**: *Deviz pe obiect*, der Wert jedes Kapitels ohne MwSt., seine MwSt. und sein Wert mit MwSt.
- **Lohn (C6)**: die Stunden und der Wert nach Beruf (aus Beruf und Arbeitsstunden der Preisliste oder aus ihren Lohnressourcen).
- **Materialien (C7)**, **Geräte (C8)** und **Transport (C9)**: die Ressourcen mit ihren Mengen, Einheitspreisen und Werten. Eine Position ohne Ressourcen zählt als eine eigene Ressource.

Die Formulare folgen der üblichen Praxis nach der Methodik des Erlasses 863/2008. Prüfen Sie sie gegen das, was Ihr Auftraggeber oder das Förderprogramm verlangt: Fragen Sie uns, wenn Sie ein anderes Layout brauchen.

**Auf Plan setzen** setzt die gezeigte Tabelle auf einen neuen Plan, druckfertig mit den anderen. **XLSX** speichert die Kostenberechnung und jedes Formular in einer Arbeitsmappe, je ein Blatt. **PDF** speichert sie als PDF, A4 quer, druckfertig.

## IFC

**Datei → IFC exportieren…** schreibt die Kostenberechnung als **IfcCostSchedule**: ihre Kapitel und Zeilen als **IfcCostItem**s, die Menge jeder Zeile und ihre Einheitspreise als vier **IfcCostValue**s (Material, Lohn, Geräte, Transport), die Elemente, die jede Zeile misst, ihr zugewiesen (**IfcRelAssignsToControl**), die Währung als **IfcMonetaryUnit** des Projekts. Importiert behält eine Kostenberechnung die Zeilen, wie die Datei sie hat, mit denselben Summen. Haken Sie **Zeilen aus dem Modell** an, um die Zeilen hinzuzufügen, die Ihr Modell ergibt.
