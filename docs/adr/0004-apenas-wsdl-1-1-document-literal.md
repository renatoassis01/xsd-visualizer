# Apenas WSDL 1.1 document/literal

O app suporta só WSDL 1.1 no estilo `document` com `use="literal"`, sobre SOAP 1.1 e 1.2. É o que a SEFAZ (NF-e, NFCom, NF3e, NFGas) e quase todo serviço atual usam; WSDL 2.0 e os estilos `rpc`/`encoded` são legado raro, com regras de montagem de mensagem bem diferentes. Construções fora desse recorte viram Validation Issue na carga do Schema Set, nunca falha silenciosa. Também fica de fora executar chamadas: os serviços da SEFAZ exigem certificado digital cliente (mTLS) até para ler o `?wsdl`.
