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

Hat deine Bibliothek schon etwas mit demselben Namen, fragt Lapin nach, bevor es ersetzt. Nichts, was eine Erweiterung mitbringt, wird je ausgeführt: Erweiterungen sind Inhalt.

**Installiert** listet, was dieser Computer hat. Jede Zeile öffnet den Ort, an dem es gelandet ist (**Meine Möbel…**, **Projektvorlagen…**), bietet **Aktualisieren** an, wenn der Katalog eine neuere Version hat, und **Deinstallieren**.

![Installiert: das Möbelpaket, mit der Schaltfläche, die Meine Möbel öffnet](shots/extensions-installed.webp)

![Die Möbeltypen des Pakets in Meine Möbel](shots/extensions-my-furniture.webp)

**Deinstallieren** entfernt aus deinen Bibliotheken, was die Erweiterung hinzugefügt hat, und nur das. Was du schon in deine Projekte übernommen hast, bleibt dort: ein in ein Projekt eingefügter Möbeltyp, ein aus einer Vorlage gestartetes Projekt.

## Skripte

Der Katalog listet auch **Python-Skripte**. In der Desktop-App kannst du schon eines installieren; es läuft, sobald Lapin seine Python-Konsole hat, und nur, wenn du es ausführst. Vor der Installation sagen seine Details, was es verlangt (das geöffnete Projekt lesen oder ändern, auf Dateien oder das Internet zugreifen). Ein Skript kann auf deinem Computer tun, was du kannst; installiere also nur Skripte von Autoren, denen du vertraust. Der Browser nimmt nur Inhaltspakete.

![Die Details eines Skripts: was es verlangt, und dass es noch nicht ausführbar ist](shots/extensions-script.webp)

## Ein anderer Katalog

**Katalog…** unten im Dialog legt die Adresse des Katalogs fest: eine https-Adresse oder der Pfad einer `index.json` auf diesem Computer oder in einem geteilten Ordner (der eigene Katalog einer Firma). Leer lassen für Lapins eigenen Katalog. Der Pfeil daneben liest den Katalog erneut.

## Eigene veröffentlichen

Jeder kann eine Erweiterung veröffentlichen: Exportiere sie aus Lapin (Meine Möbel → Exportieren, Als Vorlage speichern…, eine Profilbibliothek, ein Plankopf), hänge die Datei an ein Release eines eigenen GitHub-Repositorys und füge ihr Manifest dem Katalog per Pull Request auf [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions) hinzu. Die README dort erklärt, wie, und was die automatischen Prüfungen ansehen.
