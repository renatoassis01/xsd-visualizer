using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Elemento de topo de um Schema Set, identificado pelo nome e pelo arquivo que o declara.</summary>
public sealed class GlobalElement
{
    internal GlobalElement(XmlSchemaElement declaration, CompilationUnit unit)
    {
        Declaration = declaration;
        Unit = unit;
    }

    internal XmlSchemaElement Declaration { get; }
    internal CompilationUnit Unit { get; }

    public SchemaSet SchemaSet { get; internal set; } = null!;
    public string Name => Declaration.QualifiedName.Name;
    public string Namespace => Declaration.QualifiedName.Namespace;

    /// <summary>Arquivo XSD que declara o elemento.</summary>
    public string SourceFile => new Uri(Declaration.SourceUri!).LocalPath;
}
