---
title: Ambientes e cores de ambientes
order: 45
summary: Coloque ambientes com um clique entre as paredes, dê nome e número a eles e pinte-os ou hachure-os em uma planta por função ou departamento, com uma legenda.
---

## Colocar um ambiente

Escolha **Arquitetura → Ambiente** (<kbd>RM</kbd>) e clique dentro de um espaço fechado por paredes. O Lapin encontra o contorno do ambiente entre as faces das paredes, das paredes-cortina e dos pilares em volta do ponto. As portas não abrem o ambiente: ele se fecha através delas. Um pilar dentro do ambiente fica fora da área dele.

O ambiente acompanha as paredes. Mova uma parede e o ambiente cresce ou encolhe com ela. Se uma parede for excluída e nada mais fechar o ambiente, ele mantém o contorno que tinha, e o carimbo dele fica vermelho. **Arquitetura → Atualizar ambientes** guarda os contornos que os ambientes encontram agora.

Para um espaço sem paredes em toda a volta, como um terraço ou uma ponta de uma área aberta, use **Ambiente por contorno** e clique nos cantos dele. Esse ambiente mantém o contorno que você desenha.

![Uma planta com dois ambientes coloridos por função e uma legenda de cores](shots/rooms-plan.webp)

## As propriedades de um ambiente

Selecione um ambiente clicando no carimbo dele ou em qualquer lugar dentro dele. No painel Propriedades:

- **Nome** e **Número.** Os ambientes novos são numerados por nível: 101, 102… no térreo. O nome de um ambiente é o da função dele até você dar um nome próprio; se ficar vazio, volta para a função.
- **Função**, **Departamento** e **Acabamento de piso.** Digite qualquer coisa ou escolha um valor que outros ambientes já têm.
- **Altura.** Vazia significa a altura do nível.
- **Cor própria** e **Hachura própria** substituem o esquema de cores só para este ambiente.

Os quantitativos abaixo mostram a área líquida do ambiente (sem os pilares), o perímetro e o volume. Arraste a alça no carimbo para mover o carimbo.

![As propriedades de um ambiente](shots/room-properties.webp)

## Etiqueta de ambiente

**Arquitetura → Identificador de ambiente** (ou **Identificador de ambiente…** nas propriedades de um ambiente) define o que o carimbo de cada ambiente mostra nas plantas, linha por linha: o nome, o número, a função, o departamento, a área, a área bruta, o perímetro, a altura, o volume ou o acabamento de piso, ou um texto seu. A primeira linha fica em negrito. **Padrões de desenho → Identificadores** mostra a mesma etiqueta como texto: `{name}|{number}|{area}`.

![A caixa de diálogo Etiqueta de ambiente](shots/room-tag.webp)

## Cores de ambientes

**Vista → Cores dos ambientes** pinta os ambientes de uma planta. Escolha um esquema, escolha pelo que ele pinta (**Função**, **Nome**, **Departamento**, **Número** ou **Acabamento do piso**) e ajuste **Esta planta mostra** para o esquema. Cada valor que os ambientes têm ganha uma linha com:

- uma **Cor**,
- uma **Hachura** dos padrões do projeto, com a **Escala** e a cor da **Tinta**,
- e uma prévia de como fica.

**Completar as cores que faltam** dá a cada valor sem cor uma de uma paleta suave. **Novo** e **Duplicar** criam mais esquemas, por exemplo um para uma apresentação ao cliente e outro por departamento.

![A caixa de diálogo Cores de ambientes](shots/room-colors.webp)

As cores são desenhadas sob as paredes, os móveis e os giros das portas, para que a planta continue legível. Elas saem impressas nas pranchas e vão para as exportações em PDF e DXF. Uma planta nova não mostra cores de ambientes até você escolher um esquema para ela, em **Cores dos ambientes** ou nas propriedades da planta.

## Legenda de cores

**Anotar → Legenda de cores**: clique onde a legenda vai ficar na planta. Ela lista as cores que os ambientes da planta usam, com os nomes, e acompanha qualquer mudança no esquema. Mova-a com **Mover**.

## Cotas de ambientes

**Anotar → Cotas dos ambientes** (<kbd>ID</kbd>) cota o interior dos ambientes de uma planta de uma vez. Cada lado de um ambiente ganha uma linha de cotas alinhada logo por dentro dele. A linha tem um ponto em cada canto do lado e nos batentes de cada porta e janela nele.

- Com ambientes selecionados, só eles são cotados. Sem nada selecionado, todos os ambientes do nível da planta são.
- Os pontos ficam presos às paredes, como os de qualquer cota. Mova uma parede e as linhas de cotas a acompanham.
- Execute de novo em um ambiente para refazer as linhas de cotas dele, por exemplo depois de adicionar uma porta. As linhas antigas são substituídas, não duplicadas.
- Lados muito curtos, abaixo de 30 cm, ficam de fora. Edite ou exclua qualquer linha de cotas como faria com uma que você desenhou.

![Dois ambientes cotados por dentro: a linha de cotas de cada lado com a porta e as janelas nele](shots/room-dimensions.webp)

## Acabamentos de ambientes

**Acabamentos → Acabamento no ambiente** (<kbd>FR</kbd>) aplica um acabamento no ambiente em que você clica: o piso, o acabamento das paredes e o teto, cada um seguindo o contorno do ambiente como o próprio ambiente. Mova uma parede e os acabamentos vão com ela. **Acabamentos → Acabamentos de ambientes** edita os acabamentos; **Da biblioteca** traz alguns prontos: Sala de estar, Quarto, Banheiro, Cozinha, Térreo sobre laje e Sala técnica. Enquanto a ferramenta está ativa, o painel de propriedades mostra o acabamento que ela aplica, para escolhê-lo antes do clique. O **Acabamento** de um ambiente, nas suas propriedades, aplica outro ou o retira.

Um acabamento tem:

- **Piso**, suas camadas do piso acabado para baixo (piso cerâmico, argamassa colante, contrapiso…). As aberturas da laje no ambiente (um vão de escada) são descontadas. **Pelas portas, até o eixo da parede** continua o piso sob cada porta, de modo que o piso do ambiente vizinho o encontra no eixo da parede.
- **Paredes**, suas camadas da face da parede para o ambiente, também em volta das ilhas do ambiente. Cada camada pode mudar de material na altura, como a camada de um tipo de parede: no Banheiro, pintura acima de azulejos até 2100 (13 mm de espessura, azulejos e argamassa colante); na Cozinha, uma faixa de azulejos de 900 a 1500. Uma peça sem material não deixa nada ali. As portas e janelas das paredes em volta do ambiente são recortadas, e seus requadros revestidos com as mesmas camadas, da face do ambiente até o marco (num vão sem esquadria, até o eixo da parede); o fundo de uma janela também, a menos que um peitoril o cubra do lado do ambiente. **Altura**: vazia, até o teto.
- **Teto**, suas camadas da laje acima do ambiente para baixo (a partir da altura do ambiente quando não há laje), com as aberturas descontadas.
- **Rodapé**: um tipo de rodapé aplicado em volta do ambiente, interrompido nas portas (veja Rodapés, em Pisos e telhados).
- **Uma laje estrutural sob o piso**: uma laje de concreto armado própria sob as camadas do piso, com sua espessura e quanto passa além do contorno do ambiente (sob as paredes). É uma laje como qualquer outra, na estrutura e na sua tabela.

**Salvar** altera o acabamento, e cada ambiente que o tem é refeito. **Aplicar aos ambientes selecionados** o aplica nos ambientes selecionados.

Cada acabamento mede, por material, sua área **bruta** (o contorno do ambiente, sem descontos: a área do piso, o perímetro das paredes vezes a altura de cada peça) e sua área **líquida** (o que é executado: sem as portas, janelas e aberturas da laje, com os requadros e o piso sob as portas; a parte dos requadros está nos quantitativos, como RevealArea), e seu volume. **Acabamentos → Tabela de acabamentos** lista cada ambiente com acabamento por parte e material, com os totais por material no fim; como qualquer tabela, gera gráficos, é exportada e vai para uma prancha. No IFC um acabamento é um IfcCovering: FLOORING, CLADDING ou CEILING.

Um acabamento reveste o que as paredes do ambiente já têm: se os tipos de parede trazem o próprio reboco, dê às paredes do ambiente só o que vai por cima (pintura, azulejos), ou use tipos de parede só com o núcleo.

![Os acabamentos de ambientes: o piso do Banheiro, paredes com azulejos até 2100 e o teto](shots/room-finishes.webp)

![As propriedades de um ambiente com seu acabamento](shots/room-finish-properties.webp)

## Ambientes em 3D e no IFC

Os ambientes ficam ocultos na vista 3D, para não cobrir o edifício. **Visibilidade / Gráficos** na vista 3D pode mostrá-los.

Em uma exportação IFC, cada ambiente é um IfcSpace, com número, nome, função, áreas e volume.

## Ambientes de um arquivo IFC

**Arquivo → Importar IFC…** traz os espaços do arquivo como ambientes, seja qual for o programa que os gravou (Revit, ArchiCAD, Allplan, Bonsai…):

- cada um vai para **o seu próprio nível**, aquele em que o arquivo o coloca;
- o **contorno** vem da forma dele (uma extrusão, um B-rep ou uma malha, ou a projeção desenhada ao lado); ele mantém esse contorno, não acompanha as paredes;
- o **nome** é o nome longo do espaço e o **número** é o nome dele (como o Revit e o ArchiCAD os gravam); a **altura** e o **deslocamento da base**, quando ele começa acima do nível (uma galeria, um piso elevado), vêm da forma dele;
- a **função**, o **departamento** e o **acabamento de piso** vêm do arquivo quando ele os informa (o tipo de objeto ou a categoria do espaço, uma propriedade Department, o revestimento de piso), assim um esquema de cores por função ou por departamento os pinta na hora;
- todo o resto que o espaço tinha no arquivo, os conjuntos de propriedades e os quantitativos (Pset_SpaceCommon, BaseQuantities, os conjuntos próprios do programa), é mantido e listado, somente leitura, em **Propriedades IFC** nas propriedades dele, com um filtro quando são muitos.

A mensagem da importação diz quantos ambientes entraram. Procure-os nas plantas dos seus níveis (cada um mostra o carimbo), em uma tabela de **Ambientes** (**+** em Tabelas no navegador) ou na guia IFC do navegador; a vista 3D oculta os ambientes.

Um espaço cuja forma não dá nenhum contorno entra mesmo assim, com nome e propriedades, como um ambiente sem contorno: ele aparece na tabela, mas não é desenhado em lugar nenhum. Selecione-o (na guia IFC do navegador), abra a planta do nível dele e use **Encontrar com um clique** (clique entre as paredes dele) ou **Desenhar contorno** nas propriedades dele.
