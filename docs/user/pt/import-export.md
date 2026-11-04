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

**Importar IFC…** lê o IFC de outro programa. Antes que qualquer coisa entre, o Lapin lê o arquivo e mostra o que ele contém: os seus elementos por disciplina (arquitetura, estrutura, MEP, mobiliário e equipamentos, terreno), cada disciplina por tipo com quantos há, e os seus pavimentos com quantos elementos estão em cada um. Desmarque o que você não precisa, por exemplo MEP e mobiliário quando você trabalha na estrutura, ou mantenha só os pavimentos em que você trabalha. **Como** traz os elementos reconhecidos como elementos do Lapin, todos eles como referências, ou vinculados. Enquanto o arquivo é lido e importado, o Lapin mostra o que está fazendo e em que ponto está; um arquivo grande leva um tempo.

![Importar IFC](shots/import-ifc.webp)

Alguns arquivos trazem 2D além do modelo: símbolos de planta, projeções, hachuras e textos desenhados para um elemento, ou elementos desenhados só em 2D (linhas, textos, símbolos sem corpo). Quando um arquivo os tem, a caixa de diálogo os conta em **Desenhado em 2D**. Desmarque **o 2D próprio dos elementos** e uma planta desenha esses elementos pelo seu corte, como faz com os do Lapin; desmarque **elementos desenhados só em 2D** e eles ficam de fora. Os dois entram, a menos que você os desmarque.

![Desenhado em 2D](shots/import-ifc-2d.webp)

**Limpeza e otimização** reúne as regras da importação, as mesmas para qualquer arquivo, guardadas como suas para a próxima importação. **Manter uma única vez um corpo repetido** (ativado, a menos que você desmarque) guarda uma única vez um corpo que o arquivo repete com outros nomes (um parafuso, um flange, um suporte), com os seus elementos compartilhando-o, sem perder nada. As outras deixam de fora, quando marcadas, o que outro programa trouxe junto: **corpos planos** (textos, linhas e hachuras transformados em triângulos), **elementos mais distantes do resto** do que a distância que você digitar (os textos de um desenho, um carimbo), **duplicatas** no mesmo lugar e **portas de conexão e aberturas gravadas como objetos**. **Simplificar malhas** grava menos triângulos dentro da tolerância que você digitar; **ler números escritos como texto** transforma `DoubleLength:0,01` em 0,01 m e `1.15395 kg` em uma massa (um número inteiro sozinho, um código, continua texto). Em **O que importar**, **Conjuntos de propriedades** lista os conjuntos do arquivo com quantos elementos têm cada um; os **técnicos** (a transformação do Navisworks, ids internos, estatísticas de geometria) ficam de fora, a menos que você os marque. A mensagem da importação diz o que cada regra fez.

![Limpeza e otimização](shots/import-ifc-rules.webp)

O que o arquivo posiciona entra como um grupo com o nome dele, que você pode abrir e organizar, ou trazer solto desmarcando **Como um grupo** (veja [Grupos e organização](groups.md)).

**Tudo editável**, uma opção de **Como**, vai além do reconhecimento: o que continua referência vira um sólido do Studio feito dos seus próprios triângulos, o mesmo elemento com a sua classe, propriedades, pavimento e grupo, que aceita empurrar/puxar, arredondamentos e operações booleanas na hora (veja [Studio](studio.md)). Um corpo com mais de 5.000 triângulos, como uma bomba ou uma conexão com malha fina, continua referência, e a mensagem da importação diz quantos e por quê. **Importar Fragments…** tem o mesmo **Como**: um arquivo Fragments contém só triângulos, mas as suas paredes (com as suas portas e janelas), lajes e pilares são reconhecidos a partir deles como elementos do Lapin, o resto mantido como referências ou, com **Tudo editável**, transformado em sólidos do Studio. Os elementos de um arquivo IFC que o reconhecimento não conseguiu ler do arquivo (uma parede com as suas aberturas recortadas no corpo) também são tentados a partir dos seus triângulos. **Importar malha** pergunta **Como: editável**. As referências trazidas antes são convertidas com Modificar → **Converter em nativo** (NV): reconhecidas onde for possível, o resto transformado em sólidos do Studio, sendo um grupo selecionado os seus membros. Uma referência trazida antes fica editável, uma de cada vez, com Studio → **Tornar sólido Studio**, que a mantém no seu grupo e no seu pavimento.

O Lapin reconhece as paredes, lajes, pilares, vigas, fundações, portas e janelas e os transforma em elementos do Lapin que você pode editar. O que ele não consegue reconhecer entra como referência, desenhada mas não editável. Portas e janelas entram abrindo do jeito que o arquivo diz: a sua mão, o lado para o qual abrem e, nas janelas, como cada painel abre.

Os acabamentos que outro programa exportou como paredes próprias (um reboco de 2,5 cm em uma fachada, um gesso por dentro) viram uma camada da parede que cobrem, quando estão encostados na sua face, da mesma altura e ao longo dela: a parede ganha um tipo com essa camada (*Alvenaria 350 + Reboco de cal*), as suas próprias camadas onde estavam, e a mensagem da importação diz quantos. Um acabamento afastado da sua parede, ou que cobre só parte dela, continua uma parede fina. Um pilar que o arquivo dá como um corpo cortado no topo por uma inclinação (sob uma viga ou um telhado inclinado) entra como um pilar da sua seção, até o seu ponto mais alto.

**Mapear tipos importados** (Gerenciar) lista os tipos que uma importação criou (tipos de parede, laje, porta, janela, pilar, viga e fundação), cada um com o seu arquivo e quantos elementos tem. Escolha um dos tipos do próprio projeto, do mesmo tipo, em **Vira** e pressione **Mapear**: os seus elementos pegam o tipo do projeto, com uma parede mantendo o seu núcleo onde estava, e o tipo importado sai. Com **Lembrar** marcado, uma importação posterior de um tipo dessa categoria e nome pega o do projeto na hora, e a mensagem dela diz isso. As escolhas lembradas são listadas na caixa de diálogo, cada uma com **Esquecer**.

![Mapear tipos importados](shots/map-types.webp)

Os espaços do arquivo (do Revit, do ArchiCAD e de outros) entram como ambientes do Lapin, cada um no seu próprio nível, com o seu contorno, número, nome, altura, função, departamento e acabamento de piso: veja [Ambientes](rooms.md#ambientes-de-um-arquivo-ifc). Tudo o que um elemento importado tinha no arquivo (os seus conjuntos de propriedades e quantitativos) é mantido e mostrado em **Propriedades IFC** nas suas propriedades. Uma referência sai em **Exportar IFC…** como entrou: com o mesmo GlobalId, classe e atributos (descrição, etiqueta, tipo predefinido), os seus conjuntos de propriedades e quantitativos com cada valor tipado como o arquivo o tinha, e o seu tipo, material, portas de conexão, conexões e sistema, cada um com o seu GlobalId. Traga só o MEP de um arquivo e exporte-o, e o novo arquivo o leva inteiro.

**Importar Fragments…** abre o mesmo painel para um arquivo `.frag` da That Open: os seus elementos por disciplina e tipo, os seus pavimentos e **Como um grupo**. Um arquivo exportado deste mesmo projeto não traz nada de novo quando é importado de volta nele: os seus elementos já estão aqui.

![Importar Fragments](shots/import-frag.webp)

**Importar Fragments…** traz um arquivo `.frag`: cada item com geometria vira uma referência desenhada a partir do seu corpo, na classe que o arquivo indica, no pavimento em que o arquivo o coloca (associado pelo nome aos seus, criado quando falta), com os atributos do arquivo em **Propriedades IFC**. Itens com o mesmo corpo (a mesma forma, colocada em muitos lugares) o compartilham. Importar o mesmo arquivo de novo atualiza o que ele trouxe antes. O que é reconhecido a partir dos seus triângulos depende de **Como**, como acima.

**Exportar IFC ou Fragments…** grava o modelo como IFC ou como Fragments, escolhido como em uma importação: os elementos por disciplina e tipo, os pavimentos, a fase e só o que a vista 3D mostra. O IFC sai em IFC4 ou IFC4X3, com as partes de um elemento com camadas abaixo dele ou um corpo para cada; o Fragments a partir do ponto base do projeto, da origem interna ou do ponto de levantamento.

![Exportar IFC ou Fragments](shots/export-exchange.webp)

## DXF e DWG

**Importar DXF…** traz um desenho CAD para a vista em primeiro plano, com as suas camadas, hachuras, textos e cotas. Ele entra como desenho no qual você pode usar o snap e que pode editar. Para usar um DWG, primeiro salve-o como DXF no seu programa CAD (AutoCAD, BricsCAD ou DraftSight: **Salvar como → DXF**). A importação de PDF ainda não está disponível. Em uma planta, **Posicionado: pelas coordenadas do mapa** coloca um desenho em coordenadas reais (uma planta cadastral em Stereo 70) onde o mapa o coloca (veja [Terreno](site.md)).

## Vínculos

**Vincular IFC…** mantém outro arquivo vinculado em vez de importá-lo. Ele é somente leitura e é recarregado quando muda. Isso também funciona para um DXF. **Gerenciar → Vínculos** mostra, recarrega, converte ou remove os vínculos.

## Modelos 3D

**Exportar 3D (glTF, USD, DAE)…** grava o modelo como superfícies para renderização, visualização e realidade aumentada (RA):

| Formato | Para |
|---|---|
| GLB, glTF | Blender, three.js, visualizadores web. As imagens próprias dos seus materiais são incluídas. |
| USDZ | AR Quick Look da Apple, Reality Composer, Omniverse. Imagens incluídas. |
| USDA | OpenUSD como texto, um único arquivo. |
| DAE | COLLADA, para o SketchUp e ferramentas mais antigas. |
| OBJ + MTL | Um .zip para Twinmotion, Lumion, 3ds Max e qualquer programa de renderização. Com as imagens. |

**Importar malha (OBJ, STL, glTF)…** traz uma malha de outro programa 3D (OBJ, STL, glTF ou GLB) como referências no nível ativo. O glTF diz a sua unidade e qual lado é para cima; para OBJ e STL o Lapin pergunta. **Tornar sólido Studio**, do Studio, torna uma referência editável (veja [Studio](studio.md)).

![Exportar 3D](shots/export-3d.webp)

Você escolhe o que exportar, qual fase e a origem. **O quê** é **tudo**, ou o modelo **como a vista o mostra**: a vista 3D em primeiro plano, sem os seus elementos e categorias ocultos e os pavimentos que ela não mostra, com as camadas que ela mostra (sem acabamentos, ou só os núcleos). Quando essa vista está cortada (o corte acima dos pavimentos mostrados, um corte mostrado em 3D, a caixa de corte), **Corte** corta a exportação ali também e fecha cada corte nos seus materiais, para que o arquivo fique como a tela. Uma vista 3D que deixa algo de fora é exportada como ela mostra, a menos que você escolha outra coisa. Cada elemento mantém a sua classe IFC, o seu tipo e o seu GlobalId. Para trocar o próprio modelo em vez das suas superfícies, use **Exportar IFC**.
