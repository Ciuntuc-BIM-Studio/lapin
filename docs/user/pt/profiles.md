---
title: Bibliotecas de perfis
order: 57
summary: Os perfis que o seu trabalho usa, qualquer que seja a norma — o pequeno conjunto EN do Lapin, suas tabelas e arquivos IFC, as pastas da empresa; desenhados com as propriedades calculadas e os valores do arquivo verificados, copiados para o projeto à medida que são usados.
---

O Lapin não pode trazer todos os perfis de aço do mundo; traz um pequeno conjunto europeu e lê o resto do que você já tem: um catálogo em planilha (AISC Shapes Database, a tabela de um fabricante, a da sua empresa), uma biblioteca IFC de perfis (a do Bonsai, por exemplo) ou qualquer modelo IFC, um contorno fechado desenhado no Lapin ou em um DXF. **Gerenciar → Bibliotecas de perfis** reúne todos.

![Bibliotecas de perfis](shots/profile-libraries.webp)

## As bibliotecas

- **Integrada**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, cantoneiras iguais e desiguais, SHS, RHS e CHS acabados a quente em tamanhos comuns, e C, Z e Σ genéricos enformados a frio. Só dimensões normalizadas; o Lapin calcula todas as propriedades.
- **Neste computador**: as bibliotecas que você importa, para qualquer projeto. A versão de navegador também as guarda, nesse navegador.
- **Pastas da empresa**: no aplicativo de desktop, quantas pastas locais, de rede ou sincronizadas com arquivos `.lapinprofiles` quiser (**Adicionar uma pasta…**). Uma pasta é só de leitura até marcar **gravável**; então o botão de salvar ao lado grava lá a biblioteca escolhida, para os colegas.
- **No projeto**: só os perfis usados são copiados, com o nome da sua biblioteca, para o arquivo abrir em qualquer lugar.

Digite na pesquisa para procurar em todas as bibliotecas por nome, família, norma ou tipo; escolha um perfil para vê-lo desenhado com dimensões e propriedades.

## Propriedades

A partir das dimensões, o Lapin calcula área, Iy, Iz, módulos elásticos e plásticos, raios de giração, eixos principais e seu ângulo (cantoneiras, Z, qualquer contorno assimétrico), constante de torção It e de empenamento Iw onde há fórmula, massa por metro pela densidade do material e área de pintura por metro. Concordam com as tabelas publicadas em cerca de 1 % (It e Iw de perfis I laminados em 3 %; Iw de um U com banzos inclinados só em 20 %). Um contorno arbitrário não tem It nem Iw, a menos que o arquivo os dê.

Os valores de um arquivo são mantidos e mostrados ao lado dos do Lapin. Uma diferença acima de 2 % (área, massa), 3 % (inércias, módulos) ou 10 % (It, Iw) é assinalada e vai para a lista de verificação da importação; de uma coluna inteira deslocada em uma potência de dez avisa-se que está em outra unidade.

![Um valor do arquivo diferente do Lapin](shots/profile-check.webp)

## Importar uma tabela

**Tabela…** lê um CSV ou XLSX, uma linha por perfil. O significado e a unidade de cada coluna são deduzidos do cabeçalho em inglês, romeno, alemão ou francês (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), com as unidades (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Uma tabela com Ix e sem Iz é lida à americana: x o eixo forte, em polegadas. Corrija na caixa de diálogo o que estiver errado; o Lapin lembra a escolha para o próximo arquivo com a mesma disposição.

![As colunas de uma tabela associadas](shots/profile-table.webp)

O tipo de uma linha vem da sua coluna de tipo (W, HP, UPN, HSS, PIPE…) ou do nome; para as outras, escolha-o na caixa de diálogo.

## IFC

**IFC…** lê todas as definições de perfil de qualquer arquivo IFC: uma biblioteca ou um modelo do Tekla, Revit ou ArchiCAD, onde os perfis usados pelos elementos recebem o nome dos seus tipos. I, I assimétrico, U, T, L, C, Z, tubos, retângulo, círculo e elipse mantêm tipo e medidas; arbitrários, compostos e derivados viram contornos. Vêm também as propriedades mecânicas de `Pset_ProfileMechanical` (e as do IFC2x3); os exportadores discordam quanto às unidades e ao eixo Y, por isso o Lapin decide ambos pela geometria. Perfis de linha central e trapézio ficam de fora, e isso é dito.

![Perfis lidos de um arquivo IFC](shots/profile-ifc.webp)

**IFC** e **CSV** sob a lista gravam a biblioteca escolhida (ou os perfis do projeto) como biblioteca de perfis IFC4 ou como tabela que o Lapin volta a ler.

Também no IFC do modelo as vigas, pilares e perfis de aço são gravados pelo seu perfil paramétrico (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, retângulos e círculos), com o nome do perfil e `Pset_ProfileMechanical`, de modo que outros programas veem um IPE 300 e não uma polilinha. Contornos, Σ e Z com abas ficam perfis arbitrários; um elemento cortado por outro continua o sólido que é.

## Um contorno como perfil

Desenhe a seção com Desenho como polilinhas fechadas e círculos (os que estão dentro de outro são furos), selecione-os e clique em **Da seleção**, ou leia o desenho de um fabricante com **DXF…**. Dê um nome, escolha material e biblioteca e guarde. É assim que entram perfis de alumínio e PVC, seções pré-moldadas e formas de madeira: o mesmo depósito, com o material e a densidade deles.

![Um contorno desenhado transformado em perfil](shots/profile-outline.webp)

**Desenhar o contorno** faz o contrário: o contorno do perfil escolhido como polilinhas fechadas na vista ativa, para o Studio extrudar ou varrer.

## Usar um perfil

Em **Aço → Tipos de aço**, **Estrutura → Tipos estruturais** (pilares e vigas) e nos perfis do galpão de pórticos, escolha **De uma biblioteca de perfis…** e digite para pesquisar.

![O seletor pesquisa em todas as bibliotecas](shots/profile-picker.webp)

O perfil é copiado para o projeto e o tipo o mantém. Os corpos são leves, com cantos vivos, em todos os níveis menos um: uma vista em **LOD 400** (planta, corte, alçado, folha ou 3D) desenha a secção real, com raios de concordância, raios nas pontas e banzos inclinados. As quantidades e as exportações mantêm o corpo de cantos vivos; raios de concordância e banzos inclinados contam nas propriedades e na exportação estrutural.

![Uma viga e um pilar de aço numa vista 3D em LOD 400: raios de concordância desenhados](shots/profile-real-section.webp)

Em outros lugares o perfil dá o que a ferramenta pode usar: molduras, rasgos e sapatas corridas tomam o exterior dele como perfil 2D (**De uma biblioteca de perfis…** na lista deles), e montantes e travessas de fachadas-cortina, caixilhos e folhas de portas e janelas tomam o seu contorno real, com vazios, ao longo de cada peça, com a sua largura e profundidade; o x do perfil vai através da peça (em direção ao vidro no caixilho e na folha), o y para fora do edifício. **Simples** retira o contorno e mantém o tamanho.

![Montantes e travessas de fachada-cortina de uma secção de alumínio](shots/profile-real-mullions.webp)

![Caixilho e folha de uma janela da mesma secção](shots/profile-real-frame.webp)

Uma biblioteca pode mudar depois de usada. Um perfil do projeto diferente do de mesmo nome na sua biblioteca é assinalado **mais recente na biblioteca**; **Atualizar a partir da biblioteca** o substitui e todos os tipos feitos com ele acompanham, como um só passo de desfazer. Nada muda até você pedir. Projetos anteriores mantêm seus tipos.
