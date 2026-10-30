---
title: Das Gelände und seine Daten
order: 52
summary: Bringen Sie die Vermessung, das Grundstück, die Nachbarn und ein Orthofoto in realen Koordinaten ein, aus DXF, CSV, GeoJSON, CityJSON und einem georeferenzierten Bild.
---

Die Registerkarte **Gelände** enthält den Boden, auf dem das Gebäude steht: das Gelände, das Grundstück, die Nachbargebäude und ein Luftbild, jedes dort, wo es wirklich ist. Die Gruppe **Importieren** holt sie aus den Dateien, die Vermesser und das Katasteramt übergeben.

![Die Registerkarte Gelände](shots/ribbon-site.webp)

## Reale Koordinaten

Eine Vermessung kommt in Kartenkoordinaten: Rechts- und Hochwerte, Hunderte von Kilometern vom Nullpunkt entfernt. Lapin hält das Modell nahe seinem Ursprung und bindet es über den **Vermessungspunkt** an die Karte. Der erste Import in ein Projekt erledigt das für Sie: Der Vermessungspunkt kommt auf den Ursprung des Grundrisses, auf die ersten Koordinaten der Datei, auf den Meter gerundet, und jeder spätere Import landet an derselben Stelle.

**Verwalten → Standort** zeigt das **Koordinatensystem**, in dem die Rechts- und Hochwerte angegeben sind, sowie den Projektbasispunkt und den Vermessungspunkt. Lapin kennt Stereo 70 (EPSG:3844, das Landeskoordinatensystem Rumäniens), die UTM-Zonen 34 und 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) sowie geografische Länge und Breite (WGS 84, EPSG:4326). Eine Datei in einem anderen dieser Systeme wird beim Einlesen umgerechnet: Ein GeoJSON in Länge und Breite landet auf dem Stereo-70-Grundriss dort, wo es hingehört. Der IFC-Export schreibt das System und den Vermessungspunkt, sodass auch andere Programme das Modell auf der Karte platzieren.

![Verwalten → Standort: das Koordinatensystem und der Vermessungspunkt](shots/site-location.webp)

## Importieren

- **Gelände aus Punkten** liest eine CSV- oder Textdatei einer Vermessung: x y z, oder Nummer Hochwert Rechtswert Höhe, ein Punkt pro Zeile.
- **Punkte aus DXF** liest eine Vermessungszeichnung: POINT-Entitäten mit ihren Höhen, Blöcke, die auf einer Höhe eingefügt sind oder sie als Attribut tragen, neben Punkte geschriebene Höhen („245.37“ neben seinem Kreuz) und Höhenlinien auf ihrer Höhe. Hat die Zeichnung Punkte auf mehreren Layern, listet Lapin sie mit der Anzahl ihrer Punkte auf, und Sie wählen, welche übernommen werden. Die Punkte ergeben ein Gelände.
- **GeoJSON** bringt Grundstücke, Linien und Punkte. Eine Datei mit einem Polygon ergibt das **Grundstück**; eine mit mehreren ergibt Flächen. Das Grundstück umreißt auch die Grundstücksfläche des Geländes, mit ihrem Flächeninhalt. Dateien des Katasteramts in Stereo 70 geben das in der Datei an, und Lapin liest es; eine Datei, die nichts angibt, ist in Länge und Breite, wie es der GeoJSON-Standard verlangt.
- **CityJSON** bringt die Nachbargebäude mit ihren Oberflächen, an ihrem Ort und in ihrer Höhe. Sie sind schreibgeschützte Referenzen; in der Sonnenstudie werfen sie Schatten.
- **Orthofoto** legt ein Luftbild unter das Gelände. Wählen Sie das Bild (JPEG oder PNG, bis 8 MB) zusammen mit seinem World File (`.jgw`, `.pgw` oder `.wld`): Das World File gibt an, wo das Bild liegt. Das Foto wird unter dem Grundriss gezeichnet und überzieht in 3D das Gelände.

![Der Lageplan: das Orthofoto, die Höhenlinien des vermessenen Geländes, das Grundstück und der Fußabdruck des Nachbarn](shots/site-plan.webp)

![Dasselbe Gelände in 3D: das Foto auf dem Boden, der Nachbar in seiner Höhe](shots/site-3d.webp)

Alles Importierte wird in der Projektdatei gespeichert. Ein Objekt erhält seinen Namen aus der Datei (die Katasternummer eines Grundstücks, wenn die Datei eine hat) und behält die übrigen Eigenschaften der Datei bei sich; wählen Sie es aus, um seine Fläche oder Länge zu sehen.

## Lagepläne und Übersichtslagepläne

Der Projektbrowser hat unter den Ebenen einen Eintrag **Gelände**: die eigenen Zeichnungen des Geländes, die zu keinem Geschoss gehören. Ein neues Projekt beginnt mit zweien:

- dem **Lageplan** (rumänisch *plan de situație*), im Maßstab 1:500: das ganze Modell von oben gesehen, alle Geschosse auf einmal, samt Dächern, mit dem Gelände, den Wegen, den Pflanzen, dem Grundstück und den Nachbarn. Was ganz unter ±0.00 liegt (die Gründung), wird ausgelassen, nichts wird geschnitten und nichts Verdecktes gezeichnet. Die Ecken des Grundstücks werden nummeriert;
- dem **Übersichtslageplan** (*plan de încadrare în zonă*), im Maßstab 1:2000: dieselbe Zeichnung für das Grundstück in seiner Umgebung, über dem Orthofoto, ohne die Nummern der Ecken; die Gebäude des Projekts werden rot gefüllt und das Grundstück rot umrandet, sodass sich das Grundstück von seinen Nachbarn abhebt.

**+** neben Gelände erstellt einen weiteren der beiden; die Kopierschaltflächen duplizieren einen. Beide tragen einen **Nordpfeil**, der nach geografisch Nord zeigt (Verwalten → Standort: der Winkel zu geografisch Nord), eine **Maßstabsleiste** und, wenn das Projekt an die Karte gebunden ist, das **Koordinatengitter**: Kreuze, wo sich runde Rechts- und Hochwerte treffen, alle ein Zehntel des Maßstabs (50 m bei 1:500, 200 m bei 1:2000), mit ihren Werten. Wählen Sie die Ansicht im Browser aus und stellen Sie sie in ihren Eigenschaften unter **Ansichtseinstellungen** ein: was sie ist (Lageplan oder Übersichtslageplan), den Abstand des Gitters (0: keines), den Pfeil, die Leiste und **Projekt hervorheben** (die rote Füllung und Umrandung, in einem Übersichtslageplan standardmäßig an, auch für einen Lageplan, wenn Sie es wünschen).

**Die Karte darunter.** Das Orthofoto wird unter jedem Lageplan und Übersichtslageplan gezeichnet, gleich auf welchem Geschoss es importiert wurde, und wird mit ihnen auf Plänen und im PDF gedruckt. Für den Katasterplan oder die Zeichnung des Vermessers öffnen Sie den Lageplan und verwenden **DXF importieren…** mit **Platziert: nach den Kartenkoordinaten**. Lapin schlägt dies selbst vor, wenn die Koordinaten der Datei wie die einer Karte aussehen, zum Beispiel Stereo 70. Die Zeichnung landet dann dort, wo ihre Rechts- und Hochwerte es angeben, über den Vermessungspunkt des Projekts und seinen Winkel zu geografisch Nord. Ist das Projekt noch nicht an die Karte gebunden, bindet die Zeichnung es. Aktivieren Sie **Als Verknüpfung**, um sie schreibgeschützt zu halten und neu zu laden, wenn der Vermesser eine neue schickt. Importieren Sie sie einmal in jede Zeichnung, die sie braucht.

![Ein Übersichtslageplan: das Haus rot gefüllt, das Grundstück rot umrandet, inmitten seiner Nachbarn](shots/site-location-plan.webp)

![Ein Lageplan: das Haus von oben auf seinem Grundstück, die Ecken nummeriert, das Gitter der Karte, der Nordpfeil und die Maßstabsleiste](shots/site-drawings.webp)

![Das Gelände im Projektbrowser](shots/site-browser.webp)

Die Bauteilliste **Grundstückskoordinaten** (Bauteillisten → + → Grundstückskoordinaten) ist das Koordinatenverzeichnis des Grundstücks: jede Ecke so, wie der Lageplan sie nummeriert, ihr Hoch- und Rechtswert im System des Projekts, die Seite zur nächsten Ecke und die Fläche. Wie jede Bauteilliste kommt sie auf einen Plan neben den Lageplan und lässt sich als CSV oder XLSX ausgeben.

## Bauleitplanung und Sonne (Lapin Urban)

Die Edition **Lapin Urban** fügt dieser Registerkarte eine Gruppe **Stadtplanung** hinzu: die Bebauungsvorschriften des Grundstücks (POT, CUT, die maximalen Höhen, die Abstände zur Straße, zu den Seiten, nach hinten und zur Straßenachse), das **zulässige Bauvolumen**, das sie übrig lassen, die Kennwerte, die das Modell im Vergleich dazu erreicht, und die **Besonnungsstudie**: die Sonne und ihre Schatten in 3D für jeden Tag und jede Stunde am Ort des Projekts, sowie Schattenpläne für die Tagundnachtgleiche und die Sonnenwenden.
