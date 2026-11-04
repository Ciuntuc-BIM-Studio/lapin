---
title: TGA-Systeme
order: 57
summary: Zeichnen Sie Heizungs-, Wasser-, Abwasser-, Elektro- und Lüftungssysteme als Schemata im Grundriss, mit Quellen, Endpunkten und den Leitungen dazwischen, und exportieren Sie sie als IFC-Netz.
---

Die Registerkarte **TGA** zeichnet die Haustechnik so, wie ein Ingenieur sie skizziert: ein System je Gewerk, seine **Quelle** (ein Heizkessel, ein Wasserzähler, eine Fallleitung, eine Unterverteilung, ein Ventilator), seine **Endpunkte** (Heizkörper, Waschbecken, WCs, Steckdosen, Leuchten, Luftauslässe) und die **Leitungen** dazwischen. Jeder Knoten ist ein echtes IFC-Element mit seinen Anschlüssen; jede Leitung ist ein Rohr-, Kanal- oder Kabelsegment; die Verbindungen bleiben erhalten, wenn Sie etwas verschieben.

![Die Registerkarte TGA](shots/ribbon-mep.webp)

## Ein System mit einem Klick

Wählen Sie unter **Neues System** das Gewerk (**Heizung**, **Wasser**, **Warmwasser**, **Abwasser**, **Elektro**, **Lüftung**) und klicken Sie in den Grundriss. Lapin platziert das ganze System: die Quelle dort, wo Sie geklickt haben, einen ersten Endpunkt einige Meter entfernt und die Leitung dazwischen. Das System öffnet sich zur Bearbeitung.

![Ein Heizungssystem im Erdgeschossgrundriss: der Heizkessel, zwei Heizkörper unter den Fenstern und die Leitungen dazwischen](shots/mep-plan.webp)

Im Bearbeitungsmodus ist alles, was nicht zum System gehört, abgeblendet, und nur die Knoten und Leitungen des Systems lassen sich auswählen. Ziehen Sie die Quelle und den Endpunkt an ihren Platz: Ihre Leitung folgt. Fügen Sie Endpunkte mit **+ Heizkörper**, **+ Waschbecken**… im Eigenschaftenbereich hinzu oder mit den Werkzeugen unter **Heizung**, **Sanitär**, **Elektro** und **Luft**: Jeder neue Endpunkt wird über eine eigene Leitung an das System angeschlossen, vom nächsten freien Anschluss der Quelle oder von der nächsten Leitung (ein T-Stück). **Fertig** (oder <kbd>Esc</kbd>) verlässt den Bearbeitungsmodus und sagt Ihnen, wie viele Bögen und T-Stücke das System hat und was noch offen ist. **System bearbeiten** (<kbd>ES</kbd>) öffnet es wieder.

## Leitungen

**Leitung** (<kbd>RN</kbd>) zeichnet eine Leitung Punkt für Punkt; drücken Sie <kbd>Enter</kbd>, um abzuschließen. Ein Ende, das auf einem offenen Anschluss landet, wird mit ihm verbunden; ein Ende, das auf einer Leitung desselben Systems landet, zweigt von ihr ab.

**Verbinden** schließt das Ende einer Leitung an einen Knoten oder an eine andere Leitung an; **Trennen** löst es. **Offene Anschlüsse** wählt alles aus, was noch nicht verbunden ist.

## Eine Leitung umformen

Wählen Sie eine Leitung aus: Ihre Griffe sitzen auf der Linie, wie sie gezeichnet ist, auch nachdem Sie die verbundenen Knoten verschoben haben.

- **Einen Griff ziehen**, um diese Ecke frei zu verschieben.
- **Die Mitte eines Abschnitts ziehen**, um den Abschnitt seitlich zu verschieben; die benachbarten Abschnitte dehnen sich, sodass ein rechtwinkliger Verlauf rechtwinklig bleibt. Verschieben Sie den ersten oder letzten Abschnitt, fügt Lapin dort einen Versprung ein, sodass das Ende auf seinem Anschluss bleibt.
- **Strg-Klick** auf einen Griff nimmt ihn heraus, auf die Mitte eines Abschnitts fügt einen hinzu; **Griff hinzufügen** setzt einen dort, wo Sie klicken.
- **Umgehung** (<kbd>DU</kbd>) führt eine Leitung um ein Hindernis herum: Klicken Sie auf die Leitung, wo die Umgehung beginnt, klicken Sie, wo sie auf demselben Abschnitt endet, bewegen Sie dann die Maus zu der Seite, auf die sie ausweicht, und klicken Sie (oder geben Sie den Abstand ein). Die Leitung geht hinaus, entlang und zurück, mit vier neuen Ecken; ihre Verbindungen bleiben erhalten.
- **Pfad neu zeichnen** (<kbd>RP</kbd>) gibt einer Leitung einen neuen Verlauf: Wählen Sie sie aus (oder beginnen Sie auf ihr) und zeichnen Sie den Pfad Punkt für Punkt, <kbd>Enter</kbd> zum Abschließen. Die Leitung behält ihr System, ihre Größe und ihre Verbindungen, und ihre Enden kehren auf ihre Anschlüsse zurück.

![Eine Heizkörperleitung, mit Umgehung um ein Hindernis geführt: hinaus, entlang und zurück](shots/mep-detour.webp)


## Knoten

Jeder Knoten hat eine **Rolle**: Heizkessel, Heizkörper, Verteiler, Wasserzähler, Waschbecken, Spüle, WC, Dusche, Badewanne, Fallleitung, Reinigungsöffnung, Unterverteilung, Steckdose, Leuchte, Schalter, Ventilator, Luftauslass, Abluftgitter, Ventil, Steigung / Fall, Kappe. Die Rolle gibt ihm seine IFC-Klasse, seine Anschlüsse, seine Größe und sein Symbol im Grundriss. Ein Ventil, ein Zähler oder ein Schalter, der auf eine Leitung gesetzt wird, teilt sie und sitzt in der Mitte. Eine **Steigung / Fall** führt eine Leitung auf eine andere Höhe oder in das Geschoss darüber.

![Die Eigenschaften eines Heizkörpers: sein System, seine Rolle und Anschlüsse, seine Größe und Leistung](shots/mep-radiator-properties.webp)

Im Eigenschaftenbereich zeigt ein Knoten sein System, seine Rolle und seine Anschlüsse (offen oder verbunden); eine Leitung zeigt ihre beiden Enden. Beide übernehmen die Vorgaben des Systems, sofern Sie keinen eigenen Wert setzen: Größe, Höhe, die Ebene, in der sie verlaufen (Boden, Decke, Wand), paarweise oder einzeln, Material, Gefälle.

## Systeme

**Systeme…** listet die Systeme des Projekts und die Vorgaben des aktuellen: die Größe, die Ebene und die Höhen jeder Ebene, das Gefälle (Abwasser), ob Leitungen als Paar gezeichnet werden (Heizung: Vor- und Rücklauf) und in welchem Abstand, das Material und die Dämmung. Ändern Sie eine Vorgabe, und jeder Knoten und jede Leitung ohne eigenen Wert folgt.

![Der Dialog Systeme](shots/mep-systems.webp)

## Grundrisse, Listen und IFC

**TGA-Grundriss** erstellt einen Grundriss der Disziplin TGA für die Ebene, abgelegt unter dem TGA-Satz im Projektbrowser. Die **Geräteliste** und die **Leitungsliste** unter **System** sind Bauteillisten wie alle anderen: Sie kommen auf Pläne und werden mit ihnen exportiert.

In einem IFC-Export ist jedes System ein IfcDistributionSystem, jeder Knoten und jede Leitung hat ihre IfcDistributionPorts, und die Verbindungen werden zwischen den Anschlüssen geschrieben, sodass das Netz in einem anderen Programm verbunden ankommt, von der Quelle bis zum letzten Endpunkt.

## Gas und Heizöl

**Gas** und **Heizöl** sind eigene Gewerke unter **Neues System**. Ein Gassystem nennt in **Systeme…** sein Gas (**Erdgas** oder **Flüssiggas**) und seine **Druckstufe** (Niederdruck im Haus, Mitteldruck in der Straße); seine Rohre sind standardmäßig Stahlrohr auf den Wänden in 2,1 m Höhe, wahlweise Kupfer, Mehrschichtverbund oder **PE 100** für das Erdverlegte. Heizöl läuft in Kupfer.

Die Gasinstallation eines Hauses beginnt am **Zählerschrank** an der Fassade. Klicke **Neues System → Gas**: Der Zählerschrank kommt an den Klickpunkt und ein **Gaskessel** ins Haus, verbunden durch eine Leitung. Unterwegs setzen die Werkzeuge unter **Gas** den **Gasdruckregler**, den **Gaszähler**, die **Hauptabsperreinrichtung Gas**, die **Geräteanschlusshähne** und **flexible Anschlüsse** auf die Leitung, die sie wie jedes Formstück teilen; **Gasherd** und **Gas-Durchlauferhitzer** schließen sich mit einer eigenen Leitung an. Wo ein Gas- oder Heizölrohr eine Wand quert, geht es durch eine **Schutzhülse**, die mit der Leitung gezeichnet und gezählt wird. Ein Gaskessel, der im Heizsystem als dessen Quelle gezeichnet ist, schließt sich aus seinen Eigenschaften an die Gasinstallation an (die Schaltfläche **+** mit dem Namen des Gassystems).

![Die Gasinstallation eines Hauses: Zählerschrank, Regler und Zähler außen, Kessel und Herd in der Küche](shots/mep-gas-plan.webp)

Jedes Gerät behält seinen **Verbrauch** (m³/h) und seine **Geräteart** nach der Abgasführung (A ohne Abgasanlage, B raumluftabhängig, C raumluftunabhängig), für die Listen und die Bemessung.

**Heizöl** setzt einen **Heizöltank** (innen, außen oder erdverlegt) und einen **Ölkessel**; **Füllanschluss** und **Entlüftungshaube** schließen sich an Füll- und Entlüftungsstutzen des Tanks an, der **Heizölfilter** sitzt im Vorlauf.

### Die Versorgung in der Straße

Unter **Straße** verlegt **Gasleitung entlang…** (<kbd>GM</kbd>) eine Gasleitung entlang dessen, was du anklickst — eine Straße, eine Achse, eine gezeichnete Linie, eine Wand — mit dem **Abstand zur Linie** und der **Tiefe (Überdeckung)** aus der Leiste der Befehlszeile; unter einem Gelände wird die Tiefe vom Boden gemessen, sonst vom Geschoss. **Gasleitung** zeichnet sie Punkt für Punkt. Die Leitung kommt in ein Mitteldruck-Gassystem aus PE-Rohr, mit ihrem **Warnband**.

**Hausanschluss** (<kbd>GS</kbd>): Klicke die Leitung, dann den Zählerschrank. Lapin setzt eine **Anbohrschelle** auf die Leitung, verlegt die Anschlussleitung rechtwinklig von ihr in ihrer Überdeckung bis zum Schrank und führt sie dort in den Schrank hoch.

![Der Hausanschluss von der Gasleitung der Straße zum Zählerschrank, ausgewählt](shots/mep-gas-street.webp)

![Leitung und Hausanschluss im Erdreich, die Installation an der Fassade](shots/mep-gas-3d.webp)

![Die Eigenschaften eines Hausanschlusses: von der Anbohrschelle zum Zählerschrank, Überdeckung und Durchmesser](shots/mep-gas-service-properties.webp)

![Die Vorgaben eines Gassystems: sein Gas und seine Druckstufe](shots/mep-gas-systems.webp)

**Gas- und Heizöl-Materialliste** unter **System** listet System für System die Rohre nach Material und Durchmesser mit ihrer Länge (davon die Steigleitungen), Bögen, T-Stücke und Schutzhülsen, das Warnband, Armaturen, Zähler und Schränke und die Geräte mit installiertem Verbrauch und Leistung. In IFC ist ein Gassystem ein IfcDistributionSystem vom Typ GAS, ein Heizölsystem FUEL; der Zählerschrank ist ein IfcDistributionChamberElement, der Zähler ein IfcFlowMeter GASMETER, die Armaturen IfcValve, der Herd ein IfcBurner, der Tank ein IfcTank.

![Die Gas- und Heizöl-Materialliste](shots/mep-gas-list.webp)

Die Rohrbemessung aus dem Verbrauch der Geräte und der Nachweis des Druckverlusts sind Ingenieurarbeit, in der Edition **Lapin MEP**.

## Koordination: Öffnungskörper und Kollisionsprüfung

Jede Öffnung in Wand, Decke oder Dach kann ein eigener **Öffnungskörper** sein: ein Körper so groß wie die Öffnung, durch die ganze Dicke ihres Wirtsbauteils, Bekleidungen eingeschlossen. Kollisionsprogramme (Navisworks, Solibri, BIMcollab) prüfen die TGA mit „vollständig innerhalb“ gegen solche Körper: ein Rohr, ein Kanal oder eine Kabeltrasse ganz in einem Körper ist in Ordnung; eines, das herausragt oder eine Wand ohne Öffnung kreuzt, ist eine Kollision. Lapin führt dieselbe Prüfung selbst aus.

**Verwalten → Öffnungen vs TGA…** legt fest, für welche Öffnungen Körper entstehen (**Wandöffnungen**, **Türöffnungen**, **Fensteröffnungen**, **Deckenöffnungen**, **Dachöffnungen**), um wie viel größer oder kleiner als die Öffnungen sie sind (**Versatz der Öffnung**) und welchen **Abstand** ein TGA-Element zu den Seiten einer Öffnung halten muss. **Prüfung ausführen** geht jedes Rohr, jeden Kanal und jede Kabeltrasse durch — Lapins Leitungen ebenso wie die Segmente eines importierten TGA-Modells und dessen eigene Aussparungsanforderungen — und listet jede Stelle, an der eines eine Wand, Decke oder ein Dach kreuzt:

- **Keine Öffnung**: es kreuzt das Bauteil, wo keine ist;
- **Öffnung zu klein**: es geht durch eine Öffnung, ragt aber heraus, Abstand eingeschlossen; **Fehlt** nennt die Millimeter an der ungünstigsten Seite;
- **Passt** (mit **zeigen, was passt**): ganz im Körper, mit Abstand;
- **Leere Öffnung** (mit **Leere Öffnungen** angehakt): eine Wand- oder Deckenöffnung, durch die nichts führt (ein Treppenloch nie).

Ein Klick auf eine Zeile zeigt das Paar in 3D, beide ausgewählt. **Anmerkungen erstellen** macht aus jeder Kollision eine Anmerkung vom Typ **Clash** mit ihrem Blickpunkt (**Verwalten → Anmerkungen am Modell…**), bereit für BCF; erneut ausgeführt, aktualisiert es dieselben Anmerkungen. Die Kollisionen erscheinen auch unter den Warnungen von **Verwalten → Prüfung**.

![Öffnungen vs TGA: wofür die Körper entstehen, der Abstand und die Prüfung über drei Rohre](shots/mep-voids-check.webp)

Der Reiter **Körper** listet jeden Öffnungskörper mit seiner Marke (V1, V2… Geschoss für Geschoss), seinem Wirt, der Tür oder dem Fenster darin, Größe und Tiefe; ein Klick wählt seine Öffnung.

![Die Öffnungskörper mit Marken und Größen](shots/mep-voids-list.webp)

**Ansicht → Öffnungskörper** zeichnet sie mit der 3D-Ansicht vorn in 3D, durchscheinend violett mit ihren Marken; ein Klick wählt die Öffnung.

![Die Öffnungskörper in der Innenwand, mit den Rohren hindurch](shots/mep-voids-3d.webp)

Mit einem Grundriss, Schnitt oder einer Ansicht vorn zeichnet **Ansicht → Öffnungskörper** sie in dieser Ansicht, wie ein Koordinationsplan Aussparungen zeigt: geschnitten, wo die Ansicht sie schneidet (die Öffnung einer Wand durch deren Dicke, die einer Decke durch die Platte), gesehen, wo sie sie sieht (eine Deckenöffnung unter dem Schnitt des Grundrisses, eine Wandöffnung auf der Fläche, auf die eine Ansicht blickt), gestrichelt, wenn eine Wandöffnung unter dem Schnitt oder eine Deckenöffnung darüber liegt; jede durchkreuzt, mit ihrer Marke und darunter ihrer Größe und der Unterkante einer Wandöffnung (**400×200 +0.90**, **Ø200**). Die Engine zeichnet sie mit der übrigen Zeichnung, so drucken Pläne, das PDF und DXF (Layer **VOIDS**) sie wie der Bildschirm.

![Der Grundriss mit den Öffnungskörpern: in den Wänden geschnitten, die Deckenöffnung gesehen, jede durchkreuzt und markiert](shots/mep-voids-plan.webp)

Es ist eine Einstellung der Ansicht, auch in ihren Eigenschaften (**Ansicht → Ansichtseinstellungen**): **Öffnungskörper**, und **Größen der Aussparungen** für die Größe unter der Marke. Standardmäßig zeichnet keine Ansicht sie; der Ansichtsstil **Koordination** (**Verwalten → Ansichtsstile**, als **Stil** der Ansicht gewählt) zeichnet sie in jeder Ansicht, die ihn nimmt. Ihre Linien sind die Kategorie **Körper**: Stifte und Farben in **Verwalten → Objektstile** (Schnitt-, Projektions-, verdeckte und darüberliegende Linien), je Ansicht gezeigt oder verborgen in **Ansicht → Sichtbarkeit / Grafiken…**.

Um die Körper einem Kollisionsprogramm zu geben, hake **Öffnungskörper** in **Datei → IFC oder Fragments exportieren…** (oder **3D exportieren…**) an. In IFC ist jeder Körper ein **IfcBuildingElementProxy** vom Typ **PROVISIONFORVOID** mit **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter bei runden, Depth) und Lapins **Lapin_OpeningVoid** (Marke, Art, Wirt, Öffnung, Versatz), mit seinem Wirt verbunden; die Öffnungen bleiben IfcOpeningElement. Jeder Körper behält seine GlobalId von Export zu Export. In einem Mesh-Export bilden sie eine eigene Gruppe, **Opening voids**. Ohne Haken bleiben die Exporte wie bisher.

## MEP Pro

Die Ingenieursarbeit am Netz – der Bedarf, den jede Leitung trägt, und die Größe, die ihn aufnimmt, die Prüfungen (offene Enden, enge Bögen, Abwassergefälle, überlastete Quellen), die Verlegeregeln nach Material (Kupfer in 45°-Schritten, PVC mit höchstens 45° Richtungsänderung), Heizkörper als Vorschlag unter Fenstern, Verläufe als Vorschlag entlang der Wände, das Profil einer Leitung und die Isometrie eines Systems – steckt in der Edition **Lapin MEP**, die dieser Registerkarte die Gruppen **Auslegung** und **Assistenten** hinzufügt.
