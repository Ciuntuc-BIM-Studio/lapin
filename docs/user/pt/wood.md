---
title: Madeira
order: 54
summary: Paredes em wood frame pelo configurador: montantes num módulo, guias, travessas e cada porta e janela estruturada pelas suas regras.
---

## Paredes em wood frame

Um tipo de parede pode ser construído como wood frame: **Madeira → Wood frame** abre o configurador, com os tipos de parede do projeto à esquerda. Marque **Wood frame** e o núcleo do tipo passa a ser feito de peças em vez de uma camada: as guias, os montantes no seu módulo, as fileiras de travessas e, em volta de cada porta e janela, os montantes principais, os montantes de apoio, a verga e o peitoril. As outras camadas (revestimento, chapeamento, placas) ficam como estão, e o isolamento preenche as cavidades entre as peças.

![O configurador de wood frame](shots/wood-frame.webp)

- **Predefinições**: **Nórdica** (45 × 145 a cada 600 mm, C24) ou **Romena** (50 × 150 a cada 625 mm, C24, uma fileira de travessas a 1,25 m). Depois, qualquer campo pode ser mudado.
- **Montantes, espaçamento, classe, guias, montantes das pontas, fileiras de travessas**. **Módulo a partir de** define onde começa o módulo dos montantes: no início de cada parede ou na origem do projeto, para que as paredes de uma fachada o compartilhem.
- **Portante**: uma divisória não carrega nada e recebe uma peça deitada sobre as aberturas em vez de uma verga.

## Portas e janelas

As portas e janelas continuam sendo do arquiteto: mova uma, ou mude o seu tipo, e a estrutura acompanha. A estrutura é medida a partir do **vão bruto**: a porta ou janela mais as folgas de montagem (nas laterais, em cima, embaixo de uma janela; uma porta desce até o piso).

Cada abertura recebe a regra da sua **faixa de largura** (a tabela em **Aberturas**: até 900 mm, até 1800 mm, acima, por padrão): seus montantes principais e de apoio de cada lado, sua verga (nenhuma, maciça, LVL ou laminada colada, com sua seção e camadas) e as camadas do peitoril. Os montantes nunca ficam num vão bruto; os curtos acima da verga e abaixo do peitoril mantêm o módulo, de modo que as juntas das placas continuam caindo sobre montantes. Duas aberturas mais próximas que o **menor pilarete** compartilham uma verga e os seus montantes principais externos. Uma abertura próxima demais da ponta da parede é indicada nas propriedades da parede, nunca movida.

Selecione uma porta ou janela: a sua seção **Madeira** mostra o vão bruto, a regra que ela segue e o que recebe. Marque **Regra própria** para dar só a essa abertura outros montantes principais ou de apoio, outra verga ou outro peitoril.

## Cantos, placas e a elevação da estrutura

- **Cantos**: onde outra parede termina contra a ponta desta (um canto de topo), a parede que passa direto recebe **três montantes** (um além da face interna da outra parede), um canto **Califórnia** (um montante deitado na face interna, para o revestimento) ou **só os montantes de extremidade**. Um canto em meia-esquadria mantém os montantes das pontas das duas paredes.
- **Encontros em T**: onde uma parede termina contra esta ao longo do seu comprimento, esta recebe **um montante de cada lado** dela, ou **montantes e bloqueio em escada** entre eles.
- **Chapas**: marque uma camada da composição (OSB, placa de fibra, gesso acartonado) e informe o tamanho da chapa: a camada é colocada como chapas em pé, com as juntas no módulo dos montantes, cortadas nos vãos brutos e ao longo do topo. **Madeira → Tabela de chapas** conta as chapas que cada parede leva, a área líquida e o que é cortado fora.
- **Elevação da estrutura**: na seção **Madeira** de uma parede estruturada, o botão cria um corte ao longo da parede pelo meio do núcleo, em 1:20, com cada peça cortada, uma linha de cotas pelos vãos brutos, o módulo dos montantes embaixo e o tamanho de cada vão bruto. Coloque-o numa prancha como qualquer vista.

## Painéis pré-fabricados

Marque **Painéis pré-fabricados** para fazer a estrutura na fábrica: informe o **painel mais longo** (tão longo quanto a mesa da fábrica e o caminhão permitem) e o **mais curto**. Cada parede é cortada em painéis na junta mais longa dentro desse comprimento, no módulo dos montantes onde possível, nunca através dos montantes principais, de apoio, da verga ou do peitoril de uma abertura, nem através do reforço de um encontro em T. Cada painel é estruturado por si, com os seus próprios montantes das pontas e guias nas juntas. Um painel que não cabe no comprimento (uma abertura larga demais para ele) fica inteiro e a parede avisa.

![Uma parede pré-fabricada no configurador](shots/wood-panels.webp)

Os painéis são marcados com o nome da parede e o seu número ao longo dela (W3-P1, W3-P2…). A seção **Madeira** da parede lista os seus comprimentos; a elevação da estrutura desenha as juntas e as marcas sobre a parede; **Madeira → Painéis de parede** lista cada painel com a sua parede, comprimento, altura, aberturas, peças, volume de madeira e massa, para que possam ser encomendados, fabricados e içados em ordem. As placas de um painel também levam a sua marca. No IFC, cada painel é um IfcElementAssembly (Timber frame panel) com as suas peças e placas.

## CLT

No mesmo configurador, um tipo de parede ou de piso pode ser construído **em painéis CLT**: informe as lâminas (a espessura de cada uma, 3, 5, 7 ou 9 delas), a classe, o painel mais longo e o mais alto (ou mais largo) que a prensa faz, a junta entre painéis (de topo, meia-madeira, lingueta), a distância que uma abertura mantém da borda de um painel e a sua folga de montagem. Uma parede é cortada em painéis ao longo do seu comprimento, com as juntas afastadas das aberturas; cada painel é o próprio corpo do núcleo, com os vãos brutos recortados. Um piso é cortado em painéis transversalmente ao vão (no sentido curto, ou ao longo de x ou y). **Madeira → Painéis CLT** lista cada painel com a sua marca, tamanho, lâminas, classe, junta, área e volume; no IFC, cada um é um IfcBuildingElementPart chamado CLT panel.

- **Juntas**: as duas metades de uma **meia-madeira** se sobrepõem pela sobreposição; uma junta de **lingueta** tem um rebaixo numa face e uma tira de compensado nele, uma parte própria. As duas são modeladas nos painéis, de modo que os volumes e a tabela são o que a fábrica corta.
- **Cantos das aberturas**: uma fresa não corta um canto interno em esquadro. **O raio da fresa** fica em cada canto; um **sobrecorte em osso de cachorro** corta um círculo em cada canto para que um caixilho em esquadro ainda caiba. O configurador desenha as fresas.
- **Elevação dos painéis**: a seção **Madeira** de uma parede de CLT cria um corte ao longo dela em 1:20, com as juntas e as marcas dos painéis e os tamanhos dos vãos brutos.
- **Telhados de CLT**: selecione um telhado inclinado e marque **Painéis CLT** na sua seção **Madeira**: as suas águas são colocadas como placas de CLT em vez dos caibros ou das tesouras (os frechais e a cobertura ficam), com as janelas de telhado e as águas-furtadas recortadas, em faixas da largura do painel ao longo do beiral, emendadas de topo onde uma água é mais longa que um painel.

## Pisos com barrotes

Um tipo de piso pode ser construído **sobre barrotes**: escolha-o no configurador e selecione **sobre barrotes**. Informe a seção, o espaçamento e a classe dos barrotes, em que sentido eles vencem o vão (o sentido curto, ou ao longo de x ou y), se um **barrote de borda** fecha as bordas, as **fileiras de bloqueio** e, em volta de cada abertura no piso (uma caixa de escada, um shaft), os **barrotes de reforço** de cada lado e as **camadas do cabeceiro** nas suas pontas. Os barrotes interrompidos por uma abertura param no cabeceiro dela, e um barrote extra é colocado onde os barrotes de reforço deixam um espaço maior que o espaçamento. **Preencher as cavidades** coloca o material do núcleo entre os barrotes. O configurador mostra um piso de exemplo com uma caixa de escada.

![Um tipo de piso sobre barrotes](shots/wood-joists.webp)

Os barrotes substituem o núcleo do piso; as camadas acima e abaixo ficam. As suas peças estão na tabela de madeira, e no IFC cada uma é um IfcBeam (JOIST, um barrote de borda ou um cabeceiro pelo nome) sob o piso.

## A tabela de madeira

**Madeira → Tabela de madeira** cria uma lista de corte de cada peça de madeira do projeto, as peças das paredes e a estrutura dos telhados: peça, seção, classe, comprimento, quantidade, comprimento total e volume, com os totais por seção. No IFC, cada peça é um IfcMember (STUD, PLATE, uma verga ou um peitoril pelo nome) sob a sua parede, e o isolamento é um IfcCovering INSULATION.
