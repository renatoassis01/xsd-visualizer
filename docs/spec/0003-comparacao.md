---
status: ready-for-agent
---

# Comparação de Schema Sets

## Problem Statement

A cada Nota Técnica a SEFAZ publica um novo pacote de schemas. Descobrir o que mudou em relação ao pacote anterior — campos que entraram ou saíram, cardinalidades, tamanhos, patterns, valores de enumeração — hoje exige comparar XSDs à mão ou gerar exemplos e usar uma ferramenta de diff externa, que não mostra mudanças que um único XML não exercita (outros ramos de choice, outros valores de enumeração).

## Solution

Um botão **Comparar** abre uma Comparison entre dois Schema Sets abertos (**Before** e **After**). A Comparison lista os **Element Pairs** com seu status e, para cada par, mostra a árvore de **Changes** (🟢 Added, 🔴 Removed, 🟡 Modified; Documentation-only escondido por padrão) ao lado de um diff lado a lado dos Samples Maximal (ou Minimal) das duas versões, em verde/vermelho/âmbar, navegável a partir da árvore. Um resumo em Markdown pode ser exportado para o time.

## User Stories

1. Como analista fiscal, quero um botão "Comparar" na barra de cima, para começar uma Comparison sem procurar em menus.
2. Como analista fiscal, quero escolher o Schema Set Before e o After entre os abertos, com o primeiro aberto como Before por padrão.
3. Como analista fiscal, quero que a Comparison abra numa aba própria, para continuar usando o resto do app.
4. Como analista fiscal, quero ver a lista de Element Pairs com status novo, removido, alterado ou igual, para saber o que a NT tocou.
5. Como analista fiscal, quero que os pares sejam feitos por namespace + nome + arquivo e, na falta disso, por namespace + nome quando só houver um de cada lado.
6. Como analista fiscal, quero ver os Global Elements que ficaram sem par e poder parear dois à mão, para casos como `envEvento` em vários arquivos ou um nome que mudou.
7. Como analista fiscal, quero filtrar a lista para só os alterados, novos e removidos.
8. Como analista fiscal, quero ver, para um par, a árvore de Changes com Added em verde, Removed em vermelho e Modified em âmbar.
9. Como analista fiscal, quero que Modified cubra tipo, cardinalidade, facets (pattern, tamanhos, dígitos, faixas), valor fixo/padrão e valores de enumeração.
10. Como analista fiscal, quero ver os valores de enumeração que entraram e saíram, um a um (ex.: `cStat`: +150, −999).
11. Como analista fiscal, quero que mudanças só de documentação fiquem escondidas por padrão, com uma opção para mostrá-las, para não me afogar em ruído.
12. Como analista fiscal, quero que um nó com mudanças dentro dele mostre quantas, mesmo recolhido.
13. Como analista fiscal, quero o filtro "só mudanças" ligado por padrão na árvore.
14. Como analista fiscal, quero ir para a próxima/anterior mudança com um clique.
15. Como analista fiscal, quero selecionar uma Change e ver "antes → depois" no painel de detalhes.
16. Como analista fiscal, quero ver lado a lado o Maximal do Before e do After com as linhas que entraram em verde, as que saíram em vermelho e as que mudaram em âmbar.
17. Como analista fiscal, quero trocar o lado a lado para o Minimal.
18. Como analista fiscal, quero que clicar num nó da árvore role os dois lados até a linha dele.
19. Como analista fiscal, quero que o diff dos Samples mostre só diferenças de schema, não do gerador (mesmas regras dos dois lados; determinismo).
20. Como analista fiscal, quero exportar um resumo em Markdown (Global Elements alterados e, em cada um, caminho, tipo da Change e antes → depois), para copiar ou salvar e discutir a NT com o time.
21. Como analista fiscal, quero que a Comparison de pacotes grandes (NF-e) não trave a interface.
22. Como analista fiscal, quero que a interface da Comparison respeite o tema e o idioma escolhidos.

## Implementation Decisions

- **Core — Comparison:** comparar dois Schema Sets produz a Comparison: Element Pairs (Before, After, um dos lados pode faltar) com status Added/Removed/Modified/Unchanged. Pareamento: namespace + nome + nome do arquivo declarante; depois namespace + nome quando único dos dois lados; o resto fica sem par. Um par manual pode ser criado a partir de dois Global Elements quaisquer.
- **Core — diff estrutural:** as árvores dos dois Global Elements são percorridas em paralelo; nós são pareados pelo **caminho de nomes** (elementos e atributos; `sequence`/`choice`/`all` não entram no caminho; nós recursivos não são expandidos). Cada Change tem caminho, tipo (Added, Removed, Modified, Documentation-only) e, se Modified, a lista de diferenças (tipo, cardinalidade, cada facet, valor fixo/padrão) com antes → depois e, para enumerações, valores que entraram e saíram. O status do par é Modified se houver qualquer Change que não seja só de documentação.
- **Core — diff de Samples:** gera o Maximal (ou Minimal) dos dois lados e faz diff linha a linha (DiffPlex), produzindo linhas lado a lado marcadas igual/entrou/saiu/mudou. Cada linha sabe o caminho de nomes do elemento que a gerou, para a navegação a partir da árvore.
- **Core — resumo:** a Comparison produz um Markdown com o título Before → After, a lista de Global Elements novos, removidos e alterados e, para cada alterado, a tabela de Changes.
- **App:** botão "Comparar" com diálogo de Before/After; aba de Comparison com três áreas — lista de pares (com filtro), árvore de Changes (filtro "só mudanças", contagens nos ancestrais, próxima/anterior, mostrar documentação) com painel antes → depois, e o lado a lado com dois editores somente leitura, fundo de linha colorido e rolagem sincronizada; seletor Maximal/Minimal; "Exportar resumo". Comparação e diffs rodam fora da thread de interface.

## Testing Decisions

- Seam: a fachada pública do Core (Comparison, diff estrutural do par, diff de Samples, resumo). Nada novo além disso.
- Fixtures sintéticas Before/After cobrindo: campo novo, campo removido, cardinalidade alterada, pattern alterado, enumeração com valores a mais e a menos, mudança só de documentação, Global Element novo e removido, pareamento por arquivo e ambíguo. Expectativas escritas à mão.
- Integração: PL_009_V4 × PL_010_V1.30 (NF-e); fatos conhecidos da NT (ex.: grupo `IBSCBS` Added no `NFe`) e sanidade (Comparison de um Schema Set com ele mesmo não tem Changes).
- Interface sem testes automatizados.

## Out of Scope

- Comparar WSDLs (Services, Operations, Endpoints).
- Detectar renomeações (viram Removed + Added).
- Comparar Coverage Sets.
- Diff de Documents do usuário.

## Further Notes

- Glossário: Comparison, Before/After, Element Pair, Change (CONTEXT.md).
- O diff de Samples é complementar: mudanças que um Maximal não exercita (outros ramos, outros valores de enumeração) aparecem só na árvore de Changes.
- Pesquisa na árvore de Changes (depois desta spec): procura no Element Pair selecionado, por nome e documentação (Before e After), só entre os nós que os filtros mostram; não entra abaixo de tipos recursivos nem procura no XML (o editor tem a própria busca, ⌘F/Ctrl+F).
