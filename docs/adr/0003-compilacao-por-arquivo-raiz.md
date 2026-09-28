# Compilação por arquivo raiz

Um Schema Set não é compilado como um único `XmlSchemaSet`: cada arquivo raiz da pasta (um XSD que nenhum outro inclui/importa) é compilado separadamente, com seus includes/imports. Pastas reais misturam versões e eventos que redefinem os mesmos nomes no mesmo namespace (no PL_010 da NF-e, compilar tudo junto gera ~2300 erros; por arquivo raiz, só os 14 arquivos com defeito real falham). Consequência: um Global Element é identificado pelo nome **e** pelo arquivo que o declara, e o mesmo nome pode existir com definições diferentes no mesmo Schema Set.
