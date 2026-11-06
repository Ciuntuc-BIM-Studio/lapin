---
title: O terreno e os seus dados
order: 52
summary: Traga o levantamento, o lote, os vizinhos e uma ortofoto em coordenadas reais, de DXF, CSV, GeoJSON, CityJSON e de uma imagem georreferenciada.
---

A guia **Implantação** reúne o chão em que o edifício se apoia: o terreno, o lote, os edifícios vizinhos e uma foto aérea, cada um onde realmente está. O grupo **Importar** os traz dos arquivos que os topógrafos e o cadastro entregam.

![A guia Implantação](shots/ribbon-site.webp)

## Coordenadas reais

Um levantamento vem em coordenadas de mapa: coordenadas E e N, a centenas de quilômetros do zero. O Lapin mantém o modelo perto da sua origem e o liga ao mapa pelo **ponto de levantamento**. A primeira importação num projeto faz isso por você: o ponto de levantamento vai para a origem da planta, nas primeiras coordenadas do arquivo arredondadas ao metro, e toda importação seguinte cai no mesmo lugar.

**Gerenciar → Localização** mostra o **sistema de coordenadas** em que estão as coordenadas E e N, e os pontos base e de levantamento. O Lapin conhece o Stereo 70 (EPSG:3844, a grade nacional da Romênia), as zonas UTM 34 e 35 (EPSG:32634, 32635), o Web Mercator (EPSG:3857) e longitude e latitude (WGS 84, EPSG:4326). Um arquivo em outro desses sistemas é convertido ao entrar: um GeoJSON em longitude e latitude cai na planta em Stereo 70 onde deve estar. A exportação IFC grava o sistema e o ponto de levantamento, para que outros programas também coloquem o modelo no mapa.

![Gerenciar → Localização: o sistema de coordenadas e o ponto de levantamento](shots/site-location.webp)

## Importar

- **Terreno a partir de pontos** lê um arquivo CSV ou de texto de um levantamento: x y z, ou número N E cota, um ponto por linha.
- **Pontos de DXF** lê um desenho de levantamento: entidades POINT com as suas cotas, blocos inseridos numa altura ou que a trazem como atributo, cotas escritas ao lado de pontos ("245.37" junto à sua cruz) e curvas de nível na sua cota. Quando o desenho tem pontos em vários layers, o Lapin os lista com quantos pontos cada uma tem, e você escolhe quais usar. Os pontos formam um terreno.
- **GeoJSON** traz lotes, linhas e pontos. Um arquivo com um polígono dá o **lote**; um com vários dá áreas. O lote também contorna o lote do terreno, com a sua área. Os arquivos do cadastro em Stereo 70 dizem isso no arquivo, e o Lapin o lê; um arquivo que não diz nada está em longitude e latitude, como exige o padrão GeoJSON.
- **CityJSON** traz os edifícios vizinhos com as suas superfícies, no seu lugar e na sua altura. Eles são referências somente leitura; vão projetar sombras no estudo de insolação.
- **Ortofoto** coloca uma foto aérea sob o terreno. Escolha a imagem (JPEG ou PNG, até 8 MB) junto com o seu arquivo de georreferência (`.jgw`, `.pgw` ou `.wld`): o arquivo de georreferência diz onde a imagem fica. A foto é desenhada sob a planta e reveste o terreno em 3D.

![A planta de situação: a ortofoto, as curvas de nível do terreno levantado, o lote e a projeção do vizinho](shots/site-plan.webp)

![O mesmo terreno em 3D: a foto no chão, o vizinho na sua altura](shots/site-3d.webp)

Tudo o que é importado fica guardado no arquivo do projeto. Um elemento recebe o nome do arquivo (o número cadastral de um lote, quando o arquivo o tem) e mantém consigo as outras propriedades do arquivo; selecione-o para ver a sua área ou o seu comprimento.

## Vias, curvas e linhas

Uma **via** ou um **caminho** segue o seu eixo. Em **Corredor viário…** (CR), a guia **Curvas** dá a cada vértice do eixo um raio com uma clotoide de entrada e de saída, como um projetista de estradas traça uma curva; um vértice deixado em 0 continua sendo um canto, e curvas que não cabem entre as vizinhas são recusadas com o motivo. As estacas, o perfil longitudinal e as seções transversais seguem o eixo curvo, e o IFC 4.3 o grava como um alinhamento.

As linhas de CAD viram elementos do terreno: **Via a partir de linhas CAD** (LR) e **Linha de quebra a partir de linhas CAD** (BL) pegam uma linha ou polilinha desenhada ou trazida por um DXF, seguem toda linha do seu layer que se liga a ela ponta a ponta, e fazem uma via ao longo dela ou uma linha de quebra do terreno. Quando as linhas trazem cotas (as linhas 3D de um DXF), elas dão o perfil da via ou os níveis da linha de quebra. Essas linhas são guardadas com as suas cotas e podem ser mostradas em 3D (a categoria *linhas*).

## Plantas de situação e de localização

O navegador do projeto tem um **Implantação** abaixo dos níveis: os desenhos próprios do terreno, que não pertencem a nenhum pavimento. Um projeto novo começa com dois:

- a **Planta de implantação** (em romeno *plan de situație*), em 1:500: o modelo inteiro visto de cima, todos os pavimentos de uma vez, com os telhados e tudo, com o terreno, os caminhos, as plantas, o lote e os vizinhos. O que fica totalmente abaixo de ±0.00 (as fundações) fica de fora, nada é cortado e nada oculto é desenhado. Os vértices do lote são numerados;
- a **Planta de situação** (*plan de încadrare în zonă*), em 1:2000: o mesmo desenho para o terreno na sua região, sobre a ortofoto, sem os números dos vértices; os edifícios do projeto são preenchidos em vermelho e o lote é contornado em vermelho, para que o terreno se destaque dos vizinhos.

**+** ao lado de Implantação cria outra de qualquer uma das duas; os botões de cópia duplicam uma. As duas trazem uma **indicação do norte** apontando para o norte verdadeiro (Gerenciar → Localização: o ângulo para o norte verdadeiro), uma **escala gráfica** e, quando o projeto está ligado ao mapa, a **grade de coordenadas**: cruzes onde coordenadas E e N redondas se encontram, a cada décimo da escala (50 m em 1:500, 200 m em 1:2000), com os seus valores. Selecione a vista no navegador e defina-os nas suas propriedades, em **Configurações da vista**: o que ela é (planta de situação ou planta de localização), o passo da grade (0: nenhuma), a seta, a escala e **Destacar o projeto** (o preenchimento e o contorno vermelhos, ativado por padrão numa planta de localização, numa planta de situação também quando você quiser).

**O mapa sob elas.** A ortofoto é desenhada sob toda planta de situação e de localização, seja qual for o pavimento em que foi importada, e é impressa com elas nas pranchas e no PDF. Para a planta cadastral ou o desenho do topógrafo, abra a planta de situação e use **Importar DXF…** com **Colocado: pelas coordenadas do mapa**. O próprio Lapin oferece isso quando as coordenadas do arquivo parecem de mapa, por exemplo Stereo 70. O desenho então cai onde as suas coordenadas E e N dizem, pelo ponto de levantamento do projeto e pelo seu ângulo para o norte verdadeiro. Se o projeto ainda não está ligado ao mapa, o desenho o liga. Marque **Como vínculo** para mantê-lo somente leitura e recarregá-lo quando o topógrafo mandar um novo. Importe-o uma vez em cada desenho que precisar dele.

![Uma planta de localização: a casa preenchida em vermelho, o lote contornado em vermelho, entre os vizinhos](shots/site-location-plan.webp)

![Uma planta de situação: a casa vista de cima no seu lote, os vértices numerados, a grade do mapa, a indicação do norte e a escala gráfica](shots/site-drawings.webp)

![A Implantação no navegador do projeto](shots/site-browser.webp)

A tabela **Coordenadas do lote** (Tabelas → + → Coordenadas do lote) é o inventário de coordenadas do lote: cada vértice como a planta de situação o numera, as suas coordenadas N e E no sistema do projeto, o lado até o vértice seguinte e a área. Como qualquer tabela, ela vai para uma prancha ao lado da planta de situação, e sai como CSV ou XLSX.

## Árvores e arbustos

**Planta (vegetação)** (PN) coloca uma planta com um clique, **Área de plantio** (PZ) semeia um contorno com uma mistura de espécies numa densidade; selecione qualquer um dos dois e escolha a sua espécie nas propriedades. A lista de espécies é uma biblioteca de plantas nativas ou tradicionais na Romênia, em grupos: **árvores frutíferas** (macieira, pereira, ameixeira, cerejeira e ginjeira, damasqueiro, marmeleiro), **coníferas** (abeto-branco, abeto-falso, pinheiro-silvestre e pinheiro-negro), **árvores caducifólias** (nogueira, tília, bétula, bordo), **arbustos** (aveleira, corniso, sabugueiro, roseira-brava, lilás, espinheiro-marítimo, groselheira-vermelha, tuia, buxo) e **forrações**. Cada uma vem com o seu nome latino, a sua altura e copa habituais, e é mostrada pelo seu nome romeno quando a interface está em romeno. A tabela de plantio lista os nomes latino e romeno ao lado de cada espécie.

Em planta cada planta é desenhada vista de cima com uma linha fina, tão larga como a sua copa, só pelo seu contorno: uma nogueira ou uma tília uma copa de lóbulos suaves, uma árvore de fruto um círculo, um abeto ou uma espruce uma estrela, um pinheiro um anel dentado, um arbusto uma pequena nuvem, uma planta de sebe aparada um círculo, com uma pequena cruz no tronco de uma árvore. Alçados e cortes desenham cada planta com um só contorno: a copa de uma árvore sobre o seu tronco, o cone em andares de uma conífera, a cúpula de um arbusto. Poucas linhas cada uma, por isso uma planta de implantação com centenas de árvores continua leve. Em 3D as coníferas são cones, as outras árvores têm uma copa arredondada sobre o tronco e os arbustos uma cúpula baixa.

![Um pequeno pomar na planta: árvores frutíferas, uma nogueira, coníferas e arbustos](shots/planting-symbols.webp)

Ponha a **Ambientação em 2D** do estilo de vista em **detalhado** para os símbolos de um projeto de paisagismo, a preto: uma árvore de fruto como copa lobada com ramos bifurcados, uma nogueira ou uma tília como copa recortada em volta de um núcleo escuro, um abeto ou uma espruce como estrela de ramos com agulhas, um pinheiro como tufo redondo de agulhas, um arbusto como pequena nuvem. O Lapin desenha cada símbolo por si, por isso árvores vizinhas diferem um pouco e uma zona nunca parece carimbada; a 1:1000 e menos (uma planta de localização) só se desenham os contornos das copas.

![O mesmo pomar desenhado detalhado](shots/planting-symbols-detailed.webp)

## Pessoas e carros

**Ambientação** (EN) coloca uma pessoa ou um carro com um clique; escolha qual na barra da linha de comandos enquanto a ferramenta está ativa: um **homem de pé**, uma **mulher a caminhar**, uma **criança**, uma **pessoa ao telefone**, um **carro** ou um **SUV / carrinha**. **Fila de ambientação** (ER) coloca uma fila ao longo de uma linha que desenha: carros a cada 2,5 m, estacionados lado a lado de través, com a frente para a esquerda da linha, pessoas a cada 1,2 m, a caminhar ao longo dela. Cada um assenta no terreno por baixo, ou no pavimento do seu piso onde não há terreno.

![Pessoas à porta e carros estacionados junto à casa](shots/entourage-3d.webp)

Selecione um para o alterar nas propriedades: o tipo, a **rotação**, a **altura** (por omissão o tamanho habitual do seu tipo), quanto está **acima do terreno** (num terraço ou numa varanda) e a **cor** da roupa ou da carroçaria. Move-se, roda, espelha-se e copia-se como qualquer elemento.

Em 3D são figuras simples: pessoas com membros, roupa, cabelo e sapatos, carros com vidros, rodas e faróis. Os desenhos mantêm-nos simples, como um arquiteto desenha a ambientação, com uma linha fina: plantas e plantas de implantação de cima, os ombros e a cabeça de uma pessoa, o contorno de um carro com os cantos arredondados e o seu para-brisas; alçados e cortes como silhuetas, um só contorno cada, a figura de uma pessoa, o lado ou a frente de um carro como a vista o vê, nunca as linhas dos seus corpos.

![Em planta](shots/entourage-plan.webp)

![No alçado sul](shots/entourage-elevation.webp)

![As propriedades de um carro](shots/entourage-properties.webp)

Como pessoas, carros e plantas se desenham em 2D é uma definição do estilo de vista, **Ambientação em 2D** (nas propriedades da vista ou em **Vista → Configurações da vista**; para um estilo inteiro, **Gerenciar → Estilos de vista**): **contornos** (por omissão), **abstrato** (uma pessoa um círculo em planta e uma cabeça sobre um corpo num alçado, um carro uma caixa arredondada, uma árvore um círculo com o seu tronco), **detalhado** (os símbolos de paisagismo das plantas e cada linha dos corpos, com as sombras solares da vista, como o Lapin os desenhava antes) ou **não desenhado**. **Preenchimento das silhuetas** preenche as silhuetas de alçados e cortes a cinzento claro ou verde pálido. Desenham-se com a pena 2 (0,13 mm a 1:100), a não ser que **Gerenciar → Estilos de objeto** dê outra à categoria **Ambientação**, e **Visibilidade / Gráficos** oculta essa categoria numa vista. As folhas, o PDF e o DXF imprimem-nos como a vista os desenha; os seus corpos 3D nunca mudam. Os projetos feitos antes também os desenham como contornos.

Dão escala aos desenhos e às imagens, mas não fazem parte do edifício. Uma exportação IFC ou Fragments (File → Export…) deixa-os de fora, a menos que marque **Ambientação** na sua lista de tipos; o IFC escreve-os como IfcBuildingElementProxy «Entourage». As exportações 3D (glTF, USD, COLLADA) incluem-nos. Os testes de colisões deixam-nos de fora.

As figuras seguem as pessoas e os carros do [BimGo](https://github.com/aussieBIMguru/BimGo) de Aussie BIM Guru (MIT-0): as suas proporções, posturas e perfis.

## Regras urbanísticas e o sol (Lapin Urban)

A edição **Lapin Urban** acrescenta um grupo **Urbanismo** a esta guia: as regras urbanísticas do lote (POT, CUT, as alturas máximas, os recuos da rua, das laterais, do fundo e do eixo da via), o **volume edificável** que elas deixam, os indicadores que o modelo alcança frente a elas, e o **estudo de insolação**: o sol e as suas sombras em 3D para qualquer dia e hora no local do projeto, e plantas de sombras para o equinócio e os solstícios.
