---
title: Planejamento
order: 97
summary: O cronograma da obra em um gráfico de Gantt: tarefas em uma EAP, marcos, vínculos com defasagens, o caminho crítico calculado em um calendário com os feriados legais da Romênia; de e para MS Project, IFC e Excel.
---

## A aba Planejamento

![A aba Planejamento](shots/ribbon-planning.webp)

- **Gantt**: abre o cronograma sob a vista. A tela se divide em duas linhas: o modelo em cima e o cronograma embaixo.
- **Calendário…**: os dias e horas de trabalho, os feriados e a pausa de inverno (abaixo).
- **Tarefa** e **Marco**: uma nova tarefa ou marco antes da tarefa selecionada no Gantt (ou no fim).
- **Recuar**, **Avançar**, **Vincular tarefas** e **Desvincular tarefas**: a EAP e os vínculos das tarefas selecionadas.
- **Adicionar à tarefa…** (TA) e **Elementos sem tarefa**: os elementos do modelo nas tarefas (abaixo).
- **Importar MS Project…**, **Exportar MS Project…**, **Exportar XLSX** e **Exportar IFC…**: o cronograma para e de outros programas (abaixo).

Um projeto tem um cronograma. Ele é criado na primeira vez que você adiciona uma tarefa e começa na segunda-feira seguinte. Mude o início na barra do Gantt.

## O Gantt

![O Gantt sob a vista 3D, uma tarefa selecionada](shots/planning-gantt.webp)

À esquerda está a tabela de tarefas: o número da linha, o código **EAP**, o nome, a **Duração** em dias úteis, o **Início** e o **Término**, as **Predecessoras** e a **Folga** total. À direita, cada tarefa é uma barra na escala de tempo:

- uma **tarefa** é uma barra azul, vermelha quando está no caminho crítico;
- uma **tarefa resumo** (uma tarefa com tarefas abaixo) é um colchete escuro do início da sua primeira tarefa ao término da última;
- um **marco** é um losango.

Os dias sem trabalho aparecem sombreados e hoje é uma linha tracejada. **Dias**, **Semanas**, **Meses** e **Trimestres** mudam a escala, assim como <kbd>Ctrl</kbd> + a roda do mouse. O botão de quatro cantos mostra todo o cronograma e **Hoje** rola até hoje. **Caminho crítico** liga e desliga o vermelho. A seta ao lado do nome de uma tarefa resumo recolhe e expande as tarefas abaixo dela.

O Gantt é uma vista como as outras: tem uma aba e, no desktop, pode abrir em uma janela própria para um segundo monitor.

### Edição na tabela

Dê um clique duplo em uma célula para escrever nela. <kbd>Enter</kbd> mantém o valor e <kbd>Escape</kbd> desiste.

- **Duração**: dias úteis; 0 transforma a tarefa em um marco.
- **Início**: a tarefa começa não antes desse dia (uma restrição, veja abaixo).
- **Término**: a duração passa a ser os dias úteis do início até esse dia.
- **Predecessoras**: como no MS Project, números de linha separados por vírgulas, cada um com um tipo e uma defasagem: `3`, `5SS`, `7FF+2`, `2FS-1d`.

O teclado funciona como em uma tabela: <kbd>↑</kbd> e <kbd>↓</kbd> movem a seleção, <kbd>Insert</kbd> adiciona uma tarefa antes da selecionada (<kbd>Shift</kbd>+<kbd>Insert</kbd> um marco), <kbd>Delete</kbd> exclui as tarefas selecionadas com o que está abaixo delas, <kbd>Tab</kbd> recua e <kbd>Shift</kbd>+<kbd>Tab</kbd> avança, <kbd>F2</kbd> renomeia.

### Edição no gráfico

- **Arraste uma barra** para mover a tarefa. Ela passa a começar não antes do dia em que você a solta. A data aparece enquanto você arrasta.
- **Arraste a ponta de uma barra** para mudar sua duração. Os dias úteis aparecem enquanto você arrasta.
- **Vincule duas tarefas**: passe o mouse sobre uma barra e arraste do pequeno círculo em uma de suas pontas até outra barra. As pontas que você une dão o tipo: do término ao início da outra é término-início (FS), início com início SS, término com término FF, início com término SF.
- **Clique em uma seta** para mudar o tipo e a defasagem do vínculo (em dias úteis; negativa é uma sobreposição) ou para removê-lo.

Cada mudança é um único passo para desfazer.

## Uma tarefa em Propriedades

![Uma tarefa em Propriedades](shots/planning-task.webp)

Selecione uma tarefa e Propriedades a mostra: seu nome, seu tipo no IFC, sua duração, se é um marco, suas datas e:

- **Restrição**: **O mais cedo possível** (padrão), **Não iniciar antes de** uma data ou **Deve iniciar em** uma data. Uma tarefa que precisa começar antes de suas predecessoras terminarem tira folga delas. A folga delas fica então negativa e elas se tornam críticas.
- **Serviços úmidos**: concreto, contrapisos, reboco. Uma tarefa úmida não trabalha na pausa de inverno do calendário, quando a pausa está ligada.
- **Folga total** e **Folga livre**: quantos dias úteis a tarefa pode atrasar sem mover o término, ou sem mover o que vem depois dela. **Início mais tarde** e **Término mais tarde** são as datas mais tardias que mantêm o término.
- **Predecessoras**: cada vínculo de entrada, com seu tipo e defasagem para mudar, ou para remover. **Sucessoras** lista o que vem depois da tarefa.

## O caminho crítico

O Lapin calcula o cronograma após cada mudança. Uma passagem para a frente encontra as datas mais cedo e uma passagem para trás as mais tardias. As tarefas sem folga formam o caminho crítico, e um atraso em qualquer uma delas move o término. Um vínculo para uma tarefa resumo prende todas as tarefas abaixo dela. Um vínculo de uma tarefa resumo parte do início da sua primeira tarefa e termina no término da última. O Lapin recusa um vínculo que criaria um laço.

## Os elementos do modelo nas tarefas

![A tarefa Alvenaria selecionada: suas paredes desenhadas selecionadas em 3D](shots/planning-links.webp)

Uma tarefa leva elementos do modelo, assim o cronograma sabe o que é construído e quando. Selecione uma tarefa e seus elementos aparecem selecionados em 3D e nas plantas; um clique duplo na sua barra os seleciona e enquadra. Selecione elementos numa vista e as linhas das tarefas deles são marcadas no Gantt. A coluna **Elementos** conta os elementos de cada tarefa (os de um resumo, com os das tarefas abaixo dele).

### Uma regra

![Os elementos de uma tarefa em Propriedades](shots/planning-task-elements.webp)

Em **Elementos** nas Propriedades, marque **Encontrá-los por uma regra** e escolha o que a tarefa leva, como no filtro de uma tabela: **Classes**, **Pavimentos**, **Tipos**, **Materiais**, **Grupos** e **Fases**, cada valor com quantos elementos o têm. Toda condição definida deve valer; uma vazia deixa tudo passar. Guarda-se a regra, não os elementos: uma parede desenhada amanhã no térreo entra sozinha na tarefa “Alvenaria térreo”. **Regra a partir da seleção** faz uma regra com as classes e pavimentos dos elementos selecionados.

### Uma parte dos elementos

**Parte** diz o que a tarefa leva de cada elemento: os **elementos inteiros**, seu **núcleo** (a camada estrutural), seus **acabamentos** (todas as camadas exceto o núcleo), as camadas de um material ou as de uma categoria de material (Acabamento, Isolamento…). Assim uma parede em camadas é erguida numa tarefa Alvenaria (o núcleo) e rebocada numa tarefa Reboco (os acabamentos). Um elemento sem camadas é seu próprio núcleo e não tem acabamentos.

### À mão

![O cartão da seleção sobre o 3D, as duas tarefas de uma parede marcadas no Gantt](shots/planning-element-tasks.webp)

Enquanto o Gantt está aberto, um cartão sobre a vista do modelo diz quantos elementos estão selecionados e em quais tarefas estão. Arraste-o pela alça para a linha de uma tarefa: os elementos entram nessa tarefa. Segure <kbd>Shift</kbd> ao soltar para retirá-los dela. **Adicionar à tarefa…** (<kbd>T</kbd> <kbd>A</kbd>, também na aba Planejamento) escolhe a tarefa numa lista.

![Adicionar à tarefa](shots/planning-assign.webp)

O que você adiciona ou retira à mão são as **exceções** da tarefa: a regra fica como está. As Propriedades as contam, e o × ao lado de cada uma as esquece. As Propriedades de um elemento listam suas tarefas em **Cronograma da obra**, cada uma com um × para retirá-lo. Cada mudança é um passo para desfazer.

### Elementos sem tarefa

Assim que uma tarefa leva elementos, a barra do Gantt diz quantos elementos não têm tarefa ou têm camadas que nenhuma tarefa leva; um clique os seleciona, prontos para arrastar para uma tarefa. **Planejamento → Elementos sem tarefa** faz o mesmo. **Gerenciar → Verificar** também os lista, em *Elements with a task*: um aviso para cada um, um clique o seleciona. Elementos existentes que permanecem não precisam de tarefa.

### Em IFC e MS Project

A exportação IFC escreve o que cada tarefa constrói como IfcRelAssignsToProduct (o elemento, com as tarefas que o constroem), e aquilo em que atua uma tarefa de demolição, remoção ou desmontagem como IfcRelAssignsToProcess. A regra, a parte e as exceções viajam em `Lapin_Task`, então o Lapin relê a regra; um arquivo de outro programa dá tarefas com os elementos que ele lhes atribui. O MS Project XML as leva no campo Text1, chamado *Lapin elements*. A pasta de trabalho tem as colunas **Elements** e **Rule**.

## O calendário

![O calendário de trabalho](shots/planning-calendar.webp)

**Planejamento → Calendário…** define:

- os **dias úteis** da semana (de segunda a sexta por padrão) e as **horas** de um dia (8 a partir das 08:00);
- os **feriados**: os feriados legais da Romênia, calculados para cada ano (incluindo a Sexta-feira Santa, a Páscoa e o Pentecostes ortodoxos), ou nenhum. A lista abaixo mostra os feriados de um ano para conferir;
- a **pausa de inverno** para serviços úmidos, desligada por padrão: de 15 de dezembro a 1º de março, a menos que você defina outros dias;
- as **exceções**: dias de folga do projeto (uma obra fechada) ou dias trabalhados que normalmente não seriam (um sábado).

As durações e defasagens contam dias úteis deste calendário.

## Trocando o cronograma

- **Exportar MS Project…** grava um arquivo XML do MS Project. MS Project, ProjectLibre e GanttProject o abrem, e o Primavera P6 o importa. Ele contém as tarefas com sua EAP, durações, datas, restrições e folgas, os vínculos com seus tipos e defasagens, e o calendário com cada feriado como exceção. **Importar MS Project…** lê um arquivo assim no lugar do cronograma do projeto. Um cronograma gravado pelo Lapin e lido de novo mantém os ids e as datas das suas tarefas.
- **Exportar IFC…** (também em **Arquivo**) grava o cronograma no arquivo IFC junto com o modelo: um IfcWorkSchedule, seus IfcTask com seus tempos, os vínculos IfcRelSequence e o IfcWorkCalendar. Importar um arquivo IFC que tenha um cronograma traz também o cronograma.
- **Exportar XLSX** grava uma pasta de trabalho: a tabela do Gantt e os dias de folga do calendário.

Os campos próprios do Lapin para os quais outros programas não têm lugar (a restrição de uma tarefa, se ela é úmida, a pausa de inverno) viajam no IFC como os conjuntos de propriedades `Lapin_Task` e `Lapin_Calendar`. O MS Project não tem tarefas úmidas, então a pausa de inverno fica no Lapin.

## O que vem a seguir

Um cronograma gerado a partir do modelo, a simulação 4D do edifício crescendo em 3D, o avanço e as linhas de base, e o orçamento chegam nas próximas etapas.

