---
title: Structure
order: 50
summary: Poteaux, poutres et semelles, leurs types, et la façon dont ils se rencontrent.
---

L’onglet **Structure** regroupe l’ossature et les fondations.

![L’onglet Structure](shots/ribbon-structure.webp)

## Ossature

- **Poteau** (<kbd>CL</kbd>) : cliquez là où il se dresse. Un poteau va de son niveau au niveau supérieur.
- **Poutre** (<kbd>BM</kbd>) : cliquez ses deux extrémités. **Poutre courbe** (<kbd>BC</kbd>) suit un arc.
- **Poteaux sur quadrillage** place un poteau à chaque croisement des axes du quadrillage. Pour dessiner les axes, utilisez **Annoter → Axe de quadrillage** (<kbd>GR</kbd>).

## Fondations

- **Semelle filante** (<kbd>SF</kbd>) passe sous les murs. Ses couches extérieures sont coupées en onglet aux angles.
- **Semelle filante courbe** (<kbd>CF</kbd>) suit un arc.
- **Semelle isolée** (<kbd>PF</kbd>) se place sous un poteau.

Dessinez les fondations sur le niveau **Foundations**, ou donnez-leur un plan à elles.

**Une semelle filante n'est que le béton**, sauf si vous demandez le mur. Quand **Semelle filante** ou **Semelle filante courbe** est active, la barre de la ligne de commande propose **Semelle seule** (par défaut : une semelle en béton de 600 × 400 mm, un type à profil créé la première fois) ou **Avec mur** (le soubassement d'un type de semelle filante dessus, avec ses couches) et le type avec mur à utiliser. Le choix est conservé. Une semelle déjà dessinée change avec son type dans **Types structurels**.

![L'option de l'outil semelle filante dans la ligne de commande : semelle seule ou avec mur](shots/structure-strip-option.webp)

**Dans les plans.** Une semelle est sous le plancher, donc un plan la dessine en tirets dans le plan du niveau où elle se trouve, comme le plan d'un bâtiment montre ce qui est dessous. Pour la voir coupée et vue, utilisez un **plan de fondations** : dans le navigateur de projet, le **+** à côté de **Vues en plan** en crée un pour le niveau actif (coupe 0,50 m sous lui, vue de 2,50 m vers le bas).

**Dessiner dans un plan de fondations.** Il montre aussi le niveau en dessous, et ce que vous y dessinez avec un outil de dalle, de mur, de poteau ou de poutre va sur ce niveau (les Fondations d'un nouveau projet, à −1,50) ; les outils de semelles gardent le niveau du plan, le dessous d'une semelle étant celui de son type sous lui.

![Poteaux, poutres et semelles isolées en 3D](shots/structure-3d.webp)

## Types et angles

**Types structurels** définit les sections des poteaux, des poutres et des semelles : sections en béton et profilés en acier. La section d’un poteau est un **Rectangle**, un **Cercle** ou un **Polygone**, régulier, donné par ses **Côtés** et son **Diamètre** entre les sommets (un poteau octogonal, comme peut en avoir un IFC importé). Cliquez sur un type dans la liste pour modifier sa taille ou son nom, puis **Enregistrer le type** : tous les éléments de ce type suivent ; **Nouveau type** revient à la création d’un type.

Les poutres et les semelles se joignent d’elles-mêmes là où elles se rencontrent, un angle en onglet. Une poutre tracée d’axe de poteau à axe de poteau s’arrête aux faces des poteaux ; sa longueur, son volume et ses cadres comptent la portée libre entre elles. **Onglet**, **Bout à bout** et **Permuter** changent la façon dont deux poutres ou deux semelles se rencontrent. **Appliquer les propriétés** (<kbd>MA</kbd>) copie le type et les réglages d’un élément vers d’autres, et **Créer un similaire** (<kbd>CS</kbd>) commence le dessin d’un autre élément du même type.

Tout autre profilé vient des bibliothèques : **D'une bibliothèque de profilés…** les parcourt toutes (voir [Bibliothèques de profilés](profiles.md)).

## Semelles filantes à profil

Un type de semelle filante est soit le type standard, un voile avec ses couches sur une semelle, soit n'importe quel profil balayé le long de l'axe de la semelle, comme les profils de semelle de Revit. Dans **Types structurels → Strip**, choisissez **Forme : Profil** et un profil : un profil du catalogue (rectangulaire, à 2 ou 3 redans, à dessus incliné, excentrée avec son voile sur un bord, avec voile) avec ses dimensions, ou un profil du projet (**Gérer → Profils**, où vous pouvez aussi saisir ses sommets ou lire un contour dessiné). Le point d'insertion du profil se place sur l'axe de la semelle, au niveau **Bas** du type.

Les semelles à profil sont droites ou courbes. Deux semelles qui se rencontrent en angle sont coupées en onglet, et une semelle qui s'arrête contre une autre y pénètre et est coupée par son corps, de sorte que chaque redan rejoint le redan de l'autre. Les plans, coupes et élévations coupent le corps balayé. Les quantités donnent sa longueur, l'aire de sa section et son volume, et ses faces verticales sont celles que les Installations de chantier coffrent. En IFC, une semelle droite est un IfcFooting STRIP_FOOTING dont le corps est son profil extrudé (un IfcArbitraryClosedProfileDef), coupé par les plans des angles ; une semelle courbe est écrite par ses faces. Choisissez **Standard** pour revenir au voile sur semelle.

![Semelles filantes à redans sous une maison, en onglet aux angles](shots/footing-profile-3d.webp)

![Un type de semelle filante à profil](shots/footing-profile-type.webp)

## Une vue structurelle

La discipline d’une vue décide de ce qu’elle montre. Une vue **Structure** ne montre que les noyaux porteurs, sans les finitions. Vous choisissez la discipline dans les propriétés de la vue. Voir [Coupes, élévations et 3D](views.md).

## Charges

Le groupe **Charges** contient ce qu'il faut à un calcul de structure : cas de charge, charges surfaciques, linéiques et ponctuelles, et combinaisons. Voir [Charges](loads.md).
