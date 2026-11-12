---
title: Erweiterungen
order: 126
summary: Möbel, Projektvorlagen, Profile, Schraffurmuster, Planköpfe und Preislisten von anderen aus dem Katalog Lapin Extensions hinzufügen und wieder entfernen.
---

## Der Katalog

**Lapin Extensions** ist ein kostenloser Katalog von Dingen, die Nutzer und wir für Lapin gemacht haben: Möbelpakete, Projektvorlagen, Profilbibliotheken, Schraffurmuster, Planköpfe und Preislisten. Jedes landet in der Bibliothek, die Lapin dafür schon hat, sodass du es nutzt, als hättest du es selbst gemacht.

Öffne **Datei → Erweiterungen…**. **Durchsuchen** zeigt den Katalog: Suche nach Name, Schlagwort oder Inhalt einer Erweiterung, oder wähle eine Kategorie.

![Erweiterungen: Durchsuchen, die Details eines Möbelpakets](shots/extensions-browse.webp)

Klicke auf eine Erweiterung, um ihre Details zu sehen, bevor du sie installierst:

- **Autor** und **Lizenz**. Jede Erweiterung hat eine Lizenz, die ihr Autor gewählt hat. Eine *freizügige* (MIT, CC-BY, CC0…) erlaubt dir, ihren Inhalt in jedem Projekt zu nutzen; bei anderen *Bedingungen lesen*, bevor du sie nutzt.
- **Nicht kommerziell**: Eine Erweiterung, deren Lizenz die kommerzielle Nutzung verbietet (CC-BY-NC, PolyForm Noncommercial…), ist in der Liste und in ihren Details so markiert. Du kannst sie installieren und daraus lernen, aber nicht für bezahlte Arbeit verwenden.
- **Läuft mit**: die Lapin-Versionen, für die sie gemacht wurde. Passt sie nicht zu deinem Lapin, steht das da, statt **Installieren** anzubieten.
- **Größe**, die Datei und ihr **SHA-256**: der Fingerabdruck der Datei, die der Katalog geprüft hat.
- **Inhalt**: ihre Elemente, wenn der Autor sie auflistet.
- **Website** und **Forumthema**: wo du den Autor fragen kannst.

## Installieren, aktualisieren, deinstallieren

Klicke auf **Installieren**. Lapin lädt die Datei herunter, prüft, dass Größe und SHA-256 genau denen im Katalog entsprechen (eine unterwegs veränderte Datei wird abgelehnt, und es wird nichts installiert), und nimmt sie dann in seine Bibliothek auf:

| Art | Wohin sie kommt |
|---|---|
| Möbel | **Innenausbau → Meine Möbel…** |
| Projektvorlagen | **Datei → Neu aus Vorlage…** |
| Profile | die Profilbibliotheken auf diesem Computer |
| Schraffurmuster | Meine Muster, angeboten unter **Verwalten → Materialien** |
| Planköpfe | die Planköpfe auf diesem Computer |
| Preislisten | **Ausführung → Preislisten…**, als Richtwerte markiert: die Preise sind die des Autors, prüfe sie mit deinen eigenen |
| Typen | das Projekt, das beim Installieren offen ist, in einem Schritt rückgängig zu machen (**Verwalten → Typpakete…** exportiert deine eigenen) |

Hat deine Bibliothek schon etwas mit demselben Namen, fragt Lapin nach, bevor es ersetzt. Nichts, was eine Erweiterung mitbringt, wird je ausgeführt: Erweiterungen sind Inhalt.

Lapins Katalog ist außerdem von Lapin Extensions **signiert**: Der Katalog und jede seiner Dateien tragen eine Signatur, die Lapin prüft, bevor es die Liste liest oder eine Datei installiert; eine unterwegs veränderte Kopie wird daher abgelehnt. Eine Zeile über der Liste sagt **Signiert von Lapin Extensions**, wenn es so ist.

**Installiert** listet, was dieser Computer hat. Jede Zeile öffnet den Ort, an dem es gelandet ist (**Meine Möbel…**, **Projektvorlagen…**), bietet **Aktualisieren** an, wenn der Katalog eine neuere Version hat, und **Deinstallieren**.

![Installiert: das Möbelpaket, mit der Schaltfläche, die Meine Möbel öffnet](shots/extensions-installed.webp)

![Die Möbeltypen des Pakets in Meine Möbel](shots/extensions-my-furniture.webp)

**Deinstallieren** entfernt aus deinen Bibliotheken, was die Erweiterung hinzugefügt hat, und nur das. Was du schon in deine Projekte übernommen hast, bleibt dort: ein in ein Projekt eingefügter Möbeltyp, ein aus einer Vorlage gestartetes Projekt.

## Typen

Eine **Typen**-Erweiterung bringt Wand-, Decken-, Tür-, Fenster-, Stützen-, Träger- oder Treppentypen mit allem, woraus sie bestehen: ihren Schichten, Materialien, Profilen und Parametern. Installiert kommen ihre Typen in das jetzt offene Projekt, in einem Schritt rückgängig zu machen: Ein Material gleichen Namens im Projekt wird verwendet, jedes andere angelegt; ein genau gleicher Typ im Projekt wird wiederverwendet, einer gleichen Namens, der sich unterscheidet, kommt daneben mit (2) nach dem Namen. **Installiert → Typen zu diesem Projekt hinzufügen** bringt sie in ein anderes Projekt.

![Die Details einer Typen-Erweiterung: ihre Lizenz als nicht kommerziell markiert](shots/extensions-types.webp)

Deine eigenen Typen kommen über **Verwalten → Typpakete…** in ein Paket: Hake die Typen an, gib dem Paket einen Namen, und **Typen exportieren…** speichert eine `.lapintypes`-Datei. **Typen importieren…** im selben Dialog liest eine, lässt dich ihre Typen wählen und fügt sie genauso hinzu.

![Verwalten → Typpakete…: ein Wandtyp für den Export gewählt](shots/type-packs-export.webp)

## Skripte

Der Katalog listet auch **Python-Skripte**. In der Desktop-App kannst du schon eines installieren; es läuft, sobald Lapin seine Python-Konsole hat, und nur, wenn du es ausführst. Vor der Installation sagen seine Details, was es verlangt (das geöffnete Projekt lesen oder ändern, auf Dateien oder das Internet zugreifen). Ein Skript kann auf deinem Computer tun, was du kannst; installiere also nur Skripte von Autoren, denen du vertraust. Der Browser nimmt nur Inhaltspakete.

![Die Details eines Skripts: was es verlangt, und dass es noch nicht ausführbar ist](shots/extensions-script.webp)

## Ein anderer Katalog

**Katalog…** unten im Dialog legt die Adresse des Katalogs fest: eine https-Adresse oder der Pfad einer `index.json` auf diesem Computer oder in einem geteilten Ordner (der eigene Katalog einer Firma). Leer lassen für Lapins eigenen Katalog. Der Pfeil daneben liest den Katalog erneut. Ein eigener Katalog muss nicht signiert sein: Lapin sagt dann über der Liste, dass er **nicht von Lapin Extensions signiert** ist; installiere daraus also nur, wenn du dem Betreiber vertraust. Ein Katalog, dessen Signatur nicht stimmt, wird nie gelesen.

## Eigene veröffentlichen

Der Katalog hat eine eigene Website, **[store.lapinbim.com](https://store.lapinbim.com)**: eine Seite für jede Erweiterung mit ihren Bildern, Versionen, Lizenz und Downloads und eine Seite für jeden Autor.

Jeder kann eine Erweiterung veröffentlichen. Exportiere sie aus Lapin (Meine Möbel → Exportieren, Als Vorlage speichern…, eine Profilbibliothek, ein Plankopf), dann:

- **Im Store**: Melde dich auf store.lapinbim.com mit deinem Konto im [Forum](https://forum.lapinbim.com) an, öffne **Your extensions** (deine Erweiterungen), fülle das Formular aus und lade die Datei und ein paar Screenshots hoch. Der Store prüft die Datei so, wie Lapin es tun wird, und schickt sie zur Prüfung; deine Seite zeigt die Prüfung und, sobald sie veröffentlicht ist, wie oft sie heruntergeladen wurde. Eine neue Version läuft genauso.
- **Auf GitHub**: Hänge die Datei an ein Release eines eigenen Repositorys und füge ihr Manifest per Pull Request auf [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions) hinzu. Die README dort erklärt, wie, und was die automatischen Prüfungen ansehen.

Ein Typpaket kommt aus **Verwalten → Typpakete… → Typen exportieren…**; eine Preisliste aus **Preislisten…**, mit dem Export-Knopf einer Liste auf diesem Computer (eine `.lapinprices`-Datei, ihre Preise als Richtwerte markiert).
