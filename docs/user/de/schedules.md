---
title: Bauteillisten und Diagramme
order: 95
summary: Türen, Fenster, Stürze, Räume und alles, was ein Modul zählt, auflisten; eigene Zeilen und Spalten hinzufügen, die Werte des Modells überschreiben und die Tabelle als Diagramm zeigen.
---

## Eine Bauteilliste anlegen

Eine Bauteilliste ist eine Tabelle, die das Modell füllt: Türen und Fenster, Stürze, Räume, Bewehrung, Holz, Schalung und mehr. Klicken Sie im Browser auf **+** neben **Bauteillisten** und wählen Sie, was sie auflistet, oder nehmen Sie das Werkzeug eines Moduls (Holz → Holzliste, Baustelleneinrichtung → Schalungsliste…). Doppelklicken Sie im Browser darauf, um sie in einer Registerkarte zu öffnen. Eine Bauteilliste folgt dem Modell: Verschieben Sie eine Wand, fügen Sie ein Fenster hinzu, und die Tabelle ändert sich mit.

Die Listen für **Türen und Fenster**, für **Türen**, für **Fenster** und für **Stürze** zeigen eine Karte je Typ, mit seiner Zeichnung (standardmäßig die Innenansicht) und seinen Werten Zeile für Zeile. Die anderen Bauteillisten sind eine einfache Tabelle, eine Zeile je Element und eine Summenzeile am Ende.

![Eine Tür- und Fensterliste mit ihren Zeilen für Feuerwiderstand und Hersteller](shots/joinery-schedule.webp)

## Feuerwiderstand, Hersteller und andere Parameter

Die Parameter einer Tür oder eines Fensters kommen von selbst in die Tür- und Fensterliste:

- **Feuerwiderstand.** Die Zeile **Feuerwiderstand** liest einen Parameter namens Fire rating, Fire Rated, FireRating oder Fire resistance. Setzen Sie ihn auf den Typ, damit jede Tür des Typs ihn hat. In **Architektur → Türen und Fenster** fügt **Parameter · in Bauteillisten** → **+ Feuerwiderstand** das FireRating des Typs zu Pset_DoorCommon hinzu (Pset_WindowCommon bei einem Fenster), wo IFC es führt. Ein Parameter, den Sie selbst einer einzelnen Tür in ihren Eigenschaften hinzufügen, erscheint ebenfalls.
- **Jeder andere Parameter.** **+ Hersteller**, **+ Produktcode**, **+ Website** oder ein eigener Parameter wird in jeder Tür- und Fensterliste zu einer eigenen Zeile. Der eigene Wert einer Tür erscheint neben dem ihres Typs, wenn die Türen eines Typs sich unterscheiden.
- **In der Liste eingegeben.** Ein Wert, der in der Zeile eines Parameters (kursiv) eingegeben wird, geht in den Typ, und jede Tür des Typs folgt ihm. Leeren Sie ihn, um den Parameter vom Typ zu nehmen.

![Die Parameter des Typs im Tür- und Fensterkonfigurator](shots/joinery-params.webp)

**Nicht verwendete Typen.** Ein Typ, den keine Tür und kein Fenster im Modell verwendet, bleibt aus der Liste heraus. **Nicht verwendete Typen** in der Symbolleiste der Liste zeigt sie wieder, mit ihrer Anzahl.

## Eigene Zeilen und Spalten

Jede Bauteilliste lässt sich von Hand ausfüllen, wie eine Tabellenkalkulation:

- **Zeile** fügt eine eigene Zeile hinzu. In einer Tür- und Fensterliste hat sie eine Bezeichnung, und Sie geben ihren Wert für jeden Typ ein (Lieferant, Liefertermin…). In jeder anderen Liste ist sie eine ganze Zeile, Zelle für Zelle eingegeben (eine nicht modellierte Terrasse, eine Reserve); sie steht über der Summenzeile.
- **Spalte** fügt eine eigene Spalte hinzu. In einer Tür- und Fensterliste ist sie eine Karte ohne Typ dahinter, jeder Wert eingegeben (ein getrennt bestelltes Garagentor). In jeder anderen Liste ist sie eine Spalte nach denen des Modells (Bodenbelag, Bemerkungen…).
- **Jede Zelle** der Tabelle unter der Zeichnung lässt sich überschreiben. Ein Wert, der über den des Modells geschrieben wurde, ist markiert; **↺** gibt den Wert des Modells zurück. Was Sie eingeben, bleibt bei seiner Zeile: Die Bemerkungen eines Raums folgen ihm, wenn die Liste sortiert oder der Raum umbenannt wird. In einer **Rooms**-Liste geht eine Nummer, ein Name, eine Nutzung, Abteilung, ein Bodenbelag oder eine Höhe, die in der Zeile eines Raums eingetragen wird, in den Raum selbst, als wäre sie in seinen Eigenschaften eingetragen; der Stempel im Grundriss und jede andere Liste folgen.
- **×** neben einer eigenen Zeile oder Spalte entfernt sie, mit dem, was darin eingegeben war.
- Die Pfeile im Kopf einer Tabelle **sortieren** ihre Zeilen nach dieser Spalte: einmal aufsteigend, noch einmal absteigend, ein drittes Mal in der Reihenfolge des Modells. Die Summenzeile bleibt die letzte.

Alles wird gedruckt: auf einem Plan, im PDF und DXF und in den Exporten als CSV und XLSX.

## Diagramme

**Diagramme** in der Symbolleiste der Liste öffnet die Diagramme neben der Tabelle. **Neues Diagramm** erstellt eines aus der Tabelle:

- **Säulen**, **Balken**, **Kreis** oder **Linie**.
- **Kategorien aus**: die Spalte, die jeden Balken oder jedes Segment benennt (Nutzung, Name, Kennzeichen, Geschoss…).
- **Wie**: **Summe**, **Mittelwert**, **Kleinster** oder **Größter** Wert einer Spalte (Fläche, Summe, Volumen…), oder **Anzahl der Zeilen**.
- **Höchstens**: behält nur die größten Kategorien und fasst den Rest als Andere zusammen.
- **Titel**: ein eigener Titel oder der, den es selbst bildet (Summe von Fläche nach Nutzung).

Die Engine zeichnet jedes Diagramm nach den Tabellen, daher erscheint es in der Liste, auf einem Plan, der die Liste platziert, und im PDF genau gleich. Es folgt dem Modell und dem, was Sie eingegeben haben: Ändern Sie die Fläche eines Raums oder fügen Sie eine eigene Zeile hinzu, und das Diagramm ändert sich mit. **✎** ändert ein Diagramm, **×** entfernt es.

![Eine Raumliste mit einer eigenen Zeile und einem Kreisdiagramm der Flächen nach Nutzung](shots/schedule-chart.webp)

## Typenaufbauten

Die Liste **Typenaufbauten** (**+** → **Typenaufbauten**) zeigt jeden Wand- und Deckentyp: sein Kennzeichen, seine Gesamtdicke, wie viele Bauteile ihn verwenden, dann seine Schichten der Reihe nach mit Materialien und Dicken.

**Aufbauzeichnungen** in ihrer Leiste zeichnet jeden Typ nach der Tabelle, durch seine Schichten geschnitten, im Maßstab der Liste (1:50, wenn Sie keinen anderen wählen): eine Wand, wie ein Grundriss sie schneidet, eine Decke, wie ein Schnitt sie schneidet, Schicht 1 oben. Jede Schicht ist gefüllt und schraffiert wie ihr Material in Schnitten, die Flächen des Kerns sind kräftiger gezeichnet, die Gesamtdicke ist bemaßt, und jede Schicht ist daneben so benannt, wie die Tabelle sie nennt. Die Zeichnungen werden mit der Tabelle gedruckt, auf einem Plan und im PDF. Eine neue Liste der Typenaufbauten zeichnet sie; nehmen Sie den Haken bei **Aufbauzeichnungen** heraus, um nur die Tabelle zu behalten.

Die Zeichnungen **Auf jeder Karte** (Innenansicht, Außenansicht, Grundriss, Schnitt) in den **Einstellungen** einer Liste gehören zu den Karten der Tür- und Fensterlisten; eine einfache Tabelle hat keine Karten, deshalb werden sie dort nicht angeboten.

![Die Liste der Typenaufbauten mit dem Aufbau jedes Typs](shots/type-assemblies.webp)

## Exportieren

**CSV** und **XLSX** in der Leiste exportieren die Liste als Tabellenkalkulation (eine XLSX hat ein Blatt je Tabelle). In der Desktop-App fragt Lapin, wo die Datei gespeichert werden soll, und schlägt den Namen der Liste vor; im Browser wird die Datei heruntergeladen. Exportierte Wand- und Deckentypen werden genauso gespeichert.
