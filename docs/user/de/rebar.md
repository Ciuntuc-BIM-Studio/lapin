---
title: Bewehrung 2D
order: 52
summary: Bewehrung auf Schnitten und Grundrissen gezeichnet, Querschnitte dort geschnitten, wo Sie klicken, und die Stahlliste.
---

Der Reiter **Bewehrung 2D** zeichnet Bewehrung so, wie eine Stahlliste entsteht: Stäbe als Formen mit ihren Maßen, Bügel entlang eines Balkens verteilt, Positionsnummern in Kreisen und eine Liste, die sie zusammenzählt. Er arbeitet in jeder Ansicht: einem Schnitt, einem Grundriss, einer Fassade.

![Der Reiter Bewehrung 2D](shots/ribbon-rebar.webp)

## Ein Schnitt dort, wo Sie arbeiten

Bewehrung braucht den Querschnitt neben dem Längsschnitt. **Schnitt hier** macht beides: Sie wählen einen Balken, eine Stütze oder eine Wand in der Ansicht, in der Sie sind, und wo Sie klicken, wird ein Schnitt *quer* dazu geschnitten, senkrecht zur Achse, und neben Ihrem Klick gezeichnet, in derselben Ansicht, mit einem halben Meter Umgebung (die Decke, die Stütze). Eine Stütze wird waagerecht geschnitten, in der angeklickten Höhe. Klicken Sie in den Schnitt des Elements, nicht nur auf seine Kante: in einem Schnitt oder einer Ansicht nimmt der Klick wie im Grundriss das Element, dessen Schnitt ihn enthält.

Die eingesetzte Ansicht folgt dem Element: Verschieben Sie den Balken, und der Schnitt wandert mit. Ziehen Sie die eingesetzte Ansicht an ihrem Griff; ihre Eigenschaften geben ihr einen Maßstabsfaktor und einen Rahmen. **Querschnitt** macht denselben Schnitt als eigene Ansicht, und **Eingesetzte Ansicht** setzt eine beliebige vorhandene Ansicht in die ein, in der Sie sind, in wahrer Größe und im Maßstab der Gastansicht, damit darüber gezeichnete Stäbe in den Beton passen.

![Der Längsschnitt eines Balkens mit eingesetztem Querschnitt und seiner Bewehrung](shots/rebar-section.webp)

## Stäbe

**Gerader Stab**, **L-Stab**, **U-Stab**, **Bügel**, **S-Haken** und **Polylinien-Stab** werden mit einem Klick gesetzt (die Polylinie über ihre Eckpunkte). Jeder ist eine Form mit Parametern: Ziehen Sie einen Griff, und das Maß, für das er steht, folgt; tippen Sie es im Eigenschaftenfenster in Millimetern ein. Die Stäbe werden in ihrer Dicke gezeichnet, in einer Farbe je Durchmesser (Bewehrungseinstellungen), und mit dem Radius gebogen, den SR EN 1992-1-1 verlangt: Biegerolle 4Ø bis Ø16, 7Ø darüber.

Haken werden an jedem Ende gesetzt: 90°, 135° oder 180°, ihre gerade Länge nach der Praxis (12Ø, 10Ø, 5Ø, nie unter 50 mm), auf der anderen Seite, wenn gewünscht. Ein Bügel kommt mit zwei nach innen gedrehten 135°-Haken.

**Ankergriffe.** Ein ausgewählter Stab in der Vorderansicht zeigt neben seinen Griffen orangefarbene Rauten, die um die **Betondeckung** *außerhalb* von ihm stehen: eine am Ursprung und eine an jedem freien Ende (ein Ende mit Haken hat keine). Sie stehen für die Schalungsfläche: Ziehen Sie eine auf den Schnitt des Bauteils — seine Ecke, seine Fläche; die Zeichnung fängt dort — und der Stab endet um die Betondeckung innen. Der Anker des Endes setzt die Stablänge, der des Ursprungs verschiebt den ganzen Stab. Die Betondeckung ist die eigene des Stabs, im Feld **Betondeckung** seiner Eigenschaften eingetippt (leeren, um zurückzugehen), sonst die Bügeldeckung des Bauteiltyps plus Bügel, sonst die aus den Bewehrungseinstellungen; das Feld sagt als Hinweis, welche.

**Stab im Schnitt** ist ein Stab von vorn: ein Punkt in seinem Durchmesser, mit der Länge, die er entlang des Stabs hat. **Derselbe Stab im Schnitt** setzt den gewählten Stab anderswo als Punkt, ohne ihn doppelt zu zählen. Eine **Reihe** in den Eigenschaften wiederholt einen Stab alle so viele Millimeter, wie die Stäbe einer Lage im Querschnitt.

Der Positionskreis zeigt die Positionsnummer und was der Stab ist: `4Ø12 L=2450`. Ziehen Sie den Kreis dorthin, wo er am besten lesbar ist.

Der Stil der **Beschriftung** sagt, was sie schreibt und wo sie steht: ihre Teile der Reihe nach (Anzahl, Durchmesser, Abstand, Länge, Position, Stahl, freier Text), der Positionskreis, ob der Text waagerecht, entlang der Stäbe oder im Winkel läuft, auf welcher Seite der Stäbe sie steht und wie viele Millimeter davon, die Hinweislinie. Er wird in den **Bewehrungseinstellungen** je Art gesetzt (Stäbe, Bügel am Typ, Stützenstäbe, Verteilungen), an einem Typ neben seinen Bügeln und an einem Stab in seinen Eigenschaften; das Genaueste gewinnt. Ein Positionskreis wird nie ohne Position gezeichnet. Die Beschriftungen der Bügel eines Typs, der Stäbe einer Stütze und eines in einer anderen Ansicht gesehenen Stabs haben ebenfalls Griffe: Ziehen Sie eine, und sie behält diesen Platz in dieser Ansicht (**Beschriftung zurücksetzen** in den Eigenschaften macht es rückgängig); bis dahin stapeln sich Beschriftungen, die sich träfen, in der Reihenfolge ihrer Positionen untereinander.

Eine **Verteilung** — Bügel am Typ, Bügel entlang eines Wegs — wird als **Maßkette** ohne Werte markiert: eine Linie parallel zu den Stäben außerhalb davon, ein kurzer Strich, wo jeder Stab sie kreuzt, und an beiden Enden, die Beschriftung über der Linie entlang ihr, der Positionskreis an ihrem Anfang. Ziehen Sie den Positionskreis dorthin, wo er am besten lesbar ist; ziehen Sie die Mitte der Maßkette, um sie von den Stäben wegzurücken. **Markiert durch** im Stil der Beschriftung schaltet die Maßkette ab und lässt Text und Positionskreis neben den Stäben. Die **Hinweislinie** kann fehlen, eine Linie sein oder eine zu jedem als Punkt gesehenen Stab (die Vorgabe für Stäbe im Querschnitt: ein Stab, eine Linie; über sechzehn nur die Enden und der mittlere). Ziehen Sie die Mitte einer Hinweislinie, um sie dort zu knicken; der Knick hat dann seinen eigenen Griff, und Strg/⌘-Klick darauf entfernt ihn.

Die Längen sind die Nennlängen, auf der Stabachse zwischen den Ecken gemessen, mit Haken; die abgewickelte Länge um die Biegungen steht im Eigenschaftenfenster.

**Stab teilen** teilt einen Längsstab, der länger ist als die Lieferlänge (12 m oder 6 m, in den **Bewehrungseinstellungen**). Wo sich die beiden Stücke übergreifen, entscheiden Sie, nicht Lapin: ein unterer Stab stößt nahe den Auflagern, ein oberer fern von ihnen. Den Übergreifungsbereich geben Sie also selbst an: Klicken Sie auf den Stab an einem Ende der Übergreifung, dann am anderen; oder klicken Sie dort, wo der erste Stab endet, und tippen Sie die Übergreifungslänge (800 für 800 mm), entlang des Stabs gemessen; Enter oder Escape nach dem ersten Klick lässt die **Übergreifungslänge** der Einstellungen (50Ø, wenn keine gesetzt ist) an diesem Klick enden. In den Eigenschaften des Stabs wird derselbe Bereich als **Übergreifung von … bis …** in Millimetern vom Anfang des Stabs eingegeben, mit der Übergreifungslänge daneben (ändern Sie sie, bleibt „bis“ stehen und „von“ wandert); der erste Stab endet bei *bis*, der zweite beginnt bei *von*, und die beiden Längen werden beim Tippen angezeigt, vor **Stab teilen**. Das erste behält Position und Anfangshaken, das zweite bekommt die nächste Position und den Endhaken, und die Liste zählt sie als zwei Positionen mit ihren Längen. Das zweite Stück wird etwas neben dem ersten gezeichnet, damit die Übergreifung lesbar ist. Ein Stab über der Lieferlänge wird in seinen Eigenschaften angezeigt, und **Bei der Lieferlänge teilen** lässt die Übergreifung bei der Lieferlänge enden, sodass das erste Stück genau so lang ist.

## Bügel am Balken- oder Stützentyp

Bügel werden nicht einzeln gezeichnet: sie gehören zum **Typ**. Unter Tragwerk → Tragwerkstypen bekommt ein rechteckiger Balken- oder Stützentyp **Bügel an diesem Typ**: ihren Durchmesser, die Betondeckung und ihre Verteilung — ein Abstand durchgehend oder **drei Zonen**, jede mit ihrem Anteil der Länge (ein Drittel voreingestellt) und ihrem Abstand, enger an den Enden. Jedes Element des Typs zeigt sie dann in jedem Schnitt so, wie die Ansicht zur Achse liegt: das Rechteck innerhalb der Deckung im Querschnitt, eine Linie je Bügel im Längsschnitt, Linien über dem Balken im Tragwerksgrundriss; mit der Beschriftung `41Ø8/150` und dem Positionskreis. Sie werden in der Stahlliste unter der Position des Typs gezählt. Die Bügel einer **Stütze** können allein um den Umfang laufen, **mit einer Raute innen** (ihre Ecken in den Seitenmitten) oder **mit zwei innen, je einer je Richtung**, jede Art mit eigener Position. Eine Stütze wird geschossweise bewehrt, also liegen ihre **Längsstäbe** ebenfalls am Typ: so viele je Fläche mit gemeinsamen Ecken, ihr Durchmesser und die **Anschlussbewehrung** über die Oberkante hinaus (voreingestellt 1 m) für die Stütze darüber; Punkte im waagerechten Schnitt und im Tragwerksgrundriss, Linien im senkrechten Schnitt.

Die **Längsstäbe** eines Balkens liegen nicht am Typ (sie laufen über mehrere Felder): zeichnen Sie sie im Längsschnitt oder im Grundriss, mit ihren Haken. Ein Stab, der im Schnitt eines Balkens gezeichnet wird, gehört zu diesem Balken und steht im Raum: **Quer zur Zeichnung** legt ihn so oft in Blickrichtung, **nach der Betondeckung** (der erste und der letzte an den Flächen, innerhalb von Deckung und Bügel, die übrigen gleichmäßig) oder **im Abstand**. Der Querschnitt zeigt die Stäbe dann als Punkte dort, wo sie sind, ein Tragwerksgrundriss als Linien, und die Liste zählt sie alle: `3Ø16 L=6400`.

**Stäbe in 3D** (Ansicht → Stäbe in 3D, oder die Schaltfläche in diesem Reiter) zeichnet jeden Stab im Raum als Rohr seines Durchmessers in der 3D-Ansicht, in seiner Farbe: die Bügel des Typs an ihren Stationen, die Stäbe der Stütze, die von Ihnen gezeichneten Stäbe mit ihren Kopien. Es ist ein Bild für den Bildschirm, vom Motor angefordert, solange es eingeschaltet ist, und freigegeben, wenn es aus ist, kostet also nur Speicher, während Sie hinsehen; es wird nicht ausgewählt, nicht exportiert, und über zwei Millionen Dreiecke werden nur die ersten gezeichnet.

## Bügel entlang eines Balkens

**Bügel entlang** nimmt die zwei Enden eines Wegs; die Bügel werden so verteilt, dass an jedem Ende einer steht, im eingestellten Abstand (150 mm voreingestellt). **Zonen** ändern den Abstand unterwegs: **Drittel** setzt an jedem Ende einen halb so großen Abstand, wie ein Erdbebenbalken es verlangt; die Beschriftung sagt `25Ø8/100-200`. Zeigen Sie alle, nur die Enden und die Mitte mit einer gestrichelten Linie dazwischen, oder einen.

## Die Liste

**Stahlliste** macht eine Liste jedes Stabs nach Position: Durchmesser, Form, Anzahl, Länge eines Stabs, Gesamtlänge und Masse, mit den Summen je Durchmesser. Eine Position, deren Stäbe in Durchmesser oder Länge abweichen, wird vermerkt. Es ist eine Liste wie die anderen: auf Plänen, in CSV und XLSX.

**Bewehrungseinstellungen** hält die Wahl des Projekts: eine Farbe je Durchmesser, die Text- und Kreisgrößen auf dem Papier, den Stahl, die Betondeckung, die an den Stäben angeschriebenen Teillängen und den Stil der Beschriftungen je Art.
