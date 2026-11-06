---
title: Treppen und Rampen
order: 42
summary: Setzen Sie eine Treppe oder eine Rampe zwischen Geschosse oder bis zu einer beliebigen Höhe, richten Sie sie in ihrem Typ ein und legen Sie die wenigen Stufen zu einem Vorplatz oder einer Tür an.
---

## Treppen

Wählen Sie **Architektur → Treppe** (<kbd>ST</kbd>). Klicken Sie auf den Antritt der Treppe (die Mitte ihrer ersten Steigung) und dann auf einen Punkt in der Richtung, in die sie ansteigt. Bei einer L- oder U-Treppe legt ein dritter Klick links oder rechts des Laufs fest, in welche Richtung sie sich wendet. Die Treppe steigt von ihrem Geschoss bis zum Geschoss darüber. Lapin ermittelt die Anzahl der Steigungen, die Steigungshöhe und den Auftritt aus der Höhe und den Regeln des Typs.

Wählen Sie die Treppe aus, um sie im Eigenschaftenbereich zu ändern:

- **Treppentyp.** Die Form, die Regeln, die Konstruktion und das Geländer stammen aus dem Typ, der in **Architektur → Treppentypen** eingerichtet wird.
- **Breite**, **Wendung**, **Versatz unten** und **Versatz oben.**
- **Höhenunterschied.** Lassen Sie das Feld leer, und die Treppe steigt bis zum Geschoss darüber. Ein Wert gibt der Treppe einen eigenen Höhenunterschied, unabhängig von den Geschossen. Verwenden Sie ihn für einige Stufen zwischen zwei Höhen auf demselben Geschoss.
- **Fest.** Steigungen, Steigungshöhe, Auftritt und Podestlänge, wenn Sie sie festlegen möchten. Lassen Sie sie leer, damit die Regeln entscheiden.
- **Prüfungen.** Jeder Wert im Vergleich zu den Regeln des Typs, in Grün oder Rot.

Eine Treppe verlangt von der Decke darüber eine Öffnung über sich. Entfernen Sie das Häkchen bei **Öffnung in der Decke darüber**, wenn die Decke geschlossen bleiben soll.

## Treppenformen und fertige Typen

In **Architektur → Treppentypen** bietet die Registerkarte **Form**: **Gerade**, **Gerade mit Podest**, **L-Form** und **U-Form** (jeweils mit Podest oder mit **gewendelten Stufen**; eine U-Form ohne Auge ist eine gegenläufige Treppe), **Wendeltreppe** (um eine Spindel, mit ihrem **Spindelradius**), **Gebogen** (mit ihrem **Innenradius**) und **Zweiarmig (geteilt)**: ein Hauptlauf in der Mitte bis zu einem Podest, dann zwei Rückläufe zu beiden Seiten. Bei einer Wendel- oder gebogenen Treppe legt **Dreht sich um (°)** fest, wie weit sie dreht; leer entscheiden Steigungen und Auftritt, angegeben folgt der Auftritt daraus. Jede Treppe kann es auch in ihren Eigenschaften ändern.

Die Bibliothek fügt dem Projekt fertige Typen hinzu: gegenläufige, viertelgewendelte, Wendel-, gebogene, Kragstufen- und zweiarmige Treppe.

In der Registerkarte **Konstruktion** wählt **Ausführung** eine Laufplatte, massive Stufen auf dem Boden, **Freitragende Stufen (schwebend)** (jede Stufe eine in die Wand eingespannte Platte) oder **Stufen auf Wangen**.

Das **Geländer** hat seine **Gestaltung**: **Pfosten und Handlauf**, **Stäbe** (höchstens im **Stababstand**), **Glasfüllungen**, **Seile** oder eine **Geschlossene Brüstung**; einen eckigen oder runden **Handlauf**, seine Höhe, den **Abstand vom Rand**, wie weit er **Über die Enden hinaus** läuft, und seine Farbe. Im Eigenschaftenbereich setzt **Geländer** es für eine einzelne Treppe auf andere Seiten als der Typ.

![Der Typ der Wendeltreppe, die Stufen nummeriert](shots/stair-spiral.webp)

### Nummerierte Stufen

Unter **Im Grundriss** in der Registerkarte **Form**: **Stufen nummerieren** schreibt die Nummer jeder Stufe in einen Kreis, von der ersten Steigung an; **Beschriftung** schreibt Steigungen, Steigung und Auftritt und die Breite neben den Antritt; **„UP“ am Fuß des Pfeils** zeigt, wo sie beginnt. Grundrisse und Pläne zeigen sie.

![Nummerierte Stufen im Grundriss, eine zweiarmige und eine Kragstufentreppe](shots/stair-numbers-plan.webp)

![Eine Wendel-, eine zweiarmige und eine Kragstufentreppe in 3D](shots/stair-shapes-3d.webp)

## Eingangsstufen (ein Vorplatz)

Die Stufen hinauf zu einer Haustür haben einen eigenen Höhenunterschied: vom Gelände, einige Stufen unterhalb des Erdgeschosses, hinauf bis ±0.00. Lapin hat dafür einen fertigen Treppentyp, **Eingangsstufen**: 2 m breit, drei Steigungen zu 150 mm, massiver Beton, auf dem Erdreich betoniert, ohne Geländer.

![Eingangsstufen vor einem Vorplatz, im Grundriss](shots/entrance-steps-plan.webp)

1. **Der Vorplatz.** Zeichnen Sie das Podest als Decke: Wählen Sie **Architektur → Decke** (<kbd>SB</kbd>) und klicken Sie ihren Umriss außerhalb der Wand vor der Tür. Die Oberkante einer Decke liegt auf der Höhe ihres Geschosses (±0.00 im Erdgeschoss), und ihr Typ bestimmt ihre Dicke. Um die Dicke zu ändern, erstellen Sie in **Architektur → Deckentypen** einen Deckentyp für Vorplätze, zum Beispiel 150 mm Beton.
2. **Die Stufen.** Wählen Sie **Architektur → Eingangsstufen**. Klicken Sie auf die Mitte der Kante des Vorplatzes und dann auf die Seite, zu der die Stufen hinausführen. Die Stufen reichen bis an die Kante des Vorplatzes und enden bei ±0.00. Sie beginnen 450 mm tiefer: Ihr **Versatz unten** beträgt −450 mm.
3. **Die Höhe.** Wählen Sie die Stufen aus. Geben Sie in **Höhenunterschied** den tatsächlichen Unterschied zwischen dem Gelände und dem Vorplatz ein, zum Beispiel 300. Die Oberkante bleibt am Vorplatz und der Antritt wandert nach oben: Der Versatz unten wird zu −300 mm, und die Stufen haben zwei Steigungen. **Breite** macht sie so breit wie den Vorplatz, zum Beispiel 2400.

![Die Eigenschaften der Eingangsstufen: Höhenunterschied, Versatz unten, Ebenen](shots/entrance-steps-properties.webp)

**Ebenen** zeigt, wo die Stufen beginnen und enden, zum Beispiel −0.45 → ±0.00. Mit einer Steigung (einem Höhenunterschied von 150 mm) ist die Stufe die Kante des Vorplatzes selbst, und es gibt keinen Auftritt zu bauen. Geben Sie den Stufen zwei oder mehr Steigungen, um Auftritte vor dem Vorplatz zu erhalten.

Der Typ wird dem Projekt hinzugefügt, wenn Sie ihn zum ersten Mal verwenden. Um ihn zu ändern, öffnen Sie **Architektur → Treppentypen** und wählen Sie **Eingangsstufen**:

- **Form → Steigt → Eine eigene Höhe.** Der Höhenunterschied des Typs, überwunden **Bis zur Höhe ihres Geschosses, von unten (ein Eingang)**; jede Treppe kann ihn mit **Höhenunterschied** weiterhin ändern.
- **Regeln → Regelsatz → Eingangsstufen.** Steigungen von 120–170 mm und Auftritte von 300–400 mm, wobei eine einzelne Steigung erlaubt ist.
- **Konstruktion → Ausführung.** **Massiv, auf dem Erdreich betoniert** füllt die Stufen bis zu ihrer Unterseite aus. **Laufplatte unter den Stufen** baut sie wie eine Innentreppe.

![Der Typ Eingangsstufen im Treppenkonfigurator](shots/stair-types.webp)

Damit ein beliebiger anderer Treppentyp eine eigene Höhe überwindet, wählen Sie auf seiner Registerkarte **Form** **Steigt → Bis → Eine eigene Höhe** (siehe den nächsten Abschnitt). Um eine platzierte Treppe in Eingangsstufen umzuwandeln, wählen Sie **Eingangsstufen** als ihren **Treppentyp**: Sie rückt nach unten, sodass ihr oberes Ende auf der Höhe ihres Geschosses liegt.

![Das Haus mit seinem Vorplatz und den Eingangsstufen in 3D](shots/entrance-steps-3d.webp)

In IFC sind die Stufen eine IfcStair vom Typ STRAIGHT_RUN_STAIR. Ihr Lauf ist ein IfcStairFlight mit NumberOfRisers, NumberOfTreads, RiserHeight und TreadLength, und Pset_StairCommon trägt dieselben Werte. Der Vorplatz ist ein IfcSlab.

## Eine Treppe bis zu einer beliebigen Höhe

Eine Treppe muss nicht von einem Geschoss zum nächsten führen. Auf der Registerkarte **Form** des Typs wählt **Steigt → Bis** **Das Geschoss darüber** oder **Eine eigene Höhe** mit ihrem **Höhenunterschied**, und **Beginnt** sagt, wo sie anfängt:

- **Bis zur Höhe ihres Geschosses, von unten (ein Eingang).** Sie endet auf der Höhe des Geschosses und beginnt den Höhenunterschied tiefer, wie Eingangsstufen.
- **Von der Höhe ihres Geschosses nach oben (eine Terrasse, eine Plattform).** Sie beginnt auf der Höhe des Geschosses und steigt den Höhenunterschied hinauf, zum Beispiel 1,20 m zu einer Terrasse oder einer Laderampe.

Jede platzierte Treppe kann eine andere Höhe überwinden: Geben Sie sie in ihren Eigenschaften unter **Höhenunterschied** ein (leer: die des Typs). Steigt sie von der Geschosshöhe aus, behält ein neuer Wert den Fuß an seiner Stelle; kommt sie von unten, behält er das obere Ende.

## Der Raum unter der Treppe

Der Raum unter einem Lauf lässt sich einrichten: ein Abstellschrank, ein Büro im Schrank, eine Leseecke, ein Eingang mit Bank und Mantelhaken, ein Spielhaus für Kleinkinder oder ein Hundebett. Wählen Sie eine Treppe mit geraden Läufen und klicken Sie unten in ihren Eigenschaften auf **Raum unter der Treppe…** (oder **Architektur → Raum unter der Treppe**).

![Der Konfigurator für den Raum unter der Treppe: die Leseecke unter dem Lauf](shots/under-stair-dialog.webp)

Links stehen die fertigen Stile: **Abstellschrank**, **Büro im Schrank**, **Leseecke**, **Eingang und Garderobe**, **Spielhaus für Kleinkinder** und **Hundehütte**, darüber die Stile des Projekts. Wählen Sie einen und ändern Sie ihn in der Mitte:

- **Platzierung.** **Unter dem Lauf** (bei einer L- oder U-Treppe), die **Offene Seite** (standardmäßig von der Wendung weg), wo er entlang des Laufs **Beginnt bei** und **Endet bei** (leer: ab 300 mm Untersicht über dem Boden bis zur letzten Setzstufe) und seine **Tiefe** (leer: so breit wie die Treppe).
- **Die Front.** Der **Niedrige Teil** mit **Schranktüren** (je etwa die **Türbreite**, mit **Fachböden hinter den Türen**), einer **Geschlossenen Platte** oder offen; ein **Bücherregal am hohen Ende**.
- **Hauptelement.** Ein **Schreibtisch mit Ablage** (und Leuchte), eine **Sitzbank mit Kissen** (Schubladen vorn), eine **Garderobenbank und Mantelhaken** (Schuhfächer darunter), eine **Spielhaustür** (und ein **Fenster neben der Tür**) oder ein **Hundebett** hinter einer gerahmten Öffnung, jeweils mit seinen Maßen.
- **Materialien.** Die **Plattenstärke** und die Materialien für **Korpus**, **Fronten** und **Stoff**, aus denen des Projekts.

Rechts zeigt die Vorschau den Raum von der offenen Seite unter der Laufuntersicht und von oben, Länge, Tiefe und Höhen sowie seine Teile. Jedes Teil wird von der Schräge beschnitten; ein Teil ohne Platz (ein Schreibtisch, wo die Treppe zu niedrig ist) entfällt, und die Vorschau sagt es. **Erstellen** oder **Anwenden** ist ein Rückgängig-Schritt. **Als Stil speichern…** behält die Einrichtung als Stil des Projekts; **Stil aktualisieren** speichert ihn erneut, und alle Räume des Stils folgen.

![Der Raum unter der Treppe in den Eigenschaften](shots/under-stair-properties.webp)

Der Raum ist ein eigenes Element, das zu seiner Treppe gehört. Ändert sich die Treppe (Breite, Drehung, Stufen, Typ), wird der Raum darunter neu gebaut, und er wird mit ihr gelöscht. Seine Platzierungsfelder stehen in den Eigenschaften; **Einrichten…** öffnet den Konfigurator wieder.

![Die sechs Stile unter sechs Treppen](shots/under-stair-3d.webp)

Grundrisse, Schnitte, Ansichten und Pläne zeichnen ihn wie jedes andere Element: geschnitten, wo der Grundriss schneidet, darunter gesehen.

![Die Leseecke im Grundriss, die anderen daneben](shots/under-stair-plan.webp)

![Ein Schnitt entlang des Laufs durch die Leseecke](shots/under-stair-section.webp)

**Architektur → Liste der Räume unter Treppen** listet die Teile jedes Raums mit Stückzahl, Material und Volumen sowie Summen je Material. In IFC ist der Raum ein IfcElementAssembly ("Under-stair space") aus IfcFurniture (Fachböden, Schreibtisch, Bank, Kissen, Bett), IfcDoor, IfcPlate, IfcDiscreteAccessory (Griffe, Mantelhaken) und IfcLightFixture, mit seiner IfcStair verbunden.

## Rampen

Wählen Sie **Architektur → Rampe** (<kbd>RU</kbd>). Klicken Sie auf die Mitte des unteren Endes der Rampe und dann auf einen Punkt in der Richtung, in die sie steigt; bei einer L- oder Zickzackrampe legt ein dritter Klick links oder rechts vom Lauf fest, wohin sie sich wendet. Beim ersten Mal erhält das Projekt aus der Bibliothek **Ramp 6 %**: 1,20 m breit mit 6 % Neigung, in Läufen von höchstens 9 m mit Podesten von 1,50 m dazwischen, eine Stahlbetonplatte unter einem Belag, Handläufe auf beiden Seiten. Sie steigt bis zum Geschoss darüber, sofern ihr Typ oder ihr **Höhenunterschied** nichts anderes sagt.

**Architektur → Eingangsrampe** wird wie Eingangsstufen gesetzt: Klicken Sie auf die Mitte der Vorplatzkante und dann auf die Seite, zu der die Rampe hinausgeht. Sie steigt 450 mm vom Gelände bis ±0,00, massiv auf dem Erdreich, und **Höhenunterschied** ändert sie wie bei den Stufen.

Rampentypen entstehen im selben Konfigurator, **Architektur → Treppentypen**, mit **Neuer Rampentyp** oder aus der Bibliothek: **Switchback ramp** (im Zickzack) und **Entrance ramp** (Eingang):

- **Form.** **Gerade**, **Gerade, mit Podesten**, **L-förmig** oder **Im Zickzack (hin und zurück)** mit ihrem **Abstand zwischen Läufen**; die **Breite**, die **Neigung** in Prozent, der **Längster Lauf** (ist die Rampe länger, wird sie in gleich lange Läufe mit Podesten dazwischen geteilt) und die **Podestlänge**. Die Länge im Grundriss ist die Höhe durch die Neigung: 3 m bei 6 % sind 50 m. **Steigt** wirkt wie bei Treppen.
- **Regeln.** **Größte Neigung** (standardmäßig 8 %), **Mindestbreite** und **Durchgangshöhe**. Die Prüfungen zeigen rot eine Neigung, die steiler ist, als die Regeln erlauben, einen Lauf, der länger ist als der längste, und ein Podest, das kürzer ist als 1,50 m oder als die Breite der Rampe.
- **Konstruktion.** **Eine geneigte Stahlbetonplatte** mit ihrer **Plattendicke** oder **Massiv, auf dem Erdreich betoniert**; Belag und Geländer wie bei Treppen.

![Die Zickzackrampe im Konfigurator](shots/ramp-types.webp)

Wählen Sie eine Rampe aus, um ihren Typ, **Breite**, **Wendung**, **Versatz unten** und **Höhenunterschied**, eine eigene **Neigung** und **Podestlänge** (leer: die des Typs), die Längen ihrer Läufe und ihre Prüfungen zu sehen. Grundrisse zeichnen ihre Läufe und Podeste, den Pfeil nach oben und die Neigung in Prozent entlang jedes Laufs. Eine Rampe, die bis zum Geschoss darüber steigt, verlangt von dessen Decke eine Öffnung, wie eine Treppe.

![Die Zickzackrampe und die Eingangsrampe im Grundriss, die Neigung an jedem Lauf](shots/ramps-plan.webp)

![Die Rampen in 3D](shots/ramps-3d.webp)

In IFC ist eine Rampe ein IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP oder HALF_TURN_RAMP) mit einem IfcRampType, aus IfcRampFlight, Podesten (IfcSlab LANDING) und IfcRailing.

## Geländer

**Architecture → Railing** (<kbd>RL</kbd>) zeichnet ein eigenes Geländer an der Kante eines Balkons, einer Terrasse oder einer Galerie: klicken Sie seine Punkte, Enter beendet. Es hat die Formen der Treppengeländer (Pfosten und Handlauf, Stäbe, Glas, Seile oder eine geschlossene Brüstung), seine Höhe (1 m als Vorgabe), den Basisversatz, Profil und Größen des Handlaufs und den Pfostenabstand in den Eigenschaften; seine Punkte werden an ihren Griffen gezogen (Strg/⌘-Klick auf die Mitte eines Abschnitts fügt einen hinzu). In IFC ist es ein IfcRailing (GUARDRAIL).

## Balkone

**Architecture → Balcony** (<kbd>BY</kbd>) hängt einen Balkon an eine Wand: den Stil in der Leiste der Befehlszeile wählen und außerhalb der Wandfläche klicken, wo er hinkommt; er steht auf dieser Seite der Wand auf der Fußbodenhöhe des Geschosses. Ein **Französischer Balkon** (Juliet), neben eine Tür oder ein Fenster geklickt, wird darauf zentriert, 200 mm breiter. Wo keine Wand in der Nähe ist, steht der Balkon, wo geklickt wird. **Balcony by outline** zeichnet jede Form: die Ecken klicken, die erste Kante liegt am Gebäude. **Gallery edge** zeichnet die Kante einer Galerie über einem zweigeschossigen Raum mit zwei Punkten (der Luftraum links, die Galerie rechts).

Die mitgelieferten Stile decken die gefragten Arten ab:

- **Juliet balcony**: ein Stabgeländer vor einer Fenstertür über einer kleinen Steinplatte auf zwei Konsolen.
- **Cantilevered slab, glass**: eine Betonplatte, über eine thermische Trennung aus der Decke auskragend, im Gefälle gefliest, mit Tropfkante, Glas auf drei Seiten.
- **Stacked, on posts**: eine Platte auf Stahlstützen vorne, die auf dem Balkon darunter (oder dem Gelände) stehen; Geschoss über Geschoss wiederholt.
- **On steel brackets**, **Hung from tie rods** (Zugstäbe von den vorderen Ecken zur Wand), **Rounded corners**, **Curved front** mit massiver Brüstung.
- **Loggia**: in die Fassade eingeschnitten. Sie schneidet ihre Öffnung in die Wand (so breit wie die Loggia, so hoch wie ihre **Recess height**), baut Seitenwände und eine Deckenplatte hinter der Fläche und stellt ihre eigene **Back wall** (vom Typ der Wand, gleich hoch) in Loggiatiefe auf, wo die Tür hinkommt. Das Geländer steht an der Fläche.
- **Corner balcony**: entlang der Wand und um die Gebäudeecke, zurück entlang der Seitenwand; nahe einem Wandende geklickt, umgreift er dieses Ende.
- **Mezzanine gallery edge**: die Galerieplatte hinter ihrer Kante, eine Stirnblende über der Kante und ein Stabgeländer.

![Balkone im Obergeschoss: ein französischer Balkon an der Tür, einer auf Stützen, ein Eckbalkon, eine Loggia, ein gewölbter, ein abgerundeter und ein abgehängter; der Balkon des Geschosses darüber gestrichelt](shots/balconies-plan.webp)

Der Grundriss zeichnet Platte, Belag und Geländer eines Balkons, wie er sie schneidet oder sieht, und die Balkone des Geschosses darüber gestrichelt. Schnitte und Ansichten zeichnen Platte, Geländer, Stützen und Konsolen.

### Der Konfigurator

**Architecture → Balcony configurator** (oder das Zahnrad neben **Style** in den Eigenschaften eines Balkons) arbeitet wie der Dachkonfigurator: links die Stile (die des Projekts, dann die mitgelieferten), rechts die Felder des Balkons in Registern, über seinem Grundriss oder einem Schnitt durch die Mitte, wie der Entwurf sie bauen würde:

- **Shape**: **Kind**, **Outline** (Rechteck, abgerundete Ecken mit **Corner radius**, gewölbte Front mit **Front bulge** oder das gezeichnete Polygon), **Width** und **Depth**, der Rücksprung des Eckbalkons, Platte und **Fascia height** der Galerie.
- **Slab and finish**: **Slab thickness** und Material, **Edge** (gerade, mit **drip** — Tropfkante —, mit **nosing** des Belags oder **tapered** bis **Edge thickness**), Dicke und Material des Belags und sein **Fall (%)** nach vorne, die Untersicht und **Thermal break** (80 mm zwischen Platte und Decke innen; Liste und IFC sagen es).
- **Railing**: die Geländerformen (Pfosten und Handlauf, Stäbe, Glas, Seile, geschlossene Füllung) oder eine **parapet** mit eigener Dicke und Material, oder keines; Höhe, nur vorne oder auch seitlich, Abstand vom Rand, Handlauf, Pfosten und Farbe.
- **Supports**: **Carried by** nichts (auskragend), **posts** (rund oder eckig, Größe und Abstand), **brackets** (Tiefe, Höhe und Abstand) oder **hangers** (Höhe der Zugstabbefestigung über der Platte) und ihr Material.
- **Loggia**: Höhe der Öffnung, Dicke der Seitenwände, Platte über die Fläche hinaus, Deckenplatte und Rückwand.

![Der Balkonkonfigurator: die Stile, die Form und der Grundriss](shots/balcony-configurator.webp)

![Seine Auflager über einem Schnitt durch den Balkon](shots/balcony-configurator-section.webp)

**Apply** ändert den Balkon in einem Schritt. **Save as style…** behält ihn als Stil des Projekts (einen Balkontyp), den die Leiste des Werkzeugs und die Eigenschaften anbieten; **Update style** speichert ihn erneut in seinen Stil, und jeder Balkon des Stils übernimmt ihn; neben dem Namen eines Stils wird er umbenannt oder gelöscht (seine Balkone bleiben, wie sie sind). Ein Stil ist ein Typ: seine Kostenpositionen (Cost → Map articles) messen seine Balkone.

![Die Eigenschaften eines Balkons: sein Stil und die Felder seiner Art](shots/balcony-properties.webp)

In den Eigenschaften zeigt ein Balkon nur die Felder, die seine Art verwendet. Seine Griffe schieben ihn entlang der Wand, die Mitte der Front setzt die Tiefe (bei einer Loggia die der Öffnung), die Mitten der Seiten die Breite; ein gezeichneter wird an seinen Ecken gezogen. Öffnung und Rückwand einer Loggia folgen ihr und verschwinden mit ihr.

**Stack on storeys above** (im Konfigurator, in den Eigenschaften oder **Architecture → Stack on storeys above**) kopiert die gewählten Balkone in einem Schritt auf jedes Geschoss darüber, mit Copy to storeys: die Kopie hängt an der Wand ihres Geschosses, die dort steht, wo ihre Wand steht, eine gestapelte Loggia schneidet die Wand dieses Geschosses, Stützen stehen auf dem Balkon darunter.

![Die Südfront in 3D: der französische Balkon, die gestapelten Balkone auf Stützen, der Eckbalkon und der abgehängte](shots/balconies-3d.webp)

![Von Norden: die in die Wand geschnittene Loggia, der gewölbte Balkon mit Brüstung und der abgerundete](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** listet jeden Balkon mit Geschoss, Stil, Art, Breite, Tiefe, Fläche, Geländerlänge und -form, Auflager und thermischer Trennung, mit den Summen. In IFC ist ein Balkon ein IfcElementAssembly (USERDEFINED, „Balcony“) eines IfcElementAssemblyType aus seinen Teilen: die Platte ein IfcSlab (FLOOR), Belag und Untersicht IfcCovering (FLOORING, CEILING), das Geländer ein IfcRailing (GUARDRAIL) oder die Brüstung eine IfcWall (PARAPET), Stützen IfcColumn, Konsolen IfcDiscreteAccessory (BRACKET), Zugstäbe IfcMember, die thermische Trennung ein IfcBuildingElementPart; die Öffnung einer Loggia ist ein IfcOpeningElement in ihrer Wand. Lapin liest seine Balkone als Balkone zurück.
