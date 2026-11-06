---
title: Réseaux de fluides
order: 57
summary: Dessinez les réseaux de chauffage, d’eau, d’évacuation, d’électricité et de ventilation en schémas sur le plan, avec leurs sources, leurs terminaux et les tronçons qui les relient, et exportez-les en réseau IFC.
---

L’onglet **Fluides** dessine les installations du bâtiment comme un ingénieur les esquisse : un système par installation, sa **source** (une chaudière, un compteur d’eau, une colonne de chute, un tableau électrique, un ventilateur), ses **terminaux** (radiateurs, lavabos, WC, prises, luminaires, diffuseurs) et les **tronçons** qui les relient. Chaque nœud est un véritable élément IFC avec ses ports ; chaque tronçon est un segment de tuyau, de gaine ou de câble ; les connexions tiennent quand vous déplacez les éléments.

![L’onglet Fluides](shots/ribbon-mep.webp)

## Un système en un clic

Sous **Nouveau système**, choisissez l’installation (**Chauffage**, **Eau**, **Eau chaude**, **Évacuation**, **Électricité**, **Ventilation**) et cliquez dans le plan. Lapin place le système entier : la source là où vous avez cliqué, un premier terminal à quelques mètres, et le tronçon qui les relie. Le système s’ouvre en modification.

![Un système de chauffage sur le plan du rez-de-chaussée : la chaudière, deux radiateurs sous les fenêtres et les tronçons qui les relient](shots/mep-plan.webp)

En mode modification, tout ce qui n’appartient pas au système est estompé, et seuls les nœuds et les tronçons du système peuvent être sélectionnés. Faites glisser la source et le terminal à leur place : leur tronçon suit. Ajoutez des terminaux avec **+ Radiateur**, **+ Lavabo**… dans le panneau des propriétés, ou avec les outils sous **Chauffage**, **Plomberie**, **Électricité** et **Air** : chaque nouveau terminal rejoint le système par un tronçon qui lui est propre, depuis le port libre le plus proche de la source, ou depuis le tronçon le plus proche (un té). **Terminé** (ou <kbd>Esc</kbd>) quitte le mode modification et vous indique combien de coudes et de tés compte le système et ce qui reste ouvert. **Modifier le système** (<kbd>ES</kbd>) le rouvre.

## Tronçons

**Tronçon** (<kbd>RN</kbd>) dessine un tronçon point par point ; appuyez sur <kbd>Enter</kbd> pour terminer. Une extrémité qui tombe sur un port libre s’y connecte ; une extrémité qui tombe sur un tronçon du même système crée une dérivation.

**Connecter** relie l’extrémité d’un tronçon à un nœud ou à un autre tronçon ; **Déconnecter** la libère. **Ports libres** sélectionne tout ce qui n’est pas encore connecté.

## Remodeler un tronçon

Sélectionnez un tronçon : ses poignées sont posées sur la ligne telle qu’elle est dessinée, même après que vous avez déplacé les nœuds qu’il relie.

- **Faites glisser une poignée** pour déplacer librement cet angle.
- **Faites glisser le milieu d’un segment** pour le faire coulisser latéralement ; les segments voisins s’allongent, de sorte qu’un tracé à angles droits reste à angles droits. Faites coulisser le premier ou le dernier segment, et Lapin y ajoute un décrochement, pour que l’extrémité reste sur son port.
- **Ctrl-clic** sur une poignée la retire, sur le milieu d’un segment en ajoute une ; **Ajouter une poignée** en place une là où vous cliquez.
- **Dévoiement** (<kbd>DU</kbd>) fait contourner un obstacle à un tronçon : cliquez sur le tronçon là où le dévoiement commence, cliquez là où il finit sur le même segment, puis déplacez-vous du côté où il passe et cliquez (ou tapez la distance). Le tronçon sort, longe et revient, avec quatre nouveaux angles ; il garde ses connexions.
- **Redessiner le tracé** (<kbd>RP</kbd>) donne un nouveau tracé à un tronçon : sélectionnez-le (ou commencez dessus) et dessinez le tracé point par point, <kbd>Enter</kbd> pour terminer. Le tronçon garde son système, sa taille et ses connexions, et ses extrémités reviennent sur leurs ports.

![Un tronçon de radiateur qui contourne un obstacle avec Dévoiement : il sort, longe et revient](shots/mep-detour.webp)


## Nœuds

Chaque nœud a un **rôle** : une chaudière, un radiateur, un collecteur, un compteur d’eau, un lavabo, un évier, un WC, une douche, une baignoire, une colonne de chute, un tampon de dégorgement, un tableau électrique, une prise, un luminaire, un interrupteur, un ventilateur, un diffuseur, une grille d’extraction, une vanne, une montée / descente, un bouchon. Le rôle lui donne sa classe IFC, ses ports, sa taille et son symbole sur le plan. Une vanne, un compteur ou un interrupteur déposé sur un tronçon le scinde et se place au milieu. Une **montée / descente** mène un tronçon à une autre altitude ou au niveau supérieur.

![Les propriétés d’un radiateur : son système, son rôle et ses ports, sa taille et sa puissance](shots/mep-radiator-properties.webp)

Dans le panneau des propriétés, un nœud montre son système, son rôle et ses ports (libres ou connectés) ; un tronçon montre ses deux extrémités. Les deux prennent les valeurs par défaut du système, sauf si vous leur donnez une valeur : la taille, l’altitude, le plan dans lequel ils passent (sol, plafond, mur), en paire ou simple, le matériau, la pente.

## Systèmes

**Systèmes…** liste les systèmes du projet et les valeurs par défaut du système courant : la taille, le plan et les altitudes de chaque plan, la pente (évacuation), si les tronçons sont dessinés en paire (chauffage : aller et retour) et à quel écartement, le matériau et l’isolation. Modifiez une valeur par défaut, et chaque nœud et chaque tronçon sans valeur propre la suit.

![La boîte de dialogue Systèmes](shots/mep-systems.webp)

## Plans, listes et IFC

**Plan des fluides** crée un plan de la discipline Fluides pour le niveau, rangé dans l’ensemble Fluides de l’arborescence. La **Liste des équipements** et la **Liste des tronçons** sous **Système** sont des nomenclatures comme les autres : elles se placent sur les feuilles et s’exportent avec elles.

Dans un export IFC, chaque système est un IfcDistributionSystem, chaque nœud et chaque tronçon a ses IfcDistributionPorts, et les connexions sont écrites entre les ports, de sorte que le réseau arrive dans un autre logiciel connecté de la source jusqu’au dernier terminal.

## Gaz et fioul

**Gaz** et **Fioul** sont des services à part sous **Nouveau système**. Un système de gaz dit son gaz (**Gaz naturel** ou **GPL**) et sa **Classe de pression** (basse dans la maison, moyenne dans la rue) dans **Systèmes…** ; ses tubes sont par défaut en acier noir sur les murs à 2,10 m, et peuvent être en cuivre, multicouche ou **PE 100** pour ce qui est enterré. Le fioul passe en cuivre.

L’installation de gaz d’une maison part du **coffret de comptage** sur la façade. Clique **Nouveau système → Gaz** : le coffret va où tu cliques et une **chaudière gaz** à l’intérieur, reliés par une conduite. En chemin, les outils de **Gaz** posent sur la conduite le **détendeur de gaz**, le **compteur de gaz**, le **robinet général de gaz**, les **robinets d’appareil** et les **raccords flexibles**, qui la coupent comme tout raccord ; la **cuisinière à gaz** et le **chauffe-eau à gaz** rejoignent l’installation par une conduite à eux. Là où un tube de gaz ou de fioul traverse un mur, il passe dans un **fourreau**, dessiné et compté avec la conduite. Une chaudière gaz dessinée dans le système de chauffage comme sa source rejoint l’installation de gaz depuis ses propriétés (le bouton **+** avec le nom du système de gaz).

![L’installation de gaz d’une maison : le coffret, le détendeur et le compteur dehors, la chaudière et la cuisinière dans la cuisine](shots/mep-gas-plan.webp)

Chaque appareil garde sa **consommation** (m³/h) et son **type d’appareil** selon son évacuation (A sans conduit, B raccordé, C étanche), pour les listes et le dimensionnement.

**Fioul** pose une **cuve à fioul** (intérieure, extérieure ou enterrée) et une **chaudière fioul** ; la **bouche de remplissage** et le **chapeau d’évent** rejoignent le remplissage et l’évent de la cuve, et le **filtre à fioul** se place sur l’aller.

### L’alimentation dans la rue

Sous **Rue**, **Conduite de gaz le long de…** (<kbd>GM</kbd>) pose une conduite de gaz le long de ce que tu cliques — une route, un axe, une ligne dessinée, un mur — à la **distance à la ligne** et à la **profondeur (couverture)** de la barre de la ligne de commande ; sous un terrain la profondeur se prend depuis le sol, sinon depuis le niveau. **Conduite de gaz** la dessine point par point. La conduite va dans un système de gaz moyenne pression, en tube PE, avec son **grillage avertisseur**.

**Branchement** (<kbd>GS</kbd>) : clique la conduite, puis le coffret. Lapin pose une **prise de branchement** sur la conduite, pose le tube de branchement d’équerre à sa profondeur jusqu’au coffret, et y remonte dans le coffret.

![Le branchement de la conduite de la rue au coffret, sélectionné](shots/mep-gas-street.webp)

![La conduite et le branchement sous terre, l’installation sur la façade](shots/mep-gas-3d.webp)

![Les propriétés d’un branchement : de la prise de branchement au coffret, sa couverture et son diamètre](shots/mep-gas-service-properties.webp)

![Les valeurs par défaut d’un système de gaz : son gaz et sa classe de pression](shots/mep-gas-systems.webp)

**Métré gaz et fioul** sous **Système** liste, système par système, les tubes par matériau et diamètre avec leur longueur (et dedans les colonnes montantes), les coudes, tés et fourreaux, le grillage avertisseur, les robinets, compteurs et coffrets, et les appareils avec leur consommation et leur puissance installées. En IFC un système de gaz est un IfcDistributionSystem de type GAS et un système de fioul FUEL ; le coffret est un IfcDistributionChamberElement, le compteur un IfcFlowMeter GASMETER, les robinets des IfcValve, la cuisinière un IfcBurner, la cuve un IfcTank.

![Le métré gaz et fioul](shots/mep-gas-list.webp)

Dimensionner les tubes d’après la consommation des appareils et vérifier la perte de charge relèvent de l’ingénierie, dans l’édition **Lapin MEP**.

## Extracteurs

Le groupe **Extracteurs** de l’onglet Fluides place les extracteurs de salle de bains et de cuisine comme éléments à part entière, chacun d’un type que vous configurez :

- **Extracteur mural** (<kbd>VF</kbd>) : un ventilateur hélicoïde sur un mur, qui souffle directement à travers lui ;
- **Extracteur de plafond** : un ventilateur sous le plafond avec une gaine sortant de son côté ;
- **Extracteur en ligne** : un ventilateur tubulaire dans le plénum, sur la gaine ;
- **Extracteur de fenêtre** : un ventilateur posé dans le vitrage d’une fenêtre ;
- **Extracteur de plafond avec éclairage** : un extracteur de plafond avec une lumière dans sa grille ;
- **Extracteur avec panneau à lames** : un panneau mural avec des lames à côté du ventilateur et un cache dessous.

Choisissez un outil et cliquez. Un extracteur mural, de fenêtre ou à panneau à lames se pose sur la face du mur la plus proche du clic (à moins de 600 mm), sa face avant vers la pièce, son centre à 2,1 m du sol (un extracteur de fenêtre à 1,9 m). Un extracteur de plafond se pose où vous cliquez, sous le plafond de la pièce ; un extracteur en ligne 250 mm au-dessus. La barre de la ligne de commande choisit le type que l’outil pose, parmi les types du projet ou ceux livrés avec Lapin (un extracteur mural Ø100 avec sonde d’humidité, un extracteur de plafond avec éclairage et détecteur de mouvement, un extracteur en ligne pour deux salles de bains…).

Un extracteur mural perce une ouverture ronde dans son mur, 20 mm plus large que sa gaine : la réservation du mur, comme celle d’un tronçon de fluides (elle apparaît en plan comme un volume de réservation et passe dans l’IFC comme ouverture du mur). Elle est trouvée d’après l’emplacement de l’extracteur : un extracteur déplacé le long du mur emporte son ouverture. Un extracteur de fenêtre est dans le vitrage et ne perce rien.

![Une salle de bains avec un extracteur mural, un extracteur de plafond avec éclairage gainé vers l’extérieur à travers le mur, et un extracteur avec panneau à lames](shots/fans-plan.webp)

Les plans dessinent un extracteur par son symbole fluides : un cercle avec ses pales, sa face sur le mur, son fourreau en tirets à travers le mur et une flèche dans le sens de l’air ; la grille d’un extracteur de plafond en tirets, puisqu’elle est au-dessus de la coupe. Les plans de plafond, les coupes, les façades et la 3D dessinent les corps.

**Gaine vers la sortie** évacue l’air d’un extracteur de plafond ou en ligne : cliquez sur l’extracteur, puis là où la gaine sort. La gaine est un tronçon du réseau **Bathroom extract** du projet (créé avec la première gaine ; il perce les murs qu’il traverse), aussi large que la gaine de l’extracteur, à la hauteur de son piquage. Elle se termine par une grille sur la face extérieure du mur ou, avec **Par la toiture** activé dans la barre, par un conduit montant jusqu’à un chapeau. Déplacé ou relevé, l’extracteur emporte le début de la gaine.

![Les extracteurs en 3D : la gaine jusqu’à la grille extérieure, la grille de l’extracteur mural en façade](shots/fans-3d.webp)

### Types d’extracteurs

**Types d’extracteurs** ouvre les types : ceux du projet, et ceux livrés avec Lapin, à ajouter d’un clic. Un type définit :

- son **montage** (ceux que son genre admet) et son **diamètre de gaine** (Ø100, 125, 150 ou 200 mm) ;
- la **forme** de sa grille (ronde ou carrée), sa **taille**, son **motif** (lames, fentes, anneaux ou lisse) et sa **finition** (blanc, argent, noir, inox ou chrome) ;
- son **débit** (m³/h) et sa **puissance** (W) ;
- sa **commande** : un interrupteur, une sonde d’humidité, une temporisation, les deux, ou un détecteur de mouvement ;
- un **clapet anti-retour**, et l’**éclairage** d’un extracteur de plafond, la longueur du corps d’un extracteur en ligne, la largeur et la hauteur d’un panneau à lames, affichés seulement là où ils s’appliquent.

L’aperçu à côté est celui du moteur : le symbole en plan en haut, la face en bas. Tous les extracteurs d’un type suivent une modification.

![Les types d’extracteurs avec l’aperçu d’un panneau à lames](shots/fans-types.webp)

Sélectionnez un extracteur pour changer son type, sa **hauteur au-dessus du sol**, sa rotation et, dès qu’il a une gaine, sa **sortie** (mur ou toiture) et de combien la sortie dépasse la gaine. Faites glisser la poignée en son centre pour le déplacer, celle devant lui pour le tourner.

![Les propriétés d’un extracteur de plafond](shots/fans-properties.webp)

### Nomenclatures, coûts et IFC

La **Nomenclature des extracteurs** liste chaque extracteur avec sa pièce, son genre, sa taille, son débit, son montage, sa commande, sa hauteur et sa sortie, et le débit total. La **Vérification de l’extraction par pièce** liste chaque salle de bains, WC et cuisine (et chaque pièce avec un extracteur) avec son volume, l’extraction nécessaire et ce que donnent ses extracteurs : une salle de bains 8 renouvellements d’air par heure et au moins 54 m³/h, un WC 6 et 25 m³/h, une cuisine 6 et 108 m³/h. Ce sont des valeurs indicatives ; vérifiez-les avec la norme suivie par votre projet.

Un nouveau type porte l’article de coût `LAP-FAN`, compté par extracteur (Coûts → Affecter les articles… le change).

L’IFC écrit un extracteur comme **IfcFan** d’un **IfcFanType** avec son type prédéfini (PROPELLORAXIAL pour un extracteur mural ou de fenêtre, CENTRIFUGALRADIAL pour un extracteur de plafond, TUBEAXIAL pour un extracteur en ligne) et **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate) ; l’ouverture du mur comme IfcOpeningElement. Lapin les relit comme ses propres extracteurs.

## Coordination : volumes de réservation et contrôle des conflits

Chaque ouverture dans un mur, une dalle ou un toit peut devenir un **volume de réservation** : un solide aussi grand que l'ouverture, sur toute l'épaisseur de son hôte, revêtements compris. Les outils de détection de conflits (Navisworks, Solibri, BIMcollab) testent les réseaux contre ces volumes avec « entièrement à l'intérieur » : un tuyau, une gaine ou un chemin de câbles entièrement dans un volume passe ; celui qui en dépasse, ou qui traverse un mur sans ouverture, est un conflit. Lapin fait lui-même le même contrôle.

**Gérer → Détection de conflits…** n'exécute la détection que sur demande : rien ne la lance de lui-même, ni une modification ni **Gérer → Vérification**. Son onglet **Paramètres** fixe pour quelles ouvertures les volumes sont faits (ouvertures dans les murs, de portes, de fenêtres, dans les planchers et les toitures), de combien ils sont plus grands ou plus petits que leurs ouvertures (**Décalage du volume**) et le **Jeu** qu'un élément de fluides doit garder par rapport aux côtés d'un volume. **Lancer la détection de conflits** passe une fois par chaque tuyau, gaine et chemin de câbles — les tronçons de Lapin comme les segments d'une maquette fluides importée, et les réservations propres à une maquette fluides — et garde ce qu'elle a trouvé comme **rapport de conflits** du projet, avec la date de l'exécution, jusqu'à la prochaine. Quand la maquette change ensuite, le rapport dit qu'il est **périmé**. Ses problèmes sont groupés par type et numérotés, chaque numéro gardé d'une exécution à l'autre :

- **Sans ouverture** : il traverse l'hôte là où il n'en a pas ;
- **Non coupés : porteur ou ordre de coupe** : sans ouverture, parce que les ouvertures propres des tronçons (plus bas) ne coupent pas cet hôte ;
- **Ouverture trop petite** : il passe par une ouverture mais en dépasse, jeu compris ; **Manque** dit de combien de millimètres du côté le plus défavorable ;
- **Volume vide** (avec **Réservations vides** coché dans **Paramètres**) : une ouverture de mur ou de plancher par laquelle rien ne passe (une trémie d'escalier n'en est jamais une).

![Le rapport de conflits : sans ouverture, une ouverture trop petite et un volume vide, chacun numéroté](shots/mep-voids-check.webp)

Chaque problème est **À corriger**, **Résolu** ou **Ignoré**, avec un commentaire. Un clic sur une ligne montre la paire en 3D, toutes deux sélectionnées (**seuls en 3D** masque le reste pour l'instant) ; les flèches vont au précédent et au suivant. **Marquer dans la vue** (aussi **Vue → Marques de conflits**) dessine chaque problème ouvert dans le plan, la coupe ou l'élévation au premier plan comme un nuage rouge avec son numéro, à l'écran seulement.

![Le plan avec les conflits marqués : nuages rouges et leurs numéros](shots/mep-voids-clouds.webp)

**Créer une ouverture** découpe une ouverture simple autour de l'élément dans le mur (ronde pour un tuyau ou un câble, rectangulaire pour une gaine) ou dans le plancher, ses côtés au jeu et sa taille arrondie au-dessus comme le dit la règle fluides de l'ordre de coupe ; **Agrandir l'ouverture** agrandit l'ouverture trop petite. **Corriger la sélection** le fait pour chaque ligne cochée, **Ignorer** et **Rouvrir** changent leur état ; chacun est une seule étape à annuler. Le rapport dit alors qu'il est périmé : relancez-le, et ce qui est corrigé apparaît **Résolu** (**afficher les résolus**). Sa ligne dit alors à quelle exécution il a été trouvé résolu et où il en est : l'ouverture qu'il traverse et la taille du tronçon dedans (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), ou que le tronçon ne la traverse plus ; chaque exécution suivante met les tailles à jour.

![Relancée après les corrections : les deux conflits résolus](shots/mep-voids-fixed.webp)

**BCF** fait des conflits ouverts des remarques de type **Clash** avec leurs points de vue (**Gérer → Remarques sur le modèle…**) et les enregistre en fichier BCF ; relancé, il met à jour les mêmes remarques. **CSV** et **PDF** enregistrent le rapport sous forme de liste. **Gérer → Vérification** ne fait que lire le rapport : ses conflits ouverts sont des avertissements, de même qu'un rapport périmé.

### Des tronçons qui coupent leurs propres ouvertures

L'ordre de coupe (**Gérer → Ordre de coupe**) comprend aussi les tronçons de fluides, au-dessus des murs, des planchers et des toitures. Avec **Les tronçons coupent murs, planchers et toitures** activé, un tronçon qui traverse un mur, un plancher ou une toiture y coupe sa propre ouverture : ronde pour un tuyau ou un câble qui passe d'équerre, rectangulaire pour une gaine ou un tronçon en biais, aussi grande que l'extérieur du tronçon (son isolant, le fourreau d'un tuyau de gaz) plus le **Jeu** autour, arrondie au-dessus au pas, jamais sous la **Plus petite ouverture**. L'ouverture n'est pas gardée à part : elle se déplace et grandit avec le tronçon et disparaît quand le tronçon disparaît ou ne traverse plus. C'est un volume comme toute ouverture (dessiné, listé, contrôlé), et l'export IFC l'écrit comme **IfcOpeningElement** avec **Lapin_ProvisionOpening** (et comme réservation avec **Volumes de réservation** coché), si bien qu'une détection relancée n'a rien à signaler. Un mur, un plancher ou une toiture placé au-dessus des tronçons dans l'ordre de coupe (par sa classe ou son matériau) n'est pas coupé, ni un porteur (LoadBearing dans son Pset_…Common) sauf si **les couper aussi** est coché : la détection de conflits les signale comme **Non coupés**. Un tronçon démoli ne coupe rien, et là où un tronçon passe déjà par une ouverture, aucune n'est coupée. Un système peut en décider autrement dans **Fluides → Systèmes…** (**Les tronçons coupent murs, planchers et toitures** : selon l'ordre de coupe, oui, non). C'est activé dans un nouveau projet ; un projet fait avant le garde désactivé jusqu'à ce qu'on l'active.

Dans un mur courbe, l'ouverture se trouve là où le tronçon rencontre l'arc, sa largeur mesurée le long de celui-ci ; une rectangulaire a des côtés radiaux, comme celle d'une porte ou d'une fenêtre. Dans une toiture, elle est coupée dans le pan que le tronçon traverse : verticale pour un tuyau debout (une ventilation), allongée dans le sens du tronçon pour un tronçon en biais, comme dans un plancher ; les pannes et la panne faîtière sous le pan ne sont pas coupées, et la détection de conflits signale un tronçon qui les traverse. Déplacer ou redimensionner un tronçon ne reconstruit que les murs, planchers et toitures qu'il traversait ou traverse maintenant, quelle que soit la taille du modèle. L'IFC de Lapin relu ne reprend pas ces ouvertures quand leurs tronçons viennent aussi (les tronçons les coupent à nouveau, jamais deux fois) ; sans leur tronçon (laissé hors de l'import, ou un projet dont les tronçons ne coupent rien), chacune entre comme ouverture de son hôte, si bien que rien n'est perdu.

![Les tronçons de fluides dans l'ordre de coupe](shots/mep-voids-cut-order.webp)

L'onglet **Volumes** liste chaque volume avec sa marque (V1, V2… niveau par niveau), son hôte, la porte ou la fenêtre qu'il contient, sa taille et sa profondeur ; un clic sélectionne son ouverture.

![Les volumes avec leurs marques et leurs tailles](shots/mep-voids-list.webp)

**Vue → Volumes de réservation**, avec la vue 3D devant, les dessine en 3D, violet translucide, avec leurs marques ; un clic sur l'un sélectionne son ouverture.

![Les volumes du mur intérieur, avec les tuyaux qui les traversent](shots/mep-voids-3d.webp)

Avec un plan, une coupe ou une façade devant, **Vue → Volumes de réservation** les dessine dans cette vue, comme un plan de synthèse montre les réservations : coupés là où la vue les coupe (l'ouverture d'un mur sur toute son épaisseur, celle d'un plancher à travers la dalle), vus là où elle les voit (une trémie sous la coupe du plan, l'ouverture d'un mur sur la face que regarde une façade), en tirets quand l'ouverture d'un mur est sous la coupe ou celle d'un plancher au-dessus ; chacun barré en croix, avec sa marque et, dessous, sa taille et le niveau bas d'une ouverture dans un mur (**400×200 +0.90**, **Ø200**). Le moteur les dessine avec le reste du dessin, donc les feuilles, le PDF et le DXF (calque **VOIDS**) les impriment comme l'écran les montre.

![Le plan avec les réservations : coupées dans les murs, la trémie vue, chacune barrée et marquée](shots/mep-voids-plan.webp)

L’étiquette d’une réservation se place à côté d’elle, en travers de son mur, là où rien n’est déjà dessiné : le débattement d’une porte, le repère de la porte ou de la fenêtre, d’autres étiquettes. Si les deux côtés du mur sont pris, elle se décale le long du mur au-delà de l’ouverture, sinon plus loin.

![La réservation d’une porte : son étiquette à l’écart du débattement et du repère de la porte](shots/void-label.webp)

Pour déplacer une étiquette, cliquez dessus (ou sur la réservation) : cela sélectionne l’ouverture, dont l’étiquette montre une poignée carrée pour la déplacer et une ronde pour la tourner. Ctrl/⌘-clic sur une poignée la remet à sa place par défaut, ou de nouveau horizontale. Une étiquette déplacée est gardée par cette vue, le long et en travers du mur depuis le milieu de la réservation, si bien qu’elle suit l’ouverture quand le mur bouge ; les planches et le PDF l’impriment où vous l’avez mise.

![L’étiquette de la réservation déplacée par sa poignée](shots/void-label-moved.webp)

C'est un réglage de la vue, aussi dans ses propriétés (**Vue → Paramètres de vue**) : **Volumes de réservation**, et **Tailles des réservations** pour la taille sous la marque. Par défaut aucune vue ne les dessine ; le style de vue **Coordination** (**Gérer → Styles de vue**, choisi comme **Style** de la vue) les dessine dans chaque vue qui le prend. Leurs lignes sont la catégorie **Volumes** : plumes et couleurs dans **Gérer → Styles d’objets** (lignes coupées, en projection, cachées, au-dessus), montrées ou masquées par vue dans **Vue → Visibilité / Graphismes…**.

Pour donner les volumes à un outil de conflits, cochez **Volumes de réservation** dans **Fichier → Exporter IFC ou Fragments…** (ou **Exporter en 3D…**). En IFC chaque volume est un **IfcBuildingElementProxy** de type **PROVISIONFORVOID** avec **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter pour un rond, Depth) et le jeu Lapin **Lapin_OpeningVoid** (marque, type, hôte, ouverture et décalage), lié à son hôte ; les ouvertures restent des IfcOpeningElement. Chaque volume garde son GlobalId d'un export à l'autre. Dans un export de maillages, ils forment un groupe à part, **Opening voids**. Case décochée, les exports restent comme avant.

## MEP Pro

L’ingénierie du réseau — le débit que porte chaque tronçon et la taille qui le supporte, les vérifications (extrémités ouvertes, coudes trop serrés, pentes d’évacuation, sources surchargées), les règles de pose par matériau (le cuivre par pas de 45°, le PVC tournant au plus de 45°), les radiateurs proposés sous les fenêtres, les tracés suggérés le long des murs, le profil d’un tronçon et l’isométrie d’un système — se trouve dans l’édition **Lapin MEP**, qui ajoute les groupes **Ingénierie** et **Assistants** à cet onglet.
