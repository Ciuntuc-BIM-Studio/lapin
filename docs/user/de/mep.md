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

## MEP Pro

Die Ingenieursarbeit am Netz – der Bedarf, den jede Leitung trägt, und die Größe, die ihn aufnimmt, die Prüfungen (offene Enden, enge Bögen, Abwassergefälle, überlastete Quellen), die Verlegeregeln nach Material (Kupfer in 45°-Schritten, PVC mit höchstens 45° Richtungsänderung), Heizkörper als Vorschlag unter Fenstern, Verläufe als Vorschlag entlang der Wände, das Profil einer Leitung und die Isometrie eines Systems – steckt in der Edition **Lapin MEP**, die dieser Registerkarte die Gruppen **Auslegung** und **Assistenten** hinzufügt.
