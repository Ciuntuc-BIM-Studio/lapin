---
title: Tragwerksberechnung
order: 52
summary: Eine linear-statische Berechnung des Rahmens unter Ihren Lasten ausführen, die Diagramme der Kräfte, Momente und Verformung in 3D lesen und die Freigaben an den Stabenden festlegen.
---

**Berechnung starten** in der Gruppe **Lasten** der Registerkarte **Tragwerk** (und im Dialog Lasten) berechnet die Stäbe des Modells unter den Lasten und Kombinationen aus [Lasten](loads.md) und zeigt die Kräfte in ihnen.

![Die Gruppe Lasten mit Berechnung starten](shots/ribbon-analysis.webp)

> Die Ergebnisse sind **Richtwerte**. Lasten, Kombinationen und Berechnung sind von Ihnen zu prüfen, bevor Sie sich darauf stützen.

## Was berechnet wird

- **Stäbe** (Stützen, Balken, Stahl- und Holzbauteile) gehen mit ihrer eigenen Steifigkeit ein, aus Material und Querschnitt. Ein Stab, dessen Material keinen Elastizitätsmodul hat, bleibt unberücksichtigt und wird aufgeführt.
- **Tragende Wände** sind Auflager: Sie halten die Stäbe auf ihnen, werden aber nicht als Wände berechnet.
- **Decken und Platten** tragen nicht als Platten; sie leiten ihre Lasten nach der **45°-Regel** an die Stäbe und Wände darunter ab: Die Winkelhalbierenden der Ecken teilen die Platte in die Flächen, die jede Seite trägt, und jede wird zu einer trapez- oder dreieckförmigen Last auf dem Stab darunter.
- **Dächer mit Tragwerk** leiten die Last auf jeden Sparren über seine **Einzugsbreite**, bis zur halben Strecke zu den Nachbarn; Schnee wird je horizontaler Projektion angesetzt.
- **Pendelstäbe und Zugglieder** (ein aussteifender Rundstab, ein Zugband oder ein Seil) arbeiten axial: Sie sind an den Enden gelenkig, und ihr Gewicht geht an die Enden, sodass sie nicht wie Balken durchhängen. Die Einstellung **Axial beanspruchter Stab** in den Eigenschaften eines Stabs unter **Analyse** ändert das: **Nach Querschnitt und Nutzung**, **Ja (Pendelstab oder Zugband)** oder **Nein (ein Balken)**.
- **Balken unterschiedlicher Höhe**, die auf eine Stütze treffen, behalten jeweils ihre eigene Höhenlage; die Stütze hat einen Knoten, und die Balken sind durch einen starren Arm an ihn gebunden.
- **Wind auf den Fassaden** geht an die Balken, Riegel und Deckenränder entlang ihnen; der Streifen an ihrem Fuß geht direkt in den Boden und wird getrennt von den Summen der Stäbe aufgeführt.
- Das **Eigengewicht** der Stäbe wird entlang der Stäbe angesetzt; die von Ihnen gezeichneten und die abgeleiteten Lasten gehen dorthin, wo sie darunter etwas finden. Eine Last, die nichts findet, bleibt unberücksichtigt und wird gemeldet.

## Ausführen

Wählen Sie **Berechnung starten**. Der Dialog **Analyseergebnisse** öffnet sich:

![Der Dialog Analyseergebnisse](shots/analysis-results.webp)

- Die Zusammenfassung: Stäbe, Knoten und Unbekannte der Berechnung und wie lange sie dauerte.
- Für jeden Lastfall die **Summe seiner Lasten** gegenüber der **Summe der Auflagerkräfte** und ob sie sich **im Gleichgewicht** halten (Haken) oder nicht (Kreuz: verlassen Sie sich nicht auf diese Ergebnisse). Nicht zugeordnete Lasten stehen in kN.
- Was **nicht berücksichtigt** wurde, und warum.
- Die **größten und kleinsten** Kräfte und Momente, die größte Verschiebung und Durchbiegung (mit ihrem Verhältnis zur Spannweite, L/300) des gewählten Lastfalls, der Kombination oder der Einhüllenden.

**Diagramme anzeigen** schließt den Dialog und zeigt die Ergebnisse in der 3D-Ansicht.

## Die Diagramme in 3D

Eine Karte über der 3D-Ansicht wählt, was gezeichnet wird:

![Das Biegemoment der Einhüllenden im Grenzzustand der Tragfähigkeit](shots/analysis-moments.webp)

- **Was**: ein Lastfall, eine Kombination oder die **Einhüllende** der Kombinationen im Grenzzustand der Tragfähigkeit (GZT) oder der Gebrauchstauglichkeit (GZG): der größte und kleinste Wert an jedem Punkt.
- **Größe**: **N** Normalkraft (Zug positiv), **Vy** und **Vz** Querkräfte, **T** Torsion, **My** und **Mz** Biegemomente (**Mz** ist die starke Achse eines waagerechten Balkens, positiv bei Zug unten) oder die **Verformt**e Form.
- **Maßstab**: die Größe des Diagramms auf dem Bildschirm; das Modell selbst bleibt unverändert.
- **Auflagerkräfte**: Pfeile an den Auflagern mit ihren Werten in kN.

Jeder Stab zeigt seine Grundlinie, die Kurve, Schraffurlinien dazwischen und die Werte an den Extremen; Rot ist positiv, Blau negativ. Momente werden auf der Zugseite des Stabs gezeichnet.

![Die verformte Form](shots/analysis-deformed.webp)

## Freigaben an den Enden

Wählen Sie eine Stütze, einen Balken oder ein Bauteil: In seinen Eigenschaften gibt es einen Abschnitt **Analyse** mit den Freigaben **Anfang (i)** und **Ende (j)**.

![Der Abschnitt Analyse eines Balkens](shots/analysis-bar.webp)

- **Standard**: was der Anschluss sagt (eine Lasche, ein Knotenblech oder eine Windverband-Diagonale macht das Bauteil gelenkig), ein Holzbauteil des Dachs gelenkig um seine starke Achse, jeder andere Stab eingespannt.
- **Eingespannt**: Das Ende nimmt Momente auf. **Gelenkig**: Es dreht sich frei um beide Biegeachsen.
- **Benutzerdefiniert**: sechs Kästchen, eines für jeden Freiheitsgrad (Verschiebung längs des Stabs, längs jeder Querkraftachse, Torsion, Drehung um jede Biegeachse); ein angehakter Freiheitsgrad ist freigegeben.

Eine Änderung ist ein einziger Rückgängig-Schritt.

## Wenn sich das Modell ändert

Die Ergebnisse werden aus dem Modell berechnet, wie es ist, und nie gespeichert. Ändern Sie das Modell, verschwinden die Diagramme aus der Ansicht, und die Karte sagt es:

![Die Karte nach einer Änderung](shots/analysis-pick.webp)

**Erneut berechnen** berechnet das neue Modell.

## Ein Mechanismus

Kann sich das Tragwerk ohne Widerstand bewegen (zu viele Freigaben oder ein Teil, das nichts hält), bricht die Berechnung ab und sagt, wo:

![Ein Mechanismus](shots/analysis-mechanism.webp)

Sie nennt den Knoten, den Freiheitsgrad und die betroffenen Stäbe; **Beteiligte Bauteile auswählen** wählt sie aus, damit Sie sie beheben können, meist durch Einspannen eines Endes oder ein zusätzliches Auflager.
