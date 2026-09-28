# XSD Visualizer

Aplicativo desktop para explorar conjuntos de XSD quaisquer (não só SEFAZ) e os WSDLs que os usam, gerar exemplos XML válidos (inclusive envelopes SOAP) e validar XMLs existentes contra eles.

## Language

### Schemas

**Schema Set** (conjunto de schemas):
Todos os XSDs e WSDLs de uma pasta, com seus `include`/`import` resolvidos. Arrastar um `.xsd` ou `.wsdl` solto abre o Schema Set da pasta dele.
_Avoid_: pacote, PL, pacote de liberação (termos só da SEFAZ), schema (quando se refere à pasta inteira)

**Global Element** (elemento global):
Declaração de elemento de topo de um Schema Set; é o que pode ser raiz de um Document ou de um Sample. É identificado pelo nome e pelo arquivo que o declara: o mesmo nome pode aparecer em arquivos diferentes com definições diferentes.
_Avoid_: root, raiz, leiaute

### Serviços (WSDL)

**Service** (serviço):
Um `wsdl:service` de um WSDL do Schema Set, com seus Endpoints e Operations.
_Avoid_: web service, WS, WSDL (quando se refere ao serviço, não ao arquivo)

**Endpoint**:
Um `wsdl:port` de um Service: endereço + versão do SOAP (1.1 ou 1.2) + `soapAction` das Operations.
_Avoid_: port, porta, binding, URL

**Operation** (operação):
Uma operação de um Service, com uma Request e uma Response.
_Avoid_: método, ação, chamada

**Request / Response**:
As duas mensagens de uma Operation: a que vai para o serviço e a que volta dele.
_Avoid_: entrada/saída, input/output, retorno

**Payload**:
O XML de negócio que viaja dentro do Body de uma Request ou Response (ex.: `enviNFe`), às vezes compactado em gzip+base64 dentro de uma string.
_Avoid_: conteúdo, corpo, dados, XML de negócio

**Payload Binding**:
Ligação, feita pelo usuário, entre a Request ou Response de uma Operation e o Global Element do seu Payload, com a indicação de se o Payload vai compactado. Existe porque os WSDLs costumam declarar o Body como conteúdo livre (`xs:any`) ou string.
_Avoid_: mapeamento, vínculo da operação

**Envelope**:
O envelope SOAP completo (Envelope, Header, Body) de uma Request ou Response. Gerado pelo app, é um Sample; trazido pelo usuário, é um Document.
_Avoid_: requisição SOAP, mensagem SOAP, request XML

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

### Comparação

**Comparison** (comparação):
Confronto entre dois Schema Sets, um marcado como **Before** (antes) e outro como **After** (depois), que lista os Element Pairs e as Changes de cada um.
_Avoid_: diff (sozinho), versão antiga/nova, origem/destino

**Before / After** (antes / depois):
Os dois lados de uma Comparison: o Schema Set de referência e o que está sendo avaliado contra ele.
_Avoid_: old/new, antigo/novo, esquerda/direita

**Element Pair** (par):
Um Global Element do Before e o correspondente do After, pareados por namespace + nome (ou escolhidos à mão). Um lado pode faltar: aí o Global Element entrou ou saiu.
_Avoid_: match, correspondência

**Change** (mudança):
Diferença em um nó entre Before e After: **Added** (entrou, só no After), **Removed** (saiu, só no Before), **Modified** (nos dois, com diferença de tipo, cardinalidade, facets ou valor fixo/padrão) ou **Documentation-only** (só o texto de `xs:documentation` mudou). Os nós são pareados pelo caminho de nomes; um elemento renomeado é um Removed mais um Added.
_Avoid_: delta, alteração genérica

