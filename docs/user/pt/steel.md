---
title: Aço
order: 56
summary: Barras de aço com extremidades configuráveis — prolongadas ou recuadas, cortadas por um plano, em meia-esquadria, ajustadas a outra barra, recortadas — feitas também a partir de um contorno desenhado; perfis tubulares e formados a frio.
---

## A aba Aço

![A aba Aço](shots/ribbon-steel.webp)

- **Barra** (<kbd>SM</kbd>): uma barra de aço entre dois pontos, no nível superior do pavimento, com o topo da seção sobre o eixo, como uma viga. Seu tipo é escolhido em Propriedades antes do primeiro clique, ou na aba **Catálogo**, onde cada tipo de aço é desenhado pela sua seção.
- **Converter em barra**: os contornos desenhados selecionados viram barras (abaixo).
- **Meia-esquadria** e **Ajustar**: duas barras unidas pelas extremidades (abaixo).
- **Tipos de aço**: os tipos de barra do projeto, e novos.

Uma barra é um IfcMember: seu eixo vai a qualquer lugar do espaço (um contraventamento, um caibro, um montante), sua seção vem do seu tipo, ela pode girar em torno do eixo (**Rotação**) e o eixo pode passar pelo centro, pelo topo ou pela base da seção (**Ponto de inserção**). As vigas e os pilares da estrutura de perfil de aço recebem as mesmas extremidades.

## Extremidades

Selecione uma barra, ou uma viga ou um pilar reto de perfil de aço: sua seção **Aço** em Propriedades mostra as duas extremidades, **Início** e **Fim**. Cada extremidade é um conjunto de regras; o corpo as segue.

![As extremidades de uma barra em Propriedades](shots/steel-ends.webp)

- **Prolongamento**: quanto a extremidade passa do seu ponto; negativo, ela fica recuada (uma folga de montagem).
- **Corte**: **Perpendicular ao eixo**; **Por um plano**, girado a partir do corte reto **Em planta** (em torno da vertical da seção) e **Em elevação** (em torno do seu lado), em graus; **Meia-esquadria** com outra barra, no plano que divide ao meio o canto entre elas; **Ajustar a uma barra**, na face da outra barra a que chega (a aba de um pilar, ou o lado da sua alma quando o pilar está girado).
- **Recorte**: as abas recortadas em **Superior** e/ou **Inferior**, no **Comprimento do recorte** a partir da extremidade e na **Profundidade do recorte** a partir da borda da seção, como numa viga que chega a uma viga principal.

A meia-esquadria e o ajuste nomeiam a outra barra: mova-a, e a extremidade a acompanha; apague-a, e a extremidade volta a ser reta e diz por quê. **Meia-esquadria** na aba une em meia-esquadria as duas barras selecionadas no canto onde se encontram; **Ajustar** ajusta a primeira selecionada à segunda.

## Do esboço à barra

Desenhe o contorno da barra como uma polilinha fechada ou um retângulo (**Desenho**) e depois **Aço → Converter em barra** com um ou mais contornos selecionados. Em um só passo cada contorno é lido e substituído por uma barra:

![Dois contornos esboçados na planta](shots/steel-sketch.webp)

- seus **dois lados longos paralelos** (com meio grau de tolerância) dão o eixo, a linha média de ponta a ponta, e a **largura** entre eles;
- o **lado em cada extremidade** dá o corte dessa extremidade: reto, ou por um plano com a inclinação do lado.

Numa **planta** o contorno é a barra vista de cima: sua largura é a das abas, e um perfil dessa largura é procurado entre os tipos de barra do projeto e no catálogo (senão, o tipo escolhido para Barra); a barra fica no nível superior do pavimento. Num **corte** ou numa **elevação** o contorno é a barra vista de lado: sua largura é a altura da seção, uma extremidade inclinada é um corte em elevação e um **degrau** em ângulo reto numa extremidade é um **recorte** em cima ou embaixo; a barra fica onde foi desenhada. Um degrau desenhado em planta é recusado (um recorte se vê de lado). Um contorno que não é uma faixa (um triângulo, um L, lados não paralelos) fica como está, e o Lapin diz por quê.

## Tipos de aço

**Aço → Tipos de aço** lista os tipos de barra do projeto com o peso por metro e cria novos: um perfil do catálogo (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** e os genéricos formados a frio **C**, **Z** e **Σ**) ou uma seção tubular ou formada a frio **com dimensões próprias** (altura, largura, espessura, dobra). O novo tipo pode ser um tipo de barra, ou um **tipo de viga** ou **tipo de pilar** da estrutura, para que as ferramentas Viga e Pilar também coloquem aço. Um clique num tipo da lista o torna o que Barra coloca.

![O diálogo Tipos de aço](shots/steel-types.webp)

Os perfis tubulares são ocos por dentro, na vista 3D, nos cortes e nas quantidades. Os perfis formados a frio são formas genéricas com dimensões, de dobras vivas, sem catálogo de fabricante.

![Barras em 3D](shots/steel-3d.webp)

## IFC e quantidades

Uma barra é exportada como **IfcMember** com seu **IfcMemberType**. Seu corpo é escrito como os outros programas escrevem o aço (Tekla, Advance Steel, Revit): a seção **extrudada** (IfcExtrudedAreaSolid), **cortada** pelos planos das extremidades (IfcBooleanClippingResult com IfcHalfSpaceSolid), e cada recorte um **IfcOpeningElement** que a esvazia. Uma viga ou um pilar de aço com extremidades é escrito da mesma forma. As quantidades de cada barra dão o comprimento entre os cortes, a área da seção, o volume e o peso líquido (7850 kg/m³).
