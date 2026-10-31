---
title: Bewehrung 2D
order: 52
summary: Bewehrung auf Schnitten und Grundrissen gezeichnet, Querschnitte dort geschnitten, wo Sie klicken, und die Stahlliste.
---

Der Reiter **Bewehrung 2D** zeichnet Bewehrung so, wie eine Stahlliste entsteht: Stäbe als Formen mit ihren Maßen, Bügel entlang eines Balkens verteilt, Positionsnummern in Kreisen und eine Liste, die sie zusammenzählt. Er arbeitet in jeder Ansicht: einem Schnitt, einem Grundriss, einer Fassade.

![Der Reiter Bewehrung 2D](shots/ribbon-rebar.webp)

## Ein Schnitt dort, wo Sie arbeiten

Bewehrung braucht den Querschnitt neben dem Längsschnitt. **Schnitt hier** macht beides: Sie wählen einen Balken, eine Stütze oder eine Wand in der Ansicht, in der Sie sind, und wo Sie klicken, wird ein Schnitt *quer* dazu geschnitten, senkrecht zur Achse, und neben Ihrem Klick gezeichnet, in derselben Ansicht, mit einem halben Meter Umgebung (die Decke, die Stütze). Eine Stütze wird waagerecht geschnitten, in der angeklickten Höhe.

Die eingesetzte Ansicht folgt dem Element: Verschieben Sie den Balken, und der Schnitt wandert mit. Ziehen Sie die eingesetzte Ansicht an ihrem Griff; ihre Eigenschaften geben ihr einen Maßstabsfaktor und einen Rahmen. **Querschnitt** macht denselben Schnitt als eigene Ansicht, und **Eingesetzte Ansicht** setzt eine beliebige vorhandene Ansicht in die ein, in der Sie sind.

![Der Längsschnitt eines Balkens mit eingesetztem Querschnitt und seiner Bewehrung](shots/rebar-section.webp)

## Stäbe

**Gerader Stab**, **L-Stab**, **U-Stab**, **Bügel**, **S-Haken** und **Polylinien-Stab** werden mit einem Klick gesetzt (die Polylinie über ihre Eckpunkte). Jeder ist eine Form mit Parametern: Ziehen Sie einen Griff, und das Maß, für das er steht, folgt; tippen Sie es im Eigenschaftenfenster in Millimetern ein. Die Stäbe werden in ihrer Dicke gezeichnet, in einer Farbe je Durchmesser (Bewehrungseinstellungen), und mit dem Radius gebogen, den SR EN 1992-1-1 verlangt: Biegerolle 4Ø bis Ø16, 7Ø darüber.

Haken werden an jedem Ende gesetzt: 90°, 135° oder 180°, ihre gerade Länge nach der Praxis (12Ø, 10Ø, 5Ø, nie unter 50 mm), auf der anderen Seite, wenn gewünscht. Ein Bügel kommt mit zwei nach innen gedrehten 135°-Haken.

**Stab im Schnitt** ist ein Stab von vorn: ein Punkt in seinem Durchmesser, mit der Länge, die er entlang des Stabs hat. **Derselbe Stab im Schnitt** setzt den gewählten Stab anderswo als Punkt, ohne ihn doppelt zu zählen. Eine **Reihe** in den Eigenschaften wiederholt einen Stab alle so viele Millimeter, wie die Stäbe einer Lage im Querschnitt.

Der Positionskreis zeigt die Positionsnummer und was der Stab ist: `4Ø12 L=2450`. Ziehen Sie den Kreis dorthin, wo er am besten lesbar ist. Die Längen sind die Nennlängen, auf der Stabachse zwischen den Ecken gemessen, mit Haken; die abgewickelte Länge um die Biegungen steht im Eigenschaftenfenster.

## Bügel entlang eines Balkens

**Bügel entlang** nimmt die zwei Enden eines Wegs; die Bügel werden so verteilt, dass an jedem Ende einer steht, im eingestellten Abstand (150 mm voreingestellt). **Zonen** ändern den Abstand unterwegs: **Drittel** setzt an jedem Ende einen halb so großen Abstand, wie ein Erdbebenbalken es verlangt; die Beschriftung sagt `25Ø8/100-200`. Zeigen Sie alle, nur die Enden und die Mitte mit einer gestrichelten Linie dazwischen, oder einen.

## Die Liste

**Stahlliste** macht eine Liste jedes Stabs nach Position: Durchmesser, Form, Anzahl, Länge eines Stabs, Gesamtlänge und Masse, mit den Summen je Durchmesser. Eine Position, deren Stäbe in Durchmesser oder Länge abweichen, wird vermerkt. Es ist eine Liste wie die anderen: auf Plänen, in CSV und XLSX.

**Bewehrungseinstellungen** hält die Wahl des Projekts: eine Farbe je Durchmesser, die Text- und Kreisgrößen auf dem Papier, den Stahl, die Betondeckung und die an den Stäben angeschriebenen Teillängen.
