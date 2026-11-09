---
title: Estrutura
order: 50
summary: Pilares, vigas e fundações, os seus tipos e como eles se encontram.
---

A guia **Estrutura** reúne a estrutura e as fundações.

![A guia Estrutura](shots/ribbon-structure.webp)

## Estrutura

- **Pilar** (<kbd>CL</kbd>): clique onde ele fica. Um pilar vai do seu nível até o nível de cima.
- **Viga** (<kbd>BM</kbd>): clique nas duas pontas. **Viga curva** (<kbd>BC</kbd>) segue um arco.
- **Pilares na grade** coloca um pilar em cada cruzamento dos eixos da malha. Para desenhar os eixos, use **Anotar → Eixo de grade** (<kbd>GR</kbd>).

## Fundações

- **Sapata corrida** (<kbd>SF</kbd>) passa sob as paredes. As suas camadas externas são cortadas em meia-esquadria nos cantos.
- **Sapata corrida curva** (<kbd>CF</kbd>) segue um arco.
- **Sapata isolada** (<kbd>PF</kbd>) fica sob um pilar.

Desenhe as fundações no nível **Fundações**, ou dê a elas uma planta própria.

**Nas plantas.** Uma sapata fica sob o piso, por isso uma planta desenha-a a tracejado na planta do nível em que assenta, como a planta de um edifício mostra o que está por baixo. Para a ver cortada e vista, use uma **planta de fundações**: no navegador do projeto, o **+** ao lado de **Plantas** cria uma para o nível ativo (corte 0,50 m abaixo, vista 2,50 m para baixo).

![Pilares, vigas e sapatas isoladas em 3D](shots/structure-3d.webp)

## Tipos e cantos

**Tipos estruturais** define as seções de pilares, vigas e sapatas: seções de concreto e perfis de aço. A seção de um pilar é um **Retângulo**, um **Círculo** ou um **Polígono**, regular, dado pelos seus **Lados** e pelo seu **Diâmetro** entre vértices (um poste octogonal, como um IFC importado pode tê-lo). Clique num tipo da lista para mudar o seu tamanho ou o seu nome e depois em **Salvar tipo**: todos os elementos desse tipo acompanham; **Novo tipo** volta a criar um.

Vigas e sapatas se unem sozinhas onde se encontram, um canto em meia-esquadria. Uma viga desenhada de eixo de pilar a eixo de pilar para nas faces dos pilares; o seu comprimento, volume e estribos contam o vão livre entre eles. **Meia-esquadria**, **Topo** e **Inverter** mudam como duas vigas ou duas sapatas se encontram. **Corresponder tipo** (<kbd>MA</kbd>) copia o tipo de um elemento para outros, e **Criar similar** (<kbd>CS</kbd>) começa a desenhar outro elemento do mesmo tipo.

Qualquer outro perfil vem das bibliotecas: **De uma biblioteca de perfis…** pesquisa em todas (ver [Bibliotecas de perfis](profiles.md)).

## Sapatas corridas com perfil

Um tipo de sapata corrida é o padrão, um murete com as suas camadas sobre uma sapata, ou qualquer perfil varrido ao longo do eixo da sapata, como os perfis de fundação do Revit. Em **Tipos estruturais → Strip**, escolha **Forma: Perfil** e um perfil: um do catálogo (retangular, escalonada em 2 ou 3 degraus, com a face superior inclinada, excêntrica com o murete num bordo, com murete) com as suas medidas, ou um do projeto (**Gerenciar → Perfis**, onde também pode escrever os seus vértices ou ler um contorno desenhado). O ponto de inserção do perfil fica no eixo da sapata, à cota **Inferior** do tipo.

As sapatas com perfil são retas ou curvas. Duas que se encontram num canto unem-se em meia-esquadria, e uma que termina contra outra entra nela e é cortada pelo seu corpo, de modo que cada degrau encontra o degrau da outra. Plantas, cortes e alçados cortam o corpo varrido. As quantidades dão o seu comprimento, a área da secção e o volume, e as suas faces verticais são as que o Canteiro de obras cofra. Em IFC uma reta é um IfcFooting STRIP_FOOTING cujo corpo é o seu perfil extrudido (um IfcArbitraryClosedProfileDef), cortado pelos planos dos cantos; uma curva vai como as suas faces. Escolha **Padrão** para voltar ao murete sobre sapata.

![Sapatas corridas escalonadas sob uma casa, em meia-esquadria nos cantos](shots/footing-profile-3d.webp)

![Um tipo de sapata corrida com perfil](shots/footing-profile-type.webp)

## Uma vista estrutural

A disciplina de uma vista decide o que ela mostra. Uma vista de **Estrutura** mostra só os núcleos portantes, sem os acabamentos. Você escolhe a disciplina nas propriedades da vista. Veja [Vistas](views.md).
