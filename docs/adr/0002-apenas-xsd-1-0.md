# Apenas XSD 1.0

O app suporta só XSD 1.0 porque usa `System.Xml.Schema` do .NET, que não implementa XSD 1.1 (`xs:assert`, `xs:alternative`); suportar 1.1 exigiria uma dependência paga (Saxon-EE). Os schemas alvo (SEFAZ e a maioria dos padrões) são 1.0. Construções 1.1 viram Validation Issue na carga do Schema Set, nunca falha silenciosa.
