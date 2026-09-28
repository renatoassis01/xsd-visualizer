# XSD Visualizer

Aplicativo desktop para explorar conjuntos de XSD quaisquer (não só SEFAZ), gerar exemplos XML válidos a partir deles e validar XMLs existentes contra eles.

## Language

### Schemas

**Schema Set** (conjunto de schemas):
Todos os XSDs de uma pasta, com seus `include`/`import` resolvidos. Arrastar um `.xsd` solto abre o Schema Set da pasta dele.
_Avoid_: pacote, PL, pacote de liberação (termos só da SEFAZ), schema (quando se refere à pasta inteira)

**Global Element** (elemento global):
Declaração de elemento de topo de um Schema Set; é o que pode ser raiz de um Document ou de um Sample. É identificado pelo nome e pelo arquivo que o declara: o mesmo nome pode aparecer em arquivos diferentes com definições diferentes.
_Avoid_: root, raiz, leiaute

### XML

**Sample** (exemplo):
XML gerado pelo app a partir de um Global Element, válido contra o seu Schema Set sempre que o XSD for satisfazível pelo gerador; quando não for, o Sample existe mas é marcado como inválido, com suas Validation Issues.
_Avoid_: mock, template, XML gerado

**Maximal Sample** (exemplo máximo):
Sample com todos os opcionais presentes e um ramo escolhido em cada choice (o primeiro, ou o que o usuário fixou na árvore).
_Avoid_: exemplo completo, full

**Minimal Sample** (exemplo mínimo):
Sample só com o que o Schema Set exige.
_Avoid_: exemplo vazio, required-only

**Coverage Set** (conjunto de cobertura):
Menor conjunto de Samples de um Global Element em que cada ramo de choice, cada elemento/atributo opcional e cada valor de enumeração aparece ao menos uma vez. "Viável" é sempre segundo o XSD, nunca segundo regras de negócio externas.
_Avoid_: todas as combinações, produto cartesiano, variações

**Document** (documento):
XML existente, trazido pelo usuário, que é exibido e validado contra um Schema Set.
_Avoid_: arquivo, instância, XML de entrada

**Binding** (vínculo):
Associação de um Document a um Global Element (de algum Schema Set aberto), feita automaticamente por namespace + nome da raiz quando só há um candidato, ou escolhida pelo usuário quando há vários. Pode ser trocada para revalidar o Document contra outro Global Element.
_Avoid_: associação, match

**Validation Issue** (erro de validação):
Divergência entre um Document ou Sample e o seu Schema Set, localizada por linha e coluna.
_Avoid_: erro, warning
