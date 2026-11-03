---
title: Aço
order: 56
summary: Barras de aço com extremidades configuráveis — prolongadas ou recuadas, cortadas por um plano, em meia-esquadria, ajustadas a outra barra, recortadas — feitas também a partir de um contorno desenhado; perfis tubulares e formados a frio; galpões de pórticos com mísulas.
---

## A aba Aço

![A aba Aço](shots/ribbon-steel.webp)

- **Galpão de pórticos…**: um galpão de aço inteiro a partir de poucos números, no configurador de galpões (abaixo).
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

## Galpões de pórticos

**Aço → Galpão de pórticos…** abre o configurador de galpões. Como o da cobertura, tem os estilos à esquerda, os campos do galpão em abas no meio e, à direita, o galpão como o Lapin o construiria: um pórtico típico visto ao longo do galpão, com suas mísulas, e um resumo do que ele pesa.

![O configurador de galpões](shots/steel-hall.webp)

- **Estilos**: os galpões típicos do catálogo (**Galpão 20 m**, **Galpão 30 m**, um **Telheiro 10 m** de uma água) e os do projeto. Um estilo escolhido preenche todos os campos; qualquer um pode depois ser alterado. **Salvar como estilo…** guarda o galpão como está configurado como estilo do projeto; **Atualizar o estilo** o salva de novo no seu estilo, e todos os galpões desse estilo o seguem.
- **Pórtico**: o **Vão** (de eixo de pilar a eixo de pilar), os **Módulos** (iguais como `5 × 6`, ou cada um: `6, 6, 7.5`), a **Altura do beiral** (o topo das vigas de cobertura sobre a face externa do pilar), a **Cobertura** (duas águas ou uma água) e sua **Inclinação**, os perfis dos **Pilares** e das **Vigas de cobertura**.
- **Mísulas**: a **Mísula no beiral** e a **Mísula na cumeeira**, cada uma com seu **Comprimento** ao longo da viga desde a face do pilar (ou da cumeeira) e sua **Altura** sob a viga ali, e como é **Feita**: **cortada do perfil da viga de cobertura** (um I cortado na diagonal ao longo da alma, uma metade soldada sob a viga: a alma e a mesa da própria viga) ou **de chapas soldadas** (uma **Chapa da alma** e uma **Chapa da mesa** de espessura própria, e a largura da mesa).

![As mísulas de chapas soldadas](shots/steel-hall-haunches.webp)

- **Oitões**: **pilares de oitão sob uma viga simples** (pilares de canto e pilares no **Espaçamento** escolhido, com o eixo forte perpendicular à parede do oitão, sob uma viga mais leve que vai até a face externa das paredes, os pilares ajustados à sua face inferior), ou **um pórtico comum** em cada extremidade.

![Um oitão com pilares](shots/steel-hall-gable.webp)

- **Aço secundário**: as terças, as longarinas, o contraventamento, a viga de beiral e as escoras (abaixo).
- **Posicionamento**: onde se cruzam o eixo A e o eixo 1, e a direção do vão em planta (**Rotação**); a pré-visualização mostra a malha do galpão vista de cima.

**Criar galpão** o faz em um só passo, que um desfazer reverte: sua própria **malha de eixos** (eixos 1, 2… nos pórticos, A e B nos pilares), e em cada pórtico dois **pilares** (pilares da estrutura de um perfil de aço, com a alma no plano do pórtico e o topo cortado segundo a inclinação) e duas **vigas de cobertura** em meia-esquadria na cumeeira (uma só, do pilar baixo ao alto, numa cobertura de uma água): barras de aço ajustadas às faces dos pilares, com suas mísulas. O galpão fica no pavimento atual; o beiral e a cumeeira são alturas dos seus pórticos, não novos pavimentos. O resumo dá os pórticos, as alturas e o aço por tipo: pilares, vigas, mísulas, pilares de oitão, no total e por m² de piso.

![O galpão em 3D](shots/steel-hall-3d.webp)

O galpão continua editável. Selecione uma das suas barras: sua seção **Aço** mostra o galpão e **Editar galpão…**, que abre o configurador nele; **Aplicar** reconstrói o galpão sobre as mesmas barras, que mantêm sua identidade (uma barra que não é mais necessária sai, uma nova é acrescentada). Ali também aparecem as mísulas da viga, cada uma alterada sozinha (tipo, comprimento, altura, chapas): uma mísula ajustada assim fica como está quando o galpão é reconstruído; **×** a remove.

![As mísulas de uma viga em Propriedades](shots/steel-hall-props.webp)

## Ligações

Os pórticos de um galpão vêm com as suas **ligações** (Steel → Portal frame hall…, aba **Nós**): uma **base de pilar** sob cada pilar e pilar de oitão, uma **ligação de beiral** onde cada viga chega ao seu pilar, uma **ligação de cumeeira** onde duas vigas se encontram, e uma chapa de ligação sob a viga acima de cada pilar de oitão. Cada uma é um elemento próprio que sabe que barras liga; as suas chapas, parafusos e chumbadores são feitos dos seus parâmetros e das barras como estão, e acompanham-nas quando o galpão muda.

![A aba Nós](shots/steel-nodes-hall.webp)

- **Ligações**: ligadas ou não para todo o galpão.
- **Bases dos pilares**: **rotuladas** (uma placa de base, dois chumbadores de cada lado da alma ou quatro entre as mesas) ou **engastadas** (uma placa mais longa e espessa, de quatro a oito chumbadores fora das mesas, nervuras no plano da alma). Os **Chumbadores** são **retos**, **com gancho** ou **com placa de ancoragem** no pé; o **Número de chumbadores** e o **Comprimento dos chumbadores** (abaixo do topo do concreto) são escolhidos pelo perfil quando ficam vazios. Quando há sob o pilar uma sapata da estrutura, os chumbadores entram nela (e param 50 mm acima do seu fundo); senão descem o seu comprimento abaixo do graute.
- **Ligação de beiral**: uma chapa de topo estendida na viga, aparafusada à mesa do pilar: a sua **Espessura da chapa**, o **Diâmetro dos parafusos** e a sua **Classe**, quanto sobressai **Acima da viga** (com uma linha de parafusos ali) e os **Enrijecedores do pilar** na altura da mesa superior da viga e da mesa da mísula.
- **Ligação de cumeeira**: as chapas de topo das duas vigas aparafusadas uma à outra sobre o corte em meia-esquadria, com as suas mísulas.

O que fica vazio é escolhido pelos perfis: parafusos de M16 a M27 pela altura da barra aparafusada, uma chapa tão espessa quanto os seus parafusos, distâncias às bordas e espaçamentos pelos furos. Uma ligação recua as extremidades das suas barras com o que põe entre elas (a viga com a sua chapa de topo, o pilar com a sua placa de base, o pilar de oitão com uma folga de 20 mm), pelas regras de extremidade das próprias barras.

![Uma ligação de beiral](shots/steel-nodes-eaves.webp)

![Uma base engastada sobre chumbadores com gancho](shots/steel-nodes-base.webp)

Selecione uma ligação para vê-la nas Propriedades: as barras que liga, cada parâmetro como está agora (um tamanho escolhido pelo perfil aparece como tal) e as suas peças: chapas, parafusos e chumbadores por tamanho e classe, as soldas por garganta com o seu comprimento, o seu peso, a sapata em que entram os chumbadores. Um parâmetro mudado ali vale só para essa ligação, e o galpão reconstruído o mantém. **Editar galpão…** abre o seu galpão.

![Uma ligação nas Propriedades](shots/steel-nodes-props.webp)

As barras desenhadas à mão recebem as suas ligações com **Steel → Encontrar ligações**: uma base sob cada barra vertical, uma ligação de beiral onde uma barra está ajustada a um pilar, a de um pilar de oitão onde um pilar está ajustado sob uma barra, uma cumeeira onde duas barras estão cortadas em meia-esquadria uma com a outra, uma emenda onde duas seguem em linha, ponta com ponta; para as barras selecionadas, ou o modelo inteiro quando nada está selecionado. **Steel → Peças de aço** cria a tabela das chapas, parafusos, chumbadores e soldas das ligações. Excluir uma ligação devolve às suas barras as suas extremidades; excluir uma barra leva as suas ligações; **Excluir galpão** no configurador remove o galpão com as suas barras, ligações e eixos, num só passo.

## Aço secundário

Um galpão vem com seu **aço secundário** (Steel → Portal frame hall…, aba **Aço secundário**), feito com ele e refeito com ele quando é alterado, cada barra mantendo sua identidade:

![A aba Aço secundário](shots/steel-secondary-hall.webp)

- **Aço secundário**: ligado ou não para todo o galpão.
- **Terças**: seu **Perfil** (Z, C e Σ formados a frio, um U ou um I leve), o **Espaçamento das terças** ao longo da inclinação, quanto vão **Além das empenas** e seus **Comprimentos**: **uma por vão** (de pórtico a pórtico) ou **contínua** (todo o comprimento do galpão). Apoiam sobre a face superior dos caibros, com a alma perpendicular à cobertura, de 150 mm para dentro da face dos pilares até 200 mm da cumeeira, em tantas linhas quantas o espaçamento pede.
- **Longarinas**: seu **Perfil** e o **Espaçamento das longarinas**, da **Primeira longarina a** até sob o beiral, uma por vão na face externa dos pilares, a alma horizontal e as abas para baixo; nas empenas, de pilar a pilar (ou entre os pilares de um pórtico de empena), tão alto quanto o caibro acima permite. Uma **Viga de beiral** ocupa o topo das fachadas longas (com seu **Perfil**), as longarinas um espaçamento abaixo.
- **Contraventamento**: os **Módulos contraventados** (seus números, a partir do primeiro), o **Perfil** das barras (barras redondas de Ø16 a Ø30, cantoneiras ou tubos) e onde: **Na cobertura** (um X no plano da cobertura, sob a mesa superior dos caibros, em painéis mais ou menos do comprimento do vão) e **Nas fachadas longas** (um X no plano das fachadas, pelos eixos dos pilares, da altura da primeira longarina até sob a mísula).
- **Escoras**: de cada segunda terça e longarina até a mesa inferior do caibro e a mesa interna do pilar, dos dois lados, fora das mísulas.

![O aço secundário em 3D](shots/steel-secondary-3d.webp)

Cada terça e longarina apoia numa **patilha** em cada caibro, pilar ou pilar de oitão que atravessa: uma chapa soldada na face da barra, junto à alma da terça (por fora da alma de um C, do lado de um Z oposto à sua mesa inferior), parafusada pela alma, dois parafusos por extremidade (M12 até 250 mm de altura, M16 acima). Um pilar de canto mais estreito que os pilares recebe uma patilha mais alta, até a longarina. Cada X apoia em **chapas de nó** soldadas à alma dos caibros ou dos pilares, uma diagonal em cada face; uma cantoneira ou um tubo é parafusado com dois parafusos, a ponta roscada de uma barra redonda leva sua porca. Uma escora é parafusada nas duas pontas. São ligações como as dos pórticos: selecione uma para ver sua chapa e seus parafusos nas Propriedades e alterá-los ali. O resumo do configurador conta e pesa terças, longarinas, viga de beiral, contraventamento e escoras; a tabela **Steel parts** traz suas patilhas, chapas de nó e parafusos.

![Uma terça na sua patilha, uma escora por baixo](shots/steel-secondary-cleat.webp)

![O contraventamento da cobertura nas suas chapas de nó](shots/steel-secondary-bracing.webp)

## Tabelas e desenhos

**Steel → Steel members** faz a tabela do aço do projeto: as barras, e as vigas e pilares de um perfil de aço, por galpão (os de nenhum galpão no fim), uma **Marca** para as barras de um tipo iguais em perfil, comprimento e cortes de extremidade (C pilares, R caibros, GP pilares de oitão, P terças, SR longarinas, EB vigas de beiral, B contraventamento, FB escoras, M outras barras, BM vigas), numeradas a partir do perfil mais pesado e da mais longa. Uma linha dá o **Perfil**, o **Comprimento**, o **Corte inicial** e o **Corte final** (quanto o plano da extremidade gira em relação ao esquadro, e um recorte: os ângulos da lista de corte), o **Número**, o peso por metro, **Cada** e no total, e a área de **Pintura**; um total sob cada galpão e um geral. **Steel parts** lista as chapas, parafusos, chumbadores e soldas das ligações, com suas patilhas e chapas de nó.

![A tabela de barras de aço](shots/steel-members.webp)

**Steel → Anchor plan** desenha as bases dos pilares do galpão para as fundações: uma planta do pavimento do galpão em 1:50, cortada logo acima das chapas de base, cada chumbador num círculo, sua distância ao eixo (± quando os chumbadores ficam simétricos em relação a ele), cada base marcada pelo seu tipo (**B1**, **B2**…: mesma chapa, chumbadores, classe e graute), os vãos e a largura da grelha, e a tabela dos tipos ao lado do galpão: número, tipo, chapa, chumbadores (número, tamanho, forma), classe e graute. **Steel → Frame elevation** desenha um pórtico (o típico, ou o da barra selecionada) visto ao longo do galpão em 1:50, com seu vão e as alturas do beiral e da cumeeira. Ambos são vistas do galpão (pedidas de novo, abre-se a mesma) e vão para as pranchas como qualquer outra; acompanham o galpão quando ele muda.

![Uma planta dos chumbadores](shots/steel-anchor-plan.webp)

![A elevação de um pórtico](shots/steel-frame-view.webp)

## IFC e quantidades

Uma barra é exportada como **IfcMember** com seu **IfcMemberType**. Seu corpo é escrito como os outros programas escrevem o aço (Tekla, Advance Steel, Revit): a seção **extrudada** (IfcExtrudedAreaSolid), **cortada** pelos planos das extremidades (IfcBooleanClippingResult com IfcHalfSpaceSolid), e cada recorte um **IfcOpeningElement** que a esvazia. Uma viga ou um pilar de aço com extremidades é escrito da mesma forma. As quantidades de cada barra dão o comprimento entre os cortes, a área da seção, o volume e o peso líquido (7850 kg/m³).

Um galpão é exportado como **IfcBuildingSystem** (LOADBEARING) ao qual suas barras são atribuídas, com seus dados no conjunto de propriedades `Lapin_SteelHall` e sua própria **IfcGrid**. Uma viga de cobertura com mísulas é um **IfcElementAssembly** da viga e das suas mísulas: uma mísula cortada do perfil é um **IfcMember**, uma de chapas soldadas duas **IfcPlate**, cada uma extrudada (IfcExtrudedAreaSolid). As mísulas ficam fora das quantidades da viga; o resumo do configurador as pesa. As terças são exportadas como **IfcMember** PURLIN, o contraventamento e as escoras como BRACE, as longarinas e a viga de beiral como USERDEFINED (**Side rail**, **Eaves beam**), todos atribuídos ao sistema do galpão com suas ligações.

Uma ligação exporta-se como **IfcElementAssembly** das suas chapas (**IfcPlate**), parafusos (**IfcMechanicalFastener** BOLT), chumbadores (ANCHORBOLT) e placas de ancoragem (**IfcDiscreteAccessory** ANCHORPLATE), cada um com tamanho e classe em `Lapin_SteelPart`; realiza a ligação das barras que une (**IfcRelConnectsWithRealizingElements**; uma base, entre a sapata e o seu pilar). As soldas ficam como dados, não como corpos.
