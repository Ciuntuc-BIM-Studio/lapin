---
title: Terminplanung
order: 97
summary: Der Bauzeitenplan als Gantt-Diagramm: Vorgänge in einem PSP, Meilensteine, Verknüpfungen mit Zeitabständen, der kritische Pfad auf einem Kalender mit den gesetzlichen Feiertagen Rumäniens; die 4D-Simulation des Tag für Tag wachsenden Gebäudes, in Ansichten, auf Plänen und als Video; der Fortschritt, auf der Baustelle markiert, auch vom Telefon, gegen einen Basisplan; zu und von MS Project, IFC und Excel.
---

## Der Reiter Terminplanung

![Der Reiter Terminplanung](shots/ribbon-planning.webp)

- **Terminplan erzeugen…**: ein ganzer Terminplan aus dem Modell, in einem Schritt (unten).
- **Gantt**: öffnet den Terminplan unter der Ansicht. Der Bildschirm teilt sich in zwei Zeilen: oben das Modell, unten der Terminplan.
- **Kalender…**: die Arbeitstage und -stunden, die Feiertage und die Winterpause (unten).
- **Vorgang** und **Meilenstein**: ein neuer Vorgang oder Meilenstein vor dem im Gantt gewählten Vorgang (oder am Ende).
- **Einrücken**, **Ausrücken**, **Vorgänge verknüpfen** und **Verknüpfungen lösen**: der PSP und die Verknüpfungen der gewählten Vorgänge.
- **Zu Vorgang hinzufügen…** (TA) und **Elemente ohne Vorgang**: die Elemente des Modells in den Vorgängen (unten).
- **Simulieren**, **Bauabschnittsplan…**, **Gantt auf Plan** und **Video exportieren…**: die 4D-Simulation, das Modell, wie der Bauzeitenplan es an einem Tag hat (unten).
- **Als fertig markieren** (MD), **Als begonnen markieren** (MS), **Als nicht begonnen markieren**, **Basisplan speichern**, **Basisplan löschen** und **Fortschritt importieren…**: der Fortschritt und der Basisplan (unten).
- **MS Project importieren…**, **MS Project exportieren…**, **XLSX exportieren** und **IFC exportieren…**: der Terminplan zu und von anderen Programmen (unten).

Ein Projekt hat einen Terminplan. Er wird beim ersten Hinzufügen eines Vorgangs angelegt und beginnt am nächsten Montag. Den Beginn ändern Sie in der Leiste des Gantt.

## Den Terminplan aus dem Modell erzeugen

![Terminplan erzeugen: die Vorlagen, der Reiter Anfang und die Vorschau](shots/planning-generate.webp)

**Terminplanung → Terminplan erzeugen…** (auch in der Leiste des Gantt) macht aus dem Modell einen vollständigen Terminplan: einen Vorgang für jeden Schritt der Arbeiten und, wo der Schritt Geschoss für Geschoss geht, einen je Geschoss, jeder Vorgang schon mit seinen Elementen. Links stehen die **Vorlagen**: **Stahlbetonskelett**, **Tragendes Mauerwerk**, **Stahlhalle** und **Holzrahmenbau**. Lapin markiert die vom Modell verlangte als *empfohlen*: die Stahlhalle, wenn das Modell eine hat, den Holzrahmenbau, wenn seine Wände Ständerwände sind, tragendes Mauerwerk ohne Stützen, sonst das Stahlbetonskelett. Darüber stehen die Stile des Projekts (**Als Stil speichern…** behält, was du eingestellt hast).

Rechts ist die **Vorschau**: die Vorgänge, die der Generator anlegen würde, als Balken, jeder mit der Menge, die seine Dauer ergab, und darunter die Zählungen:

- **Verknüpfte Elemente**: die Elemente, die die Vorgänge nehmen, von allen, die einen Vorgang brauchen. Das ist hier die wichtigste Zahl. Bleiben Elemente draußen, wählt **Die Elemente ohne Vorgang zeigen** sie in der Ansicht aus, und **Ein Schritt für sie** fügt einen Schritt mit ihren Klassen hinzu.
- **Dauern aus Aufwandswerten** und **Standarddauern (ohne Aufwandswert)**: wie viele Vorgänge ein Aufwandswert getaktet hat und wie viele die Tage ihres Schritts nahmen.
- **Schalungen und Gerüste an den Vorgängen ihrer Träger** (unten).

**Anwenden** schreibt den Terminplan als einen Schritt zum Rückgängigmachen. Der Bildschirm teilt sich dann: oben das Modell, unten das Gantt.

### Der Reiter Anfang

- **Anfang**: der erste Tag des Terminplans.
- **Nach Geschossen aufteilen**: ein Vorgang je Geschoss für die Schritte, die Geschoss für Geschoss gehen (Rohbau, Mauerwerk, Oberflächen); ohne Haken ein Vorgang je Schritt.
- **Abbruch zuerst**: die Elemente, die die Phasen abbrechen, kommen in einen Vorgang **Abbruch** vor allen anderen (IFC-Art DEMOLITION). Bestehende Elemente, die bleiben, gehören keinem Vorgang.
- **Schalung** und **Gerüst**: ob diese temporären Elemente nach den Vorgängen ihrer Träger leben (unten).
- **Von Hand angelegte Vorgänge behalten**: beim erneuten Erzeugen (unten).

### Der Reiter Ablauf

![Der Reiter Ablauf: die Schritte der Reihe nach](shots/planning-generate-sequence.webp)

Die Schritte der Vorlage der Reihe nach. Jeder lässt sich abschalten, umbenennen und mit seinen Pfeilen nach oben oder unten schieben. Er sagt, ob er **einmal** für das Gebäude oder **je Geschoss** geht und wie er dem vorigen Schritt im selben Geschoss folgt: Ende–Anfang (FS) oder Anfang–Anfang (SS) mit Zeitabstand, für Oberflächen, die hinter dem Rohbau hochsteigen. Der Stift öffnet, was der Schritt baut: die Klassen der Elemente, gewählt aus denen des Modells, und den **Teil** davon (ganz, der Kern oder die Oberflächen).

Die Verknüpfungen ergeben sich aus der Reihenfolge: jeder Vorgang folgt dem vorigen Schritt im selben Geschoss und demselben Schritt im Geschoss darunter. Bei tragendem Mauerwerk und Holzrahmenbau warten die Wände eines Geschosses auch auf die Decke des Geschosses darunter.

Jeder Vorgang bekommt eine **Regel** (seine Klassen, sein Geschoss) und behält so seine Elemente: eine nach dem Erzeugen gezeichnete Wand kommt von selbst in ihren Vorgang.

### Der Reiter Dauern

![Der Reiter Dauern: die Aufwandswerte und je Schritt Aufwandswert, Kolonne und Tage](shots/planning-generate-durations.webp)

Die Dauer eines Vorgangs ergibt sich aus der Menge seiner Elemente (aus der Mengenermittlung: das Volumen des Kerns, die Fläche der Oberflächen, Türen und Fenster nach Stück, Stahl nach Gewicht) und einem **Aufwandswert**:

*Dauer = Menge × Lohnstunden je Einheit ÷ (Kolonne × Stunden am Tag)*, in ganzen Arbeitstagen. Zum Beispiel 4,5 m³ Mauerwerk zu 4 Lohnstunden je Kubikmeter, mit einer Kolonne von 2, die 8 Stunden arbeitet: 18 ÷ 16 = 1,125, also 2 Tage.

Die Aufwandswerte sind deine: Lapin liefert kein Kalkulationswerk. **Aufwandswerte importieren…** liest deine Tabelle aus einer CSV- oder XLSX-Datei: eine Spalte für den Code, eine für den Text, eine für die Einheit (m3, m2, m, Stk, kg, t…), eine für die Lohnstunden je Einheit (oder eine Leistung in Einheiten pro Stunde) und wahlweise eine für die Kolonne. **Vorlage (CSV)** speichert die Spalten mit einigen Zeilen grober Werte von Lapin, jede als *indicative* (Richtwert) markiert. **Lapins Richtwerte verwenden** legt diese Zeilen ins Projekt, um den Generator auszuprobieren; der Dialog warnt dann, dass die Dauern aus Richtwerten kommen.

Darunter wählt jeder Schritt seinen **Aufwandswert**, seine **Kolonne** (0: die des Aufwandswerts) und seine **Tage**, die gelten, wenn kein Aufwandswert sie ergibt. Der Reiter Kalender zeigt den Kalender, in dem die Dauern gezählt werden.

### Erneut erzeugt

Öffne den Generator wieder und klicke **Anwenden**: der Terminplan wird über denselben Vorgängen neu gemacht, ihre Kennungen bleiben. Was du von Hand geändert hast, bleibt:

- ein erzeugter Vorgang, den du bearbeitet hast (Name, Dauer, Termine, Regel, Teil), ist **Wie bearbeitet behalten**, angehakt in seinen Eigenschaften; nimm den Haken weg, damit er aus seinem Schritt neu entsteht;
- Elemente, die du von Hand hinzugefügt oder herausgenommen hast, bleiben seine Ausnahmen;
- deine eigenen Vorgänge und Verknüpfungen bleiben; ohne **Von Hand angelegte Vorgänge behalten** bleiben nur die erzeugten.

![Der erzeugte Terminplan: oben das Modell, unten das Gantt](shots/planning-generated.webp)

In den Eigenschaften sagt ein erzeugter Vorgang, woher seine Dauer kommt: Menge, Aufwandswert und Kolonne, oder die Tage des Schritts.

![Ein erzeugter Vorgang in den Eigenschaften](shots/planning-generated-task.webp)

### Schalungen und Gerüste

Eine Schalung oder ein Gerüst aus der **Baustelleneinrichtung** hat keinen eigenen Vorgang: sie leben nach den Vorgängen dessen, dem sie dienen. Die Schalung wird aufgestellt, wenn ihre Decke, ihr Unterzug oder ihre Wand beginnt, und ausgeschalt, wenn die Tage ihres Typs nach deren Ende vergangen sind (**Ausgeschalt nach**, in Arbeitstagen: 21 bei Decken und Unterzügen, 7 bei Wänden, Stützen und Fundamenten, sofern der Typ nichts anderes sagt). Das Gerüst steht vom ersten Vorgang an seinen Wänden bis zum letzten, ihre Oberflächen eingeschlossen. Wähle eines aus, und seine Eigenschaften sagen, wann es **Auf der Baustelle** ist und nach welchen Vorgängen es **Lebt nach**.

## Das Gantt

![Das Gantt unter der 3D-Ansicht, ein Vorgang gewählt](shots/planning-gantt.webp)

Links steht die Vorgangstabelle: die Zeilennummer, der **PSP**-Code, der Name, die **Dauer** in Arbeitstagen, **Anfang** und **Ende**, die **Vorgänger** und der gesamte **Puffer**. Rechts ist jeder Vorgang ein Balken auf der Zeitskala:

- ein **Vorgang** ist ein blauer Balken, rot, wenn er auf dem kritischen Pfad liegt;
- ein **Sammelvorgang** (ein Vorgang mit Vorgängen darunter) ist eine dunkle Klammer vom Anfang seines ersten bis zum Ende seines letzten Vorgangs;
- ein **Meilenstein** ist eine Raute.

Arbeitsfreie Tage sind schattiert, und heute ist eine gestrichelte Linie. **Tage**, **Wochen**, **Monate** und **Quartale** ändern die Skala, ebenso <kbd>Strg</kbd> + das Mausrad. Die Schaltfläche mit den vier Ecken zeigt den ganzen Terminplan, und **Heute** springt zu heute. **Kritischer Pfad** schaltet das Rot ein und aus. Der Pfeil neben dem Namen eines Sammelvorgangs klappt die Vorgänge darunter zu und auf.

Das Gantt ist eine Ansicht wie die anderen: Es hat einen Reiter und lässt sich auf dem Desktop in einem eigenen Fenster für einen zweiten Bildschirm öffnen.

### Bearbeiten in der Tabelle

Doppelklicken Sie auf eine Zelle, um darin zu schreiben. <kbd>Enter</kbd> übernimmt den Wert, <kbd>Escape</kbd> verwirft ihn.

- **Dauer**: Arbeitstage; 0 macht den Vorgang zum Meilenstein.
- **Anfang**: der Vorgang beginnt nicht früher als an diesem Tag (eine Einschränkung, siehe unten).
- **Ende**: die Dauer wird zu den Arbeitstagen vom Anfang bis zu diesem Tag.
- **Vorgänger**: wie in MS Project Zeilennummern, durch Kommas getrennt, jede mit Typ und Zeitabstand: `3`, `5SS`, `7FF+2`, `2FS-1d`.

Die Tastatur funktioniert wie in einer Tabelle: <kbd>↑</kbd> und <kbd>↓</kbd> verschieben die Auswahl, <kbd>Insert</kbd> fügt einen Vorgang vor dem gewählten ein (<kbd>Shift</kbd>+<kbd>Insert</kbd> einen Meilenstein), <kbd>Delete</kbd> löscht die gewählten Vorgänge samt dem, was darunter liegt, <kbd>Tab</kbd> rückt ein und <kbd>Shift</kbd>+<kbd>Tab</kbd> rückt aus, <kbd>F2</kbd> benennt um.

### Bearbeiten im Diagramm

- **Einen Balken ziehen**, um den Vorgang zu verschieben. Er beginnt dann nicht früher als an dem Tag, auf dem Sie ihn ablegen. Das Datum ist beim Ziehen zu sehen.
- **Das Ende eines Balkens ziehen**, um seine Dauer zu ändern. Die Arbeitstage sind beim Ziehen zu sehen.
- **Zwei Vorgänge verknüpfen**: Fahren Sie über einen Balken und ziehen Sie vom kleinen Kreis an einem seiner Enden auf einen anderen Balken. Die verbundenen Enden ergeben den Typ: vom Ende zum Anfang des anderen ist Ende-Anfang (FS), Anfang zu Anfang SS, Ende zu Ende FF, Anfang zu Ende SF.
- **Auf einen Pfeil klicken**, um Typ und Zeitabstand der Verknüpfung zu ändern (in Arbeitstagen; negativ ist eine Überlappung) oder sie zu entfernen.

Jede Änderung ist ein einzelner Schritt zum Rückgängigmachen.

## Ein Vorgang in den Eigenschaften

![Ein Vorgang in den Eigenschaften](shots/planning-task.webp)

Wählen Sie einen Vorgang, und die Eigenschaften zeigen ihn: seinen Namen, seine IFC-Art, seine Dauer, ob er ein Meilenstein ist, seine Daten und:

- **Einschränkung**: **So früh wie möglich** (Standard), **Anfang nicht früher als** ein Datum oder **Muss anfangen am** ein Datum. Ein Vorgang, der beginnen muss, bevor seine Vorgänger enden, nimmt diesen Puffer weg. Ihr Puffer wird dann negativ, und sie werden kritisch.
- **Nassarbeiten**: Beton, Estriche, Putz. Ein nasser Vorgang arbeitet nicht in der Winterpause des Kalenders, wenn die Pause eingeschaltet ist.
- **Gesamtpuffer** und **Freier Puffer**: um wie viele Arbeitstage sich der Vorgang verschieben kann, ohne das Ende zu verschieben, bzw. ohne zu verschieben, was ihm folgt. **Spätester Anfang** und **Spätestes Ende** sind die spätesten Daten, die das Ende halten.
- **Vorgänger**: jede eingehende Verknüpfung, mit Typ und Zeitabstand zum Ändern oder zum Entfernen. **Nachfolger** listet auf, was dem Vorgang folgt.

## Der kritische Pfad

Lapin berechnet den Terminplan nach jeder Änderung. Eine Vorwärtsrechnung findet die frühesten Daten und eine Rückwärtsrechnung die spätesten. Die Vorgänge ohne Puffer bilden den kritischen Pfad, und eine Verzögerung eines davon verschiebt das Ende. Eine Verknüpfung zu einem Sammelvorgang hält alle Vorgänge darunter. Eine Verknüpfung von einem Sammelvorgang beginnt am Anfang seines ersten Vorgangs und endet am Ende seines letzten. Lapin lehnt eine Verknüpfung ab, die eine Schleife ergäbe.

## Die Elemente des Modells in den Vorgängen

![Der Vorgang Mauerwerk gewählt: seine Wände in 3D gewählt dargestellt](shots/planning-links.webp)

Ein Vorgang nimmt Elemente des Modells, so weiß der Bauzeitenplan, was wann gebaut wird. Wähle einen Vorgang, und seine Elemente erscheinen in 3D und in den Grundrissen gewählt; ein Doppelklick auf seinen Balken wählt sie und passt sie ein. Wähle Elemente in einer Ansicht, und die Zeilen ihrer Vorgänge werden im Gantt markiert. Die Spalte **Elemente** zählt die Elemente jedes Vorgangs (bei einem Sammelvorgang mit denen der Vorgänge darunter).

### Eine Regel

![Die Elemente eines Vorgangs in den Eigenschaften](shots/planning-task-elements.webp)

Unter **Elemente** in den Eigenschaften setze **Per Regel finden** und wähle, was der Vorgang nimmt, wie beim Filter einer Liste: **Arten**, **Geschosse**, **Typen**, **Materialien**, **Gruppen** und **Bauphasen**, jeder Wert mit der Zahl der Elemente, die ihn haben. Jede gesetzte Bedingung muss gelten; eine leere lässt alles durch. Gespeichert wird die Regel, nicht die Elemente: Eine morgen im Erdgeschoss gezeichnete Wand kommt von selbst in den Vorgang „Mauerwerk EG“. **Regel aus der Auswahl** macht eine Regel aus den Arten und Geschossen der gewählten Elemente.

### Ein Teil der Elemente

**Teil** sagt, was der Vorgang von jedem Element nimmt: die **ganzen Elemente**, ihren **Kern** (die tragende Schicht), ihre **Beläge** (alle Schichten außer dem Kern), die Schichten eines Materials oder die einer Materialkategorie (Belag, Dämmung…). So wird eine geschichtete Wand in einem Vorgang Mauerwerk (ihr Kern) gemauert und in einem Vorgang Putz (ihre Beläge) verputzt. Ein Element ohne Schichten ist sein eigener Kern und hat keine Beläge.

### Von Hand

![Die Karte der Auswahl über dem 3D, die zwei Vorgänge einer Wand im Gantt markiert](shots/planning-element-tasks.webp)

Solange das Gantt offen ist, sagt eine Karte über der Modellansicht, wie viele Elemente gewählt sind und in welchen Vorgängen sie stehen. Ziehe sie an ihrem Griff auf die Zeile eines Vorgangs: Die Elemente kommen in diesen Vorgang. Halte beim Loslassen <kbd>Umschalt</kbd>, um sie herauszunehmen. **Zu Vorgang hinzufügen…** (<kbd>T</kbd> <kbd>A</kbd>, auch im Reiter Terminplanung) wählt den Vorgang aus einer Liste.

![Zu Vorgang hinzufügen](shots/planning-assign.webp)

Was du von Hand hinzufügst oder herausnimmst, sind die **Ausnahmen** des Vorgangs: Die Regel bleibt, wie sie ist. Die Eigenschaften zählen sie, und das × daneben vergisst sie. Die Eigenschaften eines Elements listen seine Vorgänge unter **Bauzeitenplan**, jeden mit einem ×, um es herauszunehmen. Jede Änderung ist ein Schritt zum Rückgängigmachen.

### Elemente ohne Vorgang

Sobald ein Vorgang Elemente nimmt, sagt die Leiste des Gantt, wie viele Elemente keinen Vorgang haben oder Schichten, die kein Vorgang nimmt; ein Klick wählt sie, bereit, auf einen Vorgang gezogen zu werden. **Terminplanung → Elemente ohne Vorgang** tut dasselbe. **Verwalten → Prüfung** listet sie ebenfalls, unter *Elements with a task*: eine Warnung je Element, ein Klick wählt es. Bestehende Elemente, die bleiben, brauchen keinen Vorgang.

### In IFC und MS Project

Der IFC-Export schreibt, was jeder Vorgang baut, als IfcRelAssignsToProduct (das Element mit den Vorgängen, die es bauen), und worauf ein Abbruch-, Entfernungs- oder Rückbauvorgang wirkt, als IfcRelAssignsToProcess. Regel, Teil und Ausnahmen reisen in `Lapin_Task`, so liest Lapin die Regel zurück; eine Datei aus einem anderen Programm ergibt Vorgänge mit den Elementen, die sie ihnen zuweist. MS Project XML trägt sie im Feld Text1 mit dem Namen *Lapin elements*. Die Arbeitsmappe hat die Spalten **Elements** und **Rule**.

## Die 4D-Simulation

![Das Haus an einem Tag seines Bauzeitenplans: die Decke des Obergeschosses in Arbeit in ihrer Schalung, die Wände darüber nicht begonnen](shots/planning-simulation.webp)

**Terminplanung → Simulieren** (auch **Simulieren** in der Werkzeugleiste des Gantt) zeigt das Modell, wie der Bauzeitenplan es an einem Tag hat. Das Gantt öffnet sich unter der Ansicht, wenn es nicht offen ist, mit dem Player oben und dem Tag als Band über dem Diagramm:

- was **nicht begonnen** ist, ist ausgeblendet (oder als blasser Geist gezeigt, in den Einstellungen);
- was **in Arbeit** ist, wird in seiner Farbe gezeichnet (Bernstein);
- was **gebaut** ist, wird gezeichnet, wie es ist;
- was **im Abbruch** ist, ist rot, solange sein Vorgang läuft, und dann verschwunden;
- **Schalung und Gerüste** stehen, in ihrer Farbe (Blau), nur zwischen den Tagen, die ihnen die Vorgänge ihrer Bauteile geben: die Schalung einer Decke vom Beginn des Betonierens bis zum Ausschalen, ein Gerüst vom ersten Vorgang an seinen Wänden bis zum letzten.

Eine mehrschichtige Wand, in einem Vorgang gemauert und in einem anderen verputzt, wächst in zwei Schritten: erst ihr Kern, dann der Putz. Elemente, die kein Vorgang nimmt, bleiben, wie sie sind. Eine Karte über der Modellansicht nennt den Tag, was jede Farbe bedeutet und wie viele Elemente in jedem Zustand sind.

### Abspielen

![Der Player oben im Gantt](shots/planning-simulation-bar.webp)

- **Abspielen** (▶) lässt die Tage mit der gewählten Geschwindigkeit (1 bis 60 Tage pro Sekunde) bis zum Ende laufen; **Pause** hält an.
- Die Pfeile auf beiden Seiten gehen **einen Tag** oder **eine Woche** zurück oder vor.
- Ziehe den Schieberegler, tippe ein Datum, oder klicke und ziehe auf der Zeitskala des Gantt: das Band über dem Diagramm und das Modell folgen.
- Neben dem Datum: die Vorgänge, die an dem Tag in Arbeit sind.

Von einem Tag zum nächsten ändern sich nur Farben und Sichtbarkeit der Elemente: nichts wird neu gebaut, darum läuft ein großes Modell so flüssig wie ein kleines.

### Ihre Farben

![Die Simulationseinstellungen](shots/planning-simulation-settings.webp)

Das Zahnrad öffnet die Simulationseinstellungen, im Projekt gespeichert: die Farben dessen, was in Arbeit ist, im Abbruch, der Schalung und Gerüste und der Geister; ob **Nicht begonnenes** ausgeblendet oder **als Geister** gezeigt wird; ob die **Elemente ohne Vorgang** gezeigt oder ausgeblendet werden; und ob **getrennt gebaute Schichten** getrennt erscheinen. **Standardfarben** bringt die von Lapin zurück.

### Stand am Datum in Ansichten und Plänen

![Der Stand am Datum eines Grundrisses in seinen Eigenschaften](shots/planning-view-date.webp)

Ein Grundriss, ein Schnitt, eine Ansicht oder eine gespeicherte 3D-Ansicht hat in den Eigenschaften **Stand am Datum**: Die Ansicht zeichnet dann das Modell, wie der Bauzeitenplan es an dem Tag hat, in denselben Farben, und ihre Pläne drucken es so (das PDF zeigt, was der Bildschirm zeigt). Läuft die Simulation, übernimmt **das Datum des Cursors** ihren Tag. Eine Ansicht an einem Datum zeichnet die Zustände des Bauzeitenplans, nicht die ihrer Phase. Das × neben dem Datum bringt das Modell zurück, wie es ist. Eine 3D-Ansicht mit einem Datum zeigt diesen Tag auch bei ausgeschalteter Simulation.

### Der Bauabschnittsplan

![Bauabschnittsplan: die Daten der Kapitelenden](shots/planning-stages-dialog.webp)

**Terminplanung → Bauabschnittsplan…** (auch im Player) stellt das Modell an mehreren Daten nebeneinander auf einen neuen Plan. Gezeichnet wird aus **der 3D-Ansicht, wie sie jetzt aussieht** (ein Bild je Datum) oder aus einem Grundriss, einem Schnitt oder einer Ansicht, die die Engine an jedem Datum zeichnet. Die Daten sind **das Ende jedes Kapitels** (jeder Vorgang oben im PSP), **das Ende jedes Monats** oder **gewählte**. Jedes Bild trägt sein Datum und die Vorgänge in Arbeit.

![Ein Bauabschnittsplan](shots/planning-stages-sheet.webp)

### Das Gantt auf einem Plan

![Das Gantt auf einem Plan](shots/planning-gantt-sheet.webp)

**Terminplanung → Gantt auf Plan** setzt den Bauzeitenplan auf einen neuen Plan: die Tabelle (Zeile, PSP und Vorgang, Tage, Anfang, Ende) und die Balken auf einer Zeitskala, die kritischen Vorgänge rot, die Sammelvorgänge als Klammern, die Meilensteine als Rauten, die Verknüpfungen. Ein Bauzeitenplan, der höher ist als der Plan, kommt auf so viele Pläne wie nötig, mit seiner Kopfzeile auf jedem. Er ist als Vektoren gezeichnet, wie die Ansichten, und wird im PDF gedruckt.

### Video exportieren

![Video exportieren](shots/planning-video.webp)

**Terminplanung → Video exportieren…** (auch im Player) nimmt die 3D-Ansicht auf, von wo sie jetzt schaut, von einem Tag zum anderen, ein Bild je **ein Tag** oder **eine Woche**, mit dem Datum und den Vorgängen in Arbeit darunter. Die Datei ist ein WebM-Video, oder MP4, wo das System nur das anbietet (die macOS-App). Ein Browser, der kein Video aufnimmt, speichert stattdessen die Bilder als PNG-Dateien in einem ZIP.

## Der Fortschritt

![Das Gantt mit Fortschritt: die Spalte %, der erledigte Teil dunkler in jedem Balken, der Basisplan darunter und die Abweichung](shots/planning-progress.webp)

Während die Arbeiten laufen, sagt der Terminplan, wie weit sie sind, und verschiebt, was bleibt:

- Das **Statusdatum** in der Werkzeugleiste des Gantt ist der Tag, bis zu dem der Fortschritt gemeldet ist. Die restliche Arbeit beginnt am Arbeitstag danach: ein nicht begonnener Vorgang kann nicht früher beginnen, ein laufender erledigt ab dann, was ihm bleibt. Das Diagramm zeichnet es als blaue Linie.
- Das **% abgeschlossen** eines Vorgangs wird in seiner Spalte oder in den Eigenschaften eingegeben. Ohne Eingabe kommt es aus seinen auf der Baustelle markierten Elementen: die erledigte Menge von der Menge des Vorgangs (in seiner Einheit, wenn der Generator ihm eine gab), sonst ihre Anzahl. Das eines Sammelvorgangs kommt aus seinen Vorgängen, nach ihrer Dauer gewichtet.
- **Tatsächlicher Anfang** und **tatsächliches Ende** werden in den Eigenschaften eingegeben. Leer kommen sie aus den Markierungen: der erste Tag, an dem ein Element markiert wurde, und der Tag des letzten Elements, sobald alle fertig sind. Ein abgeschlossener Vorgang steht auf seinen tatsächlichen Terminen, außerhalb des kritischen Pfads.
- Ein laufender Vorgang behält seinen tatsächlichen Anfang und erledigt den noch offenen Teil seiner Dauer (aufgerundet) nach dem Statusdatum; die Vorgänge danach verschieben sich mit ihm.

Die Balken zeigen den erledigten Teil dunkler. Die Eigenschaften sagen, woher ein Prozentsatz kommt und wie viele Arbeitstage bleiben.

![Der Fortschritt eines Vorgangs in den Eigenschaften](shots/planning-progress-task.webp)

### Elemente auf der Baustelle markieren

**Terminplanung → Als fertig markieren** (MD) und **Als begonnen markieren** (MS) markieren die in 3D oder in einem Grundriss gewählten Elemente zum Statusdatum (heute, wenn keines gesetzt ist), in jedem Vorgang, der sie enthält; **Als nicht begonnen markieren** nimmt die Markierungen weg. Die Eigenschaften eines gewählten Elements zeigen seinen Stand auf der Baustelle mit denselben drei Knöpfen und in jedem seiner Vorgänge den Tag, an dem es fertig wurde oder begann, mit einem Knopf, der es nur in diesem Vorgang als fertig markiert. Eine geschichtete Wand in einem Mauerwerks- und einem Putzvorgang wird in beiden markiert, oder in einem über seine Zeile.

![Die Markierungen einer Wand in ihren Eigenschaften](shots/planning-element-marks.webp)

Auf der Baustelle, vom Telefon: tritt der geteilten Sitzung des Projekts im Browser bei (**Datei → Sitzung teilen…** auf dem Rechner, auf dem es offen ist). Ein Gast, der nur zum Ansehen beigetreten ist, kann trotzdem Elemente markieren und den Fortschritt eines Vorgangs eingeben: das Markieren ist die einzige Änderung, die jedem Gast offensteht. Fotos und Berichte zu Vorgängen gehören zur Premium-Edition.

### Der Basisplan

**Terminplanung → Basisplan speichern** behält die Termine und die Dauer jedes Vorgangs, wie sie jetzt sind; erneut gespeichert, wird er ersetzt (ein Basisplan: mehrere Basispläne und Szenarien gehören zur Premium-Edition). Das Gantt zeichnet ihn dann als dünnen grauen Balken unter dem Balken jedes Vorgangs (**Basisplan** in der Werkzeugleiste blendet ihn aus), fügt die Spalte **Abweichung** hinzu (die Arbeitstage, um die sich das Ende verschoben hat: positiv ist verspätet, die Zeile rot markiert) und sagt in seiner Leiste, wie viele Vorgänge verspätet sind und wie weit sich das Ende verschoben hat. Die Eigenschaften geben die Termine des Basisplans und die Abweichungen von Anfang und Ende. **Basisplan löschen** entfernt ihn.

### Verspätet und voraus in der Simulation

![Die Simulation zum Statusdatum: die Fenster hinter dem Basisplan](shots/planning-progress-simulation.webp)

Mit einem Basisplan vergleicht die Simulation: ein Element, das an einem Tag nicht gebaut (oder nicht begonnen) ist, an dem der Basisplan es so hatte, ist **verspätet** (karminrot), eines, das in Arbeit oder gebaut ist, bevor der Basisplan es beginnen ließ, ist **voraus** (grün). Bis zum Statusdatum stehen die Elemente auf ihren tatsächlichen Tagen: ein als fertig markiertes ist an dem Tag gebaut, an dem es markiert wurde. **Mit dem Basisplan verglichen** in den Einstellungen der Simulation schaltet es ab; dort werden auch seine zwei Farben gewählt. Ansichten und Pläne zu einem Datum zeigen dasselbe.

### Der Fortschritt in den Dateien

- **XLSX exportieren** fügt die Spalten **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** und **GlobalId** hinzu (die Arbeitsmappe ist englisch). Fülle sie auf der Baustelle aus und lies sie mit **Terminplanung → Fortschritt importieren…** zurück: die Zeilen werden nach GlobalId, PSP-Code oder ID gefunden, und ein über der Tabelle geschriebenes *Status date* setzt das Statusdatum. Eine CSV geht auch.
- **MS Project XML** trägt das Statusdatum, den Prozentsatz jedes Vorgangs, den tatsächlichen Anfang und das tatsächliche Ende, die Restdauer und den Basisplan (Baseline 0); die Markierungen der Elemente reisen mit den Elementen in *Lapin elements*.
- **IFC** schreibt sie in die IfcTaskTime jedes Vorgangs (die Statuszeit, den tatsächlichen Anfang und das tatsächliche Ende, die Restzeit, den Fertigstellungsgrad) und den Basisplan als zweiten IfcWorkSchedule vom Typ BASELINE; Lapins eigene Felder (der eingegebene Prozentsatz, die Markierungen) stehen in `Lapin_Task`. Beides kommt auf dieselben Termine zurück.
- **Verwalten → Versionen → Vergleichen** sagt neben den Elementen, was sich im Terminplan geändert hat: das Ende, die Arbeitstage, das Statusdatum und die Vorgänge, die hinzukamen, gelöscht oder verschoben wurden (um wie viele Arbeitstage) oder deren Dauer oder Fortschritt sich geändert hat.

## Der Kalender

![Der Arbeitskalender](shots/planning-calendar.webp)

**Terminplanung → Kalender…** legt fest:

- die **Arbeitstage** der Woche (standardmäßig Montag bis Freitag) und die **Stunden** eines Tages (8 ab 08:00);
- die **Feiertage**: die gesetzlichen Feiertage Rumäniens, für jedes Jahr berechnet (einschließlich des orthodoxen Karfreitags, Ostern und Pfingsten), oder keine. Die Liste darunter zeigt die Feiertage eines Jahres zur Kontrolle;
- die **Winterpause** für Nassarbeiten, standardmäßig aus: vom 15. Dezember bis zum 1. März, wenn Sie keine anderen Tage festlegen;
- **Ausnahmen**: freie Tage für das Projekt (eine gesperrte Baustelle) oder gearbeitete Tage, die sonst keine wären (ein Samstag).

Dauern und Zeitabstände zählen Arbeitstage dieses Kalenders.

## Den Terminplan austauschen

- **MS Project exportieren…** schreibt eine MS-Project-XML-Datei. MS Project, ProjectLibre und GanttProject öffnen sie, und Primavera P6 importiert sie. Sie enthält die Vorgänge mit PSP, Dauern, Daten, Einschränkungen und Puffern, die Verknüpfungen mit ihren Typen und Zeitabständen und den Kalender mit jedem Feiertag als Ausnahme. **MS Project importieren…** liest eine solche Datei anstelle des Terminplans des Projekts. Ein von Lapin geschriebener und wieder gelesener Terminplan behält die IDs und Daten seiner Vorgänge.
- **IFC exportieren…** (auch unter **Datei**) schreibt den Terminplan zusammen mit dem Modell in die IFC-Datei: einen IfcWorkSchedule, seine IfcTask mit ihren Zeiten, die IfcRelSequence-Verknüpfungen und den IfcWorkCalendar. Beim Importieren einer IFC-Datei mit Terminplan wird dieser ebenfalls übernommen.
- **XLSX exportieren** schreibt eine Arbeitsmappe: die Tabelle des Gantt und die freien Tage des Kalenders.

Lapins eigene Felder, für die andere Programme keinen Platz haben (die Einschränkung eines Vorgangs, ob er nass ist, die Winterpause), reisen in IFC als die Eigenschaftssätze `Lapin_Task` und `Lapin_Calendar`. MS Project kennt keine nassen Vorgänge, deshalb bleibt die Winterpause in Lapin.

## Was als Nächstes kommt

Die Kostenberechnung kommt in den nächsten Stufen.
