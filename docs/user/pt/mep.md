---
title: Sistemas de instalações
order: 57
summary: Desenhe sistemas de aquecimento, água, esgoto, elétrica e ventilação como esquemas na planta, com fontes, pontos terminais e os trechos entre eles, e exporte-os como uma rede IFC.
---

A guia **Instalações** desenha as instalações do edifício como um engenheiro as esboça: um sistema por instalação, a sua **fonte** (uma caldeira, um hidrômetro, um tubo de queda, um quadro de distribuição, um ventilador), os seus **pontos terminais** (radiadores, lavatórios, bacias sanitárias, tomadas, luminárias, difusores) e os **trechos** entre eles. Cada nó é um elemento IFC real com as suas portas de conexão; cada trecho é um segmento de tubo, duto ou cabo; as conexões se mantêm quando você move as coisas.

![A guia Instalações](shots/ribbon-mep.webp)

## Um sistema em um clique

Em **Novo sistema**, escolha a instalação (**Aquecimento**, **Água**, **Água quente**, **Esgoto**, **Elétrica**, **Ventilação**) e clique na planta. O Lapin coloca o sistema inteiro: a fonte onde você clicou, um primeiro ponto terminal a alguns metros e o trecho entre eles. O sistema abre para edição.

![Um sistema de aquecimento na planta do térreo: a caldeira, dois radiadores sob as janelas e os trechos entre eles](shots/mep-plan.webp)

No modo de edição, tudo o que não é o sistema fica esmaecido, e só os nós e trechos do sistema podem ser escolhidos. Arraste a fonte e o ponto terminal para onde eles vão: o trecho deles acompanha. Adicione pontos terminais com **+ Radiador**, **+ Lavatório**… no painel Propriedades, ou com as ferramentas em **Aquecimento**, **Hidráulica**, **Elétrica** e **Ar**: cada novo ponto terminal entra no sistema por um trecho próprio, a partir da porta livre mais próxima da fonte ou do trecho mais próximo (um tê). **Concluído** (ou <kbd>Esc</kbd>) sai do modo de edição e informa quantas curvas e tês o sistema forma e o que ainda está aberto. **Editar sistema** (<kbd>ES</kbd>) o abre de novo.

## Trechos

**Trecho** (<kbd>RN</kbd>) desenha um trecho ponto a ponto; pressione <kbd>Enter</kbd> para terminar. Uma ponta que cai em uma porta aberta se conecta a ela; uma ponta que cai em um trecho do mesmo sistema faz uma derivação dele.

**Conectar** liga a ponta de um trecho a um nó ou a outro trecho; **Desconectar** a solta. **Conectores livres** seleciona tudo o que ainda não está conectado.

## Remodelar um trecho

Selecione um trecho: as alças dele ficam sobre a linha como ela está desenhada, mesmo depois de você mover os nós que ele conecta.

- **Arraste uma alça** para mover aquele canto livremente.
- **Arraste o meio de um segmento** para deslizá-lo para o lado; os segmentos vizinhos se esticam, assim um percurso em ângulo reto continua em ângulo reto. Deslize o primeiro ou o último segmento e o Lapin acrescenta um desvio ali, para que a ponta continue na sua porta.
- **Ctrl-clique** em uma alça para retirá-la, ou no meio de um segmento para adicionar uma; **Adicionar alça** coloca uma onde você clica.
- **Desvio** (<kbd>DU</kbd>) leva um trecho em volta de algo no caminho: clique no trecho onde o desvio começa, clique onde ele termina no mesmo segmento, depois vá para o lado para onde ele vai e clique (ou digite a distância). O trecho sai, segue ao lado e volta, com quatro cantos novos; ele mantém as conexões.
- **Redesenhar trajetória** (<kbd>RP</kbd>) dá um novo percurso a um trecho: selecione-o (ou comece nele) e desenhe o percurso ponto a ponto, <kbd>Enter</kbd> para terminar. O trecho mantém o sistema, a dimensão e as conexões, e as pontas voltam para as suas portas.

![O trecho de um radiador levado em volta de um obstáculo com Contornar: sai, segue ao lado e volta](shots/mep-detour.webp)


## Nós

Cada nó tem uma **função**: caldeira, radiador, coletor, hidrômetro, lavatório, pia, bacia sanitária, chuveiro, banheira, tubo de queda, peça de inspeção, quadro de distribuição, tomada, luminária, interruptor, ventilador, difusor, grelha de exaustão, válvula, subida/descida, tampão. A função dá ao nó a classe IFC, as portas, a dimensão e o símbolo na planta. Uma válvula, um medidor ou um interruptor solto sobre um trecho o divide e fica no meio. Uma **subida/descida** leva um trecho para outra cota ou para o pavimento de cima.

![As propriedades de um radiador: o sistema, a função e as portas, a dimensão e a potência](shots/mep-radiator-properties.webp)

No painel Propriedades, um nó mostra o sistema, a função e as portas (abertas ou conectadas); um trecho mostra as duas pontas. Os dois usam os padrões do sistema, a menos que você defina um valor neles: dimensão, cota, o plano em que correm (piso, teto, parede), em par ou simples, material, declividade.

## Sistemas

**Sistemas…** lista os sistemas do projeto e os padrões do sistema atual: a dimensão, o plano e as cotas de cada plano, a declividade (esgoto), se os trechos são desenhados em par (aquecimento: ida e retorno) e com que afastamento, o material e o isolamento. Mude um padrão e todos os nós e trechos sem valor próprio o acompanham.

![A caixa de diálogo Sistemas](shots/mep-systems.webp)

## Plantas, listas e IFC

**Planta de instalações** cria uma planta da disciplina de instalações para o nível, arquivada no conjunto Instalações do navegador. A **Lista de equipamentos** e a **Lista de trechos** em **Sistema** são tabelas como as outras: vão para as pranchas e são exportadas com elas.

Em uma exportação IFC, cada sistema é um IfcDistributionSystem, cada nó e trecho tem as suas IfcDistributionPorts, e as conexões são gravadas entre as portas, assim a rede chega a outro programa conectada da fonte até o último ponto terminal.

## Gás e óleo combustível

**Gás** e **Óleo combustível** são serviços próprios em **Novo sistema**. Um sistema de gás diz seu gás (**Gás natural** ou **GLP**) e sua **Classe de pressão** (baixa dentro da casa, média na rua) em **Sistemas…**; seus tubos são por padrão de aço preto nas paredes a 2,1 m, e podem ser de cobre, multicamada ou **PE 100** para o que fica enterrado. O óleo corre em cobre.

A instalação de gás de uma casa começa na **caixa do medidor** na fachada. Clique em **Novo sistema → Gás**: a caixa vai onde você clica e uma **caldeira a gás** dentro, ligadas por um trecho. No caminho, as ferramentas de **Gás** colocam no trecho o **regulador de gás**, o **medidor de gás**, o **registro geral de gás**, os **registros dos aparelhos** e os **conectores flexíveis**, que o dividem como qualquer conexão; o **fogão a gás** e o **aquecedor a gás** se ligam à instalação por um trecho próprio. Onde um tubo de gás ou de óleo atravessa uma parede, ele passa por uma **luva de proteção**, desenhada e contada com o trecho. Uma caldeira a gás desenhada no sistema de aquecimento como sua fonte se liga à instalação de gás pelas suas propriedades (o botão **+** com o nome do sistema de gás).

![A instalação de gás de uma casa: a caixa, o regulador e o medidor do lado de fora, a caldeira e o fogão na cozinha](shots/mep-gas-plan.webp)

Cada aparelho guarda seu **consumo** (m³/h) e seu **tipo de aparelho** pela exaustão (A sem chaminé, B com chaminé aberta, C estanque), para as listas e o dimensionamento.

**Óleo combustível** coloca um **tanque de óleo** (interno, externo ou enterrado) e uma **caldeira a óleo**; o **bocal de abastecimento** e o **terminal de respiro** se ligam ao abastecimento e ao respiro do tanque, e o **filtro de óleo** fica na ida.

### O abastecimento na rua

Em **Rua**, **Rede de gás ao longo de…** (<kbd>GM</kbd>) assenta uma rede de gás ao longo do que você clica — uma rua, um alinhamento, uma linha desenhada, uma parede — na **distância à linha** e na **profundidade (recobrimento)** da barra da linha de comando; sob um terreno a profundidade é medida a partir do solo, senão a partir do nível. **Rede de gás** a desenha ponto a ponto. A rede vai num sistema de gás de média pressão, em tubo de PE, com sua **fita de advertência**.

**Ramal de ligação** (<kbd>GS</kbd>): clique na rede, depois na caixa. O Lapin coloca um **tê de derivação** na rede, assenta o tubo do ramal em esquadro a partir dela na sua profundidade até a caixa, e sobe ali para dentro da caixa.

![O ramal de ligação da rede da rua até a caixa do medidor, selecionado](shots/mep-gas-street.webp)

![A rede e o ramal debaixo da terra, a instalação na fachada](shots/mep-gas-3d.webp)

![As propriedades de um ramal: do tê de derivação à caixa, seu recobrimento e diâmetro](shots/mep-gas-service-properties.webp)

![Os padrões de um sistema de gás: seu gás e sua classe de pressão](shots/mep-gas-systems.webp)

**Quantitativo de gás e óleo** em **Sistema** lista, sistema por sistema, os tubos por material e diâmetro com seu comprimento (e dele as prumadas), as curvas, tês e luvas, a fita de advertência, os registros, medidores e caixas, e os aparelhos com consumo e potência instalados. Em IFC um sistema de gás é um IfcDistributionSystem do tipo GAS e um de óleo FUEL; a caixa é um IfcDistributionChamberElement, o medidor um IfcFlowMeter GASMETER, os registros IfcValve, o fogão um IfcBurner, o tanque um IfcTank.

![O quantitativo de gás e óleo](shots/mep-gas-list.webp)

Dimensionar os tubos pelo consumo dos aparelhos e verificar a perda de carga é engenharia, na edição **Lapin MEP**.

## Exaustores

O grupo **Ventiladores** na guia Instalações coloca exaustores de banheiro e cozinha como elementos próprios, cada um de um tipo que você configura:

- **Exaustor de parede** (<kbd>VF</kbd>): um ventilador axial numa parede, soprando direto através dela;
- **Exaustor de teto**: um ventilador sob o teto com um duto saindo pela lateral;
- **Ventilador de duto**: um ventilador tubular no forro, sobre o duto;
- **Exaustor de janela**: um ventilador montado no vidro de uma janela;
- **Exaustor de teto com luz**: um exaustor de teto com uma luz na grelha;
- **Exaustor com painel de venezianas**: um painel de parede com venezianas ao lado do ventilador e uma tampa embaixo.

Escolha uma ferramenta e clique. Um exaustor de parede, de janela ou com painel de venezianas vai para a face da parede mais próxima do clique (até 600 mm), com a frente para o ambiente e o centro a 2,1 m do piso (um de janela a 1,9 m). Um exaustor de teto vai onde você clica, sob o teto do ambiente; um de duto 250 mm acima dele. A barra da linha de comandos escolhe o tipo que a ferramenta coloca, entre os tipos do projeto ou os incluídos no Lapin (um exaustor de parede Ø100 com sensor de umidade, um de teto com luz e sensor de presença, um de duto para dois banheiros…).

Um exaustor de parede corta uma abertura redonda na sua parede, 20 mm mais larga que o seu duto: a passagem da parede, como a de uma tubulação (aparece como volume de abertura nas plantas e vai para o IFC como abertura da parede). Ela é encontrada pela posição do exaustor, então um exaustor movido ao longo da parede leva a sua abertura junto. Um exaustor de janela fica no vidro e não corta nada.

![Um banheiro com um exaustor de parede, um exaustor de teto com luz levado para fora através da parede e um exaustor com painel de venezianas](shots/fans-plan.webp)

As plantas desenham um exaustor pelo seu símbolo de instalações: um círculo com as pás, a frente na parede, a luva tracejada através da parede e uma seta no sentido do ar; a grelha de um exaustor de teto tracejada, pois está acima do corte. Plantas de forro, cortes, elevações e 3D desenham os corpos.

**Duto até a saída** leva para fora o ar de um exaustor de teto ou de duto: clique no exaustor e depois onde o duto sai. O duto é uma tubulação do sistema **Exaustão de banheiros** do projeto (criado com o primeiro duto; corta as paredes que atravessa), da largura do duto do exaustor, na altura do seu bocal. Termina numa grelha na face externa da parede ou, com **Pelo telhado** ativo na barra, num tubo que sobe até um chapéu. Movido ou elevado, o exaustor leva junto o início do duto.

![Os exaustores em 3D: o duto até a grelha externa, a grelha do exaustor de parede na fachada](shots/fans-3d.webp)

### Tipos de exaustor

**Tipos de ventilador** abre os tipos: os do projeto e os incluídos, para adicionar com um clique. Um tipo define:

- a sua **montagem** (as que o seu gênero aceita) e o **diâmetro do duto** (Ø100, 125, 150 ou 200 mm);
- a **forma** da grelha (redonda ou quadrada), o **tamanho**, o **padrão** (venezianas, fendas, anéis ou lisa) e o **acabamento** (branco, prata, preto, inox ou cromado);
- a **vazão de ar** (m³/h) e a **potência** (W);
- o **comando**: um interruptor, um sensor de umidade, um temporizador, ambos ou um sensor de presença;
- um **registro antirretorno**, e a **luz** de um exaustor de teto, o comprimento de um de duto, a largura e a altura de um painel de venezianas, mostrados só onde se aplicam.

A pré-visualização ao lado é a do motor: o símbolo em planta em cima, a frente embaixo. Todo exaustor de um tipo segue uma alteração.

![Os tipos de exaustor com a pré-visualização de um painel de venezianas](shots/fans-types.webp)

Selecione um exaustor para alterar o seu tipo, a sua **altura acima do piso**, a rotação e, quando tem duto, a sua **saída** (parede ou telhado) e quanto a saída sobe acima do duto. Arraste a alça no centro para movê-lo, a da frente para girá-lo.

![As propriedades de um exaustor de teto](shots/fans-properties.webp)

### Quantitativos, custos e IFC

O **Quantitativo de ventiladores** lista cada exaustor com ambiente, gênero, tamanho, vazão, montagem, comando, altura e saída, e a vazão total. A **Verificação da exaustão por ambiente** lista cada banheiro, WC e cozinha (e cada ambiente com um exaustor) com o seu volume, a exaustão de que precisa e o que os seus exaustores dão: um banheiro 8 trocas de ar por hora e no mínimo 54 m³/h, um WC 6 e 25 m³/h, uma cozinha 6 e 108 m³/h. São valores indicativos; confira-os com a norma que você segue.

Um tipo novo tem a composição de custo `LAP-FAN`, contada por exaustor (**Custos → Atribuir composições…** a altera).

O IFC escreve um exaustor como **IfcFan** de um **IfcFanType** com o seu tipo predefinido (PROPELLORAXIAL para um de parede ou de janela, CENTRIFUGALRADIAL para um de teto, TUBEAXIAL para um de duto) e **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate); a abertura da parede como IfcOpeningElement. O Lapin os lê de volta como os seus próprios exaustores.

## Coordenação: volumes de abertura e verificação de conflitos

Cada abertura numa parede, laje ou telhado pode ser um **volume de abertura** próprio: um sólido do tamanho da abertura, por toda a espessura do seu hospedeiro, acabamentos incluídos. Os programas de conflitos (Navisworks, Solibri, BIMcollab) testam as instalações contra esses volumes com "completamente dentro": um tubo, duto ou eletrocalha inteiro dentro de um volume passa; um que sai dele, ou que atravessa uma parede sem abertura, é um conflito. O Lapin faz a mesma verificação.

**Gerenciar → Verificação de conflitos…** executa a verificação só quando pedida: nada a executa sozinho, nem uma edição nem **Gerenciar → Verificar**. A sua aba **Configurações** define para que aberturas se fazem volumes (aberturas em paredes, portas, janelas, lajes e coberturas), quanto maiores ou menores que as suas aberturas eles são (**Deslocamento do volume**) e a **Folga** que um elemento de instalações deve manter dos lados de um volume. **Executar verificação de conflitos** passa uma vez por cada tubo, duto e eletrocalha — os troços do Lapin e os segmentos de um modelo de instalações importado da mesma forma, e as reservas de passagem próprias de um modelo de instalações — e guarda o que encontrou como o **relatório de conflitos** do projeto, com a data em que foi executada, até ser executada de novo. Quando o modelo muda depois disso, o relatório diz que está **desatualizado**. Os seus problemas são agrupados por tipo e numerados, e cada número se mantém de uma execução à seguinte:

- **Sem abertura**: atravessa o hospedeiro onde ele não tem nenhuma;
- **Não cortados: estrutural ou ordem de corte**: sem abertura, porque as aberturas próprias dos troços (abaixo) não cortam esse hospedeiro;
- **Abertura pequena demais**: passa por uma abertura mas sai dela, folga incluída; **Falta** diz quantos milímetros do pior lado;
- **Volume vazio** (com **Aberturas vazias** marcado em **Configurações**): uma abertura de parede ou laje por onde nada passa (um vão de escada nunca é uma).

![O relatório de conflitos: sem abertura, uma abertura pequena demais e um volume vazio, cada um numerado](shots/mep-voids-check.webp)

Cada problema está **A corrigir**, **Resolvida** ou **Ignorada**, com um comentário. Um clique numa linha mostra o par em 3D, ambos selecionados (**sozinhos em 3D** esconde o resto por enquanto); as setas vão ao anterior e ao seguinte. **Marcar na vista** (também **Vista → Marcas de conflitos**) desenha cada problema aberto na planta, no corte ou na elevação à frente como uma nuvem vermelha com o seu número, só na tela.

![A planta com os conflitos marcados: nuvens vermelhas com os seus números](shots/mep-voids-clouds.webp)

**Fazer abertura** corta uma abertura simples à volta do elemento na parede (redonda para um tubo ou um cabo, retangular para um duto) ou na laje, com os lados à distância da folga e o tamanho arredondado para cima como diz a regra de instalações da ordem de corte; **Alargar abertura** aumenta a abertura pequena demais. **Corrigir seleção** faz isso para cada linha marcada, **Ignorar** e **Reabrir** mudam o seu estado; cada um é um só passo para desfazer. O relatório diz então que está desatualizado: execute-o de novo, e o que foi corrigido aparece **Resolvida** (**mostrar resolvidas**). A sua linha diz então em que execução foi encontrada resolvida e como está agora: a abertura por onde passa e o tamanho do troço nela (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), ou que o troço já não passa; cada execução seguinte atualiza os tamanhos.

![Executada de novo depois das correções: os dois conflitos resolvidos](shots/mep-voids-fixed.webp)

**BCF** transforma os conflitos abertos em observações do tipo **Clash** com os seus pontos de vista (**Gerenciar → Observações do modelo…**) e salva-as como arquivo BCF; executado de novo, atualiza as mesmas observações. **CSV** e **PDF** salvam o relatório como lista. **Gerenciar → Verificar** só lê o relatório: os seus conflitos abertos são avisos, e um relatório desatualizado também.

### Troços que cortam as suas próprias aberturas

A ordem de corte (**Gerenciar → Ordem de corte**) inclui também os troços de instalações, acima de paredes, lajes e coberturas. Com **Os troços cortam paredes, lajes e coberturas** ativado, um troço que atravessa uma parede, uma laje ou uma cobertura corta ali a sua própria abertura: redonda para um tubo ou um cabo que passa em esquadro, retangular para um duto ou um troço inclinado, do tamanho do exterior do troço (o seu isolamento, a manga de um tubo de gás) mais a **Folga** à volta, arredondada para cima ao passo, nunca abaixo da **Abertura mínima**. A abertura não é guardada à parte: desloca-se e cresce com o troço e desaparece quando o troço desaparece ou deixa de atravessar. É um volume como qualquer abertura (desenhado, listado, verificado), e a exportação IFC escreve-a como **IfcOpeningElement** com **Lapin_ProvisionOpening** (e como reserva de passagem com **Volumes de abertura** marcado), de modo que uma verificação executada de novo não encontra nada a indicar. Uma parede, laje ou cobertura colocada acima dos troços na ordem de corte (pela sua classe ou pelo seu material) não é cortada, nem uma estrutural (LoadBearing no seu Pset_…Common) a menos que **cortá-las também** esteja marcado: a verificação de conflitos indica-as como **Não cortados**. Um troço demolido não corta nada, e onde um troço já passa por uma abertura não se corta outra. Um sistema pode decidir de outra forma em **Instalações → Sistemas…** (**Os troços cortam paredes, lajes e coberturas**: como a ordem de corte, sim, não). Está ativado num projeto novo; um projeto feito antes mantém-no desativado até ser ativado.

Numa parede curva a abertura fica onde o troço encontra o arco, com a largura medida ao longo dele; uma retangular tem os lados radiais, como a de uma porta ou janela. Numa cobertura é cortada na água que o troço atravessa: vertical para um tubo em pé (uma ventilação), alongada no sentido do troço para um inclinado, como numa laje; as madres e a cumeeira sob a água não são cortadas, e a verificação de conflitos indica um troço que as atravessa. Mover ou redimensionar um troço reconstrói apenas as paredes, lajes e coberturas que atravessava ou atravessa agora, por maior que seja o modelo. O IFC do próprio Lapin, lido de volta, não volta a tomar estas aberturas quando os seus troços também vêm (os troços cortam-nas de novo, nunca duas vezes); sem o seu troço (deixado fora da importação, ou num projeto cujos troços não cortam nada) cada uma entra como abertura do seu elemento, de modo que nada se perde.

![Os troços de instalações na ordem de corte](shots/mep-voids-cut-order.webp)

A guia **Volumes** lista cada volume com a sua marca (V1, V2… pavimento a pavimento), o hospedeiro, a porta ou janela nele, tamanho e profundidade; um clique seleciona a abertura.

![Os volumes com marcas e tamanhos](shots/mep-voids-list.webp)

**Vista → Volumes de abertura**, com a vista 3D à frente, desenha-os em 3D, violeta translúcido com as suas marcas; um clique num seleciona a abertura.

![Os volumes na parede interna, com os tubos através deles](shots/mep-voids-3d.webp)

Com uma planta, corte ou alçado à frente, **Vista → Volumes de abertura** desenha-os nessa vista, como um desenho de coordenação mostra as aberturas previstas: cortados onde a vista os corta (a abertura de uma parede em toda a sua espessura, a de uma laje através dela), vistos onde os vê (uma abertura de laje abaixo do corte da planta, a abertura de uma parede na face que um alçado olha), tracejados quando a abertura de uma parede fica abaixo do corte ou a de uma laje acima; cada um em cruz, com a sua marca e, por baixo, o tamanho e a cota inferior de uma abertura em parede (**400×200 +0.90**, **Ø200**). O motor desenha-os com o resto do desenho, por isso as folhas, o PDF e o DXF (camada **VOIDS**) imprimem-nos como o ecrã os mostra.

![A planta com os volumes: cortados nas paredes, a abertura da laje vista, cada um em cruz e marcado](shots/mep-voids-plan.webp)

A etiqueta de um vazio fica ao lado dele, através da sua parede, onde nada mais está desenhado: o giro de uma porta, o marcador da porta ou janela, outras etiquetas. Se os dois lados da parede estão ocupados, ela se desloca ao longo da parede para além da abertura, senão mais para fora.

![O vazio de uma porta: sua etiqueta longe do giro e do marcador da porta](shots/void-label.webp)

Para mover uma etiqueta, clique nela (ou no vazio): isso seleciona a abertura, cuja etiqueta mostra um grip quadrado para movê-la e um redondo para girá-la. Ctrl/⌘-clique num grip devolve a etiqueta ao seu lugar padrão, ou horizontal de novo. Uma etiqueta movida é guardada por aquela vista, ao longo e através da parede a partir do meio do vazio, então segue a abertura quando a parede se move; as folhas e o PDF a imprimem onde você a pôs.

![A etiqueta do vazio movida pelo seu grip](shots/void-label-moved.webp)

É uma definição da vista, também nas suas propriedades (**Vista → Configurações da vista**): **Volumes de abertura**, e **Tamanhos das aberturas** para o tamanho sob a marca. Por omissão nenhuma vista os desenha; o estilo de vista **Coordenação** (**Gerenciar → Estilos de vista**, escolhido como **Estilo** da vista) desenha-os em cada vista que o usa. As suas linhas são a categoria **Volumes**: canetas e cores em **Gerenciar → Estilos de objeto** (linhas de corte, projeção, ocultas, acima), mostradas ou ocultas por vista em **Vista → Visibilidade / Gráficos…**.

Para passar os volumes a um programa de conflitos, marque **Volumes de abertura** em **Arquivo → Exportar IFC ou Fragments…** (ou **Exportar 3D…**). Em IFC cada volume é um **IfcBuildingElementProxy** do tipo **PROVISIONFORVOID** com **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter se redondo, Depth) e o conjunto do Lapin **Lapin_OpeningVoid** (marca, tipo, hospedeiro, abertura e deslocamento), ligado ao hospedeiro; as aberturas continuam IfcOpeningElement. Cada volume mantém o seu GlobalId de uma exportação para outra. Numa exportação de malhas formam um grupo próprio, **Opening voids**. Sem a marca, as exportações ficam como antes.

## MEP Pro

A engenharia sobre a rede — a demanda que cada trecho leva e a dimensão que a comporta, as verificações (pontas abertas, curvas fechadas demais, caimentos de esgoto, fontes sobrecarregadas), as regras de assentamento por material (cobre em passos de 45°, PVC virando no máximo 45°), radiadores propostos sob as janelas, percursos sugeridos ao longo das paredes, o perfil de um trecho e a isométrica de um sistema — está na edição **Lapin MEP**, que acrescenta os grupos **Engenharia** e **Assistentes** a esta guia.
