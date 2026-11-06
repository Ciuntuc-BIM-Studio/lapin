---
title: Ebenen und Grundrisse
order: 60
summary: Fügen Sie Ebenen hinzu, legen Sie ihre Höhen fest und sehen Sie eine andere Ebene unter dem Grundriss, auf dem Sie zeichnen.
---

<!-- video levels -->

## Ebenen

Der Projektbrowser listet oben die Ebenen auf, die höchste zuerst. **+** neben **Ebenen** fügt eine Ebene über der höchsten hinzu.

Wählen Sie eine Ebene aus, um ihre **Basisebene** und ihre **Obere Ebene** zu sehen. Beide können über oder unter ±0.00 liegen. Ihre Höhe ist der Abstand zwischen ihnen. Wenn Sie die Basis verschieben, bleibt die Oberkante, wo sie ist. Ein neues Projekt beginnt mit Foundations, Ground floor und First floor. Eine Ebene ist höchstens 100 m hoch und liegt innerhalb von ±10 km um ±0.00: ein in der falschen Einheit eingegebener Wert wird abgelehnt. Eine Treppe, die mehr als 50 m bis zur Ebene darüber steigen würde, endet dort und sagt warum.

![Die Eigenschaften einer Ebene: Basisebene, obere Ebene und Höhe](shots/level-properties.webp)

Jede Ebene hat ihren eigenen Grundriss unter **Grundrisse**. Doppelklicken Sie auf einen Grundriss, um auf dieser Ebene zu zeichnen. Die Ebene, auf der Sie zeichnen, wird im Feld oben links in der Ansicht angezeigt.

In Schnitten und Ansichten wird jede Ebene rechts mit Namen und Höhe markiert. **Verwalten → Objektstile → Verdeckte Linien** legt auch die **Höhenmarke** fest: ein offenes Dreieck, ein Dreieck mit schwarzer linker Hälfte (wie die rumänische Norm es zeichnet), ein schwarzes Dreieck oder ein Strich, und ihre Größe auf dem Papier.

Höhenkoten werden in den Einheiten des Projekts geschrieben: in Metern (+3.00) in einem metrischen Projekt, wie Pläne sie schreiben, und in Fuß und Zoll (+9'-10 1/8") in einem imperialen. Ihnen folgen der Projektbrowser, der Kasten oben links in der Ansicht, die Höhenmarken in Schnitten und Ansichten und die Höhenkoten.

## Die Ebene darunter sehen

Um beim Zeichnen eine andere Ebene zu sehen, wählen Sie sie in der Leiste des Grundrisses unter **Unterlage**. Der Grundriss dieser Ebene wird darunter gezeichnet, blass und grau. Sie können darauf fangen, sie aber nicht auswählen.

![Das Obergeschoss über dem als Unterlage angezeigten Erdgeschoss gezeichnet](shots/underlay.webp)

Die Unterlage hilft Ihnen nur beim Zeichnen; sie wird nicht gedruckt. Um Elemente anderer Ebenen auf einem Grundriss zu drucken, öffnen Sie die Eigenschaften der Grundrissansicht und wählen Sie sie unter **Angezeigte Geschosse**. Sie können einige oder alle Ebenen anzeigen, auf Wunsch in Halbton.

## In andere Geschosse kopieren

Um Wände, Decken oder einen ganzen Grundriss auf anderen Ebenen zu wiederholen, wählen Sie ihn aus (Wände mit ihren Türen und Fenstern, Decken, Räume, eine Gruppe…) und wählen Sie **Ändern → In Geschosse kopieren**. Sie können auch `COPYSTOREY` in die Befehlszeile tippen oder mit der rechten Maustaste auf die Auswahl in einem Grundriss oder in der 3D-Ansicht klicken. Markieren Sie die Ebenen, in die kopiert wird. **Alle darüber** und **Alle darunter** markieren jede Ebene über oder unter der Ebene der Auswahl, und ein Umschalt-Klick markiert alle Ebenen zwischen der angeklickten und der zuletzt angeklickten. Die Ebene, auf der die Auswahl steht, lässt sich nicht markieren.

![In Geschosse kopieren: zwei Ebenen markiert, und die Optionen](shots/copy-to-storeys.webp)

- **Relativ** (Standard): Jede Kopie steht in derselben Höhe über ihrer neuen Ebene wie das Original über seiner. Eine im Erdgeschoss gezeichnete Wand steht im Obergeschoss.
- **Absolut**: Jede Kopie bleibt, wo das Original im Raum ist, auf der neuen Ebene. Ihr Basisversatz gleicht den Unterschied aus.
- **Auch gehostete Elemente**: Türen und Fenster gehen mit ihren Wänden, Öffnungen mit dem, was sie schneiden. Die Beschriftungen und Bemaßungen der kopierten Elemente gehen vom Grundriss ihrer Ebene in den Grundriss der neuen Ebene. Ein Raum nimmt seine Beläge mit, eine Treppe den Raum unter ihr. Ohne diese Option werden die Wände ohne ihre Türen und Fenster kopiert.
- **Beziehungen zwischen den Kopien neu bilden**: An einer Ecke oder einem T verbundene Wände werden auf jeder neuen Ebene genauso verbunden. Eine Wand, die an eine mitkopierte Decke oder ein mitkopiertes Dach angebunden ist, wird an deren Kopie angebunden. Eine Gruppe wird zu einer Gruppe der Kopien, und eine Baugruppe wird als Ganzes kopiert. Eine Treppe steigt bis zur Ebene über ihrer neuen und schneidet dort ihre eigene Öffnung in die Decke.
- **Duplikate überspringen**: Eine Ebene, die dasselbe Element schon an derselben Stelle hat, bekommt kein zweites. Die Meldung unten sagt, wie viele übersprungen wurden.

Die Kopien verwenden dieselben Typen wie die Originale und erhalten neue IFC-GlobalIds. Alle Kopien sind ein einziger Rückgängig-Schritt und bleiben danach ausgewählt, sodass Sie sie gemeinsam verschieben oder ändern können. In einer gemeinsamen Sitzung macht nur der Gast seine eigenen Kopien rückgängig.

![Die Wände und die Decke des Erdgeschosses in die zwei Ebenen darüber kopiert, in 3D](shots/copy-to-storeys-3d.webp)

Um die Auswahl stattdessen in eine andere Ebene zu verschieben, wählen Sie **Ändern → In Geschoss verschieben**. Die Elemente behalten ihre Höhen über der neuen Ebene oder bleiben, wo sie im Raum sind.
