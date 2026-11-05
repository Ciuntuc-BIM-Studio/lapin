---
title: Materialien, Texturen und Schraffuren
order: 90
summary: Legen Sie für jedes Material Farbe, Schraffuren und Fugen fest, legen Sie Ihr eigenes Bild darauf und bringen Sie Ihre eigenen .pat-Schraffurmuster mit.
---

**Verwalten → Materialien** listet die Materialien des Projekts auf. Wählen Sie eines aus, um es zu ändern.

![Der Dialog Materialien](shots/materials-dialog.webp)

## Erscheinungsbild: wie ein Material gerendert wird

Die Registerkarte **Erscheinungsbild** legt fest, wie das Material in 3D und in den Renderern aussieht, in die Sie exportieren. Die Registerkarte **2D-Grafik** behält seine Schraffuren und Stifte wie bisher.

![Das Erscheinungsbild eines Materials mit seiner Live-Vorschau](shots/material-appearance.webp)

- **Farbe** und **Textur**: eine gezeichnete Textur (Ziegel, Holzdielen, Fliesen…) oder Ihr eigenes Bild. **W** und **H** legen fest, wie groß eine Kachel auf dem Modell liegt, in Metern; **Drehung** dreht sie.
- **Rauheit**: 0 ist ein Spiegel, 1 ist Kreide. **Metallanteil**: 1 für blankes Metall (eloxiertes Aluminium, Stahl, Chrom), 0 für alles Lackierte oder Pulverbeschichtete, Holz und Stein.
- **Transparenz**: wie viel durchscheint. **Glas (Transmission)** und **IOR** (1,52 für Glas) lassen Glas im Renderer brechen.
- **Leuchtet** gibt Licht ab, wie der Diffusor einer Leuchte.

Die Kugel zeigt das Ergebnis sofort. Was Sie nicht setzen, kommt aus dem Namen: ein Material namens *Glas* ist klar, *Stahl* metallisch, *Parkett* und *Laminat* sind Holzdielen. **Erscheinungsbild aus dem Namen** nimmt Ihre Werte wieder weg.

## Aus der Bibliothek

**Aus der Bibliothek…** bietet fertige Materialien: Metalle (Aluminium RAL 7016 anthrazit, eloxiert, Stahl, Kupfer…), Glas (klar, blau, grün, grau, satiniert, reflektierend), Hölzer als Dielen, Beton, Ziegel, Putz und Farbe, Fliesen und Stein, Kunststoff und Stoff. Ein Klick fügt eines dem Projekt hinzu; ändern Sie es wie jedes andere.

![Die Materialbibliothek](shots/material-library.webp)

## Daten: Beschreibung, Parameter, wie es gemessen wird

Der Reiter **Daten** hält, was das Material über sich sagt. Du schreibst es einmal; jeder Typ, in dem das Material ist, liest es: das Schichtaufbau-Etikett, die Mengenermittlung, die Kostenberechnung, der IFC-Export.

![Die Daten eines Materials: Beschreibung, Parameter, wie es gemessen wird, Katalogdicken](shots/material-data.webp)

- **Beschreibung**: der Ausschreibungstext, über mehrere Zeilen (*Mineralwolleplatten, λ = 0,035 W/mK, gedübelt*). Ein Schichtaufbau-Etikett schreibt ihn statt des Namens.
- **Parameter**: ein Name, eine Art und ein Wert, wie bei Bauteilen. **Einen üblichen hinzufügen…** bietet **Wärmeleitfähigkeit λ**, **Dampfdiffusionswiderstand μ** und **Dichte**, die mit ihren Standardnamen in IFC gehen (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, in SI-Einheiten), sowie **Brandverhalten** und **Druckfestigkeit**, die deine eigenen bleiben. Deine eigenen Parameter gehen als *Lapin_Material* in IFC.
- **Gemessen nach** sagt, wie **Kosten → Mengenermittlung** (nach Material) und die Kostenberechnung es zählen: **Fläche** (m², eine Zeile je Dicke: Platten, Dämmung, Bahnen), **Volumen** (m³: Beton, Schotter, Estrich), **Masse** (kg, aus der Dichte) oder **Stück** (Platten der angegebenen Größe). Leer wird es gemessen wie seine Bauteile.
- **Katalogdicken (mm)**: die Dicken, in denen der Hersteller es liefert, etwa *50, 80, 100, 150, 200, 240*. Die Schichttabellen von **Wandtypen** und **Deckentypen** bieten sie neben der Dicke an (eine andere kannst du trotzdem tippen), und eine Schicht anderer Dicke wird mit einem Warnzeichen markiert.

Der IFC-Export schreibt die Beschreibung als Description des Materials und die Parameter als seine Eigenschaftssätze; der Import liest sie zurück, auch aus der Datei eines anderen Programms.

## Ein Material für alle Rahmen und alles Glas

Neue Türen und Fenster bestehen aus gemeinsamen Materialien, Teil für Teil: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** und **Hardware**. Ändern Sie **Window frame** einmal, und alle Fensterrahmen ändern sich.

Für andere Materialien öffnen Sie **Architektur → Türen und Fenster**, wählen für einen Typ **Rahmenmaterial**, **Glasmaterial** und die übrigen und klicken auf **Für alle Fenster verwenden** (oder **Für alle Türen verwenden**). Jeder Typ dieser Art übernimmt sie, in einem Rückgängig-Schritt. Vorhangfassaden haben ebenfalls ein Rahmen- und ein Glasmaterial.

![Die Materialien der Teile im Tür- und Fensterkonfigurator](shots/joinery-materials.webp)

## Duplizieren, ersetzen, auswählen

- **Duplizieren** kopiert ein Material unter einem neuen Namen.
- **Ersetzen durch** ein Material, dann **Ersetzen**: jede Schicht, jeder Typ, jede Tür, jedes Fenster und Element mit dem ersten nimmt nun das zweite, in einem Rückgängig-Schritt. **Ersetzen und löschen** entfernt außerdem das erste. Sonst lässt sich ein verwendetes Material nicht löschen.
- **Elemente auswählen** wählt alle Elemente mit einem Teil aus diesem Material.
- **Materialien suchen** und die Kategorienliste filtern die Liste.

## Materialien für das Rendering

Twinmotion, Lumion, Enscape, Blender und andere Renderer tauschen Materialien nach Namen aus. Lapin schreibt jedes Material **einmal, unter seinem eigenen Namen**, mit Farbe, Textur, Rauheit, Metallanteil und Glas, und teilt das Netz jedes Elements nach Material.

1. Benennen Sie Ihre Materialien so, wie Sie sie im Renderer haben wollen, zum Beispiel *Aluminium RAL 7016 anthracite* für alle Rahmen und *Glass blue tinted* für alles Glas.
2. **Datei → 3D-Export (glTF, USD, DAE)…** und wählen Sie **GLB** (empfohlen: eine Datei mit den Texturen, den genauen Namen und dem Glas), **USDZ** oder **OBJ + MTL** (eine .zip mit den Texturen; Leerzeichen in Namen werden zu Unterstrichen).
3. Importieren Sie die Datei in den Renderer. Dort teilen alle Rahmen ein Material; ersetzen Sie es einmal durch ein Metall des Renderers, und alle ändern sich.

![Ein Haus mit dunkelgrauen Rahmen und blauem Glas](shots/materials-render-3d.webp)

Auch der IFC-Export benennt den Oberflächenstil jedes Materials nach ihm, mit seiner Transparenz und seiner Art zu spiegeln.

## Was ein Material festlegt

- **Farbe (3D)** und die Flächenzeichnung darauf in 3D. Lapin leitet sie aus dem Namen ab, zum Beispiel die Steinlagen eines Ziegels oder das Raster einer Fliese. **Enden** sowie **Oben und unten** können anders aussehen als die Flächen.
- **Schraffur** für jeden Ort, an dem es gezeichnet wird: geschnitten in einem Schnitt, geschnitten in einem Grundriss und gesehen in einer Ansicht. Jede hat ihren Maßstab, Winkel, ihre Ausrichtung, Füllung und ihren Stift. Eine Ansicht oder ein Schnitt schraffiert die Flächen, die sie sieht, mit der Ansichtsschraffur, wo nichts Näheres sie verdeckt (Ansichten, Oberflächenschraffuren); ein Material hat keine, bis Sie ihm eine geben.
- **Kategorie**, zum Beispiel Bekleidung, Dämmung oder Tragwerk. Die Ansichtsstile verwenden sie, um die Bekleidungen in einer Tragwerksansicht wegzulassen.
- **Folie** zeichnet eine dünne Schicht als gestrichelte Linie statt als Band.
- **Fugen zeichnen.** Standardmäßig läuft ein Material ohne Linie von einem Element ins nächste weiter, etwa bei zwei Wänden in einer Flucht oder bei Putz über zwei Geschosse. Aktivieren Sie die Option, um die Fuge zu zeichnen.

## Ihr eigenes Bild auf einem Material

Ein Material kann ein eigenes Bild tragen: ein Foto des echten Ziegels, Holzes oder Steins.

1. Wählen Sie das Material aus und klicken Sie auf **Eigenes Bild → Bild importieren…**. Wählen Sie eine PNG- oder JPEG-Datei.
2. Legen Sie **W** und **H** fest: wie breit und wie hoch, in Metern, eine Kopie des Bildes auf dem Modell ist. Das Bild wiederholt sich über die Flächen.

![Das eigene Bild eines Materials, aufgelegt mit 1.2 × 1.2 m](shots/own-image.webp)

Das Bild erscheint in der 3D-Ansicht und wird in der Projektdatei gespeichert. Es geht auch in die 3D-Exporte ein: **GLB**, **glTF** und **USDZ** nehmen es mit. **DAE** und **USDA** sind Einzeldateien ohne Platz für Bilder, daher behalten sie die Farbe des Materials.

![Dasselbe Haus mit dem eigenen Bild des Materials in 3D](shots/own-image-3d.webp)

Bilder, die größer als 2048 Pixel sind, werden beim Import auf diese Größe verkleinert. Die Papierkorb-Schaltfläche entfernt das Bild vom Material.

## Ihre eigenen Schraffurmuster (.pat)

**.pat importieren** liest eine AutoCAD-Schraffurmusterdatei. Jedes darin enthaltene Muster wird dem Projekt hinzugefügt und kann als Schraffur eines Materials gewählt werden.

Lapin bewahrt die Datei außerdem unter **Meine Muster** auf Ihrem Computer auf. In jedem anderen Projekt holt **Zu diesem Projekt hinzufügen** diese Muster wieder herein. Ein Projekt behält die Muster, die es verwendet, sodass es auf einem anderen Computer genauso aussieht.

![Meine Muster: die importierten .pat-Dateien, aufbewahrt für jedes Projekt](shots/my-patterns.webp)
