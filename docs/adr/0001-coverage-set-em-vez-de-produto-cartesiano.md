# Coverage Set em vez de produto cartesiano

"Gerar todos os exemplos" de um Global Element produz um Coverage Set — o menor conjunto de Samples em que cada ramo de choice (incluindo substitution groups, tipos abstratos e `xsi:type`), cada opcional e cada valor de enumeração aparece ao menos uma vez — e não todas as combinações. O produto cartesiano é inviável em schemas reais (a NF-e daria milhões de arquivos); com escolhas independentes variando em paralelo, o número de Samples fica na ordem do maior choice/enum.

## Consequences

- Interações entre escolhas (ex.: `ICMS40` junto com `dest/CPF`) não são garantidas; para ver uma combinação específica, o usuário fixa os ramos na árvore e gera um Maximal Sample.
- "Viável" é sempre segundo o XSD; regras de negócio externas (dígito verificador, regras da SEFAZ) não entram.
