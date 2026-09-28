---
status: ready-for-agent
---

# XSD Visualizer v1

## Problem Statement

Quando a SEFAZ (ou qualquer outro emissor de padrões) publica novos XSDs, entender o que mudou e como fica um XML válido é trabalhoso: os schemas vêm espalhados em vários arquivos ligados por `include`/`import`, cheios de `xs:choice`, `xs:pattern`, enumerações e opcionais. Hoje não há uma forma rápida de abrir uma pasta de XSDs, navegar pela estrutura, ver exemplos XML válidos cobrindo todas as opções do schema e checar se XMLs existentes ainda passam no schema novo — sem depender de ferramentas pagas como o XMLSpy ou de uma plataforma específica.

## Solution

Um aplicativo desktop multiplataforma (Windows, macOS, Linux) em que o usuário seleciona ou arrasta uma pasta de XSDs, que vira um **Schema Set**. O app mostra a árvore de cada **Global Element** com cardinalidade, tipos, facets e documentação; gera **Samples** válidos e determinísticos (**Maximal Sample**, **Minimal Sample** e o **Coverage Set** completo, em que cada ramo de choice, opcional e valor de enumeração aparece ao menos uma vez); e permite abrir **Documents** existentes, que são vinculados (**Binding**) ao Schema Set certo e validados num editor XML com os **Validation Issues** destacados. Samples e Documents podem ser salvos. O app é genérico — não depende de nada da SEFAZ.

## User Stories

### Abrir Schema Sets

1. Como analista fiscal, quero selecionar uma pasta de XSDs pelo diálogo do sistema, para abrir um Schema Set.
2. Como analista fiscal, quero arrastar uma pasta para a janela, para abrir um Schema Set sem navegar por diálogos.
3. Como desenvolvedor, quero arrastar um `.xsd` solto e ter a pasta dele aberta como Schema Set, para não precisar lembrar que o XSD depende dos arquivos vizinhos.
4. Como analista fiscal, quero manter vários Schema Sets abertos ao mesmo tempo, para comparar a versão antiga e a nova de um leiaute lado a lado.
5. Como desenvolvedor, quero que `include`/`import` entre arquivos da pasta sejam resolvidos automaticamente, para que o Schema Set compile como um todo.
6. Como desenvolvedor, quero que imports remotos de schemas W3C comuns (xmldsig, `xml.xsd`) funcionem offline, porque eles vêm embutidos no app.
7. Como desenvolvedor, quero que outros imports remotos sejam baixados e guardados em cache, para abrir schemas de terceiros sem copiar dependências manualmente.
8. Como desenvolvedor, quero ver uma Validation Issue clara quando um import remoto não puder ser resolvido (offline e sem cache), para entender por que o Schema Set está incompleto.
9. Como desenvolvedor, quero ver uma Validation Issue na carga quando o Schema Set usar construções de XSD 1.1, para não receber resultados silenciosamente errados.
10. Como desenvolvedor, quero ver os erros de compilação do Schema Set (tipos não encontrados, definições duplicadas) com arquivo, linha e coluna, para corrigir o schema.
11. Como usuário frequente, quero que o app reabra os Schema Sets da última sessão, para continuar de onde parei.
12. Como usuário frequente, quero uma lista de Schema Sets recentes, para reabrir rapidamente pastas que uso muito.
13. Como desenvolvedor editando XSDs, quero que o app recarregue o Schema Set quando um arquivo da pasta mudar em disco, para ver o efeito da minha edição imediatamente.
14. Como usuário, quero fechar um Schema Set aberto, para limpar o espaço de trabalho.

### Visualizar a estrutura

15. Como analista fiscal, quero ver a lista de Global Elements de cada Schema Set, para saber quais documentos o schema define.
16. Como analista fiscal, quero navegar pela árvore de um Global Element, para entender a estrutura do documento.
17. Como analista fiscal, quero ver a cardinalidade de cada nó (`0..1`, `1..1`, `1..n`, `0..unbounded`), para saber o que é obrigatório e o que repete.
18. Como analista fiscal, quero ver claramente onde há `sequence`, `choice` e `all`, para entender quais elementos são alternativos.
19. Como analista fiscal, quero ver atributos junto com os elementos na árvore, para ter a estrutura completa.
20. Como analista fiscal, quero selecionar um nó e ver no painel de detalhes o tipo, os facets (pattern, enumeração, tamanho min/max, totalDigits, fractionDigits, min/max inclusive/exclusive) e a `xs:documentation`, para entender as regras do campo.
21. Como analista fiscal, quero pesquisar na árvore por nome de elemento/atributo ou por texto da documentação, para achar um campo específico em schemas grandes como a NF-e.
22. Como desenvolvedor, quero ver nós recursivos marcados como recursivos e expansíveis sob demanda, para navegar em schemas recursivos sem a árvore crescer infinitamente.
23. Como desenvolvedor, quero ver substitution groups, elementos/tipos abstratos e tipos derivados (`xsi:type`) como alternativas na árvore, para entender o polimorfismo do schema.
24. Como desenvolvedor, quero ver uma nota nos nós `xs:any`/`xs:anyAttribute` informando o namespace aceito, para saber que ali cabe conteúdo arbitrário.
25. Como analista fiscal, quero fixar na árvore qual ramo de um choice (ou qual substituto/tipo derivado) usar, para gerar um exemplo com a combinação que me interessa.

### Gerar Samples

26. Como analista fiscal, quero gerar o Maximal Sample de um Global Element, para ver um XML com todos os campos possíveis preenchidos.
27. Como analista fiscal, quero gerar o Minimal Sample de um Global Element, para ver o mínimo que o schema exige.
28. Como analista fiscal, quero que o Maximal Sample respeite os ramos que fixei na árvore, para ver a combinação escolhida.
29. Como analista fiscal, quero que cada Sample seja válido contra o Schema Set (patterns, enumerações, tamanhos, faixas numéricas), para poder usá-lo como referência confiável.
30. Como desenvolvedor, quero que elementos repetíveis apareçam `max(minOccurs, min(maxOccurs, 2))` vezes no Maximal Sample e `minOccurs` vezes no Minimal, para ver que o campo repete sem inchar o arquivo.
31. Como desenvolvedor, quero que Samples sejam determinísticos (mesmo Schema Set → mesmos bytes), para comparar com diff os Samples de duas versões de um schema.
32. Como analista fiscal, quero gerar o Coverage Set de um Global Element, para ter exemplos que, juntos, mostram todas as opções viáveis do schema.
33. Como analista fiscal, quero que cada Sample do Coverage Set tenha um comentário no topo listando o que ele cobre (ramos, opcionais, valores de enumeração), para saber por que aquele arquivo existe.
34. Como analista fiscal, quero gerar os Samples de todos os Global Elements de um Schema Set de uma vez para uma pasta, no layout `<SchemaSet>/<GlobalElement>/<GlobalElement>.{max,min,cov-NN}.xml`, para ter um acervo completo de exemplos.
35. Como desenvolvedor, quero que um Sample que o gerador não consiga tornar válido (regex não suportada, facets conflitantes, wildcard obrigatório estrito) ainda seja gerado, mas marcado como inválido com suas Validation Issues, para nunca perder informação silenciosamente.
36. Como desenvolvedor, quero que wildcards opcionais sejam omitidos nos Samples e que um wildcard obrigatório lax/skip receba só um placeholder, para que o gerador não invente conteúdo além do necessário para o Sample ser válido.
37. Como desenvolvedor, quero que tipos recursivos sejam expandidos uma única vez nos Samples, para que a geração termine.
38. Como analista fiscal, quero abrir qualquer Sample gerado no editor, para lê-lo com syntax highlight.
39. Como analista fiscal, quero salvar um Sample com "salvar como", para usá-lo fora do app.

### Documents e validação

40. Como analista fiscal, quero arrastar ou selecionar um XML existente, para abri-lo como Document.
41. Como analista fiscal, quero que o Document seja vinculado automaticamente ao único Schema Set aberto que declara o seu namespace e elemento raiz, para validar sem passos extras.
42. Como analista fiscal, quero escolher o Schema Set quando houver mais de um candidato (ex.: NF-e 3.10 e 4.00 com o mesmo namespace), para validar contra a versão certa.
43. Como analista fiscal, quero que um Document sem nenhum candidato abra no editor sem validação e com um aviso, para ainda poder vê-lo.
44. Como analista fiscal, quero trocar o Binding de um Document para outro Schema Set, para responder "meu XML antigo passa no schema novo?".
45. Como analista fiscal, quero ver a lista de Validation Issues do Document com linha e coluna, para localizar cada problema.
46. Como analista fiscal, quero clicar numa Validation Issue e ir para a posição dela no editor, para corrigir rapidamente.
47. Como analista fiscal, quero ver as Validation Issues sublinhadas no editor, para identificá-las visualmente.
48. Como analista fiscal, quero editar o Document no editor e ter a validação refeita enquanto digito, para ver na hora se a correção funcionou.
49. Como analista fiscal, quero salvar o Document sobrescrevendo o original, para persistir minhas correções.
50. Como analista fiscal, quero confirmação antes de salvar um Document que ainda tem Validation Issues, para não gravar um XML inválido por engano.
51. Como analista fiscal, quero "salvar como" para um Document, para gravar uma cópia sem tocar no original.

### Geral

52. Como usuário de Windows, macOS ou Linux, quero um executável self-contained, para rodar o app sem instalar o .NET.
53. Como usuário brasileiro, quero a interface em português, para usar o app no meu idioma.
54. Como mantenedor, quero os textos da interface em arquivos de recursos, para poder adicionar inglês depois sem refatorar.

## Implementation Decisions

- **Stack:** .NET 10 (LTS), Avalonia UI (última estável), AvaloniaEdit para o editor XML, CommunityToolkit.Mvvm para MVVM. Publicação self-contained por plataforma (win-x64, osx-arm64, osx-x64, linux-x64).
- **Módulos:**
  - **Core** (sem dependência de UI): carga de Schema Set, resolução de schemas, modelo da árvore, gerador de Samples, Coverage Set, Binding e validação. Exposto por uma única fachada pública, que é o seam de teste.
  - **App** (Avalonia): views e view models finos sobre a fachada do Core; drag-and-drop, diálogos, sessão/recentes, monitoramento de pastas, editor.
  - **Core.Tests** (xUnit).
- **Fachada do Core** (operações, não assinaturas):
  - Abrir Schema Set a partir de uma pasta (ou de um `.xsd`, abrindo a pasta dele) → Schema Set com Global Elements e Validation Issues de carga.
  - Obter a árvore de um Global Element (nós com cardinalidade, compositor, tipo, facets, documentação, marca de recursão, alternativas polimórficas, wildcards).
  - Gerar Maximal/Minimal Sample a partir de um Global Element e de um conjunto de ramos fixados → XML + status de validade + Validation Issues.
  - Gerar Coverage Set de um Global Element → lista de Samples, cada um com a descrição do que cobre.
  - Fazer Binding de um Document contra os Schema Sets abertos → um candidato, vários, ou nenhum.
  - Validar um XML contra um Schema Set → Validation Issues com linha e coluna.
- **Resolução de schemas:** resolver injetável na abertura do Schema Set, na ordem: schemas W3C embutidos → cache local → download (e grava no cache). Falha vira Validation Issue.
- **Validação:** `System.Xml.Schema` do .NET; apenas XSD 1.0 (ADR-0002). O validador do .NET é a fonte de verdade sobre validade.
- **Geração de valores:** determinística (sem aleatoriedade não semeada). Valores satisfazem enumerações, patterns (gerador de strings a partir de regex), tamanhos, totalDigits/fractionDigits e faixas. Enumerações usam os valores declarados. Após gerar, o Sample é validado; em caso de falha há um número limitado de novas tentativas com valores alternativos; se persistir, o Sample é marcado inválido com as Validation Issues.
- **Regras estruturais do gerador:**
  - Repetições: Minimal = `minOccurs`; Maximal = `max(minOccurs, min(maxOccurs, 2))`.
  - Recursão: expandida uma vez; depois, apenas o mínimo (opcionais recursivos omitidos).
  - Choices, substitution groups, elementos/tipos abstratos e `xsi:type` são tratados todos como "alternativas": fixáveis na árvore e cobertas pelo Coverage Set. Sem fixação, usa-se a primeira alternativa.
  - Wildcards opcionais são omitidos. Um wildcard obrigatório com `processContents` lax/skip recebe um elemento-placeholder (`<exemplo/>`) num namespace permitido, pois qualquer elemento não declarado é válido ali; um obrigatório strict fica sem conteúdo e o Sample sai marcado inválido. (Ajuste feito na implementação: os envelopes de evento genéricos da SEFAZ, como `detEvento`, têm `xs:any` lax obrigatório.)
  - A `Signature` (xmldsig) sai estruturalmente válida com valores fictícios.
- **Coverage Set** (ADR-0001): não é produto cartesiano. Alternativas independentes variam em paralelo, de modo que o número de Samples fique próximo do maior choice/enumeração. Cada ramo, opcional e valor de enumeração aparece ao menos uma vez. Cada Sample leva um comentário XML no topo listando o que cobre.
- **Saída do "gerar todos":** `<pasta escolhida>/<SchemaSet>/<GlobalElement>/<GlobalElement>.max.xml`, `.min.xml`, `.cov-NN.xml`.
- **Binding:** por namespace + nome do elemento raiz. Um candidato → automático; vários → pergunta; nenhum → abre sem validação com aviso. O Binding pode ser trocado depois.
- **Salvar:** Documents têm "salvar" (sobrescreve, com confirmação se houver Validation Issues) e "salvar como"; Samples só têm "salvar como".
- **Sessão:** Schema Sets abertos e recentes persistidos por usuário; Schema Sets reabertos na inicialização; pastas monitoradas com recarga automática.
- **Idioma:** interface em português, com todos os textos em arquivos de recursos.

## Testing Decisions

- **Um bom teste** exercita só a fachada pública do Core: entra um Schema Set (XSD de fixture) e/ou um XML; sai XML gerado, Validation Issues, candidatos de Binding ou nós da árvore. Nada de afirmar sobre classes internas do gerador.
- **Oráculo de validade:** todo Sample gerado num teste deve passar no validador do .NET contra o próprio Schema Set (exceto nos testes que verificam de propósito o caso "marcado como inválido").
- **Cobertura verificada pelo resultado:** os testes do Coverage Set leem os XMLs gerados e confirmam que cada ramo, opcional e valor de enumeração da fixture aparece em pelo menos um Sample — não inspecionam como o gerador decidiu.
- **Determinismo:** gerar duas vezes o mesmo Schema Set deve produzir bytes idênticos.
- **Fixtures:** XSDs pequenos e sintéticos, um por caso — sequence/choice/all, opcionais, repetições, recursão, substitution group, tipo abstrato, `xsi:type`, cada tipo de facet, wildcard, include/import local, import remoto, construção XSD 1.1, regex não suportada/facets conflitantes. Mais um pacote real da NF-e como teste de integração (todo Sample do Coverage Set valida).
- **Resolução remota:** testada com um resolver fake injetado (sem rede), cobrindo embutido → cache → download → falha.
- **Não testado automaticamente na v1:** a camada Avalonia (views/view models), drag-and-drop, monitoramento de pastas e persistência de sessão. Testes headless do Avalonia ficam para depois.
- **Prior art:** nenhum — repositório novo.

## Out of Scope

- Diff estrutural entre dois Schema Sets (comparação é feita via diff externo dos Samples determinísticos).
- Visualização em diagrama gráfico (estilo XMLSpy).
- XSD 1.1 (`xs:assert`, `xs:alternative`).
- Regras de negócio fora do XSD (dígito verificador de CNPJ/CPF/chave, regras de validação da SEFAZ).
- Assinatura digital real em Samples.
- Preenchimento de wildcards com conteúdo de outros Schema Sets.
- Interface em outros idiomas além do português.
- Instaladores nativos (MSI, DMG, pacotes Linux) — só executáveis self-contained.

## Further Notes

- Glossário em `CONTEXT.md`; decisões em `docs/adr/0001-coverage-set-em-vez-de-produto-cartesiano.md` e `docs/adr/0002-apenas-xsd-1-0.md`.
- O caso de uso motivador é acompanhar novos pacotes de schemas da SEFAZ (NF-e, CT-e, NFS-e etc.), mas nada no app deve ser específico da SEFAZ.
- O SDK do .NET não está instalado na máquina de desenvolvimento atual; instalar o SDK 10 é o primeiro passo da implementação.
