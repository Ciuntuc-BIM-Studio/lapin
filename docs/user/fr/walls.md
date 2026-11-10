---
title: Murs
order: 20
summary: Dessinez les murs angle par angle, modifiez-les dans le panneau des propriétés et définissez des types de murs avec leurs couches.
---

<!-- video walls -->

## Dessiner des murs

Ouvrez l’onglet **Architecture** et choisissez **Mur**, ou tapez <kbd>WA</kbd>.

Tant que l’outil est actif, le panneau des propriétés montre **Nouvel élément** : le type avec lequel les murs sont dessinés. Choisissez-en un autre là avant le premier clic, et voyez ses couches, son épaisseur et combien de murs l’utilisent déjà ; **Modifier les types…** ouvre Wall types. Porte, fenêtre, dalle, faux plafond et mur-rideau montrent leurs types de la même façon.

![L’onglet Architecture](shots/ribbon-architecture.webp)

Cliquez là où le mur commence, puis cliquez à chaque angle. Le mur suivant suit la souris. Pendant que vous dessinez, vous pouvez taper une longueur et un angle dans les cases au-dessus de la vue au lieu de cliquer. Appuyez sur <kbd>Enter</kbd> ou <kbd>Esc</kbd> pour arrêter.

![Dessin de murs : le mur suivant suit la souris, avec sa longueur et son angle affichés](shots/wall-drawing.webp)

Là où deux murs se rencontrent, Lapin les joint de lui-même. Leurs couches se prolongent l’une dans l’autre comme elles seraient construites.

- **Mur courbe** (<kbd>WC</kbd>) dessine un mur le long d’un arc.
- **Mur-rideau** (<kbd>CW</kbd>) dessine un mur vitré avec des meneaux et des panneaux ; son type peut prendre leur forme réelle d'une [bibliothèque de profilés](profiles.md).
- Les murs se joignent d’eux-mêmes là où ils se rencontrent : un angle en onglet, un mur qui finit sur un autre en T, un mur qui en traverse un autre en X. À un T ou à un X, les couches se rejoignent comme on les construit : le noyau du mur qui finit traverse les finitions de l’autre jusqu’à son noyau, et ses finitions s’arrêtent à la face de l’autre, où elles rejoignent leurs semblables, si bien que le plan montre une seule composition, sans ligne au travers. **Joindre** (<kbd>JW</kbd>), **Onglet** et **Bout à bout** changent la façon dont deux murs se rencontrent quand ce n’est pas ce que vous voulez ; **Nouveaux angles en onglet** fait du bout à bout le réglage par défaut sur cet ordinateur. Dans les deux cas, chaque couche tourne l’angle d’elle-même : les finitions rejoignent leurs semblables et continuent sans rupture, et les âmes se rencontrent sur la diagonale (onglet) ou l’une va jusqu’à la face extérieure de l’autre (bout à bout), si bien que le plan des murs multicouches montre des angles nets. Chaque couche entre dans l'autre mur à travers ses couches de **Priorité** inférieure et s'arrête à la première de priorité égale ou supérieure (voir **Types de murs** plus bas) : par défaut, les noyaux rencontrent les noyaux et les finitions s'arrêtent aux faces ; à un angle, un noyau de priorité supérieure traverse celui de l'autre, en onglet comme en about. La **Priorité de coupe** d'un mur ne le fait jamais entrer dans un mur auquel il est joint : entre murs joints, elle décide seulement quel noyau traverse quand les deux noyaux ont la même priorité (à un angle en about ou à un croisement).
- **Permuter** change lequel des deux murs passe à travers l’autre.

**La ligne sur laquelle vous dessinez.** Tant que l’outil Wall est actif, la liste dans la barre de la ligne de commande choisit la ligne cliquée : **Axe du noyau** (l’axe, comme avant), **Noyau, face extérieure**, **Noyau, face intérieure**, **Face extérieure** ou **Face intérieure**. L’extérieur est la droite du mur dans le sens du tracé (tournez autour du bâtiment dans le sens antihoraire pour qu’il soit dehors). L’axe du mur reste sur son noyau, décalé de la ligne, et les murs tracés l’un après l’autre sur la même face se rejoignent à l’angle de cette face.

## Modifier un mur

Cliquez sur un mur pour le sélectionner. Ses poignées apparaissent. Faites glisser les extrémités pour l’allonger ou le raccourcir, ou faites glisser la croix au milieu pour le déplacer.

![Un mur sélectionné avec ses poignées](shots/wall-selected.webp)

Le panneau des propriétés affiche le mur sélectionné. Le haut du panneau contient son type et ses couches. En dessous se trouvent sa longueur, sa hauteur, son décalage de base, son niveau et sa phase. Changez une valeur et le modèle suit. Les valeurs de chaque mur sont les siennes. Son **épaisseur** (Thickness) vient des couches de son type : saisissez-en une nouvelle et les murs sélectionnés prennent le type de même composition et de cette épaisseur, le noyau redimensionné et les finitions conservées. Lapin prend le type du projet s’il en a un avec exactement cette composition, sinon il en crée un nommé d’après le leur (*Partition wall 130mm* devient *Partition wall 150mm*). Les autres murs de l’ancien type restent tels quels, et <kbd>Ctrl</kbd> <kbd>Z</kbd> annule le tout en une étape.

![Les propriétés d’un mur](shots/wall-properties.webp)

Plus bas, **Dépendances** montre ce qui est construit à partir du mur et ce à partir de quoi le mur est construit. **Utilisé par** liste les éléments qui changent quand le mur change : les murs qu’il raccorde, ses portes et fenêtres, un mur attaché à lui. Cliquez sur un nom pour sélectionner l’élément. **Construit à partir de** liste les éléments dont dépend le mur, puis son type et ses matériaux. Quand vous supprimez un élément dont d’autres dépendent, un message nomme ceux qui ont été reconstruits ; <kbd>Ctrl</kbd> <kbd>Z</kbd> ramène tout.

**Retourner** fait passer les couches du mur de l’autre côté. Vous pouvez aussi cliquer sur les petites flèches affichées à côté d’un mur sélectionné dans le plan.

Un mur reconnu depuis un IFC importé dont le fichier coupe l’extrémité en biais garde cette coupe : **Angle de coupe au début** et **Angle de coupe à la fin** dans ses propriétés, en degrés par rapport à l’équerre. Une jonction à cette extrémité la remplace.

**Alignement des hachures** règle la façon dont les couches de ce mur sont hachurées en plan. **material** suit le réglage de chaque matériau. **axis** pose la hachure de chaque couche le long de l’axe du mur, depuis le début de l’axe : un mur de briques en biais montre ses assises dans son sens, et elles se suivent d’une couche à l’autre. **view** part de l’origine de la vue pour chaque hachure, sans rotation.

### Un mur selon son profil

**Profil…** dans les propriétés d’un mur dessine son sommet en élévation, vu de son début à sa fin : un pignon, une pente, des redans, un mur qui suit un escalier. Faites glisser les points (**Maj** garde la hauteur d’un point), cliquez sur la ligne du haut pour en ajouter un, faites un clic droit sur un point pour l’enlever, ou tapez-les dans le tableau en millimètres. **Partir de** donne un **Pignon**, une **Pente** ou des **Gradins** jusqu’à la hauteur que vous tapez. Un redan, c’est deux points l’un au-dessus de l’autre. **Appliquer** donne sa forme au mur ; **Sommet plat** le rend de nouveau plat.

Le mur reste un mur : son épaisseur, ses couches, ses jonctions et ses portes et fenêtres sont comme avant, et il s’exporte en IFC avec sa forme.

![Un pignon dessiné dans l’éditeur de profil](shots/wall-profile-editor.webp)

![Le mur ouest de la maison en pignon, sa fenêtre gardée](shots/wall-profile-3d.webp)

### Le contour et les barres d’un mur-rideau

**Profil…** dans les propriétés d’un mur-rideau dessine son contour en élévation, comme pour un mur : un pignon, une pente, des gradins ou un contour quelconque, avec des points à faire glisser, à ajouter ou à retirer et un tableau en millimètres. Le mur monte jusqu’à son point le plus haut. Sa trame gagne une ligne là où le contour se brise (une brisure à moins d’un meneau et demi d’une ligne de la trame est ramenée sur elle) ; chaque travée garde les traverses qui tiennent sous son point le plus bas ; le panneau au-dessus de la dernière monte jusqu’au contour et une barre de rive le suit, inclinée ; les meneaux sont coupés à lui. Les réglages du type restent, et **Profil…** rouvre le contour pour le changer, sans reconstruire ; **Sommet plat** rend le rectangle. Il se voit en 3D, élévations et coupes, les nomenclatures mesurent les panneaux et les barres tels que dessinés, et l’IFC les porte comme pièces.

![Un pignon dessiné comme contour d’un mur-rideau](shots/curtain-boundary-editor.webp)

![Le mur-rideau sous son pignon, en 3D](shots/curtain-boundary-3d.webp)

**Barres…** donne à des meneaux et traverses choisis un profil propre. L’élévation du mur montre chaque barre : cliquez-en une (<kbd>Maj</kbd> ou <kbd>Ctrl</kbd>/<kbd>⌘</kbd> en ajoute, ou prenez tous les meneaux ou toutes les traverses), choisissez un profil des [bibliothèques de profilés](profiles.md) ou une boîte simple, sa **Rotation** et sa **Symétrie**, son alignement en travers de la barre (face avant ou arrière sur son plan) et le long (arête de début ou de fin sur la ligne de trame ou le niveau) et ses décalages en travers et vers l’extérieur du bâtiment ; l’image à côté montre la section telle qu’elle est posée. **Appliquer aux éléments choisis** leur donne le style, **Revenir à celui du type** l’enlève. Le profil propre d’un type a les mêmes champs de position dans **Murs-rideaux…**, pour chaque mur du type. Le profilé reste une copie dans le projet : **Mettre à jour depuis la bibliothèque** change chaque barre qui en est faite, dans ce mur et dans d’autres.

![Barres : un profil posé sur les meneaux choisis](shots/curtain-members-dialog.webp)

![Deux meneaux avec un profil propre, en 3D](shots/curtain-members-3d.webp)

**Panneaux et portes…** donne aux cellules choisies un panneau à elles ou une porte. L'élévation du mur montre chaque cellule telle que le moteur la construit : cliquez sur une cellule (<kbd>Maj</kbd> ou <kbd>Ctrl</kbd>/<kbd>⌘</kbd> en ajoute d'autres). Un **type de panneau** est enregistré une fois dans le projet et donné aux cellules de n'importe quel mur-rideau : verre, plein, un **remplissage encadré** (un cadre de sa largeur et de sa profondeur autour de la cellule, le remplissage en verre ou plein en retrait de sa face extérieure selon le retrait) ou **ouvert** (sans panneau) ; son épaisseur, son décalage hors de l'axe et ses matériaux (ceux du type de mur-rideau si aucun n'est choisi). **Donner aux cellules choisies** l'applique, **Le panneau du type** le retire ; un type de panneau modifié change toutes ses cellules. **Porte dans les cellules choisies** remplace chaque cellule choisie par une porte : une nouvelle porte de mur-rideau (simple ou double, battante ou coulissante, sa largeur et sa hauteur, vantaux vitrés ou pleins, le profil du cadre tiré des [bibliothèques de profils](profiles.md)) ou n'importe quel type de porte du projet. **Remplir la cellule** lui donne la taille de la cellule ; sinon elle garde celle de son type, se place à gauche, au centre ou à droite dans la cellule avec un décalage, et reçoit une traverse au-dessus et un meneau à côté là où il reste du verre, vitrés avec le panneau de la cellule. Dans la rangée du bas, la porte repose sur le sol et la traverse basse est coupée sous elle. Les réglages sont gardés par cellule, ils suivent donc la trame quand elle change : une porte qui remplit sa cellule grandit ou rapetisse avec elle, et une dont la cellule a disparu le signale et revient avec elle. La porte est une porte : sélectionnez-la pour changer sa main ou son côté, son type, ou la déplacer dans une autre cellule (Déplacer le long du mur) ; les étiquettes, les nomenclatures de portes (à la taille où elle est) et l'export IFC (une IfcDoor faisant partie de son IfcCurtainWall) la comptent.

![Panneaux et portes : une allège encadrée et une porte dans les cellules choisies](shots/curtain-cells-dialog.webp)

![Un mur-rideau avec une porte et des allèges encadrées, en 3D](shots/curtain-cells-3d.webp)

Sous un contour dessiné avec **Profil…**, une porte va dans une cellule rognée partout où son rectangle tient sous le contour : les meneaux à côté d'elle montent jusqu'au contour, les vitrages à côté d'eux s'y remplissent, une traverse franchit sa tête et le verre au-dessus suit la pente. Quand elle remplit une telle cellule, sa tête s'arrête une traverse sous le côté le plus bas du contour. Là où elle ne tient pas, Lapin dit de combien et quoi faire (l'abaisser, la déplacer vers le côté le plus haut ou choisir une cellule plus haute). Un panneau encadré dans une cellule rognée est encadré le long de tout son contour, côté incliné compris. Une élévation tournée vers l'extérieur d'une porte dessine ses symboles d'ouverture, en tirets, comme pour toute porte ou fenêtre.

![Une porte sous le contour incliné d'un mur-rideau, avec des panneaux encadrés le long de la pente, en 3D](shots/curtain-cells-gable.webp)

![Le symbole d'ouverture de la porte dans l'élévation](shots/curtain-cells-elevation.webp)

## Types de murs

Un type de mur est un ensemble de couches, de la face à droite du sens dans lequel le mur est tracé (l’extérieur, si vous tracez autour d’un bâtiment dans le sens inverse des aiguilles d’une montre) à la face à sa gauche. Chaque couche a un matériau et une épaisseur, et une couche est le **noyau**. La ligne du mur suit le noyau. Pour modifier les types, choisissez **Architecture → Types de murs**. Une couche autre que l’âme peut avoir sa propre hauteur : **De** et **Jusqu’à** règlent jusqu’où elle monte au-dessus du pied du mur (une faïence jusqu’à 1500, un bardage à partir de 300) ; vide, elle court sur toute la hauteur.

La colonne **Priorité** (de 0 à 100) indique comment la couche rencontre les couches d'un autre mur là où les murs se joignent : elle traverse les couches de l'autre de priorité inférieure et s'arrête à la première de priorité égale ou supérieure. Vide, la couche prend la priorité de son matériau dans **Gérer → Ordre de coupe**, sinon 50 pour le noyau et 0 pour les autres couches (le nombre affiché en grisé). Ainsi, un noyau en béton de 100 n'est pas interrompu par une ossature métallique de 99 et traverse la cloison, et un enduit dur de 60 arrête le noyau d'un mur qui vient s'y terminer. L'export IFC l'écrit comme la Priority de la couche, et l'import la relit.

La colonne **Note** dit comment la couche est posée dans ce type (*en couche continue, Baumit SupraFix*) : elle appartient au type, pas au matériau, et une étiquette de composition l’écrit après la couche ; l’IFC la porte comme Description de la couche. Quand le matériau de la couche a des épaisseurs de catalogue (**Gérer → Matériaux → Données**), la petite flèche à côté de l’épaisseur les propose et un signe d’avertissement marque une couche d’une autre épaisseur.

Une couche autre que le noyau peut aussi changer de matériau sur sa hauteur : le bouton **couches** au bout de sa ligne ajoute un morceau (carrelage, 2100 par défaut), depuis le bas de la couche vers le haut ; ajoutez-en d’autres pour une bande (peinture jusqu’à 900, carrelage jusqu’à 1500), et au-dessus du dernier morceau la couche continue dans son propre matériau jusqu’en haut. Chaque morceau est dessiné, coupé en plan et en coupe, et métré dans son propre matériau.

Une couche autre que le noyau peut avoir une épaisseur de 0, pour un matériau trop fin pour être modélisé mais qu’il faut métrer : une étanchéité, un pare-vapeur, une peinture. Elle ne forme aucun solide ; les plans et les coupes la dessinent en trait épais discontinu là où elle se trouve entre deux couches (sur une face, le trait de la face est le sien), le dessin des Compositions des types la nomme comme les autres, et elle est métrée en surface : la face de la couche sur laquelle elle repose, ouvertures déduites, dans les quantités du mur et dans le métré par matériau.

![Une couche d’enduit avec du carrelage jusqu’à 2100 et l’enduit au-dessus](shots/wall-layer-parts.webp)

![Types de murs : les couches d’un mur en brique](shots/wall-types.webp)

La légende au-dessus des couches montre le mur en plan comme le montre un mur sélectionné : l’axe en tirets avec une pointe de flèche dans le sens où il a été tracé, **D** sur sa face droite et **G** sur sa face gauche. Les couches sont numérotées de 1, sur la face D, à la dernière, sur la face G, avec les mêmes numéros que les lignes du tableau ; le noyau, qui porte la ligne du mur, est mis en évidence. Un mur retourné avec **Retourner** a ses couches dans l’autre sens, de G à D. Le message d’un enregistrement refusé disparaît dès que vous modifiez à nouveau le type.

- **+ Couche** ajoute une couche. Les flèches montent ou descendent une couche.
- **Zones en hauteur** divise le noyau de bas en haut, par exemple une première assise, la maçonnerie et un chaînage.
- **Linteaux** choisit les linteaux posés au-dessus des portes et des fenêtres : préfabriqués ou coulés en place, avec leur appui et leur hauteur.
- **Dessiner avec** ferme la boîte de dialogue et commence à dessiner des murs de ce type.

Un changement apporté à un type change chaque mur de ce type.

**Import…**, **Export CSV** et **Export JSON**, sous la liste des types, font passer les types de murs et de dalles d’un projet à un autre, ou vers un tableur et retour : le CSV a une ligne par couche (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), les lignes d’un type à la suite, et les points-virgules et virgules décimales d’Excel sont lus aussi. À l’import, un type du même nom prend les couches du fichier et les autres sont créés, en une seule étape à annuler ; un matériau que le projet n’a pas est créé d’après son nom. La nomenclature **Compositions des types** (Schedules → + → Type assemblies) liste chaque type de mur et de dalle avec son repère, son épaisseur totale, le nombre d’éléments qui l’utilisent et ses couches dans l’ordre.

## Moulures et engravures de mur

**Architecture → Moulure de mur** (<kbd>WB</kbd>) fait courir un profil le long de la face d'un mur : une corniche, un bandeau, un soubassement, un appui. **Engravure de mur** (<kbd>RV</kbd>) le creuse dans le mur : une rainure, un joint creux. Choisissez le profil et la hauteur dans la barre de la ligne de commande, puis cliquez sur la face du mur. En 3D la moulure se place à la hauteur cliquée ; en plan, à la hauteur saisie, du côté cliqué. **depuis le haut** mesure la hauteur vers le bas depuis le sommet du mur, pour une corniche sous l'avant-toit. **Moulure sur les murs sélectionnés** et **Engravure sur les murs sélectionnés** posent la même chose sur chaque mur sélectionné, sur sa face extérieure.

Une moulure court sur toute la longueur de la face et s'interrompt aux portes et fenêtres qu'elle traverserait. Là où deux murs se rencontrent en angle et portent tous deux une moulure du même profil à la même hauteur sur les faces qui se rejoignent, les deux se rejoignent en onglet, une seule moulure qui tourne l'angle. Sélectionnez une moulure pour la modifier dans les Propriétés : son profil, son matériau, sa face (**Changer de côté**), sa hauteur, son décalage par rapport à la face, où elle commence et finit le long du mur, et si elle s'interrompt aux ouvertures. Les propriétés d'un mur listent les moulures et engravures qu'il porte.

Moulures et engravures suivent leur mur quand il est copié, déplacé vers un autre niveau ou supprimé. Elles sont métrées en longueur et en volume (pour une engravure, le volume retiré). En IFC, une moulure est un IfcCovering MOLDING et une engravure un IfcOpeningElement RECESS dans son mur ; les deux reviennent telles qu'elles étaient.

![Corniches, bandeaux et une engravure sur les murs extérieurs d'une maison](shots/wall-sweeps-3d.webp)

### Moulures sur le type de mur

Une moulure que tous les murs d'un type doivent porter (un bandeau de soubassement, une corniche, un joint creux) appartient au type. Dans **Architecture → Types de murs**, sous les couches, **Moulures et engravures** les liste, une ligne chacune : le profil, **Moulure de mur** ou **Engravure de mur**, la face (D, la face de la première couche du type, ou G, comme la légende les nomme), la hauteur au-dessus de la base du mur (ou sous son arase avec **Depuis le haut**), le décalage vers l'extérieur de la face, le matériau d'une moulure et ses retours. **Moulure de mur** et **Engravure de mur** sous le tableau ajoutent une ligne. Enregistrez le type : tous ses murs les reçoivent, ainsi qu'un mur dessiné avec ce type ou changé pour lui. Ce sont des moulures comme les autres : métrées, dans la nomenclature des moulures de mur, coupées d'onglet aux angles où des murs du type se rencontrent, écrites en IFC. Les moulures d'un mur retourné changent de face avec ses couches. **Exporter en JSON**, sous la liste des types, les emporte, avec le contour des profils et les matériaux par leur nom (le CSV n'a que les couches).

Sélectionnez-en une sur un mur : les Propriétés indiquent **Du type**. Si vous changez sa hauteur, son décalage ou un autre de ses champs, ce champ devient propre au mur (**Propres :** hauteur) : les changements ultérieurs du type ne le touchent plus. **Revenir au type** lui redonne les valeurs du type. **Désactiver sur ce mur**, ou la supprimer, laisse la moulure du type désactivée sur ce mur ; **Réactiver les moulures du type**, dans **Moulures et engravures** du mur, les ramène.

![Une moulure sur le type de mur, dans la boîte de dialogue Types de murs](shots/wall-type-sweeps.webp)

### Retours

Une moulure qui atteint une extrémité libre de son mur (aucun mur n'y est joint) fait retour : coupée d'onglet à l'extrémité, son profil tourne l'angle sur l'about du mur et s'arrête dans le plan de la face, si bien que la moulure est fermée au lieu de montrer sa coupe. **Retours aux extrémités libres**, dans les Propriétés, choisit les deux (par défaut pour une moulure), au début, à la fin ou aucun, pour des extrémités droites. Une engravure ne fait jamais retour. À un angle joint à un autre mur, une moulure ne fait pas retour : elle va jusqu'à l'angle, ou rejoint une moulure semblable sur l'autre mur. Aux portes et fenêtres elle s'arrête droit ; cochez **Retours aux ouvertures** pour qu'elle fasse aussi retour autour des tableaux. **Retrait depuis le début** et **Retrait depuis la fin** l'arrêtent avant les extrémités de la face (droit à cet endroit), et **Profil retourné** retourne son profil.

### Déplacer et copier une moulure

Sélectionnez une moulure et utilisez **MOVE** ou **COPY** comme pour tout élément ; son mur ne bouge pas. Ce que le déplacement parcourt le long du mur la fait glisser sur celui-ci (une moulure sur toute la face reçoit alors son propre début et sa propre fin) ; ce qu'il parcourt en travers du mur est ignoré. Déplacée vers le haut ou vers le bas dans une élévation ou une coupe, sa hauteur change. Une copie arrive sur le même mur, décalée, ou sur un autre mur quand elle tombe dedans, sur la face où elle tombe. **ROTATE** et **MIRROR** la gardent sur son mur : en miroir par rapport à l'axe du mur, elle passe sur l'autre face. Une moulure du type que vous déplacez garde sa nouvelle place comme propre ; déplacée sur un autre mur, elle devient une moulure propre de ce mur et celle du type reste désactivée sur le premier.

### Profils

**Gérer → Profils** contient les profils du projet, les contours selon lesquels sont balayés les moulures, les engravures et les semelles filantes. **Nouveau profil** part du catalogue avec ses dimensions : un rectangle, un bandeau chanfreiné, une corniche, un demi-rond, un nez d'appui, un bandeau avec goutte d'eau ; une rainure en V, rectangulaire ou ronde ; pour les semelles, rectangulaire, à 2 ou 3 redans, à dessus incliné, excentrée, avec voile. Ou choisissez **Sommets saisis** : chaque sommet u v en millimètres, séparés par un point-virgule, un troisième nombre faisant du côté qui part de ce sommet un arc. **À partir d'un contour dessiné** lit une polyligne fermée ou un rectangle dessiné avec Dessin et sélectionné dans sa vue. Le cercle de l'aperçu est le point d'insertion : pour une moulure, il est sur la face du mur à la hauteur de la moulure, u vers l'extérieur de la face (dans le mur pour une engravure). Modifiez un profil, et tout ce qui l'utilise suit.

![La boîte de dialogue Profils](shots/profiles.webp)

### Bardages

Modélisez les bardages avec les couches du mur lui-même, pas avec des moulures. Ajoutez une couche de finition à l'extérieur du type de mur (pierre, bardage bois, fibres-ciment, une ITE), avec son épaisseur et son matériau. **De** et **Jusqu’à** lui donnent une hauteur propre (un soubassement en pierre jusqu'à 600, de l'enduit au-dessus), et le bouton **couches** de la couche change son matériau par bandes sur sa hauteur. Pour un bardage qui se tient à distance du mur, dessinez un mur à part sur la face (l'option de ligne **Face extérieure** de l'outil mur) avec un type fait des couches du bardage. Le bardage posé lame par lame (lames verticales ou horizontales, tasseaux) n'est pas encore modélisé.

## Murs sous un toit

Les murs du même niveau qu’un toit s’arrêtent d’eux-mêmes dessous et montent jusqu’à ses pignons. Pour un mur d’un autre niveau, sélectionnez-le et choisissez **Architecture → Attacher au toit** (<kbd>AR</kbd>), puis cliquez le toit — ou tout autre corps jusqu’auquel le mur doit monter : un solide Studio, un élément importé. Voir [Dalles et toits](floors-roofs.md).
