---
title: Import et export
order: 110
summary: Échangez le modèle en IFC, importez des dessins DXF, liez des fichiers et exportez des modèles 3D pour le rendu et la RA.
---

Tout ce qui entre ou sort se trouve dans le menu **Fichier**.

![Le menu Fichier](shots/file-menu.webp)

## IFC et Fragments

**Exporter IFC ou Fragments…** écrit le modèle en IFC. Chaque mur, dalle, porte et toit est un véritable élément IFC avec ses couches, son type et ses propriétés, si bien que le fichier s’ouvre comme un modèle dans d’autres outils BIM. Les portes et les fenêtres indiquent comment elles s’ouvrent : le sens de chaque vantail et de chaque ouvrant, le côté vers lequel il pivote, et les panneaux, de la manière dont IFC les définit, pour que les autres logiciels dessinent les mêmes ouvertures.

L’export demande le schéma (**IFC4**, lu partout, ou **IFC4X3**), le détail (les pièces comme éléments à part entière ou un corps par élément), la phase et la façon d’écrire les corps. **Géométrie** est un **B-rep à facettes** (exact, ce qu’attendent le plus souvent les autres logiciels) ou des **ensembles de faces** : les mêmes faces dans un fichier bien plus petit. **Simplifier** écrit moins de triangles là où un corps est un maillage fin, comme les gaines et les tuyaux du fichier d’un autre logiciel, jamais plus loin de lui que la **tolérance** que vous saisissez (2 mm au départ). Les corps que Lapin construit lui-même (murs, dalles, portes) gardent leurs faces exactes, et simplifier ne change que la forme d’un corps, jamais ses propriétés.

![Exporter IFC](shots/export-ifc.webp)

Poutres, poteaux et profilés d'acier sont écrits par leur profil paramétrique, nommé, avec ses valeurs mécaniques ([Bibliothèques de profilés](profiles.md)).

**Importer IFC…** lit le fichier IFC d’un autre logiciel. Avant que quoi que ce soit n’entre, Lapin lit le fichier et montre ce qu’il contient : ses éléments par discipline (architecture, structure, fluides, mobilier et équipements, site), chaque discipline par catégorie avec leur nombre, et ses niveaux avec le nombre d’éléments sur chacun. Décochez ce dont vous n’avez pas besoin, par exemple les fluides et le mobilier quand vous travaillez sur la structure, ou ne gardez que les niveaux sur lesquels vous travaillez. **Comment** importe les éléments reconnus comme éléments Lapin, tous comme références, ou liés. Pendant la lecture et l’import du fichier, Lapin montre ce qu’il fait et où il en est ; un gros fichier prend un certain temps.

![Importer IFC](shots/import-ifc.webp)

Certains fichiers portent de la 2D en plus de leur modèle : symboles de plan, emprises, hachures et textes dessinés pour un élément, ou éléments dessinés seulement en 2D (lignes, textes, symboles sans corps). Quand un fichier en a, la boîte de dialogue les compte sous **Dessiné en 2D**. Décochez **la 2D propre des éléments** et le plan dessine ces éléments par leur coupe, comme ceux de Lapin ; décochez **éléments dessinés seulement en 2D** et ils restent dehors. Les deux entrent si vous ne les décochez pas.

![Dessiné en 2D](shots/import-ifc-2d.webp)

**Nettoyage et optimisation** regroupe les règles de l’import, les mêmes pour tout fichier, gardées comme les vôtres pour l’import suivant. **Garder une seule fois un corps répété** (cochée sauf si vous la décochez) garde une fois un corps que le fichier répète sous d’autres noms (un boulon, une bride, un support) ; ses éléments le partagent, rien n’est perdu. Les autres laissent de côté, quand vous les cochez, ce qu’un autre logiciel a amené : les **corps plats** (textes, lignes et hachures changés en triangles), les **éléments plus loin du reste** que la distance saisie (les textes d’un dessin, un cartouche), les **doublons** au même endroit et les **ports et réservations écrits comme objets**. **Simplifier les maillages** écrit moins de triangles dans la tolérance saisie ; **lire les nombres écrits comme texte** change `DoubleLength:0,01` en 0,01 m et `1.15395 kg` en une masse (un nombre entier seul, un code, reste du texte). Sous **Quoi importer**, **Jeux de propriétés** montre les jeux du fichier avec le nombre d’éléments de chacun ; les **techniques** (la transformation de Navisworks, des identifiants internes, des statistiques de géométrie) restent dehors sauf si vous les cochez. Le message de l’import dit ce qu’a fait chaque règle.

![Nettoyage et optimisation](shots/import-ifc-rules.webp)

Ce que le fichier place arrive en un seul groupe à son nom, que vous pouvez ouvrir et nettoyer, ou séparément si vous décochez **En un seul groupe** (voir [Groupes et nettoyage](groups.md)).

**Niveaux.** Les niveaux d’une importation sont ceux du fichier : ils remplacent ceux de votre projet. Chaque niveau du fichier arrive avec son nom, son GlobalId et son altitude telle que le fichier la donne, lue dans l’unité du fichier (millimètres ou mètres). Un niveau de votre projet à l’altitude d’un niveau du fichier (à 5 cm près) est fusionné avec lui : ses éléments restent où ils sont, et ses vues, son plan et ce qui y est dessiné passent au niveau du fichier, qui garde le nom du fichier. Un de vos niveaux qui n’est à aucune altitude du fichier est supprimé quand il ne contient rien (aucun élément, rien de dessiné sur son plan), ses vues passant au niveau du fichier le plus proche ; celui qui contient quelque chose reste, et le message de l’importation dit lequel. Les Fondations, le Rez-de-chaussée et le Premier étage d’un nouveau projet cèdent ainsi la place à ceux du fichier. Le tout est une seule étape d’annulation avec l’importation. Un fichier lié garde ses niveaux pour lui et ne change aucun des vôtres. Une fois l’importation terminée, la vue 3D cadre le modèle, sauf si vous avez déplacé la caméra pendant qu’il arrivait.

**Tout modifiable**, un choix de **Comment**, va plus loin que la reconnaissance : ce qui resterait une référence devient un solide Studio fait de ses propres triangles, le même élément avec sa classe, ses propriétés, son niveau et son groupe, que pousser/tirer, les congés et les booléens prennent aussitôt (voir [Studio](studio.md)). Un corps de plus de 5 000 triangles, comme une pompe ou un raccord finement maillé, reste une référence, et le message de l’import dit combien et pourquoi. **Importer Fragments…** a le même **Comment** : un fichier Fragments ne contient que des triangles, et pourtant ses murs (avec leurs portes et fenêtres), dalles et poteaux en sont reconnus comme éléments Lapin, le reste gardé en références ou, avec **Tout modifiable**, fait solides Studio. Les éléments d’un IFC que la reconnaissance n’a pu lire dans le fichier (un mur aux ouvertures taillées dans son corps) sont essayés aussi à partir de leurs triangles. **Importer un maillage** demande **Comme : modifiables**. Les références importées auparavant se convertissent avec Modifier → **Convertir en natif** (NV) : reconnues où c’est possible, le reste en solides Studio ; un groupe sélectionné vaut ses membres. Une référence importée auparavant se rend modifiable une à une avec Studio → **Faire un solide Studio**, qui la garde dans son groupe et sur son niveau.

Lapin reconnaît les murs, les dalles, les poteaux, les poutres, les semelles, les portes et les fenêtres, et les transforme en éléments Lapin que vous pouvez modifier. Ce qu’il ne peut pas reconnaître entre comme référence, dessinée comme le fichier la donne : sa forme ne se modifie pas, mais elle se déplace, pivote, se reflète, se copie et se répète comme n’importe quel élément. Un élément ne devient un élément Lapin que lorsqu’il est celui du fichier : Lapin mesure chaque élément reconnu par rapport au corps que le fichier lui donne, et celui qui en diffère de plus de 1 mm ou de 0,5 % de son volume (un poteau dont le fichier incline le sommet, une extrémité de mur en redans, qu’un élément Lapin rendrait droits) entre comme la référence qu’il était ; le message de l’import dit combien et pourquoi. Les portes et les fenêtres arrivent avec l’ouverture que le fichier indique : leur sens, le côté vers lequel elles s’ouvrent et, pour les fenêtres, la façon dont s’ouvre chaque panneau. Un toit plat d’un autre programme arrive comme un toit Lapin quand son IfcRoof est fait de deux dalles ou plus posées l’une sur l’autre, ou d’un seul corps avec un ensemble de couches : la dalle (ou couche) du dessus est sa couverture, avec son matériau et son épaisseur, celles du dessous son support, du matériau de la plus épaisse ; le contour est celui du support, sans débord, à sa sous-face. Un toit en pente, ou un toit d’une seule dalle sans couches, reste tel qu’il est venu. Les éléments reconnus ne reçoivent que ce que le fichier contient : pas de linteaux, d’appuis, de seuils ni de tableaux de Lapin, et chaque baie a la taille du fichier ; activez-les dans les types des éléments si vous les voulez.

Les finitions qu’un autre programme a exportées comme murs à part (un enduit de 2,5 cm en façade, un plâtre à l’intérieur) deviennent une couche du mur qu’elles couvrent, quand elles sont collées à sa face, aussi hautes que lui et le long de lui : le mur reçoit un type avec cette couche (*Masonry 350 + Lime render*), ses propres couches restent où elles étaient, et le message de l’importation dit combien. Une finition écartée de son mur, ou qui n’en couvre qu’une partie, reste un mur mince. Un poteau que le fichier donne comme un corps coupé en tête par une pente (sous une poutre ou un toit incliné) entre comme poteau de sa section, jusqu’à son point le plus haut.

**Associer les types importés** (Gérer) liste les types qu’une importation a créés (types de murs, de dalles, de portes, de fenêtres, de poteaux, de poutres et de fondations), chacun avec son fichier et son nombre d’éléments. Choisissez sous **Devient** un type du projet du même genre et appuyez sur **Associer** : ses éléments prennent le type du projet, un mur garde son noyau là où il était, et le type importé disparaît. Avec **Mémoriser** coché, une importation ultérieure d’un type de ce genre et de ce nom prend aussitôt celui du projet, et son message le dit. Les choix mémorisés sont listés dans le dialogue, chacun avec **Oublier**.

![Associer les types importés](shots/map-types.webp)

Les espaces du fichier (de Revit, ArchiCAD et autres) entrent comme pièces Lapin, chacune sur son propre niveau, avec leur contour, leur numéro, leur nom, leur hauteur, leur fonction, leur service et leur revêtement de sol : voir [Pièces et couleurs des pièces](rooms.md#pieces-depuis-un-fichier-ifc). Tout ce qu’un élément importé avait dans le fichier (ses jeux de propriétés et ses quantités) est conservé et affiché sous **Propriétés IFC** dans ses propriétés. Une référence sort d’**Exporter IFC…** telle qu’elle est entrée : avec le même GlobalId, la même classe et les mêmes attributs (description, repère, type prédéfini), ses jeux de propriétés et ses quantités avec chaque valeur du type qu’elle avait dans le fichier, et son type, son matériau, ses ports, ses connexions et son système, chacun avec son GlobalId. N’importez que les réseaux (CVC, plomberie, électricité) d’un fichier et exportez-les : le nouveau fichier les contient en entier.

**Importer Fragments…** ouvre le même panneau pour un fichier `.frag` de That Open : ses éléments par discipline et par type, ses niveaux, et **En un seul groupe**. Un fichier exporté de ce projet même n’apporte rien de nouveau quand on l’y réimporte : ses éléments y sont déjà.

![Importer Fragments](shots/import-frag.webp)

**Importer Fragments…** fait entrer un fichier `.frag` : chaque élément avec géométrie devient une référence dessinée d’après son corps, dans la classe que nomme le fichier, sur le niveau où le fichier le place (les niveaux du fichier remplacent les vôtres : voir **Niveaux** plus haut), avec les attributs du fichier sous **Propriétés IFC**. Les éléments au même corps (la même forme posée en plusieurs endroits) le partagent. Importé de nouveau, le même fichier met à jour ce qu’il avait apporté. Ce qui est reconnu d’après ses triangles dépend de **Comment**, comme plus haut. Ses jeux de propriétés et de quantités, son type et ses matériaux arrivent avec les attributs ; le verre reste transparent ; un fichier fait par le convertisseur de That Open se place là où le mettrait son IFC, et ses faces percées (un mur autour de sa fenêtre) gardent leurs trous.

**Exporter IFC ou Fragments…** écrit le modèle en IFC ou en Fragments, choisi comme un import : les éléments par discipline et par type, les niveaux, la phase, et seulement ce que montre la vue 3D. L’IFC sort en IFC4 ou IFC4X3, les parties d’un élément en couches sous lui ou un corps chacune ; Fragments depuis le point de base du projet, l’origine interne ou le point topographique. Un fichier Fragments porte ce que montre une visionneuse IFC : le type de chaque élément, ses quantités, vos paramètres, ses matériaux et les coordonnées cartographiques, comme les écrit le convertisseur de That Open.

![Exporter IFC ou Fragments](shots/export-exchange.webp)

### SDNF (détail de charpente métallique)

SDNF, le Steel Detailing Neutral File, est le format texte dans lequel les logiciels de détail de charpente métallique échangent une structure (Tekla Structures, Advance Steel, SDS2, ProStructures, E3D) : chaque barre une ligne avec son profil, sa nuance, le point de la section par lequel passe la ligne et sa rotation ; des platines ; des files. Lapin écrit la version 3.0 et lit la 3.0 et la 2.0, dans les éditions qui ont le module acier.

**Exporter IFC, Fragments ou BOS…** avec **SDNF** coché écrit les poteaux et les poutres (de tout matériau) et les barres en acier des niveaux et des types choisis, les platines de leurs assemblages et les files, en millimètres. Chaque barre garde le nom de son profil (IPE 300, HEA 240, un profil de votre propre bibliothèque), sa nuance et un numéro lié à son id, pour que le fichier puisse revenir. Les poutres courbes partent en barres courbes, et les trous des platines et des barres en trous, dont ceux des boulons et des tiges d'ancrage des assemblages.

![Exporter SDNF](shots/export-sdnf.webp)

**Importer SDNF…** fait entrer un fichier en éléments propres à Lapin, en une seule étape que vous pouvez annuler : un poteau vertical en poteau, une poutre horizontale en poutre, tout le reste (un contreventement, un chevron, une barre inclinée) en barre en acier, chacun sur son niveau, son profil pris dans les profils du projet et la bibliothèque de Lapin ; ses platines et ses files entrent aussi. Un profil qu'aucune bibliothèque n'a (W12X50, quand vous n'avez pas de table AISC) garde son nom sur une section provisoire et est signalé, jusqu'à ce que vous donniez un profil à son type. Un fichier écrit par Lapin, ou le même fichier importé de nouveau après que le dessinateur l'a modifié, met à jour les éléments dont il est issu au lieu d'en ajouter de nouveaux. Une barre courbe horizontale entre comme poutre courbe (une barre cintrée dans un autre plan est laissée de côté et signalée) ; les trous sont percés dans les platines et les barres qu'ils traversent, et remplacés, non doublés, quand le fichier revient. SDNF ne porte pas de boulons : seuls leurs trous viennent ; les relâchements aux extrémités et les charges ne sont pas importés.

![La halle relue depuis SDNF](shots/import-sdnf-3d.webp)

## JG Structures (calcul de structure)

**Exporter JG Structures (JSON)…** écrit le modèle analytique du projet dans le fichier d'échange de JG Structures, un logiciel de calcul de structure : un fichier JSON selon son contrat (version 1.0.0). Lapin tire le modèle de ce que vous avez dessiné. Poteaux, poutres et barres d'acier deviennent des barres sur leurs axes ; les murs porteurs (Pset_WallCommon.LoadBearing sur le mur ou son type) et les planchers deviennent des coques au milieu de leur couche porteuse, sans les finitions. Ils partagent leurs nœuds : une poutre qui porte un plancher se place dans le plan moyen du plancher, les extrémités des poteaux et des murs rejoignent la poutre ou le plancher en tête et en pied, les angles d'un plancher passent sur les poteaux et murs en dessous, et le plancher est découpé le long des poutres qui le traversent. Les pieds de poteaux et de murs sur une fondation sont encastrés ; le pied d'un poteau acier est articulé ou encastré selon son assemblage.

Choisissez les niveaux, les types d'éléments et les unités : les centimètres et kilogrammes-force du contrat, ou le SI (mètres et kilonewtons). Les matériaux prennent leurs valeurs dans leurs données (Gérer → Matériaux, Pset_MaterialMechanical en pascals) ou dans la classe de résistance de leur nom, C25/30 ou S355, selon les Eurocodes ; l'aire, les inerties, les modules et la constante de torsion d'une section sont calculés à partir de sa géométrie. Ce que Lapin ne sait pas reste vide (null) dans le fichier, jamais zéro. Avant l'export, le rapport compte ce qui entre, contrôle le fichier selon les règles du contrat et liste ce qui manque encore au calcul : un matériau sans classe, un poteau qui ne repose sur rien.

Là où les éléments ne se rencontrent pas sur leurs axes (une panne sur une traverse, une lisse sur un poteau, un contreventement sur son gousset, des chevrons sur la sablière), Lapin place le nœud sur l'élément porteur et garde la distance comme excentrement à l'extrémité de la barre : l'ossature secondaire d'un hall est donc reliée elle aussi. Les murs gardent leurs portes, fenêtres et ouvertures, les planchers leurs trémies : les coques du contrat n'ayant pas de trous, elles sont découpées en morceaux autour, et un plancher en L en morceaux convexes le long des poutres qui le portent. Un mur sous une toiture monte jusqu'à elle, et la toiture est exportée aussi : sa dalle en coques, ou les chevrons, pannes et sablières d'une charpente bois en barres de sa classe de bois (C24 selon l'EN 338). Les cornières et autres sections non symétriques sont données dans leurs axes principaux. **Pieds sur fondations** choisit comment sont tenus les pieds des poteaux et des murs posés sur une fondation : encastrés (par défaut) ou articulés. Un pied de poteau acier garde son propre assemblage.

![Exporter JG Structures](shots/export-jg.webp)

## Optimiser un fichier IFC

**Optimiser IFC…** (dans Fichier, ou sur l’écran d’accueil : aucun projet n’a besoin d’être ouvert) fait une copie plus légère d’un ou de plusieurs fichiers IFC sans changer ce qu’ils disent. La copie garde chaque GlobalId, nom, jeu de propriétés, quantité, type, matériau et relation exactement tels que le fichier les a écrits, et son en-tête aussi ; rien de Lapin n’est ajouté. Seule la géométrie change : les maillages (ensembles de faces, B-reps facettés de faces planes) sont nettoyés (sommets en double soudés, triangles dégénérés et répétés et coordonnées inutilisées supprimés) et simplifiés, jamais plus loin de l’original que la **Précision** (Rapide 5 mm, Équilibré 1 mm, Précis 0,1 mm, ou la vôtre), mesurée dans les deux sens ; les corps identiques sont écrits une seule fois et partagés par leurs produits ; et ce que rien ne référence (points, styles inutilisés, représentations vides) disparaît. Les extrusions, les booléens et les autres corps exacts restent tels quels. Arrondir les coordonnées, écrire les B-reps en ensembles de faces (IFC4 et IFC4X3 seulement) et un `.ifczip` compressé sont là quand vous le voulez.

![Optimiser IFC](shots/optimize-ifc.webp)

La copie est écrite à côté de l’original, ou dans un dossier de votre choix, sous le nom de l’original avec une **Terminaison** (`.optimized` pour commencer) : `maison.ifc` donne `maison.optimized.ifc`. L’original n’est jamais écrit, et si la copie existe déjà, Lapin demande avant de la remplacer. Pour chaque fichier, le rapport donne la taille, les entités et les triangles avant et après, les corps partagés, les orphelins supprimés, l’écart maximal mesuré et la durée ; **Ouvrir le dossier** montre la copie et **Ouvrir dans Lapin** l’ouvre dans une autre fenêtre. Dans le navigateur, la copie est téléchargée. Un fichier déjà dans le projet garde son chemin de retour : **Optimiser le fichier source…** sur le groupe d’un IFC importé (dans ses propriétés) ou sur un lien (Gérer → Liens) fait de même avec le fichier d’où il vient, tant qu’il est encore sur le disque.

![Le rapport](shots/optimize-ifc-report.webp)

## DXF et DWG

**Importer DXF…** fait entrer un dessin de CAO dans la vue au premier plan, avec ses calques, ses hachures, ses textes et ses cotes. Il arrive sous forme de dessin auquel vous pouvez vous accrocher et que vous pouvez modifier. Pour utiliser un DWG, enregistrez-le d’abord en DXF depuis votre logiciel de CAO (AutoCAD, BricsCAD ou DraftSight : **Save As → DXF**). Un PDF ou une image arrive pour être décalqué avec **Fichier → Importer un PDF ou une image…** ou **Dessin → PDF ou image** : les lignes d’un PDF vectoriel comme dessin à l’échelle où il a été dessiné, ses images et tout PNG ou JPEG comme images translucides (voir [Dessin](drafting.md)). Dans un plan, **Placé : par les coordonnées de la carte** pose un dessin en coordonnées réelles (un plan cadastral en Stereo 70) là où la carte le place (voir [Le site et ses données](site.md)).

Le dessin se trouve aussi dans la **vue 3D**, sur le plan de la vue où il est arrivé : sur un plan au niveau de son étage, sur une coupe ou une façade sur le plan de cette vue, dans les couleurs de ses calques, ses hachures estompées, en lignes fines qui ne cachent jamais le modèle. Un calque éteint dans **Dessin → Calques** reste éteint en 3D. **Vue → DXF en 3D** (aussi dans **Vue → Paramètres de vue** d’une vue 3D) affiche ou masque tous les imports ; un import se commute par **En 3D** dans les propriétés de n’importe laquelle de ses formes, un DXF lié par **3D** dans **Gérer → Liens**. En dessinant en 3D à ce niveau, avec les outils de Dessin ou l’Esquisse de Studio, on s’accroche à ses lignes, de sorte que vous pouvez le décalquer en 3D comme sur le plan. Les lignes auxquelles le fichier a donné des altitudes se tiennent dans l’espace à ces altitudes, comme avant.

![Un DXF de plan de masse dans la vue 3D, au niveau du rez-de-chaussée](shots/dxf-3d.webp)

Un DXF importé fait partie du projet : **File → Save** le garde dans le fichier `.lapin` avec ses calques, hachures, textes et tout groupe que vous en avez fait, et il est là quand le projet s'ouvre de nouveau, sur le plan du niveau. La vue en plan où il est entré est désormais le plan du niveau, celle que liste le navigateur du projet, et ce qui était dessiné avant sur le niveau passe sur elle. Lapin demande d'enregistrer avant de se fermer s'il reste des modifications non enregistrées. Pour garder le dessin comme un fichier à part, cochez **As a link** dans la boîte d'import : il reste lié, en lecture seule, et **Manage → Links** le recharge quand le fichier change.

## Liens

**Lier IFC…** garde un autre fichier lié au lieu de l’importer. Il est en lecture seule et rechargé quand il change. Cela fonctionne aussi pour un DXF. **Gérer → Liens** affiche, recharge, convertit ou supprime les liens. Un DXF lié se trouve en 3D comme un DXF importé ; sa case **3D** dans **Gérer → Liens** l’en retire. **Visibilité / Graphismes** (<kbd>VV</kbd>) masque un lien, l’affiche en demi-teinte ou lui donne ses propres catégories dans une seule vue (*Coupes, élévations et 3D*, Visibilité / Graphismes).

Un IFC lié garde ce que son fichier dit des matériaux de ses éléments. Murs, planchers et toitures avec compositions sont dessinés dans leurs couches même en tant que références, si bien qu’un ingénieur masque les finitions du lien d’architecture (**Murs ▸ Finitions**, **Dalles ▸ Finitions**) et qu’un style de vue de structure ne dessine que leurs âmes ; les barres d’une maquette de structure liée sont dans la catégorie **Armatures**, que toute vue 3D laisse de côté jusqu’à ce qu’on l’affiche. Un lien fait avec une version antérieure reçoit les couches de ses murs quand il est rechargé (celles des planchers tout de suite).

## Nuages de points

**File → Link point cloud…** lie un scan laser ou un nuage de photogrammétrie, dans les formats ouverts uniquement : **LAS** (1.0–1.4, tous les formats de point), **LAZ** (LAS compressé), **E57**, **PLY** (ASCII et binaire) et des points en texte, **XYZ**, **PTS** et **CSV**. Les formats propriétaires (RCP et RCS d'Autodesk ReCap, FLS de FARO, LGS de Leica) ne sont pas lus : exportez d'abord le scan depuis son programme en E57 ou en LAS.

Lapin demande où se trouve le nuage et dans quelle unité :

- **par les coordonnées cartographiques du projet** : le x et le y du fichier sont des coordonnées est et nord, placées par le point de levé et l'angle au nord de **Manage → Location**, ses hauteurs au-dessus de l'altitude du point de levé. Lapin le propose quand le fichier nomme un système de coordonnées ou que ses coordonnées sont loin de zéro et que le projet a une localisation ;
- **origine sur origine** : l'origine du fichier sur celle du projet ;
- **centré sur l'origine du projet** : son milieu sur l'origine, son point le plus bas à ±0.00 ;
- **son unité** : comme le dit le fichier (LAS et E57 la disent), sinon mètres ; centimètres, millimètres, pieds, pieds d'arpentage US et pouces sont proposés. Quand le nuage paraît invraisemblable pour un bâtiment ou un terrain (moins de 20 cm ou plus de 50 km), ou que le fichier indique une autre unité, Lapin prévient.

Le nuage reste dans son fichier. Il n'est pas copié dans le projet : le projet ne garde que le lien (le chemin du fichier, où se trouve le nuage, comment il est dessiné). Lapin l'indexe une fois, en arrière-plan, en un octree de tuiles dans son propre dossier sur cet ordinateur (pas à côté du projet), et **Manage → Links** montre où il en est. Ensuite la vue 3D charge les tuiles dont la caméra a besoin, les plus proches d'abord et plus fines à mesure qu'on s'approche, dans un budget de points fixé par **View → Display quality** (1,5, 3 ou 6 millions de points dessinés à la fois), si bien qu'un scan de cent millions de points tourne aussi souplement qu'un petit.

![Un scan de la maison lié comme nuage de points, dans la vue 3D](shots/point-cloud-3d.webp)

Dans **Manage → Links** un nuage a sa propre ligne : ses points, où en est son index, **Shown**, **Halftone**, **3D**, ce qui colore ses points (**RGB**, **Intensity**, **Elevation** ou **Classification**, selon les classes ASPRS : sol, végétation, bâtiment, eau…) et la **Size** d'un point en pixels. Le bouton à épingle le replace (par la carte, origine sur origine ou centré, son unité, déplacé en x, y et z, tourné) ; le bouton de rechargement relit son fichier et ne le réindexe que s'il a changé. **Unlink** retire le lien ; l'index reste dans le dossier de Lapin et sert de nouveau le nuage s'il est lié de nouveau.

![Manage → Links avec un nuage de points](shots/point-cloud-links.webp)

Un nuage n'est pas exporté avec le modèle : IFC et Fragments portent le modèle, pas le scan, et **Export 3D…** n'en prend un échantillon en points dans GLB et glTF que sur demande. Plans, coupes et élévations le dessinent aussi (*Coupes, élévations et 3D*, Nuages de points dans les vues). Dans le navigateur on peut lier un nuage jusqu'à 160 Mo ; il est indexé dans la page et doit être lié de nouveau après le rechargement de la page. Les nuages plus grands demandent l'application de bureau.

## Modèles 3D

**Export 3D (glTF, USD, DAE)…** écrit le modèle sous forme de surfaces pour le rendu, la visualisation et la réalité augmentée (RA) :

| Format | Pour |
|---|---|
| GLB, glTF | Blender, three.js, visionneuses web. Les images propres à vos matériaux sont incluses. |
| USDZ | AR Quick Look d’Apple, Reality Composer, Omniverse. Images incluses. |
| USDA | OpenUSD en texte, un seul fichier. |
| DAE | COLLADA, pour SketchUp et des outils plus anciens. |
| OBJ + MTL | Un .zip pour Twinmotion, Lumion, 3ds Max et tout logiciel de rendu. Images comprises. |

**Importer un maillage (OBJ, STL, glTF)…** fait entrer un maillage d’un autre logiciel 3D (OBJ, STL, glTF ou GLB) sous forme de références sur le niveau actif. glTF indique son unité et l’axe vers le haut ; pour OBJ et STL, Lapin le demande. Un meuble ou une décoration importés ainsi se placent comme n’importe quel élément : **Déplacer**, **Copier**, **Faire pivoter**, **Symétrie** et **Réseau** dans Modifier, **Déplacer 3D** dans Studio pour sa hauteur. Ses copies partagent ses triangles, donc une rangée de chaises pèse comme une seule. **Faire un solide Studio** dans Studio rend modifiable la forme d’une référence (voir [Studio](studio.md)).

![Six chaises d’un fichier OBJ autour d’une table, faites avec Modifier → Réseau](shots/reference-array.webp)

![Exporter 3D](shots/export-3d.webp)

Vous choisissez ce qu’il faut exporter, quelle phase et l’origine. **Quoi** vaut **tout**, ou le modèle **tel que la vue le montre** : la vue 3D au premier plan, sans ses éléments et catégories masqués ni les niveaux qu’elle n’affiche pas, avec les couches qu’elle affiche (sans les finitions, ou le noyau seulement). Quand cette vue est coupée (la coupe au-dessus des niveaux affichés, une coupe affichée en 3D, la zone de coupe), **Coupe** coupe aussi l’export à cet endroit et ferme chaque coupe dans ses matériaux, si bien que le fichier ressemble à l’écran. Une vue 3D qui omet quelque chose est exportée telle qu’elle s’affiche, sauf si vous en décidez autrement. Chaque élément garde sa classe IFC, son type et son GlobalId. Pour échanger le modèle lui-même plutôt que ses surfaces, utilisez **Exporter IFC…**.

## Lapin pour le navigateur

**Fichier → Lapin pour le navigateur (.html)…** écrit le projet sous forme de page web, un seul fichier .html que vous pouvez envoyer à n’importe qui. Il s’ouvre d’un double-clic dans n’importe quel navigateur (Chrome, Edge, Safari, Firefox), sur n’importe quel ordinateur, sans internet et sans rien installer :

- **3D** : le modèle tel que le montre la vue 3D. Faites glisser pour le tourner, faites glisser avec le bouton droit pour le déplacer, la molette pour zoomer ; **Ajuster** l’affiche en entier. Choisissez un niveau seul ou avec ceux du dessous ; cliquez sur un élément pour le sélectionner (Maj ajoute à la sélection), puis **Isolez**-le ou **Masquez**-le ; **Tout afficher** ramène tout.
- **Dessins** : les vues en plan, coupes, élévations et feuilles que vous avez cochées, dessinées comme Lapin les imprime, nettes à tout zoom. Faites glisser pour déplacer, la molette pour zoomer, **Imprimer** pour imprimer celui qui est affiché. Cliquez sur un élément d’un dessin pour le sélectionner : c’est le même élément qu’en 3D.
- **Devis**, quand le projet en a un : ses chapitres et ses lignes, les totaux, marqués *indicatifs* quand il utilise les valeurs approximatives propres à Lapin. Cliquez sur l’en-tête d’une colonne pour trier, tapez dans **Filtre** pour trouver une ligne, cliquez sur une ligne pour sélectionner ses éléments.
- **Propriétés** : ce qui est sélectionné, avec sa classe, son type, son niveau, son matériau, ses propriétés et ses quantités.

![Fichier → Lapin pour le navigateur](shots/html-export-dialog.webp)

Dans la boîte de dialogue, vous choisissez les dessins qui entrent (tous par défaut), le modèle 3D, le devis et **le projet lui-même**. La page indique sa taille avant d’être écrite. La page parle la langue que parle Lapin au moment de l’export.

![La page dans un navigateur : le modèle 3D](shots/html-export-3d.webp)

![Un plan dans la page, un mur cliqué montrant ses propriétés](shots/html-export-drawing.webp)

![Le devis dans la page](shots/html-export-costs.webp)

Avec le projet à l’intérieur, la page est aussi le projet : **Fichier → Ouvrir** dans Lapin prend le .html et ouvre le projet qu’il contient, non enregistré ; **Enregistrer** demande où le garder en .lapin, et n’écrit jamais sur la page. Sans le projet, la page ne montre que le modèle, et Lapin ne l’ouvre pas. Une page web que Lapin n’a pas écrite est refusée.

La page est un instantané : ce qu’elle montre est le projet au moment de l’export, et rien de ce qu’un lecteur y fait ne change le projet. Elle pèse ce que pèsent ses parties : le projet, le modèle 3D (à peu près la taille d’un export GLB) et les dessins, avec environ 0,6 Mo pour la visionneuse elle-même.
