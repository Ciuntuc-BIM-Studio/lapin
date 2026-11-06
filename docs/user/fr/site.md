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

## Routes, courbes et tracés CAO

Une **route** ou une **allée** suit son axe. Dans **Corridor…** (CR), l’onglet **Courbes** donne à chaque sommet de l’axe un rayon avec une clothoïde d’entrée et de sortie, comme un projeteur routier trace une courbe ; un sommet laissé à 0 reste un angle, et les courbes qui ne tiennent pas entre leurs voisines sont refusées, avec la raison. Les abscisses, le profil en long et les profils en travers suivent l’axe courbe, et l’IFC 4.3 l’écrit comme un alignement.

Les tracés CAO deviennent des éléments du site : **Route depuis lignes CAO** (LR) et **Ligne de rupture depuis lignes CAO** (BL) prennent une ligne ou une polyligne dessinée ou venue d’un DXF, suivent toutes les lignes de son calque qui s’y raccordent bout à bout, et en font une route le long d’elle ou une ligne de rupture du terrain. Quand les lignes portent des altitudes (les lignes 3D d’un DXF), elles donnent le profil en long de la route ou les altitudes de la ligne de rupture. Ces lignes gardent leurs altitudes et peuvent s’afficher en 3D (la catégorie *tracés*).

## Plans de masse et plans de situation

L’arborescence du projet a un **Site** sous les niveaux : les dessins propres au site, qui n’appartiennent à aucun niveau. Un nouveau projet commence avec deux :

- le **Plan de masse** (en roumain *plan de situație*), au 1:500 : tout le modèle vu de dessus, tous les niveaux à la fois, toitures comprises, avec le terrain, les allées, la végétation, la parcelle et les voisins. Ce qui se trouve entièrement sous ±0.00 (les fondations) est omis, rien n’est coupé et rien de caché n’est dessiné. Les sommets de la parcelle sont numérotés ;
- le **Plan de situation** (*plan de încadrare în zonă*), au 1:2000 : le même dessin pour le site dans son quartier, sur l’orthophoto, sans les numéros des sommets ; les bâtiments du projet sont remplis en rouge et la parcelle est contournée en rouge, pour que le site se détache de ses voisins.

**+** à côté de Site en crée un autre de l’un ou l’autre type ; les boutons de copie en dupliquent un. Les deux portent une **flèche du nord** orientée vers le nord géographique (Gérer → Emplacement : l’angle avec le nord géographique), une **échelle graphique** et, quand le projet est rattaché à la carte, le **quadrillage de coordonnées** : des croix là où se croisent des abscisses et des ordonnées rondes, tous les dixièmes de l’échelle (50 m au 1:500, 200 m au 1:2000), avec leurs valeurs. Sélectionnez la vue dans l’arborescence et réglez-les dans ses propriétés, sous **Paramètres de vue** : ce qu’elle est (plan de masse ou plan de situation), le pas du quadrillage (0 : aucun), la flèche, l’échelle graphique, et **Mettre le projet en évidence** (le remplissage et le contour rouges, activé par défaut dans un plan de situation, et pour un plan de masse aussi quand vous le souhaitez).

**La carte en dessous.** L’orthophoto est dessinée sous chaque plan de masse et chaque plan de situation, quel que soit le niveau sur lequel elle a été importée, et s’imprime avec eux sur les feuilles et dans le PDF. Pour le plan cadastral ou le dessin du géomètre, ouvrez le plan de masse et utilisez **Importer DXF…** avec **Placé : par les coordonnées de la carte**. Lapin le propose de lui-même quand les coordonnées du fichier ressemblent à celles d’une carte, par exemple en Stereo 70. Le dessin arrive alors là où l’indiquent ses abscisses et ses ordonnées, par le point topographique du projet et son angle avec le nord géographique. Si le projet n’est pas encore rattaché à la carte, le dessin le rattache. Cochez **Comme lien** pour le garder en lecture seule et le recharger quand le géomètre en envoie un nouveau. Importez-le une fois dans chaque dessin qui en a besoin.

![Un plan de situation : la maison remplie en rouge, la parcelle contournée en rouge, parmi ses voisins](shots/site-location-plan.webp)

![Un plan de masse : la maison vue de dessus sur sa parcelle, les sommets numérotés, le quadrillage de la carte, la flèche du nord et l’échelle graphique](shots/site-drawings.webp)

![Le Site dans l’arborescence du projet](shots/site-browser.webp)

La nomenclature **Coordonnées de la parcelle** (Nomenclatures → + → Coordonnées de la parcelle) est l’inventaire des coordonnées de la parcelle : chaque sommet tel que le plan de masse le numérote, son ordonnée et son abscisse dans le système du projet, le côté jusqu’au sommet suivant, et la surface. Comme toute nomenclature, elle se place sur une feuille à côté du plan de masse, et s’exporte en CSV ou en XLSX.

## Arbres et arbustes

**Plante** (PN) pose une plante d’un clic, **Zone de plantation** (PZ) sème un contour d’un mélange d’espèces à une densité ; sélectionnez-les et choisissez leurs espèces dans les propriétés. Les espèces viennent d’une bibliothèque de plantes indigènes ou traditionnelles en Roumanie, par groupes : **arbres fruitiers** (pommier, poirier, prunier, cerisier, griottier, abricotier, cognassier), **conifères** (sapin pectiné, épicéa, pin sylvestre, pin noir), **feuillus** (noyer, tilleul, bouleau, érable), **arbustes** (noisetier, cornouiller mâle, sureau, églantier, lilas, argousier, groseillier, thuya, buis) et **couvre-sol**. Chacune a son nom latin, sa hauteur et son houppier habituels. Le tableau de plantation montre les noms latin et roumain à côté de chaque espèce.

En plan chaque plante est dessinée vue de dessus d’un trait fin, aussi large que sa couronne, par son seul contour : un noyer ou un tilleul une couronne aux lobes doux, un arbre fruitier un cercle, un sapin ou un épicéa une étoile, un pin un anneau dentelé, un arbuste un petit nuage, une plante de haie taillée un cercle, avec une petite croix au tronc d’un arbre. Les façades et les coupes dessinent chaque plante d’un seul contour : la couronne d’un arbre sur son tronc, le cône étagé d’un conifère, le dôme d’un arbuste. Quelques traits chacune, si bien qu’un plan de masse de centaines d’arbres reste léger. En 3D les conifères sont des cônes, les autres arbres ont une couronne arrondie sur leur tronc et les arbustes un dôme bas.

![Un petit verger en plan : fruitiers, un noyer, des conifères et des arbustes](shots/planting-symbols.webp)

Réglez l’**Entourage en 2D** du style de vue sur **détaillé** pour les symboles d’un plan paysager, en noir : un arbre fruitier en couronne lobée aux branches fourchues, un noyer ou un tilleul en couronne festonnée autour d’un cœur sombre, un sapin ou un épicéa en étoile de rameaux d’aiguilles, un pin en touffe ronde d’aiguilles, un arbuste en petit nuage. Lapin dessine lui-même chaque symbole, si bien que des arbres voisins diffèrent un peu et qu’une zone n’a jamais l’air tamponnée ; au 1:1000 et moins (un plan de situation) seuls les contours des couronnes sont dessinés.

![Le même verger dessiné détaillé](shots/planting-symbols-detailed.webp)

## Personnes et voitures

**Entourage** (EN) pose une personne ou une voiture d’un clic ; choisissez laquelle dans la barre de la ligne de commande tant que l’outil est actif : un **homme debout**, une **femme qui marche**, un **enfant**, une **personne au téléphone**, une **voiture** ou un **SUV / fourgon**. **Rangée d’entourage** (ER) en pose une rangée le long d’une ligne que vous dessinez : des voitures tous les 2,5 m, garées côte à côte en travers, l’avant vers la gauche de la ligne, des personnes tous les 1,2 m, qui marchent le long d’elle. Chacune se pose sur le terrain en dessous, ou sur le sol de son niveau là où il n’y a pas de terrain.

![Des personnes à la porte et des voitures garées à côté de la maison](shots/entourage-3d.webp)

Sélectionnez-en une pour la modifier dans les propriétés : son type, sa **rotation**, sa **hauteur** (par défaut la taille habituelle de son type), sa hauteur **au-dessus du terrain** (sur une terrasse ou un balcon) et la **couleur** de ses vêtements ou de sa carrosserie. Elle se déplace, tourne, se reflète et se copie comme tout élément.

En 3D ce sont des figures simples : des personnes avec leurs membres, vêtements, cheveux et chaussures, des voitures avec leurs vitres, roues et phares. Les dessins les gardent simples, comme un architecte dessine l’entourage, d’un trait fin : les plans et les plans de masse vus de dessus, les épaules et la tête d’une personne, le contour d’une voiture aux angles arrondis et son pare-brise ; les façades et les coupes en silhouettes, un seul contour chacune, la figure d’une personne, le côté ou l’avant d’une voiture selon ce que voit la vue, jamais les lignes de leurs corps.

![En plan](shots/entourage-plan.webp)

![Dans la façade sud](shots/entourage-elevation.webp)

![Les propriétés d’une voiture](shots/entourage-properties.webp)

La façon dont les personnes, les voitures et les plantes se dessinent en 2D est un réglage du style de vue, **Entourage en 2D** (dans les propriétés de la vue ou **Vue → Paramètres de vue** ; pour tout un style, **Gérer → Styles de vue**) : **contours** (par défaut), **abstrait** (une personne un cercle en plan et une tête sur un corps en façade, une voiture une boîte arrondie, un arbre un cercle avec son tronc), **détaillé** (les symboles paysagers des plantes et chaque ligne des corps, avec les ombres solaires de la vue, comme Lapin les dessinait avant) ou **non dessiné**. **Remplissage des silhouettes** remplit les silhouettes des façades et des coupes de gris clair ou de vert pâle. Elles se dessinent avec la plume 2 (0,13 mm au 1:100), sauf si **Gérer → Styles d’objets** en donne une autre à la catégorie **Entourage**, et **Visibilité / Graphismes** masque cette catégorie dans une vue. Les feuilles, le PDF et le DXF les impriment comme la vue les dessine ; leurs corps 3D ne changent jamais. Les projets faits avant les dessinent aussi en contours.

Elles donnent l’échelle aux dessins et aux images, mais ne font pas partie du bâtiment. Un export IFC ou Fragments (File → Export…) les laisse de côté sauf si vous cochez **Entourage** dans sa liste de types ; IFC les écrit en IfcBuildingElementProxy « Entourage ». Les exports 3D (glTF, USD, COLLADA) les prennent. Les tests de conflits les laissent de côté.

Les figures suivent les personnes et les voitures de [BimGo](https://github.com/aussieBIMguru/BimGo) par Aussie BIM Guru (MIT-0) : leurs proportions, attitudes et profils.

## Portails

Le groupe **Portails** de l’onglet Site place des portails avec leurs piliers : un **Portail battant**, un **Portail battant double** (<kbd>GT</kbd>), un **Portail coulissant**, un **Portail coulissant autoportant**, un **Portillon**, un **Portail avec portillon latéral** et un **Portail pliant**. Lapin n’a pas de clôture propre : dessinez la clôture comme un mur (un mur bas, du type voulu), ou laissez les piliers du portail se poser sur la limite de la parcelle.

Cliquez sur les deux extrémités du portail. Avec **Sur les lignes** activé dans la barre (par défaut), le milieu du portail se pose sur l’axe du mur ou la ligne dessinée la plus proche à moins de 1,5 m (un mur, la limite de la parcelle, une ligne de dessin), le long de celle-ci ; désactivé, il se tient entre les deux points. La barre choisit aussi le type posé, parmi les styles du projet ou ceux livrés avec Lapin.

Un portail posé sur un mur le coupe à partir des faces extérieures de ses piliers, sur toute sa hauteur : le mur de clôture s’arrête aux piliers.

![Un portail coulissant et un portillon dans un mur de clôture, un portail double sur la limite de la parcelle](shots/gates-plan.webp)

Les plans dessinent les piliers coupés, les vantaux ouverts selon leur angle d’ouverture avec leur débattement, les panneaux d’un portail pliant repliés ; un portail coulissant fermé, avec une flèche dans le sens d’ouverture, son rail et la zone où il se range en tirets, aussi longue que son vantail (le vantail d’un portail autoportant dépasse l’ouverture de sa queue de contrepoids, environ 45 %). Les façades, les coupes et la 3D dessinent les corps.

![Les portails en 3D](shots/gates-3d.webp)

### Le configurateur

**Types de portails** ouvre le configurateur : les styles du projet, et ceux livrés avec Lapin, à ajouter d’un clic (un portail double en fer forgé, un coulissant à lames horizontales, un pliant en acier, un portillon cintré, un portail d’entrée en bois, un portail à grillage de sécurité, un autoportant automatique, un portail double avec portillon, une barrière de ferme, un coulissant en tôle perforée, un portillon de jardin en bois). Un type définit, par groupes, seulement ce que son genre admet :

- **Portail :** la largeur libre et la hauteur, le jeu au sol, la largeur d’un portillon, les panneaux de chaque côté d’un portail pliant, l’angle d’ouverture ;
- **Vantaux :** la taille du cadre, le remplissage (barreaux verticaux, lames horizontales, un panneau plein, grillage ou tôle perforée), l’espacement des barreaux ou des lames, des pointes de lance sur les barreaux, le couronnement (droit, cintré ou en pointe) et sa flèche ;
- **Piliers :** maçonnés, poteaux en acier ou aucun, leur taille et leur hauteur, le chapeau d’un pilier maçonné (plat ou pyramidal) ;
- **Quincaillerie :** une motorisation (un bras sur les vantaux d’un battant, un boîtier à côté du rail d’un coulissant) et poignée et serrure ;
- **Matériaux** par rôle : cadre, remplissage, piliers, chapeaux, quincaillerie et moteur. Un matériau nommé ici que le projet n’a pas est créé.

L’aperçu est celui du moteur : le plan avec son débattement ou sa zone de rangement en haut, l’élévation en bas. Tous les portails d’un type suivent une modification.

![Le configurateur de portails avec un portail double en fer forgé](shots/gates-types.webp)

Sélectionnez un portail pour changer son type, sa rotation, sa hauteur au-dessus du terrain, le côté vers lequel ses vantaux **s’ouvrent**, la main de ses gonds (ou le côté où se range un coulissant), et de combien il est dessiné ouvert (0 : fermé). **Autre côté** et **Autre main** dans ses propriétés, ou les flèches à côté de lui en plan, les inversent. **Modifier → Symétrie** lui donne l’autre main.

![Les propriétés d’un portail](shots/gates-properties.webp)

### Nomenclature, coûts et IFC

La **Nomenclature des portails** liste les portails par type : genre, dimension libre, vantaux, remplissage, couronnement, piliers, motorisation et nombre. Un nouveau type porte les articles de coût `LAP-GATE` et, avec une motorisation, `LAP-GATEMOT`, comptés par portail.

L’IFC écrit un portail comme **IfcDoor** de PredefinedType **GATE**, d’un **IfcDoorType** avec son fonctionnement (DOUBLE_DOOR_SINGLE_SWING, SLIDING_TO_LEFT, DOUBLE_DOOR_FOLDING… ; un portail avec portillon USERDEFINED), sa largeur libre et sa hauteur comme OverallWidth et OverallHeight et Pset_DoorCommon. Les piliers font partie du corps du portail : un seul élément porte le portail tel qu’il a été configuré, et Lapin le relit comme son propre portail.

## Règles d’urbanisme et soleil (Lapin Urban)

L’édition **Lapin Urban** ajoute un groupe **Urbanisme** à cet onglet : les règles d’urbanisme de la parcelle (POT, CUT, les hauteurs maximales, les reculs par rapport à la rue, aux limites latérales, au fond de parcelle et à l’axe de la voie), le **gabarit constructible** qu’elles laissent, les indicateurs que le modèle atteint par rapport à elles, et l’**étude d’ensoleillement** : le soleil et ses ombres en 3D pour n’importe quel jour et n’importe quelle heure à l’emplacement du projet, et des plans d’ombres pour l’équinoxe et les solstices.
