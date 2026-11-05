---
title: Documents
order: 105
summary: Les pièces écrites du projet — la notice descriptive, le cahier des charges, le bordereau des plans — faites à partir de modèles, remplies depuis la maquette et exportées en DOCX, ODT et PDF sans aucun autre logiciel.
---

## Ce qu'est un document

Un projet ne se résume pas à ses plans : il a aussi ses pièces écrites, une notice descriptive, un cahier des charges, un bordereau des plans. Dans Lapin, ce sont des **documents** : chacun part d'un modèle et se remplit depuis le projet — ses informations, ses surfaces, ses niveaux et ses pièces, ses types de murs, ses nomenclatures et ses feuilles. Ce que vous seul savez (la structure, la sécurité incendie, l'installation de chantier), vous l'écrivez dans les saisies du document. Un document suit la maquette : changez un type de mur ou ajoutez une pièce, et le document le dira la prochaine fois que vous l'ouvrirez.

Lapin écrit lui-même les fichiers, en DOCX, ODT et PDF, sans Word ni LibreOffice sur l'ordinateur, dans l'application de bureau comme dans le navigateur.

## Informations du projet

**Gérer → Informations du projet…** contient ce que les documents demandent au projet : son nom, son numéro et sa phase, le maître d'ouvrage et son adresse, l'adresse du site, le numéro cadastral et le livre foncier, le concepteur et son adresse, l'architecte en chef, qui a dessiné et qui a vérifié. Écrivez-les une fois : tous les documents les lisent, et les cartouches des feuilles prennent ici qui a dessiné et qui a vérifié quand une feuille ne le dit pas. Chaque champ est enregistré quand vous le quittez.

![Informations du projet : le numéro, la phase, le maître d'ouvrage, le site et le concepteur](shots/project-info.webp)

Les mêmes champs se trouvent dans l'onglet **Informations du projet** de la boîte de dialogue Documents.

## Faire un document

**Gérer → Documents…** (ou le **+** à côté de **Documents** dans le navigateur du projet) ouvre les documents du projet. À gauche, les documents déjà faits et, sous **Nouveau à partir d'un modèle**, les modèles ; cliquez sur l'un d'eux et le nouveau document est créé et rempli aussitôt.

![Documents : les documents du projet et les modèles, le document sur ses pages, ses saisies et ce qui reste à compléter](shots/documents.webp)

Au centre, le document tel qu'il s'imprimera, page après page, avec sa table des matières, ses tableaux et ses chiffres. Un champ que le projet n'a pas encore est marqué là où il se trouve. Le navigateur du projet liste les documents sous **Documents** ; un clic ouvre la boîte de dialogue sur l'un d'eux.

## Saisies

L'onglet **Saisies** contient les textes que seul le concepteur peut écrire, groupés comme les chapitres du document. Dans les modèles roumains, chacun commence par une indication marquée **[De completat]** (à compléter) qui dit ce qu'il faut y mettre. Écrivez par-dessus : l'aperçu suit un instant après que vous avez cessé de taper. **Rétablir la valeur par défaut** remet le texte du modèle.

![Saisies : la structure écrite par-dessus l'indication du modèle](shots/documents-inputs.webp)

## À compléter

**À compléter** liste ce qui manque encore au document : un champ du projet laissé vide, une valeur que la maquette n'a pas. Cliquez sur l'un pour voir où il se trouve dans le document et aller au champ qui le remplit (un champ du projet ouvre l'onglet **Informations du projet**). La liste se vide au fur et à mesure.

![À compléter : les champs qui manquent encore au document](shots/documents-missing.webp)

## Mettre à jour, renommer, copier

- **Mettre à jour depuis le modèle** reprend le texte et la mise en page du modèle, pour un document fait avant que le modèle ne change. Vos saisies sont conservées.
- **Renommer**, **Dupliquer** et **Supprimer** agissent sur le document affiché. Une suppression est une seule étape d'annulation.

## Modifier un document

**Modifier**, en bas du dialogue Documents, fait de la page du milieu un éditeur du texte propre au document ; **Aperçu** le montre de nouveau sur ses pages. Ce que vous changez n'appartient qu'à ce document : le modèle reste tel qu'il était (**Enregistrer comme modèle…**, plus bas, garde vos changements comme modèle).

![Modifier un document : les champs comme étiquettes avec leurs valeurs, un tableau du projet sélectionné et ses réglages à droite](shots/documents-editor.webp)

- **La barre d'outils** porte les styles du texte : **Paragraphe**, **Titre 1**–**3**, gras, italique, listes à puces et numérotées, annuler et rétablir. Les touches habituelles marchent aussi (Ctrl+B, Ctrl+I, Ctrl+Z dans l'éditeur).
- **Les champs** apparaissent comme des étiquettes avec leur nom et leur valeur actuelle, par exemple le maître d'ouvrage, *Popescu Ion și Maria*. Un champ que le projet ne remplit pas encore est orange.
- **Les blocs du projet** — un tableau, une image, le sommaire — sont dessinés avec leur vrai contenu. Un clic en sélectionne un : ses réglages s'affichent à droite, à la place des onglets.

L'enregistrement est automatique : un instant après que vous cessez de taper, et quand vous revenez à **Aperçu** ; chaque changement est un pas d'annulation du projet.

### Insérer

**Insérer** place au curseur :

- **Champ…** : un champ du projet, cherché par son nom ; la liste montre la valeur de chacun. Dans une répétition, les champs de ses éléments sont aussi proposés (le nom d'un niveau, la surface d'une pièce).

  ![Insérer → Champ… : « client » cherché, les champs avec leurs valeurs](shots/documents-insert-field.webp)

- **Tableau du projet…** : une nomenclature ou une liste du projet (les menuiseries, les pièces, les matériaux…). Dans ses réglages : la **Source**, le **Titre**, les colonnes affichées et la ligne de total.
- **Image d'une vue…** : un plan, une coupe, une élévation ou une vue 3D. Dans ses réglages : **Échelle 1:** ou **Largeur (mm)** (jamais plus large que le texte) et une **Légende**.
- **Champs clé–valeur** : un tableau à deux colonnes de libellés et de valeurs ; une valeur peut contenir des champs, comme `{{project.name}}`.
- **Répéter pour…** : le texte à l'intérieur est écrit une fois pour chaque élément d'une collection : les niveaux, les pièces, les toitures, les matériaux, les feuilles, les documents… Un bandeau au-dessus indique **Pour chaque** et combien d'éléments il y a ; ses flèches **‹ ›** changent l'élément que l'éditeur montre pendant que vous écrivez. Dans ses réglages, **Seulement les éléments où** n'en garde que certains, et **Texte quand il n'y en a aucun** ajoute ce qui s'écrit quand il n'y en a aucun.
- **Condition…** : le texte à l'intérieur n'est écrit que quand un champ a une valeur (ou, coché, seulement quand il est vide). **Sinon : texte quand elle n'est pas remplie** ajoute une seconde partie, écrite à sa place.
- **Texte à compléter…** : une nouvelle saisie, avec son libellé, son groupe et son texte par défaut ; son texte s'écrit dans l'onglet **Saisies**, en aperçu.
- **Sommaire** et **Saut de page**.

![Une répétition sur les niveaux, une condition à l'intérieur et les pièces du niveau montré, changé avec ‹ ›](shots/documents-repeat.webp)

Dans les réglages d'un bloc, **Retirer** l'enlève ; pour une répétition ou une condition, **Garder seulement le contenu** enlève le bandeau et garde ce qu'il contient.

## Vos modèles

**Enregistrer comme modèle…** garde le texte, la mise en page, les saisies et le style du document comme modèle. Donnez-lui un titre et une description et choisissez où le garder :

- **Ce projet** : il part avec le fichier du projet, chez quiconque l'ouvre.
- **La bibliothèque de cet ordinateur** : tout projet de cet ordinateur le propose.
- **Les deux**.

**Enregistrer comme** peut remplacer un modèle enregistré avant ; les documents déjà faits à partir de lui gardent leur texte jusqu'à **Mettre à jour depuis le modèle**. Le nouveau modèle apparaît aussitôt sous **Nouveau à partir d'un modèle**.

![Enregistrer comme modèle : le titre, la description et où le garder](shots/documents-save-template.webp)

**Gérer → Modèles de documents…** montre les modèles en trois groupes : **Intégrés** (ceux de Lapin), **Ce projet** et **Cet ordinateur**. Pour celui choisi :

- **Renommer** et **Supprimer** (supprimer un modèle du projet est un pas d'annulation ; les documents faits à partir de lui gardent leur texte).
- **Dupliquer dans le projet** ou **Dupliquer dans la bibliothèque** : un modèle intégré ne se change qu'à travers une copie.
- **Copier dans la bibliothèque** ou **Copier dans le projet**, pour le passer d'un endroit à l'autre.
- **Exporter…** écrit le modèle dans un fichier `.json` ; **Importer…** en lit un dans ce projet ou dans la bibliothèque de cet ordinateur. C'est ainsi que des collègues partagent les modèles d'une entreprise.

![Modèles de documents : les intégrés, ceux du projet et ceux de l'ordinateur, celui choisi avec son style](shots/document-templates.webp)

## Le style de l'entreprise

Un document, et un modèle du projet ou de la bibliothèque, a son propre style : l'onglet **Style** du dialogue Documents pour le document affiché, la partie **Style** de Gérer → Modèles de documents… pour un modèle. Les modèles intégrés gardent l'apparence de Lapin : dupliquez-en un pour lui donner la vôtre.

- **Police (DOCX, ODT)** : Arial (celle de Lapin), Calibri, Times New Roman, Georgia, Verdana ou Liberation Sans.
- **Police du PDF** : Helvetica ou Times, les polices propres au PDF ; par défaut la plus proche de la police ci-dessus.
- **Taille du texte (pt)** : la taille du texte courant, de 6 à 24 pt.
- **Couleur des titres**.
- **Logo dans l'en-tête** : **Choisir le logo…** prend un PNG ou un JPEG (moins de 1 Mo) ; réglez sa **Largeur (mm)** et s'il se place **À gauche** ou **À droite**. Il est écrit de la même façon en DOCX, ODT et PDF.

Un échantillon sous les champs montre le haut de la page tel que les fichiers le montreront. **L'apparence propre à Lapin** retire le style. Chaque changement est un pas d'annulation.

![L'onglet Style : la couleur des titres et un logo dans l'en-tête, avec l'échantillon](shots/documents-style.webp)

## Exporter

- **Exporter DOCX** et **Exporter ODT** écrivent un fichier que vous ouvrez et modifiez dans Word ou LibreOffice, avec ses titres comme styles propres au logiciel, sa table des matières et sa numérotation des pages comme champs, et les images du projet.
- **Exporter PDF** écrit le document mis en page par Lapin, prêt à imprimer ou à envoyer.

Un champ encore vide reste vide dans les fichiers : vérifiez donc **À compléter** avant d'exporter.

## Émettre des documents

**Gérer → Émettre…** emporte les documents avec les feuilles et la maquette (voir [Dossier du projet et émissions](cde.md)). Sous **Documents, chacun en PDF :** chaque document est coché d'emblée ; cochez **Aussi en DOCX** pour une copie modifiable en plus. Chaque fichier est nommé selon la convention ISO 19650 (`RP` pour une notice, `SP` pour un cahier des charges), son empreinte calculée et listé dans le bordereau d'envoi, et le document prend la révision et le statut de l'émission.

![Émettre : les documents avec les feuilles et la maquette](shots/issue-documents.webp)

## Les modèles

Lapin est livré avec trois modèles, en roumain pour l'instant :

- **Memoriu tehnic de arhitectură** (la notice technique d'architecture, selon la loi roumaine 50/1991) : les données générales des Informations du projet ; le nombre de niveaux (par exemple `P+1E`) et la hauteur maximale ; les surfaces — bâtie, développée, utile et du sous-sol — au total et par niveau ; les pièces de chaque niveau avec leurs surfaces ; les murs par type, avec leurs couches ; les planchers et les toitures ; la nomenclature des menuiseries ; les finitions des pièces ; des saisies pour l'existant, la fonction, la structure, les façades, les installations, la sécurité incendie, l'accessibilité, l'efficacité énergétique et l'installation de chantier.
- **Caiet de sarcini: tâmplărie și finisaje** (le cahier des charges menuiseries et finitions) : les matériaux du projet par catégorie, la nomenclature des menuiseries avec leurs quantités, les finitions pièce par pièce, et des saisies pour les généralités, les exigences, l'exécution et la réception, avec des textes par défaut à adapter.
- **Borderou de piese scrise și desenate** (le bordereau des pièces écrites et dessinées) : les documents et les feuilles du projet, avec leur numéro, titre, échelle et révision.

Partez de l'un d'eux, changez-le dans l'éditeur, puis **Enregistrer comme modèle…** pour avoir le vôtre.

## Pour les assistants IA

Un assistant connecté par le pont MCP de Lapin peut créer un document, écrire ses saisies et l'exporter avec `lapin_document_export` : voir [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (en anglais).
