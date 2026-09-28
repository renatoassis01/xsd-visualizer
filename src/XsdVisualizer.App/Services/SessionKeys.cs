using XsdVisualizer.Core;

namespace XsdVisualizer.App.Services;

/// <summary>Chaves da sessão para Operations e mensagens: "&lt;wsdl&gt;|&lt;service&gt;|&lt;operation&gt;[|Request/Response]".</summary>
public static class SessionKeys
{
    public static string Of(Operation operation) => $"{operation.Service.SourceFile}|{operation.Service.Name}|{operation.Name}";

    public static string Of(OperationMessage message) => $"{Of(message.Operation)}|{message.Direction}";
}
