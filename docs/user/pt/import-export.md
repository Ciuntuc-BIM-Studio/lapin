---
title: Importar e exportar
order: 110
summary: Troque o modelo como IFC, traga desenhos DXF, vincule arquivos e exporte modelos 3D para renderização e RA.
---

Tudo o que entra ou sai está no menu **Arquivo**.

![O menu Arquivo](shots/file-menu.webp)

## IFC e Fragments

**Exportar IFC ou Fragments…** grava o modelo como IFC. Cada parede, laje, porta e telhado é um elemento IFC de verdade, com as suas camadas, o seu tipo e as suas propriedades, então o arquivo abre como um modelo em outras ferramentas BIM. Portas e janelas dizem como abrem: a mão de cada folha, para que lado ela gira e os painéis, do jeito que o IFC os define, para que outros programas desenhem as mesmas aberturas.

A exportação pede o esquema (**IFC4**, lido em todo lugar, ou **IFC4X3**), o detalhe (as partes como elementos próprios, ou um corpo por elemento), a fase e como os corpos são gravados. **Geometria** é um **B-rep facetado** (exato, o que outros programas esperam com mais frequência) ou **conjuntos de faces**: as mesmas faces em um arquivo muito menor. **Simplificar** grava menos triângulos onde um corpo é uma malha fina, como os dutos e as tubulações do arquivo de outro programa, nunca mais longe dele do que a **tolerância** que você digitar (2 mm para começar). Os corpos que o próprio Lapin constrói (paredes, lajes, portas) mantêm as suas faces exatas, e simplificar muda só a forma de um corpo, nunca as suas propriedades.

![Exportar IFC](shots/export-ifc.webp)

Vigas, pilares e perfis de aço são gravados pelo seu perfil paramétrico, com nome e valores mecânicos ([Bibliotecas de perfis](profiles.md)).

**Importar IFC…** lê o IFC de outro programa. Antes que qualquer coisa entre, o Lapin lê o arquivo e mostra o que ele contém: os seus elementos por disciplina (arquitetura, estrutura, MEP, mobiliário e equipamentos, terreno), cada disciplina por tipo com quantos há, e os seus pavimentos com quantos elementos estão em cada um. Desmarque o que você não precisa, por exemplo MEP e mobiliário quando você trabalha na estrutura, ou mantenha só os pavimentos em que você trabalha. **Como** traz os elementos reconhecidos como elementos do Lapin, todos eles como referências, ou vinculados. Enquanto o arquivo é lido e importado, o Lapin mostra o que está fazendo e em que ponto está; um arquivo grande leva um tempo.

![Importar IFC](shots/import-ifc.webp)

Alguns arquivos trazem 2D além do modelo: símbolos de planta, projeções, hachuras e textos desenhados para um elemento, ou elementos desenhados só em 2D (linhas, textos, símbolos sem corpo). Quando um arquivo os tem, a caixa de diálogo os conta em **Desenhado em 2D**. Desmarque **o 2D próprio dos elementos** e uma planta desenha esses elementos pelo seu corte, como faz com os do Lapin; desmarque **elementos desenhados só em 2D** e eles ficam de fora. Os dois entram, a menos que você os desmarque.

![Desenhado em 2D](shots/import-ifc-2d.webp)

**Limpeza e otimização** reúne as regras da importação, as mesmas para qualquer arquivo, guardadas como suas para a próxima importação. **Manter uma única vez um corpo repetido** (ativado, a menos que você desmarque) guarda uma única vez um corpo que o arquivo repete com outros nomes (um parafuso, um flange, um suporte), com os seus elementos compartilhando-o, sem perder nada. As outras deixam de fora, quando marcadas, o que outro programa trouxe junto: **corpos planos** (textos, linhas e hachuras transformados em triângulos), **elementos mais distantes do resto** do que a distância que você digitar (os textos de um desenho, um carimbo), **duplicatas** no mesmo lugar e **portas de conexão e aberturas gravadas como objetos**. **Simplificar malhas** grava menos triângulos dentro da tolerância que você digitar; **ler números escritos como texto** transforma `DoubleLength:0,01` em 0,01 m e `1.15395 kg` em uma massa (um número inteiro sozinho, um código, continua texto). Em **O que importar**, **Conjuntos de propriedades** lista os conjuntos do arquivo com quantos elementos têm cada um; os **técnicos** (a transformação do Navisworks, ids internos, estatísticas de geometria) ficam de fora, a menos que você os marque. A mensagem da importação diz o que cada regra fez.

![Limpeza e otimização](shots/import-ifc-rules.webp)

O que o arquivo posiciona entra como um grupo com o nome dele, que você pode abrir e organizar, ou trazer solto desmarcando **Como um grupo** (veja [Grupos e organização](groups.md)).

**Pavimentos.** Os pavimentos de uma importação são os do arquivo: eles substituem os do seu projeto. Cada pavimento do arquivo entra com o seu nome, o seu GlobalId e a sua cota como o arquivo a dá, lida na unidade do arquivo (milímetros ou metros). Um pavimento do seu projeto na cota de um dos pavimentos do arquivo (com até 5 cm de diferença) é unido a ele: os seus elementos ficam onde estão, e as suas vistas, a sua planta e o que nela foi desenhado passam para o pavimento do arquivo, que mantém o nome do arquivo. Um pavimento seu que não está em nenhuma cota do arquivo é removido quando não contém nada (nenhum elemento, nada desenhado na sua planta), e as suas vistas passam para o pavimento do arquivo mais próximo; um que contém algo fica, e a mensagem da importação diz qual. Assim, as Fundações, o Térreo e o Primeiro pavimento de um projeto novo dão lugar aos do arquivo. Tudo é um só passo de desfazer junto com a importação. Um arquivo vinculado guarda os seus pavimentos para si e não muda nenhum dos seus. Quando a importação termina, a vista 3D enquadra o modelo, a menos que você tenha movido a câmera enquanto ele entrava.

**Tudo editável**, uma opção de **Como**, vai além do reconhecimento: o que continua referência vira um sólido do Studio feito dos seus próprios triângulos, o mesmo elemento com a sua classe, propriedades, pavimento e grupo, que aceita empurrar/puxar, arredondamentos e operações booleanas na hora (veja [Studio](studio.md)). Um corpo com mais de 5.000 triângulos, como uma bomba ou uma conexão com malha fina, continua referência, e a mensagem da importação diz quantos e por quê. **Importar Fragments…** tem o mesmo **Como**: um arquivo Fragments contém só triângulos, mas as suas paredes (com as suas portas e janelas), lajes e pilares são reconhecidos a partir deles como elementos do Lapin, o resto mantido como referências ou, com **Tudo editável**, transformado em sólidos do Studio. Os elementos de um arquivo IFC que o reconhecimento não conseguiu ler do arquivo (uma parede com as suas aberturas recortadas no corpo) também são tentados a partir dos seus triângulos. **Importar malha** pergunta **Como: editável**. As referências trazidas antes são convertidas com Modificar → **Converter em nativo** (NV): reconhecidas onde for possível, o resto transformado em sólidos do Studio, sendo um grupo selecionado os seus membros. Uma referência trazida antes fica editável, uma de cada vez, com Studio → **Tornar sólido Studio**, que a mantém no seu grupo e no seu pavimento.

O Lapin reconhece as paredes, lajes, pilares, vigas, fundações, portas e janelas e os transforma em elementos do Lapin que você pode editar. O que ele não consegue reconhecer entra como referência, desenhada como o arquivo a dá: a sua forma não se edita, mas ela é movida, girada, espelhada, copiada e repetida como qualquer elemento. Um elemento só se torna elemento do Lapin quando é o do próprio ficheiro: o Lapin mede cada um que reconhece face ao corpo que o ficheiro lhe dá, e um que difere mais de 1 mm ou de 0,5 % do volume (um pilar cujo topo o ficheiro inclina, uma extremidade de parede em degraus, que um elemento do Lapin deixaria retos) entra como a referência que era; a mensagem da importação diz quantos e porquê. Portas e janelas entram abrindo do jeito que o arquivo diz: a sua mão, o lado para o qual abrem e, nas janelas, como cada painel abre. Uma cobertura plana de outro programa entra como cobertura do Lapin quando o seu IfcRoof é feito de duas ou mais lajes empilhadas, ou de um só corpo com um conjunto de camadas: a laje (ou camada) de cima é o revestimento, com o seu material e espessura, as de baixo a laje da cobertura, do material da mais espessa; o contorno é o da laje, sem beiral, na sua face inferior. Uma cobertura inclinada, ou uma de uma só laje sem camadas, fica como veio. Os elementos reconhecidos recebem apenas o que o ficheiro tem: sem vergas, peitoris, soleiras ou ombreiras do Lapin, e cada vão com o tamanho do ficheiro; ative-os nos tipos dos elementos se os quiser.

Os acabamentos que outro programa exportou como paredes próprias (um reboco de 2,5 cm em uma fachada, um gesso por dentro) viram uma camada da parede que cobrem, quando estão encostados na sua face, da mesma altura e ao longo dela: a parede ganha um tipo com essa camada (*Alvenaria 350 + Reboco de cal*), as suas próprias camadas onde estavam, e a mensagem da importação diz quantos. Um acabamento afastado da sua parede, ou que cobre só parte dela, continua uma parede fina. Um pilar que o arquivo dá como um corpo cortado no topo por uma inclinação (sob uma viga ou um telhado inclinado) entra como um pilar da sua seção, até o seu ponto mais alto.

**Mapear tipos importados** (Gerenciar) lista os tipos que uma importação criou (tipos de parede, laje, porta, janela, pilar, viga e fundação), cada um com o seu arquivo e quantos elementos tem. Escolha um dos tipos do próprio projeto, do mesmo tipo, em **Vira** e pressione **Mapear**: os seus elementos pegam o tipo do projeto, com uma parede mantendo o seu núcleo onde estava, e o tipo importado sai. Com **Lembrar** marcado, uma importação posterior de um tipo dessa categoria e nome pega o do projeto na hora, e a mensagem dela diz isso. As escolhas lembradas são listadas na caixa de diálogo, cada uma com **Esquecer**.

![Mapear tipos importados](shots/map-types.webp)

Os espaços do arquivo (do Revit, do ArchiCAD e de outros) entram como ambientes do Lapin, cada um no seu próprio nível, com o seu contorno, número, nome, altura, função, departamento e acabamento de piso: veja [Ambientes](rooms.md#ambientes-de-um-arquivo-ifc). Tudo o que um elemento importado tinha no arquivo (os seus conjuntos de propriedades e quantitativos) é mantido e mostrado em **Propriedades IFC** nas suas propriedades. Uma referência sai em **Exportar IFC…** como entrou: com o mesmo GlobalId, classe e atributos (descrição, etiqueta, tipo predefinido), os seus conjuntos de propriedades e quantitativos com cada valor tipado como o arquivo o tinha, e o seu tipo, material, portas de conexão, conexões e sistema, cada um com o seu GlobalId. Traga só o MEP de um arquivo e exporte-o, e o novo arquivo o leva inteiro.

**Importar Fragments…** abre o mesmo painel para um arquivo `.frag` da That Open: os seus elementos por disciplina e tipo, os seus pavimentos e **Como um grupo**. Um arquivo exportado deste mesmo projeto não traz nada de novo quando é importado de volta nele: os seus elementos já estão aqui.

![Importar Fragments](shots/import-frag.webp)

**Importar Fragments…** traz um arquivo `.frag`: cada item com geometria vira uma referência desenhada a partir do seu corpo, na classe que o arquivo indica, no pavimento em que o arquivo o coloca (os pavimentos do arquivo substituem os seus: veja **Pavimentos** acima), com os atributos do arquivo em **Propriedades IFC**. Itens com o mesmo corpo (a mesma forma, colocada em muitos lugares) o compartilham. Importar o mesmo arquivo de novo atualiza o que ele trouxe antes. O que é reconhecido a partir dos seus triângulos depende de **Como**, como acima. Os seus conjuntos de propriedades e de quantidades, o seu tipo e os seus materiais vêm com os atributos; o vidro continua transparente; um arquivo feito pelo conversor da That Open fica onde o seu IFC o colocaria, e as suas faces com furos (uma parede em volta da janela) os mantêm.

**Exportar IFC ou Fragments…** grava o modelo como IFC ou como Fragments, escolhido como em uma importação: os elementos por disciplina e tipo, os pavimentos, a fase e só o que a vista 3D mostra. O IFC sai em IFC4 ou IFC4X3, com as partes de um elemento com camadas abaixo dele ou um corpo para cada; o Fragments a partir do ponto base do projeto, da origem interna ou do ponto de levantamento. Um arquivo Fragments leva o que um visualizador IFC mostra: o tipo de cada elemento, as suas quantidades, os seus parâmetros, os seus materiais e as coordenadas do mapa, como o conversor da That Open as grava.

![Exportar IFC ou Fragments](shots/export-exchange.webp)

### SDNF (detalhamento de aço)

SDNF, o Steel Detailing Neutral File, é o formato de texto em que os programas de detalhamento de aço trocam uma estrutura (Tekla Structures, Advance Steel, SDS2, ProStructures, E3D): cada barra uma linha com o seu perfil, a sua classe, o ponto da seção por onde passa a linha e a sua rotação; chapas; eixos. O Lapin escreve a versão 3.0 e lê a 3.0 e a 2.0, nas edições com o módulo de aço.

**Exportar IFC, Fragments ou BOS…** com **SDNF** marcado escreve os pilares e as vigas (de qualquer material) e as barras de aço dos pavimentos e tipos escolhidos, as chapas das suas ligações e os eixos, em milímetros. Cada barra mantém o nome do seu perfil (IPE 300, HEA 240, um perfil da sua própria biblioteca), a sua classe e um número ligado ao seu id, para que o arquivo possa voltar. As vigas curvas saem como barras curvas, e os furos das chapas e das barras como furos, entre eles os dos parafusos e chumbadores das ligações.

![Exportar SDNF](shots/export-sdnf.webp)

**Importar SDNF…** traz um arquivo como elementos próprios do Lapin, num só passo que pode desfazer: um pilar vertical como pilar, uma viga horizontal como viga, todo o resto (um contraventamento, uma asna, uma barra inclinada) como barra de aço, cada um no seu pavimento, com o perfil tirado dos perfis do projeto e da biblioteca do Lapin; as chapas e os eixos também entram. Um perfil que nenhuma biblioteca tem (W12X50, quando não tem uma tabela AISC) mantém o nome numa seção provisória e é listado, até dar um perfil ao seu tipo. Um arquivo escrito pelo Lapin, ou o mesmo arquivo de novo depois de o detalhista o alterar, atualiza os elementos de onde veio em vez de acrescentar novos. Uma barra curva horizontal entra como viga curva (uma que se curva em outro plano fica de fora e é indicada); os furos são cortados nas chapas e barras que atravessam e substituídos, não duplicados, quando o arquivo volta. O SDNF não traz parafusos, por isso só chegam os seus furos; as liberações nas extremidades e as cargas não são importadas.

![O galpão lido de volta do SDNF](shots/import-sdnf-3d.webp)

## JG Structures (cálculo estrutural)

**Exportar JG Structures (JSON)…** escreve o modelo analítico do projeto no arquivo de intercâmbio do JG Structures, um programa de cálculo estrutural: um arquivo JSON conforme o seu contrato (versão 1.0.0). O Lapin deriva o modelo do que você desenhou. Pilares, vigas e barras de aço viram barras nos seus eixos; as paredes portantes (Pset_WallCommon.LoadBearing na parede ou no seu tipo) e as lajes viram cascas no meio da sua camada resistente, sem acabamentos. Elas compartilham os nós: uma viga que sustenta uma laje fica no plano médio da laje, as extremidades de pilares e paredes chegam à viga ou à laje no topo e na base, os cantos de uma laje passam para os pilares e paredes abaixo dela, e a laje é dividida ao longo das vigas que a atravessam. As bases de pilares e paredes sobre uma fundação são engastadas; a base de um pilar de aço é articulada ou engastada, como diz a sua ligação.

Escolha os pavimentos, os tipos de elementos e as unidades: os centímetros e quilogramas-força do contrato, ou o SI (metros e quilonewtons). Os materiais tomam seus valores dos seus dados (Gerenciar → Materiais, Pset_MaterialMechanical em pascais) ou da classe de resistência no nome, C25/30 ou S355, pelos Eurocódigos; a área, as inércias, os módulos e a constante de torção de uma seção são calculados da sua geometria. O que o Lapin não sabe fica vazio (null) no arquivo, nunca zero. Antes de exportar, o relatório conta o que entra, verifica o arquivo pelas regras do contrato e lista o que ainda falta ao cálculo: um material sem classe, um pilar que não se apoia em nada.

Onde os elementos não se encontram nos seus eixos (uma madre sobre uma travessa, uma madre de fachada num pilar, um contraventamento na sua chapa de gousset, as varas sobre o frechal), o Lapin coloca o nó no elemento que os suporta e guarda a distância como excentricidade na extremidade da barra, pelo que a estrutura secundária de um pavilhão também fica ligada. As paredes mantêm as suas portas, janelas e aberturas, e as lajes as suas aberturas: como as cascas do contrato não têm furos, são divididas em peças à volta delas, e uma laje em L em peças convexas ao longo das vigas que a suportam. Uma parede sob uma cobertura sobe até ela, e a cobertura também é exportada: a sua laje como cascas, ou as varas, madres e frechais de uma cobertura de madeira como barras da sua classe de resistência (C24 segundo a EN 338). As cantoneiras e outras secções não simétricas são dadas segundo os seus eixos principais. **Apoios sobre fundações** escolhe como são fixados os pés de pilares e paredes sobre uma fundação: encastrados (predefinido) ou articulados. A base de um pilar de aço mantém a sua própria ligação.

![Exportar JG Structures](shots/export-jg.webp)

## Otimizar um arquivo IFC

**Otimizar IFC…** (em Arquivo, ou na tela inicial: nenhum projeto precisa estar aberto) faz uma cópia mais leve de um ou mais arquivos IFC sem mudar o que eles dizem. A cópia mantém cada GlobalId, nome, conjunto de propriedades, quantidade, tipo, material e relação exatamente como o arquivo os escreveu, e também o cabeçalho; nada do Lapin é acrescentado. Só a geometria muda: as malhas (conjuntos de faces, B-reps facetados de faces planas) são limpas (vértices duplicados soldados, triângulos degenerados e repetidos e coordenadas não usadas removidos) e simplificadas, nunca mais longe do original do que a **Precisão** (Rápido 5 mm, Equilibrado 1 mm, Preciso 0,1 mm ou a sua), medida nos dois sentidos; corpos idênticos são escritos uma vez e compartilhados pelos seus produtos; e o que nada referencia (pontos, estilos não usados, representações vazias) some. Extrusões, booleanas e os outros corpos exatos ficam como estão. Arredondar as coordenadas, escrever os B-reps como conjuntos de faces (só IFC4 e IFC4X3) e um `.ifczip` compactado estão lá quando você quiser.

![Otimizar IFC](shots/optimize-ifc.webp)

A cópia é escrita ao lado do original, ou numa pasta que você escolher, com o nome do original e uma **Terminação** (`.optimized` para começar): `casa.ifc` dá `casa.optimized.ifc`. O original nunca é escrito, e se a cópia já existir o Lapin pergunta antes de substituí-la. Para cada arquivo, o relatório dá o tamanho, as entidades e os triângulos antes e depois, os corpos compartilhados, os órfãos removidos, o maior desvio medido e a duração; **Abrir pasta** mostra a cópia e **Abrir no Lapin** a abre em outra janela. No navegador a cópia é baixada. Um arquivo já trazido para o projeto guarda o caminho de volta: **Otimizar o arquivo de origem…** no grupo de um IFC importado (nas suas propriedades) ou num vínculo (Gerenciar → Vínculos) faz o mesmo com o arquivo de onde veio, enquanto ele ainda estiver no disco.

![O relatório](shots/optimize-ifc-report.webp)

## DXF e DWG

**Importar DXF…** traz um desenho CAD para a vista em primeiro plano, com as suas camadas, hachuras, textos e cotas. Ele entra como desenho no qual você pode usar o snap e que pode editar. Para usar um DWG, primeiro salve-o como DXF no seu programa CAD (AutoCAD, BricsCAD ou DraftSight: **Salvar como → DXF**). Um PDF ou uma imagem entra para decalcar com **Arquivo → Importar PDF ou imagem…** ou **Desenho → PDF ou imagem**: as linhas de um PDF vetorial como desenho na escala em que foi desenhado, as suas imagens e qualquer PNG ou JPEG como imagens translúcidas (veja [Desenho](drafting.md)). Em uma planta, **Posicionado: pelas coordenadas do mapa** coloca um desenho em coordenadas reais (uma planta cadastral em Stereo 70) onde o mapa o coloca (veja [Terreno](site.md)).

O desenho fica também na **vista 3D**, no plano da vista em que entrou: numa planta no nível do seu pavimento, num corte ou fachada no plano dessa vista, nas cores das suas camadas, com as hachuras esmaecidas, como linhas finas que nunca escondem o modelo. Uma camada desligada em **Desenho → Layers** continua desligada em 3D. **Vista → DXF em 3D** (também em **Vista → Configurações da vista** de uma vista 3D) mostra ou oculta todas as importações; uma importação liga e desliga com **Em 3D** nas propriedades de qualquer uma das suas formas, um DXF vinculado com **3D** em **Gerenciar → Vínculos**. Ao desenhar em 3D nesse nível, com as ferramentas de Desenho ou o Esboço do Studio, o snap pega as suas linhas, então você pode decalcá-lo em 3D como na planta. As linhas a que o arquivo deu cotas ficam no espaço nessas cotas, como antes.

![Um DXF de implantação na vista 3D, no nível do térreo](shots/dxf-3d.webp)

Um DXF importado faz parte do projeto: **File → Save** guarda-o no arquivo `.lapin` com suas camadas, hachuras, textos e qualquer grupo que você tenha feito dele, e ele está lá quando o projeto é aberto de novo, na planta do nível. A vista em planta em que entrou passa a ser a planta do nível, a que o navegador do projeto lista, e o que estava desenhado antes no nível passa para ela. O Lapin pergunta se quer salvar antes de fechar com alterações não salvas. Para manter o desenho como um arquivo próprio, marque **As a link** na caixa de importação: fica vinculado, somente leitura, e **Manage → Links** o recarrega quando o arquivo muda.

## Vínculos

**Vincular IFC…** mantém outro arquivo vinculado em vez de importá-lo. Ele é somente leitura e é recarregado quando muda. Isso também funciona para um DXF. **Gerenciar → Vínculos** mostra, recarrega, converte ou remove os vínculos. Um DXF vinculado fica em 3D como um importado; a sua caixa **3D** em **Gerenciar → Vínculos** desliga isso. **Visibilidade / Gráficos** (<kbd>VV</kbd>) oculta um vínculo, mostra-o em meio-tom ou dá-lhe suas próprias categorias numa só vista (*Cortes, elevações e 3D*, Visibilidade / Gráficos).

Um IFC vinculado mantém o que o seu arquivo diz dos materiais dos seus elementos. Paredes, lajes e coberturas com conjuntos de camadas são desenhadas nas suas camadas mesmo como referências, de modo que um engenheiro oculta os acabamentos do vínculo de arquitetura (**Paredes ▸ Acabamentos**, **Lajes ▸ Acabamentos**) e um estilo de vista estrutural desenha só os seus núcleos; as barras de um modelo estrutural vinculado estão na categoria **Armaduras**, que toda vista 3D deixa de fora até ser mostrada. Um vínculo feito com uma versão anterior recebe as camadas das paredes quando é recarregado (as das lajes de imediato).

## Nuvens de pontos

**File → Link point cloud…** vincula uma varredura a laser ou uma nuvem de fotogrametria, só em formatos abertos: **LAS** (1.0–1.4, todos os formatos de ponto), **LAZ** (LAS comprimido), **E57**, **PLY** (ASCII e binário) e pontos como texto, **XYZ**, **PTS** e **CSV**. Formatos proprietários (RCP e RCS do Autodesk ReCap, FLS da FARO, LGS da Leica) não são lidos: exporte antes a varredura do seu programa como E57 ou LAS.

O Lapin pergunta onde a nuvem fica e em qual unidade:

- **pelas coordenadas de mapa do projeto**: o x e o y do arquivo são leste e norte, posicionados pelo ponto de levantamento e o ângulo ao norte de **Manage → Location**, suas alturas acima da cota do ponto de levantamento. O Lapin sugere isso quando o arquivo nomeia um sistema de coordenadas ou suas coordenadas estão longe de zero e o projeto tem uma localização;
- **origem com origem**: a origem do arquivo sobre a do projeto;
- **centralizada na origem do projeto**: seu meio na origem, seu ponto mais baixo em ±0.00;
- **sua unidade**: como o arquivo diz (LAS e E57 dizem), senão metros; são oferecidos centímetros, milímetros, pés, pés topográficos dos EUA e polegadas. Quando a nuvem fica improvável para um edifício ou um terreno (menos de 20 cm ou mais de 50 km), ou o arquivo diz outra unidade, o Lapin avisa.

A nuvem fica no seu arquivo. Ela não é copiada para o projeto: o projeto guarda só o vínculo (o caminho do arquivo, onde a nuvem fica, como é desenhada). O Lapin a indexa uma vez, em segundo plano, num octree de blocos na sua própria pasta neste computador (não ao lado do projeto), e **Manage → Links** mostra o andamento. Depois a vista 3D traz os blocos de que a câmera precisa, primeiro os próximos e mais finos à medida que você se aproxima, dentro de um orçamento de pontos definido por **View → Display quality** (1,5, 3 ou 6 milhões de pontos desenhados de uma vez), de modo que uma varredura de cem milhões de pontos gira tão suave quanto uma pequena.

![Uma varredura da casa vinculada como nuvem de pontos, na vista 3D](shots/point-cloud-3d.webp)

Em **Manage → Links** uma nuvem tem sua própria linha: seus pontos, o andamento do índice, **Shown**, **Halftone**, **3D**, o que colore seus pontos (**RGB**, **Intensity**, **Elevation** ou **Classification**, pelas classes ASPRS: terreno, vegetação, edifício, água…) e o **Size** de um ponto em pixels. O botão do alfinete a posiciona de novo (pelo mapa, origem com origem ou centralizada, sua unidade, movida em x, y e z, girada); o botão de recarregar lê o arquivo de novo e só o indexa de novo se ele mudou. **Unlink** remove o vínculo; o índice fica na pasta do Lapin e serve a nuvem de novo se ela for vinculada outra vez.

![Manage → Links com uma nuvem de pontos](shots/point-cloud-links.webp)

Uma nuvem não é exportada com o modelo: IFC e Fragments levam o modelo, não a varredura, e **Export 3D…** leva uma amostra como pontos em GLB e glTF só quando pedido. Plantas, cortes e elevações também a desenham (*Cortes, elevações e 3D*, Nuvens de pontos nas vistas). No navegador pode-se vincular uma nuvem de até 160 MB; ela é indexada na página e precisa ser vinculada de novo depois de recarregar a página. Nuvens maiores precisam do aplicativo para desktop.

## Modelos 3D

**Exportar 3D (glTF, USD, DAE)…** grava o modelo como superfícies para renderização, visualização e realidade aumentada (RA):

| Formato | Para |
|---|---|
| GLB, glTF | Blender, three.js, visualizadores web. As imagens próprias dos seus materiais são incluídas. |
| USDZ | AR Quick Look da Apple, Reality Composer, Omniverse. Imagens incluídas. |
| USDA | OpenUSD como texto, um único arquivo. |
| DAE | COLLADA, para o SketchUp e ferramentas mais antigas. |
| OBJ + MTL | Um .zip para Twinmotion, Lumion, 3ds Max e qualquer programa de renderização. Com as imagens. |

**Importar malha (OBJ, STL, glTF)…** traz uma malha de outro programa 3D (OBJ, STL, glTF ou GLB) como referências no nível ativo. O glTF diz a sua unidade e qual lado é para cima; para OBJ e STL o Lapin pergunta. Um móvel ou uma decoração trazidos assim são colocados como qualquer elemento: **Mover**, **Copiar**, **Girar**, **Espelhar** e **Matriz** em Modificar, **Mover 3D** no Studio para a sua altura. As cópias dele compartilham os seus triângulos, então uma fileira de cadeiras pesa o mesmo que uma. **Tornar sólido Studio**, do Studio, torna editável a forma de uma referência (veja [Studio](studio.md)).

![Seis cadeiras de um ficheiro OBJ à volta de uma mesa, feitas com Modificar → Matriz](shots/reference-array.webp)

![Exportar 3D](shots/export-3d.webp)

Você escolhe o que exportar, qual fase e a origem. **O quê** é **tudo**, ou o modelo **como a vista o mostra**: a vista 3D em primeiro plano, sem os seus elementos e categorias ocultos e os pavimentos que ela não mostra, com as camadas que ela mostra (sem acabamentos, ou só os núcleos). Quando essa vista está cortada (o corte acima dos pavimentos mostrados, um corte mostrado em 3D, a caixa de corte), **Corte** corta a exportação ali também e fecha cada corte nos seus materiais, para que o arquivo fique como a tela. Uma vista 3D que deixa algo de fora é exportada como ela mostra, a menos que você escolha outra coisa. Cada elemento mantém a sua classe IFC, o seu tipo e o seu GlobalId. Para trocar o próprio modelo em vez das suas superfícies, use **Exportar IFC**.

## Lapin para o navegador

**Arquivo → Lapin para o navegador (.html)…** escreve o projeto como uma página web, um único arquivo .html que você pode enviar a qualquer pessoa. Abre com um duplo clique em qualquer navegador (Chrome, Edge, Safari, Firefox), em qualquer computador, sem internet e sem instalar nada:

- **3D**: o modelo como a vista 3D o mostra. Arraste para girá-lo, arraste com o botão direito para deslocá-lo, a roda para ampliar; **Ajustar** o mostra inteiro. Escolha um pavimento sozinho ou com os de baixo; clique em um elemento para selecioná-lo (Shift acrescenta à seleção) e depois **Isole**-o ou **Oculte**-o; **Mostrar tudo** traz tudo de volta.
- **Desenhos**: as plantas, cortes, elevações e pranchas que você marcou, desenhados como o Lapin os imprime, nítidos em qualquer zoom. Arraste para deslocar, a roda para ampliar, **Imprimir** para imprimir o que está à mostra. Clique em um elemento de um desenho para selecioná-lo: é o mesmo elemento em 3D.
- **Orçamento**, quando o projeto tem um: seus capítulos e linhas, os totais, marcados como *indicativos* quando usa os valores aproximados do próprio Lapin. Clique no título de uma coluna para ordenar, digite em **Filtro** para achar uma linha, clique em uma linha para selecionar seus elementos.
- **Propriedades**: o que estiver selecionado, com sua classe, tipo, pavimento, material, suas propriedades e quantidades.

![Arquivo → Lapin para o navegador](shots/html-export-dialog.webp)

Na caixa de diálogo você escolhe quais desenhos entram (todos por padrão), o modelo 3D, o orçamento e **o próprio projeto**. A página mostra o seu tamanho antes de ser escrita. A página fala o idioma que o Lapin fala quando você a exporta.

![A página em um navegador: o modelo 3D](shots/html-export-3d.webp)

![Uma planta na página, uma parede clicada mostrando suas propriedades](shots/html-export-drawing.webp)

![O orçamento na página](shots/html-export-costs.webp)

Com o projeto dentro, a página também é o projeto: **Arquivo → Abrir** no Lapin pega o .html e abre o projeto que ele contém, sem salvar; **Salvar** pergunta onde guardá-lo como .lapin e nunca escreve sobre a página. Sem o projeto, a página só mostra o modelo e o Lapin não a abre. Uma página web que o Lapin não escreveu é recusada.

A página é um instantâneo: o que ela mostra é o projeto no momento da exportação, e nada do que um leitor faz nela altera o projeto. Ela tem o tamanho das suas partes: o projeto, o modelo 3D (cerca do tamanho de uma exportação GLB) e os desenhos, com cerca de 0,6 MB para o próprio visualizador.
