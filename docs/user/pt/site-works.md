---
title: Canteiro de obras
order: 55
summary: Andaimes de fachada e fôrmas para pisos e vigas, distribuídos a partir do modelo pelos seus configuradores.
---

A guia **Canteiro de obras** modela as obras provisórias do canteiro: andaimes de fachada e as fôrmas de pisos e vigas. O Lapin os distribui a partir do modelo e conta o que eles precisam. Quando o modelo muda, eles o acompanham.

![A guia Canteiro de obras](shots/ribbon-site-works.webp)

Andaimes e fôrmas são **provisórios**: são montados e desmontados na fase de **Execução**. O primeiro que você coloca adiciona essa fase se o projeto não a tiver e a torna a fase ativa. As vistas de fases anteriores os deixam de fora. Veja **Gerenciar → Fases**.

![Um andaime em volta da casa, a fôrma do primeiro pavimento sobre as escoras](shots/site-works-3d.webp)

## Andaimes

- **Andaime nas paredes** (<kbd>WS</kbd>): selecione as paredes (Shift-clique para mais) e pressione o botão. O andaime contorna as faces externas delas, na distância do seu tipo em relação à parede, até o topo das paredes. Ele fecha os cantos sozinho. Mova uma parede ou aumente a sua altura, e o andaime acompanha.
- **Andaime** (<kbd>FS</kbd>): desenhe a sua linha na planta ao longo da fachada e pressione Enter para terminar. O andaime fica à esquerda da linha. Escolha o outro lado nas propriedades.

Cada trecho reto é dividido nos comprimentos de módulo do tipo, com o menor número de módulos que alcança a ponta. Os níveis sobem até a plataforma superior ficar ao alcance do topo de trabalho. O guarda-corpo fica acima da plataforma superior. Os pés ficam sobre o terreno, em sapatas reguláveis. Se o terreno cair mais do que as sapatas compensam, o Lapin avisa.

![As propriedades de um andaime](shots/scaffold-properties.webp)

Nas propriedades você pode definir o **cota de trabalho**: deixe vazio para o topo das paredes. Você também pode escolher se os pés ficam **sobre o terreno** ou **no pavimento**, ou **desvinculá-lo** das paredes.

Cada andaime e cada fôrma tem um **tipo**. Você o escolhe no topo das propriedades, ou passa o tipo de um elemento para outros com Igualar tipo (MA). Em **Parâmetros de tipo** estão os valores que o configurador define: o sistema, os comprimentos de módulo, a largura, o nível, o compensado, os espaçamentos, o item de orçamento. Você também pode mudá-los ali. Uma mudança é do tipo, então todos os elementos desse tipo a acompanham. Criar semelhante (CS) desenha um novo do mesmo tipo. Um tipo que você cria no configurador é o que o próximo andaime ou a próxima fôrma recebe.

**Tipos de andaime…** abre o configurador. Ele define o sistema (modular ou de quadros), os comprimentos de módulo, a largura e o nível, a distância da parede, as plataformas, os guarda-corpos e os rodapés, as ancoragens, as sapatas reguláveis, os módulos de acesso e a tela de proteção. A elevação mostra um trecho do comprimento e da altura que você escolhe, com o que ele conta. Com um andaime selecionado, o configurador abre no tipo dele; clique em outro na lista para mudar esse outro. A lista diz quantos andaimes cada tipo tem. **Salvar tipo** aplica a mudança a todos os andaimes do tipo.

![O configurador de andaimes](shots/scaffold-configurator.webp)

## Fôrmas

- **Fôrmas** (<kbd>FW</kbd>): selecione pisos e vigas e pressione o botão. Cada um recebe a fôrma de todas as suas faces que podem ser formadas.
- **Fôrma na face** (<kbd>FF</kbd>): escolha uma face. Em 3D, a face sob o cursor se acende com o seu nome: o fundo de um piso, o fundo de uma viga ou uma das suas laterais. Na planta, clique na borda de um piso ou dentro do seu contorno. Uma face escolhida depois vai para a mesma fôrma.

A **fôrma de um piso** cobre o seu fundo com compensado sobre vigas secundárias, sobre vigas principais, sobre escoras. As vigas sob o piso não recebem fôrma de novo, nem as paredes e os pilares em que ele se apoia, nem as suas aberturas. Os espaçamentos vêm da tabela do tipo, pela espessura do piso. As escoras descem até o piso de baixo, o terreno ou o pavimento abaixo. As bordas livres também recebem fôrma, com a altura do piso mais uma platibanda. Nas propriedades você escolhe quais faces recebem fôrma e a direção das vigas principais.

A **fôrma de uma viga** é o seu fundo, sobre travessas e escoras, e as suas laterais. Cada lateral tem a altura da viga abaixo do piso concretado com ela. Pilares e vigas mais altas a interrompem. Você pode adicionar chanfros ao longo das bordas do fundo.

**Tipos de fôrma…** abre o configurador, para pisos e para vigas. Para pisos, ele define o sistema (vigas de madeira e escoras, ou painéis), o compensado, as vigas e os seus comprimentos de catálogo, a tabela de espaçamentos, as bordas, as escoras e os reaproveitamentos. Para vigas, ele define as travessas, as escoras, os gastalhos pela altura da viga, o chanfro e os reaproveitamentos. O corte mostra as camadas e a escora que o motor escolhe para a altura que você escolhe. Com uma fôrma selecionada, ele abre no tipo e no gênero dessa fôrma.

![O configurador de fôrmas](shots/formwork-configurator.webp)

## Paredes, pilares, cintas e sapatas

**Fôrmas** e **Fôrma na face** também funcionam em paredes, pilares e sapatas:

- **Uma parede** recebe as duas faces e as suas pontas livres, menos as portas e janelas. Uma parede em camadas com uma cinta ou uma faixa de concreto recebe só essas. Nas propriedades você pode escolher as duas faces, as cintas ou as pontas livres.
- **Um pilar** recebe as suas laterais, até o piso ou a viga concretada sobre ele.
- **Uma sapata** recebe as laterais de cada degrau de uma sapata isolada, ou as laterais da base e do colarinho de uma sapata corrida.

O que estiver encostado numa face fica de fora: outra parede, um pilar. Um piso concretado em cima para a face no seu fundo. O tipo define os painéis (as suas larguras e alturas), as ancoragens através das paredes, as gravatas dos pilares e as escoras de prumo.

![O configurador para paredes e pilares](shots/wall-formwork-configurator.webp)

## Planta de fôrmas

**Planta de fôrmas** cria uma planta do pavimento ativo na fase de Execução, cortada um metro acima do piso, **vista de baixo**: uma planta de forro. As escoras são cortadas. A fôrma acima delas é desenhada como você a veria olhando para cima: as vigas principais e secundárias, o compensado e as bordas. Ela fica orientada como a planta, para que as duas coincidam.

**Planta de fôrmas vista de cima** cria a mesma planta vista de cima, com a fôrma desenhada tracejada sobre o corte. Qualquer planta pode alternar entre as duas nas suas propriedades (**Visto de**) ou em **Intervalo de vista**.

![A planta de fôrmas do térreo](shots/formwork-plan.webp)

## O que eles contam

Cada andaime e cada fôrma lista as suas quantidades nas propriedades:

- a área, os módulos, os níveis, os montantes, as plataformas e as ancoragens do andaime;
- a área de contato da fôrma por face, as escoras por tamanho e as vigas por comprimento de catálogo.

**Tabela de fôrmas** e **Tabela de andaimes** adicionam as suas tabelas ao navegador do projeto, em Tabelas, para colocar numa prancha ou exportar.

A tabela de fôrmas termina com uma linha por tipo:

- **o conjunto**: o máximo de fôrma concretado de uma vez. Isso é por fase de execução quando a obra é dividida em várias, senão por pavimento.
- **o que comprar**: o jogo, ou o total dividido pelos reaproveitamentos do tipo, o que for maior.

Cada tipo tem o seu **item** para o orçamento: o código nas normas que o seu orçamento usa e o seu texto. Você os informa no configurador, e a tabela os mostra em cada linha.

No IFC, eles são gravados como conjuntos (`IfcElementAssembly` FORMWORK e SCAFFOLD) com o status TEMPORARY. As peças são membros, chapas e acessórios. Cada uma vem com o seu recurso de produto de construção, e a fôrma é ligada ao elemento que ela forma.
