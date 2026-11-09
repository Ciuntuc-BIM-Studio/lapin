---
title: Portes et fenêtres
order: 30
summary: Placez des portes et des fenêtres dans les murs, déplacez-les et concevez leurs types dans le configurateur.
---

<!-- video doors-windows -->

## Placer une porte ou une fenêtre

Choisissez **Architecture → Porte** (<kbd>DR</kbd>) ou **Fenêtre** (<kbd>WN</kbd>), puis cliquez sur un mur là où vous voulez l’ouverture. Lapin découpe l’ouverture dans chaque couche du mur et ajoute le linteau que demande le type de mur.

Tant que Door ou Window est actif, le panneau des propriétés montre le type qu’il posera, avec sa taille : choisissez-en un autre là avant de cliquer sur le mur.

![Un plan avec une porte et des fenêtres](shots/openings-plan.webp)

Pour déplacer une porte ou une fenêtre le long de son mur, sélectionnez-la et faites-la glisser. Pour la modifier, utilisez le panneau des propriétés : son type, sa hauteur d’allège, et le côté vers lequel elle s’ouvre (**Côté**). Avec plusieurs portes et fenêtres sélectionnées, **Main**, **Côté**, l’allège et le type les changent toutes en une seule étape, de même qu’une flèche de retournement en plan.

Pour une distance exacte, utilisez **Déplacer** (<kbd>M</kbd>) ou **Copier** (<kbd>CO</kbd>) dans la ligne de commande, comme pour tout élément : sélectionnez la porte, la fenêtre ou l’ouverture du mur, cliquez un point de base, puis tapez la distance (`500`) ou cliquez un second point. Seule la part du déplacement le long du mur compte : l’ouverture glisse le long de son mur et garde son allège. Déplacée dans un autre mur du même niveau, elle passe dans ce mur et s’ouvre toujours de la même façon dans la pièce. Une copie est une nouvelle porte ou fenêtre du même type, avec le numéro suivant. Si elle ne tient pas (au-delà de l’extrémité du mur, ou sur une autre ouverture), rien ne bouge. **Faire pivoter** et **Symétrie** la placent de la même façon, et une symétrie change sa main.

![Les propriétés d’une porte](shots/door-properties.webp)

## Baies sans porte ni fenêtre

**Architecture → Wall opening** (<kbd>WO</kbd>) découpe une baie seule dans un mur là où vous cliquez : un passage, une trappe, un trou pour une porte qui viendra plus tard. Sa largeur, sa hauteur et son allège se règlent dans le panneau des propriétés avant le clic. Sélectionnée, sa taille, sa place le long du mur et son allège s’y modifient ; comme une porte, elle doit tenir dans le mur et rester à l’écart de ses autres baies. En IFC, c’est une ouverture qui évide le mur, sans rien pour la remplir.

Sélectionnée, une baie a des poignées comme une fenêtre : faites glisser son milieu pour la déplacer le long du mur, ou un tableau pour l’élargir ou la rétrécir tandis que l’autre reste en place ; la longueur près de la souris peut se taper, comme en dessinant. On la prend d’un clic dans son vide sur le plan, ou dans son trou dans la vue 3D, où son contour s’allume. **Déplacer** (<kbd>M</kbd>) la fait glisser le long du mur d’une distance tapée, et sa hauteur et son allège sont dans le panneau des propriétés.

**Couvrement** donne la forme de son haut : **Rectangle**, **Arc segmentaire** (un arc surbaissé), **Arc elliptique** (une demi-ellipse, haute d’un quart de la largeur sauf indication contraire), **Plein cintre** ou **Angles arrondis** (un haut droit aux angles supérieurs arrondis). **Flèche de l’arc** règle la montée d’un arc segmentaire ou elliptique, **Rayon des angles** l’arrondi des angles (150 mm sauf indication contraire, jamais plus de la moitié de la largeur) ; vide, celui de la forme. La baie dans le mur suit le contour en 3D, dans les élévations et les coupes et dans l’IFC ; le plan montre ses tableaux comme avant.

**Finition de l’embrasure** habille ses tableaux et son couvrement dans le trou : **Enduit** (15 mm sauf indication) ou un **Habillage** (20 mm de bois), avec son **Épaisseur du revêtement** et son **Matériau du revêtement** ; une baie au-dessus du sol est habillée aussi en dessous. La finition fait partie du corps du mur, en 3D, en façades, en coupes et dans les quantités, et la baie libre est ce qu’elle laisse. **Linteau** est **La règle du mur** (le linteau que demande le type de mur, comme au-dessus d’une porte) ou **Aucun**. Plusieurs baies sélectionnées changent ensemble, en une seule étape d’annulation.

**Modifier la baie…** ouvre son configurateur : les mêmes champs au-dessus d’un dessin de la baie dans son mur, appliqués ensemble. **Changer en porte…** et **Changer en fenêtre…** la remplissent d’une porte ou d’une fenêtre du type choisi, là où elle est : sa place, son allège et son GlobalId restent, et **Ajuster le type à la baie** lui donne un type de sa taille et de son couvrement (celui du projet à cette taille, sinon un fait du type choisi et nommé d’après sa taille). Dans l’autre sens, **Changer en baie** sur une porte ou une fenêtre sélectionnée l’enlève et laisse son trou comme une baie de même taille et de même forme. Chacun est une seule étape d’annulation.

![Une baie dans son configurateur : couvrement en plein cintre, embrasures enduites](shots/wall-opening-dialog.webp)

## Le configurateur

**Architecture → Portes et fenêtres** ouvre le configurateur. Chaque type de porte et de fenêtre du projet est listé à gauche. Vous pouvez modifier :

- le nom et le repère du type (la lettre dans les étiquettes) ;
- sa largeur et sa hauteur ;
- la profondeur et la largeur du cadre ;
- ses colonnes et ses rangées de vantaux, et la façon dont chaque vantail s’ouvre ;
- la finition de l’embrasure, l’encadrement en façade, le seuil et les couleurs.

Les dessins à droite montrent le type tel qu’il sera dessiné en élévation, en plan et en coupe, pendant que vous le modifiez. En dessous, un **aperçu 3D** montre le type posé dans un morceau de mur du premier type de mur du projet, avec son tableau, ses appuis, son habillage et ses matériaux, pendant que vous le modifiez (une fenêtre en baie, en arc ou en caisson aussi) : faites glisser pour le tourner, la molette pour zoomer, glissez avec le bouton droit pour le déplacer. Il commence vu de l’extérieur.

**Gauche et droite.** Le sens d’ouverture d’une porte est nommé comme le nomment les architectes : regardez de la poignée vers les paumelles ; un vantail qui s’ouvre à gauche de cette ligne s’ouvre à gauche, un qui s’ouvre à droite, à droite. Les choix du configurateur pour un vantail le disent ainsi, avec le côté de ses paumelles dans l’**Ouverture** d’un compartiment, vue de l’extérieur, du côté d’où l’on pousse les vantaux : *Ouverture à droite (paumelles à gauche)*. Un fichier IFC nomme la main à sa façon (les paumelles vues en regardant dans le sens où s’ouvre le vantail), si bien que la même porte y est écrite SINGLE_SWING_LEFT ; Lapin passe de l’une à l’autre en écrivant et en lisant l’IFC. La gauche et la droite d’une fenêtre se voient de l’intérieur, du côté vers lequel s’ouvrent ses ouvrants : *Battant à droite (paumelles à gauche)* dans le configurateur, dont les compartiments sont vus de l’extérieur. IFC écrit la même fenêtre comme gauche. L’élévation est vue de l’intérieur, du côté vers lequel s’ouvrent les vantaux, sauf si **Depuis l’extérieur** est coché ; comme le plan, elle montre la porte telle qu’elle est dans le modèle. Une porte retournée sur l’autre main (**Main**, ou une flèche de retournement le long du mur) est l’image en miroir de son type ; les nomenclatures la mettent sur une fiche à part (voir [Nomenclatures](schedules.md)).

![Le configurateur des portes et fenêtres](shots/joinery-configurator.webp)

**Enregistrer le type** conserve vos modifications. Chaque porte ou fenêtre de ce type les suit. **Placer avec** ferme le configurateur et commence à placer ce type.

### Compartiments, meneaux et profilés

Une porte ou une fenêtre est une grille de compartiments. Chaque compartiment choisit comment il s’ouvre et ce qui le remplit : un même type peut ainsi réunir deux vantaux avec des parties latérales et une imposte au-dessus, ou une fenêtre avec un panneau plein sous un ouvrant.

**Colonnes (relatives)** et **Rangées (relatives)** divisent vite la baie : `1 1` donne deux compartiments égaux, `1 1 1` trois, `2 1` un large à côté d’un étroit. Les rangées se comptent de bas en haut.

**Largeurs des colonnes** et **Hauteurs des rangées** donnent les mêmes tailles en millimètres. Saisissez-les, la dernière prend le reste. Dans l’élévation, vous pouvez aussi faire glisser un meneau ou une traverse ; la distance s’affiche pendant le glissement.

**Largeur du meneau** et **Largeur de la traverse** ont leurs propres tailles, à part de la **Largeur du cadre**. Un cadre de 50 mm avec des meneaux de 50 mm se lit partout comme 50 mm.

**Compartiments** liste chaque compartiment. Cliquez sur l’un d’eux, dans la liste ou dans l’élévation, pour le modifier :

- **Ouverture** : fixe, un ouvrant (à la française, oscillo-battant, à soufflet, à l’italienne, pivotant, coulissant) ou, sur une porte, un vantail.
- **Remplissage** : **Verre** ou **Panneau plein**. Les vantaux et les parties latérales d’une porte peuvent être pleins, tout comme un compartiment de fenêtre (un panneau sous un ouvrant, un jour opaque).
- **Scinder** : **En colonnes** ou **En rangées**, à une distance en millimètres du bord gauche ou bas du compartiment (la moitié par défaut). Scindé en travers, un vantail de porte garde sa partie basse et la partie haute devient une imposte vitrée fixe.
- **Fusionner** : avec le compartiment voisin, au-dessus ou en dessous, quand ils partagent un bord entier. Les meneaux et traverses dont plus aucun compartiment n’a besoin disparaissent.

![Une porte de magasin en un seul type : deux vantaux, des parties latérales et une imposte sur toute la largeur](shots/joinery-panes.webp)

**Système de profilés** part d’un profilé **PVC**, **Aluminium**, **Bois** ou **Acier (fin)** : il fixe les tailles du dormant, des meneaux, des traverses et des ouvrants et donne aux profilés leurs formes fines. Le dormant a une **Feuillure** sur laquelle l’ouvrant se ferme, l’ouvrant recouvre le dormant de son **Recouvrement de l’ouvrant**, des **parcloses** tiennent le verre, et une fenêtre a un jet d’eau sous sa traverse basse. Chaque taille reste modifiable. **Simple (profilés droits)** garde des pièces rectangulaires simples, comme avant.

**Dormant d'une bibliothèque** et **Ouvrant d'une bibliothèque** prennent un profilé de vos [bibliothèques de profilés](profiles.md) (une section d'aluminium ou de PVC lue d'un DXF, par exemple) : les barres droites du dormant et de l'ouvrant ont alors son contour réel, vides compris, en 3D, en coupe et en plan, avec sa largeur et sa profondeur. Il remplace ce dont il est la forme réelle : la butée, la lèvre et les larmiers du système ; les parcloses restent. Les têtes courbes et les fenêtres rondes gardent une bande simple, comme les montants et traverses de la grille. **Simple** revient à un rectangle de cette taille.

![Une fenêtre PVC : les ouvrants qui recouvrent le dormant, les parcloses, un panneau plein sous un fixe](shots/joinery-profiles-3d.webp)

Sous **Détails**, **Tablette intérieure** et **Appui extérieur** activent ou désactivent les appuis, et **Habillage des tableaux** active la finition de l’embrasure sur les tableaux et le linteau. Là où appuis et habillages ne servent pas, décochez-les.

### Volets, grilles, jardinières et auvents

<!-- video window-dressings -->

Sous **Sur la façade**, un type de fenêtre peut avoir :

- des **Volets** : à lames, pleins ou à planches, à un ou deux vantaux, représentés ouverts (rabattus contre le mur à côté de la fenêtre) ou fermés devant elle ;
- une **Grille de défense** : dans le tableau côté intérieur (par défaut, là où se posent d’ordinaire les grilles de défense), dans le tableau côté extérieur, sur la façade ou sur la face intérieure, avec des barreaux horizontaux, verticaux ou dans les deux sens, leur section et leur espacement ;
- une **Jardinière** sous la fenêtre ;
- un **Auvent** au-dessus, avec sa saillie et sa pente.

Chacun se place d’après les faces réelles du mur à l’endroit de la fenêtre, à partir des couches du mur, et reste donc juste sur n’importe quel type de mur. Quand la fenêtre a un encadrement, ce qui est sur la façade se tient devant l’encadrement. Vous pouvez déplacer chacun à la main : **Depuis le nu du mur** règle de combien il s’écarte du mur (pour une grille dans le tableau, **En retrait du nu** règle sa profondeur), **Depuis l’axe de la fenêtre** le déplace le long du mur, et **Vers le haut ou le bas** le déplace en hauteur. Ils apparaissent en 3D, dans les plans, les élévations et les coupes, et les nomenclatures les mesurent.

![Des volets, une grille, une jardinière et un auvent dans le configurateur](shots/joinery-dressings.webp)

![Les fenêtres habillées, en 3D](shots/window-dressings-3d.webp)

### Formes, styles et ornements

**Forme** donne à une porte ou une fenêtre une tête cintrée (**Arc en plein cintre**, **Arc segmentaire**, **Arc elliptique**, **Angles arrondis**, **Arc brisé**, **Tête triangulaire**) ou la rend **Ronde ou ovale** (ronde quand la largeur et la hauteur sont égales). Un type cintré garde ses vantaux sous une traverse à la naissance de l’arc, et au-dessus une imposte fixe qui suit l’arc, avec des petits-bois si on le souhaite : **Éventail**, **Les meneaux prolongés** ou **Croix**. **Flèche de l’arc** règle la montée d’un arc segmentaire, elliptique, brisé ou triangulaire, **Rayon des angles** l’arrondi des angles d’une tête aux angles arrondis. La baie dans le mur, le cadre, le vitrage, l’encadrement et les dessins suivent le contour.

**Traverse à la naissance de l’arc**, cochée par défaut, garde cette traverse et l’imposte fixe au-dessus. Décochez-la pour une fenêtre battante cintrée : la rangée supérieure de vitrages monte dans l’arc, et le dormant, l’ouvrant, le vitrage, les petits-bois et le symbole d’ouverture suivent l’arc tout autour, sans barre dessous. Le vitrage s’ouvre comme vous le choisissez (à la française, oscillo-battant, fixe), les meneaux montent jusqu’à l’arc, et les plans, coupes, élévations, l’aperçu 3D et l’IFC le montrent ainsi. Le style **Fenêtre battante cintrée** pose d’un coup un arc segmentaire avec un seul ouvrant à la française.

![Une fenêtre battante cintrée : un seul ouvrant à la française qui suit un arc segmentaire, sans traverse dessous](shots/joinery-arched-casement.webp)

**Petits-bois** divisent les vitrages : **Grille (colonial)** avec ses carreaux en largeur et en hauteur, **Prairie** avec des barres près des bords, ou des **Losanges**.

Sur la façade, en plus de l’encadrement, une fenêtre peut avoir une **Clé de voûte**, un **Couronnement au-dessus de la baie** (une **Corniche**, un **Fronton**, un **Fronton cintré** ou un **Larmier** qui suit une tête cintrée) et une **Allège sous l’appui** avec ses consoles.

**Appliquer un style…** règle d’un coup la forme, les petits-bois et les ornements : Moderne, Classique, Géorgien, Victorien, Roman, Baroque, Gothique, Tudor, Prairie, Traditionnel ou une fenêtre ronde. Tout reste modifiable. La liste montre ensuite le style du type ; dès que vous changez quelque chose que le style a réglé, elle indique *Géorgien (modifié)*, et choisir le style à nouveau l’applique à nouveau.

![Une fenêtre baroque : plein cintre avec éventail, clé de voûte et fronton cintré](shots/joinery-styles.webp)

![Fenêtres cintrées en 3D](shots/window-styles-3d.webp)

### Bow-windows, fenêtres arrondies et en boîte

Un type de fenêtre peut faire saillie sur son mur. Dans le configurateur, **Projects** en fait un **Bay** (un centre et deux côtés en biais), un **Bow** (des panneaux égaux sur un arc) ou un **Box** (côtés d'équerre avec le mur), et **New bay window** commence un nouveau type en bow-window à 45°. La fenêtre remplit toujours une seule ouverture du mur, aussi large et haute que le type : elle se pose, se déplace, se copie, se retourne, se numérote et se liste comme toute fenêtre. **Architecture → Bay window** (<kbd>BW</kbd>) en pose une dans le mur cliqué : le type choisi dans le configurateur s'il fait saillie, sinon le premier bow-window du projet, sinon un bow-window à 45°.

Les colonnes de la grille sont ses faces. Pour un bow-window et une boîte, la première et la dernière colonne sont ses côtés, celles d'entre elles son centre ; une fenêtre arrondie a une face par colonne, leurs largeurs se partageant l'arc. Chaque compartiment s'ouvre comme le dit son **Ouverture** : les côtés peuvent s'ouvrir et le centre rester fixe. Puis :

- **Projection**, de la face extérieure du mur à la face extérieure des cadres, et pour un bow-window l'angle **Sides to the wall** (15° à 90°) ;
- **Sides** : les côtés de la boîte vitrés ou pleins, avec leur matériau ;
- **Seat inside** : l'assise à l'appui, ce qu'elle avance dans la pièce, son matériau ;
- **Sill outside** sous les faces ;
- **Base** : aucune (accroché au mur), **murs jusqu'au sol**, **une dalle en saillie** ou **une dalle sur consoles**, avec épaisseur, débord et matériau ;
- **Head** : un toit **à quatre pans**, **en appentis** ou **plat**, ou une **dalle**, son épaisseur à l'égout, son débord, sa pente et son matériau, et un **solin contre le mur**.

Cadres, ouvrants, vitrages et poignées sont ceux du type : ses matériaux et poignées valent pour toutes les faces. **Bay style** règle tout à la fois : bow-window à 45° ou peu profond à 30°, bow-window posé au sol, fenêtre arrondie de cinq ou quatre panneaux, boîte à côtés vitrés ou pleins, oriel sur consoles. Comme pour **Appliquer un style…**, la liste montre le style de bow-window de la fenêtre, avec *(modifié)* dès que vous changez ce qu’il a réglé. **Save as bay style…** garde celui configuré comme style du projet, à côté de ceux de Lapin ; renommez-le ou supprimez-le dans la liste sous les champs.

En plan, les faces sont coupées, l'assise, l'appui et la base dessinés sous la coupe et le toit en tirets au-dessus ; élévations, coupes et 3D montrent tout le corps. La nomenclature des fenêtres a une fiche pour le type, ses faces dépliées, avec une ligne **Projecting** (genre, saillie, faces) ; sa surface est celle des faces vitrées et ses détails mesurent le toit, l'assise et la base. En IFC c'est un seul IfcWindow à partitionnement défini par l'utilisateur (*Bay 45°, projection 600, 3 faces, 4 panels*) dont le corps est tout le bow-window, ses panneaux face par face ; Lapin relit le bow-window.

![Une fenêtre arrondie dans le configurateur](shots/bay-configurator.webp)

![Bow-window, fenêtre arrondie, boîte et oriel en plan](shots/bay-plan.webp)

![Bow-window, fenêtre arrondie, boîte et oriel en 3D](shots/bay-3d.webp)

### Poignées

**Poignée** choisit la poignée du type : **Béquille**, **Bouton**, **Barre de tirage**, **Poignée de fenêtre** ou aucune. Par défaut, une porte a une béquille et une fenêtre une poignée de fenêtre. Une porte a sa poignée sur les deux faces, à la **Hauteur de la poignée** indiquée ; **Couleur de la poignée** règle sa couleur. Les poignées se voient en 3D, en coupe et en élévation, et dans les dessins du configurateur quand **Quincaillerie** est cochée.

### Position du cadre et appuis

**Position in the wall** (position dans le mur) fixe où se trouve le cadre dans l’épaisseur du mur comme une valeur, et non seulement centré : choisissez **From the exterior finish**, **From the core** ou **From the wall’s centre**, puis **Set back**, de combien la face extérieure du cadre est en retrait de cette référence (depuis le centre, de combien le centre du cadre). *From the exterior finish* et 50 mm place le cadre à 50 mm derrière l’enduit. Le cadre, les embrasures, les appuis et les symboles d’ouverture suivent, en plans, coupes, 3D et IFC. Un type fait sans position reste **Centred in the wall**. Dans le panneau des propriétés, une porte ou une fenêtre a **Frame in the wall** pour se donner sa propre position (**As the type** l’abandonne) et **Set back** pour sa distance.

![Le configurateur avec le cadre à 50 mm derrière la finition extérieure](shots/joinery-position.webp)

L’**Exterior sill** (appui extérieur) a une **Slope** (pente) en degrés (5° au départ ; 15° pour un appui métallique). Un appui incliné s’éloigne du cadre en descendant et retire du mur tout ce qui le surmonte : l’enduit, le noyau et le plâtre sont coupés selon sa face supérieure inclinée, en 3D, en coupes et élévations et dans les quantités.

### Données : valeur U, résistance au feu, quincaillerie

**Data…**, sous le type dans le configurateur (et dans le panneau des propriétés, pour les portes et fenêtres sélectionnées), ouvre une boîte de texte libre : **U-value**, **g-value**, **Safety glass** (feuilleté côté intérieur, trempé des deux côtés…), **Fire rating** (EI 30, EI2 60-C5…) et **Fittings** ; **More data** ajoute les vôtres, par exemple *Sauna window* ou *Frosted glass*. Rien ne se choisit dans une liste. La valeur propre d’une porte ou d’une fenêtre l’emporte sur celle de son type ; un champ vide revient à celle du type (affichée en gris).

![La boîte de données d’un type de fenêtre](shots/joinery-data.webp)

Ce sont les paramètres du type : les nomenclatures de portes et fenêtres montrent chacun comme une ligne, les étiquettes les lisent comme `{U-value}`, `{g-value}`, `{Safety glass}`, `{Fire rating}` et `{Fittings}`, et l’IFC les conserve. La résistance au feu est `FireRating` de `Pset_WindowCommon` ou `Pset_DoorCommon` ; une valeur U lisible comme un nombre (0.9 ou *0,9 W/m²K*) y est aussi `ThermalTransmittance`, avec `GlazingAreaFraction` pour une fenêtre ; le reste est dans le jeu `Lapin_Joinery`.

![Les données propres d’une fenêtre dans le panneau des propriétés](shots/joinery-data-properties.webp)

### Paramètres

**Paramètres · dans les nomenclatures**, en bas du configurateur, contient les paramètres propres du type : **+ Résistance au feu** (rangée dans Pset_DoorCommon ou Pset_WindowCommon d’IFC comme FireRating), **+ Fabricant**, **+ Code produit**, **+ Site web**, ou tout paramètre que vous nommez. Les portes et fenêtres du type en héritent, et chacun devient une ligne des nomenclatures des menuiseries : voir [Nomenclatures et graphiques](schedules.md).

## Pare-soleil

**Architecture → Sunshade** (<kbd>SN</kbd>) pose un pare-soleil sur la porte ou fenêtre cliquée : à la taille de son ouverture, au-delà des tableaux, au-dessus du linteau ou devant, et il la suit quand elle bouge. **Sunshade along a facade** prend deux points : sur un mur il court le long de sa face extérieure, ailleurs le long de la ligne, vers sa droite. La barre des deux outils choisit le type, ou un style dont un nouveau type est fait.

Les genres :

- **Canopy** (auvent) : dalle, à lames ou en verre, avec saillie, épaisseur, inclinaison et bandeau, sur consoles, tirants ou poteaux ;
- **Vertical fins** : leur profondeur, épaisseur, espacement et angle, lisses haut et bas ;
- **Louvres (brise-soleil)** : lames horizontales, plates, profil aile ou caisson, leur profondeur, espacement et angle, écartées du mur, entre plaques d'extrémité ;
- **Egg-crate** : plaques horizontales et verticales ensemble ;
- **Perforated screen** : une tôle écartée du mur, à trous ronds ou carrés ou à fentes, leur taille et pas ;
- **Awning** (store banne) : toile en pente, lambrequin, coffre et bras articulés ;
- **Pergola** : chevrons, une poutre, lattes dessus et poteaux jusqu'au sol ;
- **External venetian blind** : lamelles, sa descente, son coffre et ses coulisses.

**Sunshade types** (dans Types) modifie un type : son genre et chacun de ses paramètres, ses matériaux, à côté de son dessin de l'extérieur, en coupe et en plan. Chaque pare-soleil du type suit une modification. Un nouveau type part d'un style : auvent en béton, en bois sur consoles, en verre sur tirants, à lames, marquise en tuiles, brise-soleil, ailettes verticales, grille alvéolaire en béton, écran perforé en Corten, store banne en toile, pergola en bois, brise-soleil orientable ; **Save as style…** garde un type comme style du projet.

![Les propriétés d'un auvent après le calcul de midi](shots/sunshade-properties.webp)

Sélectionné, un pare-soleil montre son type, sur quoi il est et sa propre **Projection** (vide : celle du type). **Full shade at noon, 21 June** calcule, pour un auvent, un store ou une pergola au-dessus d'une fenêtre, la saillie qui met toute la fenêtre à l'ombre quand le soleil est au plus haut le jour le plus long, au lieu du projet (**Manage → Location** ; Bucarest tant qu'aucun n'est fixé), et la fixe. Sur une façade sud à 45° N le soleil est alors à 68,4° : une fenêtre de 1,5 m sous un auvent à 150 mm au-dessus du linteau demande 650 mm. Ses poignées font glisser ses extrémités le long du mur ou de la ligne, et sa saillie.

![Les types de pare-soleil : lames, de l'extérieur, en coupe et en plan](shots/sunshade-types.webp)

Les plans dessinent en tirets un pare-soleil au-dessus de la coupe, coupé celui qui la traverse (ailettes, écran) et vu celui en dessous ; élévations, coupes et 3D montrent son corps avec la hachure de son matériau, et il porte son ombre avec le reste (**View → Sun and shadows**). **Sunshade schedule** les liste par type avec leur support, longueur et saillie, nombre et surface ombragée ; leurs quantités (longueur, saillie, surface, volume) prennent les articles de prix de leur type. En IFC ce sont des IfcShadingDevice d'un IfcShadingDeviceType : un store AWNING, un brise-soleil orientable JALOUSIE, les lames LOUVER en IFC 4.3 (définies par l'utilisateur en IFC4), les autres définis par l'utilisateur (Canopy, Fins, Egg-crate, Screen, Pergola) ; Lapin les relit tels qu'ils étaient.

![Pare-soleil en plan](shots/sunshades-plan.webp)

![Un auvent, des lames, des ailettes, un store, une pergola et un brise-soleil orientable en 3D](shots/sunshades-3d.webp)

## Étiquettes en plan

**Annoter → Étiquettes de portes et fenêtres** place un marqueur sur chaque porte et fenêtre d’un plan : une ligne en travers du mur, le repère à l’extérieur, et les dimensions et l’allège à l’intérieur. Pour changer l’aspect des étiquettes, allez dans **Gérer → Normes de dessin → Étiquettes**.

### Modifier un repère à l’écran

Chaque repère peut être disposé à la main, partie par partie. Cliquez sur sa ligne ou sur l’un de ses textes pour le sélectionner : chaque texte a alors une poignée carrée (glissez-la pour déplacer le texte) et une ronde après sa fin (glissez-la pour tourner le texte), et chaque extrémité de la ligne a une poignée (glissez-la pour allonger ou raccourcir la ligne ; le texte de cette extrémité suit). Ctrl/⌘-clic sur une poignée remet cette partie comme avant. Chaque glissement est un pas d’annulation. Avec plusieurs repères du même plan sélectionnés, une partie glissée sur l’un change la même partie de tous.

![Le repère d’une porte sélectionné : une poignée par texte, sa poignée de rotation et une à chaque extrémité de la ligne](shots/marker-grips.webp)

![Le repère déplacé et tourné, l’extrémité extérieure de la ligne allongée](shots/marker-edited.webp)

Les modifications appartiennent au repère, donc à ce plan : la même porte peut être disposée autrement dans un autre plan. Elles sont gardées le long et en travers du mur, si bien qu’elles suivent la porte quand elle se déplace, tourne ou passe de l’autre côté. Dans les propriétés du repère, **Repère** liste ses parties : tapez la rotation d’un texte en degrés ou l’avancée d’une extrémité en millimètres papier, remettez une partie avec ↺, ou toutes avec **Remettre l’étiquette en place**. Le moteur dessine les repères tels qu’ils sont modifiés, donc les planches, le PDF et le DXF les impriment pareil.

![Les parties d’un repère dans ses propriétés](shots/marker-properties.webp)

Que les textes soient en travers de la ligne du repère (le long du mur, par défaut) ou le long de celle-ci se règle pour chaque type d’étiquette, dans **Gérer → Normes de dessin → Étiquettes**, colonne **Direction du texte** : **perpendiculaire à la ligne d’étiquette** ou **le long de la ligne d’étiquette**. Dans les deux cas ils sont dessinés lisibles, jamais à l’envers ; un texte tourné à la main reste lisible aussi.

![Les textes du repère de la porte le long de la ligne d’étiquette](shots/marker-along.webp)
