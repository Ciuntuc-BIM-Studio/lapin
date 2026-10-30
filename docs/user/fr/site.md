---
title: Le site et ses données
order: 52
summary: Importez le levé, la parcelle, les voisins et une orthophoto en coordonnées réelles, depuis DXF, CSV, GeoJSON, CityJSON et une image géoréférencée.
---

L’onglet **Site** contient le terrain sur lequel se dresse le bâtiment : le terrain naturel, la parcelle, les bâtiments voisins et une photo aérienne, chacun à son emplacement réel. Le groupe **Importer** les apporte depuis les fichiers que remettent les géomètres et le cadastre.

![L’onglet Site](shots/ribbon-site.webp)

## Coordonnées réelles

Un levé arrive en coordonnées cartographiques : des abscisses (E) et des ordonnées (N), à des centaines de kilomètres de zéro. Lapin garde le modèle près de son origine et le rattache à la carte par le **point topographique**. Le premier import dans un projet s’en charge pour vous : le point topographique va à l’origine du plan, sur les premières coordonnées du fichier arrondies au mètre, et chaque import suivant arrive au même endroit.

**Gérer → Emplacement** indique le **système de coordonnées** dans lequel sont exprimées les abscisses et les ordonnées, ainsi que le point de base et le point topographique. Lapin connaît Stereo 70 (EPSG:3844, le système national de la Roumanie), les fuseaux UTM 34 et 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) et la longitude et la latitude (WGS 84, EPSG:4326). Un fichier dans un autre de ces systèmes est converti à l’import : un GeoJSON en longitude et latitude arrive sur le plan en Stereo 70 à sa place. L’export IFC écrit le système et le point topographique, de sorte que les autres logiciels placent eux aussi le modèle sur la carte.

![Gérer → Emplacement : le système de coordonnées et le point topographique](shots/site-location.webp)

## Importer

- **Terrain à partir de points** lit un fichier CSV ou texte issu d’un levé : x y z, ou numéro, ordonnée, abscisse, altitude, un point par ligne.
- **Points depuis DXF** lit un plan de levé : des entités POINT avec leurs altitudes, des blocs insérés à une altitude ou la portant en attribut, des altitudes écrites à côté des points (« 245.37 » à côté de sa croix), et des courbes de niveau à leur altitude. Quand le dessin a des points sur plusieurs calques, Lapin les liste avec le nombre de points de chacun, et vous choisissez lesquels prendre. Les points forment un terrain.
- **GeoJSON** apporte des parcelles, des lignes et des points. Un fichier avec un seul polygone donne la **parcelle** ; un fichier avec plusieurs donne des zones. La parcelle délimite aussi la parcelle du terrain, avec sa surface. Les fichiers du cadastre en Stereo 70 l’indiquent dans le fichier, et Lapin le lit ; un fichier qui n’indique rien est en longitude et latitude, comme l’exige la norme GeoJSON.
- **CityJSON** apporte les bâtiments voisins avec leurs surfaces, à leur emplacement et à leur hauteur. Ce sont des références en lecture seule ; ils porteront des ombres dans l’étude d’ensoleillement.
- **Orthophoto** pose une photo aérienne sous le site. Choisissez l’image (JPEG ou PNG, jusqu’à 8 MB) avec son fichier de géoréférencement (`.jgw`, `.pgw` ou `.wld`) : ce fichier indique où se trouve l’image. La photo est dessinée sous le plan et habille le terrain en 3D.

![Le plan de masse : l’orthophoto, les courbes de niveau du terrain levé, la parcelle et l’emprise du voisin](shots/site-plan.webp)

![Le même site en 3D : la photo au sol, le voisin à sa hauteur](shots/site-3d.webp)

Tout ce qui est importé est conservé dans le fichier du projet. Un objet prend son nom du fichier (le numéro cadastral d’une parcelle, quand le fichier en a un) et garde avec lui les autres propriétés du fichier ; sélectionnez-le pour voir sa surface ou sa longueur.

## Plans de masse et plans de situation

L’arborescence du projet a un **Site** sous les niveaux : les dessins propres au site, qui n’appartiennent à aucun niveau. Un nouveau projet commence avec deux :

- le **Plan de masse** (en roumain *plan de situație*), au 1:500 : tout le modèle vu de dessus, tous les niveaux à la fois, toitures comprises, avec le terrain, les allées, la végétation, la parcelle et les voisins. Ce qui se trouve entièrement sous ±0.00 (les fondations) est omis, rien n’est coupé et rien de caché n’est dessiné. Les sommets de la parcelle sont numérotés ;
- le **Plan de situation** (*plan de încadrare în zonă*), au 1:2000 : le même dessin pour le site dans son quartier, sur l’orthophoto, sans les numéros des sommets ; les bâtiments du projet sont remplis en rouge et la parcelle est contournée en rouge, pour que le site se détache de ses voisins.

**+** à côté de Site en crée un autre de l’un ou l’autre type ; les boutons de copie en dupliquent un. Les deux portent une **flèche du nord** orientée vers le nord géographique (Gérer → Emplacement : l’angle avec le nord géographique), une **échelle graphique** et, quand le projet est rattaché à la carte, le **quadrillage de coordonnées** : des croix là où se croisent des abscisses et des ordonnées rondes, tous les dixièmes de l’échelle (50 m au 1:500, 200 m au 1:2000), avec leurs valeurs. Sélectionnez la vue dans l’arborescence et réglez-les dans ses propriétés, sous **Paramètres de vue** : ce qu’elle est (plan de masse ou plan de situation), le pas du quadrillage (0 : aucun), la flèche, l’échelle graphique, et **Mettre le projet en évidence** (le remplissage et le contour rouges, activé par défaut dans un plan de situation, et pour un plan de masse aussi quand vous le souhaitez).

**La carte en dessous.** L’orthophoto est dessinée sous chaque plan de masse et chaque plan de situation, quel que soit le niveau sur lequel elle a été importée, et s’imprime avec eux sur les feuilles et dans le PDF. Pour le plan cadastral ou le dessin du géomètre, ouvrez le plan de masse et utilisez **Import DXF…** avec **Placed: by the map's coordinates**. Lapin le propose de lui-même quand les coordonnées du fichier ressemblent à celles d’une carte, par exemple en Stereo 70. Le dessin arrive alors là où l’indiquent ses abscisses et ses ordonnées, par le point topographique du projet et son angle avec le nord géographique. Si le projet n’est pas encore rattaché à la carte, le dessin le rattache. Cochez **As a link** pour le garder en lecture seule et le recharger quand le géomètre en envoie un nouveau. Importez-le une fois dans chaque dessin qui en a besoin.

![Un plan de situation : la maison remplie en rouge, la parcelle contournée en rouge, parmi ses voisins](shots/site-location-plan.webp)

![Un plan de masse : la maison vue de dessus sur sa parcelle, les sommets numérotés, le quadrillage de la carte, la flèche du nord et l’échelle graphique](shots/site-drawings.webp)

![Le Site dans l’arborescence du projet](shots/site-browser.webp)

La nomenclature **Coordonnées de la parcelle** (Nomenclatures → + → Coordonnées de la parcelle) est l’inventaire des coordonnées de la parcelle : chaque sommet tel que le plan de masse le numérote, son ordonnée et son abscisse dans le système du projet, le côté jusqu’au sommet suivant, et la surface. Comme toute nomenclature, elle se place sur une feuille à côté du plan de masse, et s’exporte en CSV ou en XLSX.

## Règles d’urbanisme et soleil (Lapin Urban)

L’édition **Lapin Urban** ajoute un groupe **Planning** à cet onglet : les règles d’urbanisme de la parcelle (POT, CUT, les hauteurs maximales, les reculs par rapport à la rue, aux limites latérales, au fond de parcelle et à l’axe de la voie), le **volume constructible** qu’elles laissent, les indicateurs que le modèle atteint par rapport à elles, et l’**étude d’ensoleillement** : le soleil et ses ombres en 3D pour n’importe quel jour et n’importe quelle heure à l’emplacement du projet, et des plans d’ombres pour l’équinoxe et les solstices.
